using System.Globalization;

namespace Wera.Core;

/// <summary>전투 화면의 한 줄이 무엇인가: 슬롯 세 칸의 줄, 또는 가운데 영역(경계선).</summary>
public enum BattleRowKind
{
    Slots,
    Border,
}

/// <summary>
/// 전투 화면의 한 줄. 슬롯 줄(<see cref="BattleRowKind.Slots"/>)은 줄 이름(전위/중위/후위)과 왼쪽, 가운데, 오른쪽 슬롯 셋이고,
/// 경계선(<see cref="BattleRowKind.Border"/>)은 왼쪽에 적 파티 이름, 가운데에 전투 이름, 오른쪽에 아군 파티 이름이다.
/// </summary>
public sealed record BattleRowView(
    BattleRowKind Kind, bool IsEnemy, string Title, IReadOnlyList<PartySlotView> Slots,
    string LeftText = "", string CenterText = "", string RightText = "");

// 전투(임시): 지금 보는 파티로 정해진 전투를 시작하고, 작전 화면(슬롯 네 줄과 가운데 영역)을 그린다.
public sealed partial class GameSession
{
    /// <summary>[110] 전투(임시)가 고정으로 시작하는 전투의 코드(전투 마스터).</summary>
    public const long TemporaryBattleCode = 5001;

    /// <summary>작전 화면의 커맨드: [101] 행동설정, [102] 실행(아직 동작은 없다).</summary>
    public const string BattleActionSettingEntryCode = "101";
    public const string BattleExecuteEntryCode = "102";

    /// <summary>작전 화면 구분선 아래 오른쪽의 구성 전환 버튼 코드: [1000] ▲, [1001] ▼.</summary>
    public const string BattleScrollUpEntryCode = "1000";
    public const string BattleScrollDownEntryCode = "1001";

    /// <summary>
    /// 작전 화면이 이어서 그리는 줄은 위에서부터 적 후위, 적 중위, 적 전위, 경계선, 아군 전위, 아군 중위, 아군 후위의 일곱 개이고
    /// 그중 다섯 개(슬롯 네 줄과 경계선)를 한 번에 보여 준다. 보는 구성은 두 가지뿐이다: 적 3줄 / 아군 1줄(맨 위 줄 0)과
    /// 적 1줄 / 아군 3줄(맨 위 줄 2). ▲와 ▼가 둘 사이를 바로 오간다.
    /// </summary>
    public const int BattleRowCount = 7;
    public const int BattleWindowSize = 5;

    /// <summary>작전 화면의 두 구성에서 맨 위에 보이는 줄의 번호: 적 3 / 아군 1, 적 1 / 아군 3.</summary>
    public const int BattleTopEnemyHeavy = 0;
    public const int BattleTopAllyHeavy = BattleRowCount - BattleWindowSize;

    /// <summary>진행 중인 전투의 코드. 전투 마스터에 없어 시작하지 못했거나 전투 화면이 아니면 null.</summary>
    public long? CurrentBattleCode { get; private set; }

    /// <summary>이 전투에서 만난 적 프리셋 파티의 코드(전투 적 마스터에서 가중치로 뽑는다). 없으면 null.</summary>
    public long? CurrentEnemyPartyCode { get; private set; }

    /// <summary>
    /// 작전 화면에서 맨 위에 보이는 줄의 번호: <see cref="BattleTopEnemyHeavy"/>(0; 적 후위, 적 중위, 적 전위, 경계선, 아군 전위)나
    /// <see cref="BattleTopAllyHeavy"/>(2; 적 전위, 경계선, 아군 전위, 아군 중위, 아군 후위). 처음 들어가면 0이다.
    /// </summary>
    public int BattleTop { get; private set; }

    /// <summary>전투를 시작한다: 전투(<see cref="TemporaryBattleCode"/>)에 나올 수 있는 프리셋 파티를 가중치로 하나 뽑고 맨 위 줄로 돌아간다.</summary>
    void BeginBattle()
    {
        BattleTop = 0;
        CurrentEnemyPartyCode = null;
        CurrentBattleCode = Pack.FindBattle(TemporaryBattleCode) is null ? null : TemporaryBattleCode;
        if (CurrentBattleCode is not { } battle) return;

        var candidates = Pack.BattleEnemiesOf(battle);
        var total = candidates.Sum(c => c.Weight);
        if (total <= 0) return;
        var roll = NextRandom(total);
        foreach (var candidate in candidates)
        {
            if (roll < candidate.Weight) { CurrentEnemyPartyCode = candidate.PresetPartyCode; return; }
            roll -= candidate.Weight;
        }
    }

