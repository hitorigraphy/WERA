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

    /// <summary>작전 화면 아래의 커맨드가 지금 어떤 모습인가.</summary>
    public enum BattleCommandMode
    {
        /// <summary>기본 커맨드: 윗줄 [101] 다음 캐릭터, [102] 행동 설정, [103] 아이템 / 아랫줄 [104] 이전 캐릭터, [105] 교체, [106] 실행, [107] 도망.</summary>
        Basic,
        /// <summary>행동 후보: 지금 보고 있는 캐릭터가 고를 수 있는 스킬들(지금은 고정으로 스킬1~4)과 [106] 취소.</summary>
        ActionCandidates,
        /// <summary>대상 후보: 고른 스킬의 메인 타겟이 될 수 있는 적 유닛들(쓰러지지 않은 적, 코드는 적 슬롯 번호+1)과 [106] 취소.</summary>
        TargetCandidates,
    }

    /// <summary>행동 후보의 스킬 이름. 사용 가능한 스킬 목록은 나중에 쓰고, 지금은 고정으로 스킬1~4다.</summary>
    public static IReadOnlyList<string> BattleSkillNames { get; } = ["스킬1", "스킬2", "스킬3", "스킬4"];

    /// <summary>지금 보고 있는(행동을 설정하는) 아군 캐릭터의 파티 슬롯 번호. 파티에 유닛이 없으면 null.</summary>
    public int? BattleCurrentSlot { get; private set; }

    /// <summary>지금 작전 화면 아래의 커맨드 모습.</summary>
    public BattleCommandMode BattleCommands { get; private set; }

    /// <summary>이번 턴에 아군 유닛(파티 슬롯 번호)마다 설정한 행동(스킬 번호 1~4). 설정하지 않은 유닛은 없다.</summary>
    readonly Dictionary<int, int> battleActions = [];

    /// <summary>이번 턴에 아군 유닛(파티 슬롯 번호)마다 고른 메인 타겟(적 슬롯 번호). 스킬과 함께 정해진다.</summary>
    readonly Dictionary<int, int> battleTargets = [];

    /// <summary>대상 후보에서 고르는 중인 스킬 번호(행동 후보에서 스킬을 고른 뒤). 없으면 null.</summary>
    int? battlePendingSkill;

    /// <summary>슬롯의 아군 유닛이 이번 턴에 정한 메인 타겟(적 슬롯 번호). 정하지 않았으면 null.</summary>
    public int? BattleTargetOf(int slot) => battleTargets.TryGetValue(slot, out var target) ? target : null;

    /// <summary>슬롯의 아군 유닛이 이번 턴에 설정한 행동(스킬 번호). 설정하지 않았으면 null.</summary>
    public int? BattleActionOf(int slot) => battleActions.TryGetValue(slot, out var skill) ? skill : null;

    /// <summary>파티의 모든 유닛이 이번 턴의 행동을 설정했는가([106] 실행은 이때만 누를 수 있다).</summary>
    public bool BattleAllActionsSet => BattleUnitSlots().All(battleActions.ContainsKey);

    /// <summary>아군 파티에 유닛이 있는 슬롯 번호들(칸 순서).</summary>
    List<int> BattleUnitSlots() =>
        Enumerable.Range(0, PartySlotCount).Where(i => PartyInstance.Units[i] is not null).ToList();

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
    /// 이 전투의 적 유닛(적 슬롯 번호 → 임시 캐릭터 인스턴스). 전투를 시작할 때 만난 프리셋 파티의 유닛마다 그 캐릭터의 마스터 초깃값(레벨 0 행)으로
    /// 만들고 InstanceId는 "enemy-슬롯"이다. 세이브에 들어가지 않고 다음 전투를 시작하면 새로 만든다(포획한 적은 이 인스턴스를 보유 캐릭터로 옮긴다).
    /// </summary>
    public IReadOnlyDictionary<int, CharacterInstance> EnemyInstances => enemyInstances;
    readonly Dictionary<int, CharacterInstance> enemyInstances = [];

    /// <summary>
    /// 이 전투의 적 파티 유닛(적 슬롯 번호 0~8, 빈 칸은 null). 아군처럼 캐릭터와 장비를 가진 <see cref="PartyUnit"/>이고, 캐릭터는 같은 슬롯의 <see cref="EnemyInstances"/>,
    /// 장비는 프리셋 유닛 장비 마스터대로 만든 임시 유니크 아이템 인스턴스(InstanceId "enemy-슬롯-장비 슬롯 코드")를 가리킨다.
    /// </summary>
    public IReadOnlyList<PartyUnit?> EnemyUnits => enemyUnits;
    readonly PartyUnit?[] enemyUnits = new PartyUnit?[PartySlotCount];
    readonly Dictionary<string, UniqueItemInstance> enemyItems = [];

    /// <summary>서킷에 넘길 유닛 저장소: 아군 유닛은 보유 캐릭터와 보유 유니크 아이템을, 적 유닛은 이 전투의 임시 인스턴스를 가리킨다. 서킷이 바꾼 값은 곧바로 세션에 반영된다.</summary>
    public UnitStore CreateUnitStore() => new(Characters, UniqueItemInstances, enemyUnits, enemyInstances, enemyItems);

    /// <summary>
    /// 작전 화면에서 맨 위에 보이는 줄의 번호: <see cref="BattleTopEnemyHeavy"/>(0; 적 후위, 적 중위, 적 전위, 경계선, 아군 전위)나
    /// <see cref="BattleTopAllyHeavy"/>(2; 적 전위, 경계선, 아군 전위, 아군 중위, 아군 후위). 턴이 시작될 때(전투를 시작할 때 포함)와
    /// 이전/다음 캐릭터로 다른 캐릭터를 설정할 때는 2로 강제로 바뀌고, 행동 설정을 열 때는 0으로 바뀐다. 그 밖에는 ▲▼로 자유롭게 바꾼다.
    /// </summary>
    public int BattleTop { get; private set; }

    /// <summary>전투를 시작한다: 전투(<see cref="TemporaryBattleCode"/>)에 나올 수 있는 프리셋 파티를 가중치로 하나 뽑고 맨 위 줄로 돌아간다.</summary>
    void BeginBattle()
    {
        CurrentEnemyPartyCode = null;
        BattleOutcome = BattleOutcomeKind.None;
        battleLog.Clear();
        enemyInstances.Clear();
        enemyItems.Clear();
        Array.Clear(enemyUnits);
        CurrentBattleCode = Pack.FindBattle(TemporaryBattleCode) is null ? null : TemporaryBattleCode;
        BeginTurn();
        if (CurrentBattleCode is not { } battle) return;

        var candidates = Pack.BattleEnemiesOf(battle);
        var total = candidates.Sum(c => c.Weight);
        if (total <= 0) return;
        var roll = NextRandom(total);
        foreach (var candidate in candidates)
        {
            if (roll < candidate.Weight) { CurrentEnemyPartyCode = candidate.PresetPartyCode; CreateEnemyInstances(); return; }
            roll -= candidate.Weight;
        }
    }

    /// <summary>만난 적 파티의 유닛마다 파티 유닛(<see cref="EnemyUnits"/>)과 임시 캐릭터 인스턴스(<see cref="EnemyInstances"/>), 임시 장비를 만든다.</summary>
    void CreateEnemyInstances()
    {
        if (CurrentEnemyPartyCode is not { } code || Pack.FindPresetParty(code) is not { } party) return;
        for (var slot = 0; slot < party.UnitCodes.Count; slot++)
            if (party.UnitCodes[slot] is { } unitCode && Pack.FindPresetUnit(unitCode) is { } unit)
            {
                enemyInstances[slot] = new CharacterAfterCreateCircuit(Pack, Pack.NewCharacterInstance(unit.CharacterCode, $"enemy-{slot}", slot)).Execute();
                enemyUnits[slot] = new PartyUnit(unit.CharacterCode, CreateEnemyEquipment(slot, unit.Code));
            }
    }

    /// <summary>프리셋 유닛이 장비 슬롯에 낀 것들: 유니크 아이템은 임시 인스턴스를 만들고(생성 후 서킷을 돌린다), 커먼 아이템과 캐릭터는 코드로 가리킨다.</summary>
    Dictionary<long, EquipRef> CreateEnemyEquipment(int slot, long presetUnitCode)
    {
        var equipment = new Dictionary<long, EquipRef>();
        foreach (var equip in Pack.PresetUnitEquipsOf(presetUnitCode))
        {
            if (!Pack.Inventory.TryGetValue(equip.ItemCode, out var entry)) continue;
            switch (entry.Kind)
            {
                case InventoryKind.UniqueItem:
                    var id = $"enemy-{slot}-{equip.EquipmentSlotCode.ToString(CultureInfo.InvariantCulture)}";
                    enemyItems[id] = new UniqueItemAfterCreateCircuit(Pack, new UniqueItemInstance(id, equip.ItemCode, 0,
                        Pack.MasterTagsOfUniqueItem(equip.ItemCode).ToList(), Pack.MasterStatusesOfUniqueItem(equip.ItemCode))).Execute();
                    equipment[equip.EquipmentSlotCode] = EquipRef.Unique(id);
                    break;
                case InventoryKind.CommonItem:
                    equipment[equip.EquipmentSlotCode] = EquipRef.Common(equip.ItemCode);
                    break;
                case InventoryKind.Character:
                    equipment[equip.EquipmentSlotCode] = EquipRef.Char(equip.ItemCode);
                    break;
            }
        }
        return equipment;
    }

    /// <summary>
    /// 턴을 시작한다(전투를 시작할 때도): 설정해 둔 행동을 모두 지우고, 첫 유닛을 보는 캐릭터로 삼고, 기본 커맨드로 돌아가며,
    /// 화면을 적 1 / 아군 3으로 강제로 바꾼다(보는 캐릭터의 이름이 파란색으로 보인다).
    /// </summary>
    void BeginTurn()
    {
        battleActions.Clear();
        battleTargets.Clear();
        battlePendingSkill = null;
        BattleCurrentSlot = BattleUnitSlots() is { Count: > 0 } slots ? slots[0] : null;
        BattleCommands = BattleCommandMode.Basic;
        BattleTop = BattleTopAllyHeavy;
    }

    /// <summary>
    /// 이전(-1)/다음(1) 캐릭터로 보는 캐릭터를 옮긴다(칸 순서이고 끝에서 처음으로 이어진다). 기본 커맨드로 돌아가고
    /// 화면을 적 1 / 아군 3으로 강제로 바꾼다.
    /// </summary>
    void SelectBattleCharacter(int delta)
    {
        var slots = BattleUnitSlots();
        if (slots.Count == 0) return;
        var at = BattleCurrentSlot is { } current ? slots.IndexOf(current) : -1;
        BattleCurrentSlot = slots[((at + delta) % slots.Count + slots.Count) % slots.Count];
        battlePendingSkill = null;
        BattleCommands = BattleCommandMode.Basic;
        BattleTop = BattleTopAllyHeavy;
    }

    /// <summary>[102] 행동 설정: 커맨드가 보는 캐릭터의 행동 후보로 바뀌고 화면은 적 3 / 아군 1로 바뀐다.</summary>
    void OpenBattleActionCandidates()
    {
        if (BattleCurrentSlot is null) return;
        BattleCommands = BattleCommandMode.ActionCandidates;
        BattleTop = BattleTopEnemyHeavy;
    }

    /// <summary>행동 후보에서 스킬을 고르면 메인 타겟을 고르는 대상 후보로 바뀐다(아직 행동은 정해지지 않는다).</summary>
    void ChooseBattleSkill(int skill)
    {
        if (BattleCurrentSlot is null) return;
        battlePendingSkill = skill;
        BattleCommands = BattleCommandMode.TargetCandidates;
    }

    /// <summary>대상 후보에서 적을 고르면 보는 캐릭터의 행동(스킬과 메인 타겟)이 정해지고 다음 캐릭터로 넘어간다.</summary>
    void ChooseBattleTarget(int enemySlot)
    {
        if (BattleCurrentSlot is { } slot && battlePendingSkill is { } skill)
        {
            battleActions[slot] = skill;
            battleTargets[slot] = enemySlot;
        }
        SelectBattleCharacter(1);
    }

    /// <summary>메인 타겟이 될 수 있는 적 슬롯 번호들(임시 인스턴스가 있고 쓰러지지 않은 적, 칸 순서).</summary>
    List<int> BattleTargetSlots() =>
        enemyInstances.Where(e => !e.Value.IsDead).Select(e => e.Key).Order().ToList();

    /// <summary>적 슬롯 하나의 표시: 임시 캐릭터 인스턴스의 현재 값을 보여 준다. 빈 칸은 빈 슬롯.</summary>
    PartySlotView EnemySlotView(int slot)
    {
        if (!enemyInstances.TryGetValue(slot, out var state)) return PartySlotView.Empty(slot);
        return new PartySlotView(
            slot, state.CharacterCode, $"Lv.{state.Level}", CharacterName(state.CharacterCode),
            Gauge.Render(state.Hp, state.MaxHp, gray: state.IsDead),
            Gauge.Render(state.Xp, state.XpMax, gray: state.IsDead),
            AttributeBadges(state.CharacterCode), []);
    }

    static string BattleRowTitle(int rowInSide) => rowInSide switch { 0 => "전위", 1 => "중위", _ => "후위" };

    /// <summary>작전 화면의 일곱 줄 전부(위에서부터 적 후위, 적 중위, 적 전위, 경계선, 아군 전위, 아군 중위, 아군 후위).</summary>
    IReadOnlyList<BattleRowView> AllBattleRows()
    {
        var enemy = CurrentEnemyPartyCode is { } code ? Pack.FindPresetParty(code) : null;
        var enemySlots = Enumerable.Range(0, PartySlotCount).Select(EnemySlotView).ToList();
        var allySlots = PartySlotViews.Select(v => v with { IsSelected = v.Slot == BattleCurrentSlot, Name = BattleActionOf(v.Slot) is { } skill ? BattleSkillNames[skill - 1] : v.Name }).ToList();     // 보는 캐릭터의 이름은 파란색

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

    /// <summary>
    /// 작전 화면의 항목: 기본 커맨드나 행동 후보(돌아가기는 없다), [1000] ▲(적 3 / 아군 1 구성으로, 그 구성일 때는 회색), [1001] ▼(적 1 / 아군 3 구성으로, 그 구성일 때는 회색).
    /// [106] 실행은 모든 유닛의 행동이 설정되어야 누를 수 있다(아직 동작은 없다).
    /// </summary>
    IReadOnlyList<ScreenEntry> BattleEntries() =>
    [
        .. BattleCommandEntries(),
        .. BattleSlotEntries(),
        new ScreenEntry(BattleScrollUpEntryCode, "▲", Screen.Battle, EntryAction.BattleScroll, "-1", Enabled: BattleTop > 0),
        new ScreenEntry(BattleScrollDownEntryCode, "▼", Screen.Battle, EntryAction.BattleScroll, "1", Enabled: BattleTop < BattleTopAllyHeavy),
    ];

    IEnumerable<ScreenEntry> BattleCommandEntries()
    {
        if (BattleOutcome != BattleOutcomeKind.None) yield break;       // 결판이 났으면 커맨드는 없다
        if (BattleCommands == BattleCommandMode.ActionCandidates)
        {
            for (var i = 0; i < BattleSkillNames.Count; i++)
                yield return new ScreenEntry((101 + i).ToString(CultureInfo.InvariantCulture), BattleSkillNames[i], Screen.Battle,
                    EntryAction.BattleChooseSkill, (i + 1).ToString(CultureInfo.InvariantCulture));
            yield return new ScreenEntry("106", "취소", Screen.Battle, EntryAction.BattleCancelActions);
            yield break;
        }
        if (BattleCommands == BattleCommandMode.TargetCandidates)
        {
            foreach (var enemy in BattleTargetSlots())
                yield return new ScreenEntry((enemy + 1).ToString(CultureInfo.InvariantCulture), CharacterName(enemyInstances[enemy].CharacterCode), Screen.Battle,
                    EntryAction.BattleChooseTarget, enemy.ToString(CultureInfo.InvariantCulture));
            yield return new ScreenEntry("106", "취소", Screen.Battle, EntryAction.BattleCancelActions);
            yield break;
        }
        yield return new ScreenEntry("101", "다음 캐릭터", Screen.Battle, EntryAction.BattleSelectCharacter, "1");
        yield return new ScreenEntry("102", "행동 설정", Screen.Battle, EntryAction.BattleOpenActions);
        yield return new ScreenEntry("103", "아이템", Screen.Battle, EntryAction.BattleCommand);
        yield return new ScreenEntry("104", "이전 캐릭터", Screen.Battle, EntryAction.BattleSelectCharacter, "-1");
        yield return new ScreenEntry("105", "교체", Screen.Battle, EntryAction.BattleCommand);
        yield return new ScreenEntry("106", "실행", Screen.Battle, EntryAction.BattleExecute, Enabled: BattleAllActionsSet);
        yield return new ScreenEntry("107", "도망", Screen.Battle, EntryAction.BattleRun);
    }

    /// <summary>▲(음수)는 적 3 / 아군 1 구성으로, ▼(양수)는 적 1 / 아군 3 구성으로 바로 넘어간다(그 사이 구성은 보여 주지 않는다).</summary>
    void ScrollBattle(int delta) =>
        BattleTop = delta < 0 ? BattleTopEnemyHeavy : BattleTopAllyHeavy;
}