    /// <summary>적 슬롯 하나의 표시: 프리셋 유닛의 캐릭터를 캐릭터 스테이터스 마스터의 레벨 0 값으로 보여 준다. 빈 칸은 빈 슬롯.</summary>
    PartySlotView EnemySlotView(int slot, long? presetUnitCode)
    {
        if (presetUnitCode is not { } unitCode || Pack.FindPresetUnit(unitCode) is not { } unit) return PartySlotView.Empty(slot);
        var state = Pack.NewCharacterInstance(unit.CharacterCode);
        return new PartySlotView(
            slot, unit.CharacterCode, $"Lv.{state.Level}", CharacterName(unit.CharacterCode),
            Gauge.Render(state.Hp, state.MaxHp, gray: state.IsDead),
            Gauge.Render(state.Xp, state.XpMax, gray: state.IsDead),
            AttributeBadges(unit.CharacterCode), []);
    }

    static string BattleRowTitle(int rowInSide) => rowInSide switch { 0 => "전위", 1 => "중위", _ => "후위" };

    /// <summary>작전 화면의 일곱 줄 전부(위에서부터 적 후위, 적 중위, 적 전위, 경계선, 아군 전위, 아군 중위, 아군 후위).</summary>
    IReadOnlyList<BattleRowView> AllBattleRows()
    {
        var enemy = CurrentEnemyPartyCode is { } code ? Pack.FindPresetParty(code) : null;
        var enemySlots = Enumerable.Range(0, PartySlotCount).Select(i => EnemySlotView(i, enemy?.UnitCodes[i])).ToList();
        var allySlots = PartySlotViews;

        BattleRowView Row(bool isEnemy, int rowInSide) => new(
            BattleRowKind.Slots, isEnemy, BattleRowTitle(rowInSide),
            (isEnemy ? enemySlots : allySlots).Skip(rowInSide * 3).Take(3).ToList());

        return
        [
            Row(true, 2), Row(true, 1), Row(true, 0),
            new BattleRowView(BattleRowKind.Border, false, "", [],
                enemy?.Name ?? "", CurrentBattleCode is { } battle ? Pack.FindBattle(battle)!.Name : "", PartyName),
            Row(false, 0), Row(false, 1), Row(false, 2),
        ];
    }

    /// <summary>지금 작전 화면에 보이는 줄들(<see cref="BattleWindowSize"/>개, <see cref="BattleTop"/>부터).</summary>
    public IReadOnlyList<BattleRowView> BattleRows => AllBattleRows().Skip(BattleTop).Take(BattleWindowSize).ToList();

    /// <summary>작전 화면의 항목: [101] 행동설정, [102] 실행, [1000] ▲(적 3 / 아군 1 구성으로, 그 구성일 때는 회색), [1001] ▼(적 1 / 아군 3 구성으로, 그 구성일 때는 회색), 돌아가기.</summary>
    IReadOnlyList<ScreenEntry> BattleEntries() =>
    [
        new ScreenEntry(BattleActionSettingEntryCode, "행동설정", Screen.Battle, EntryAction.BattleCommand),
        new ScreenEntry(BattleExecuteEntryCode, "실행", Screen.Battle, EntryAction.BattleCommand),
        new ScreenEntry(BattleScrollUpEntryCode, "▲", Screen.Battle, EntryAction.BattleScroll, "-1", Enabled: BattleTop > 0),
        new ScreenEntry(BattleScrollDownEntryCode, "▼", Screen.Battle, EntryAction.BattleScroll, "1", Enabled: BattleTop < BattleTopAllyHeavy),
        new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home),
    ];

    /// <summary>▲(음수)는 적 3 / 아군 1 구성으로, ▼(양수)는 적 1 / 아군 3 구성으로 바로 넘어간다(그 사이 구성은 보여 주지 않는다).</summary>
    void ScrollBattle(int delta) =>
        BattleTop = delta < 0 ? BattleTopEnemyHeavy : BattleTopAllyHeavy;
}
