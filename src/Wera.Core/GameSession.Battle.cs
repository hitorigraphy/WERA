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
        /// <summary>행동 후보: 지금 보고 있는 캐릭터가 장착한 스킬들([101]부터)과 [106] 취소.</summary>
        ActionCandidates,
        /// <summary>교체 후보: 보는 캐릭터와 자리를 바꿀 아군 슬롯(자신을 뺀 모든 칸, 빈 칸 포함)이 흰색이 되어 슬롯 번호로 고르고, 커맨드는 [101] 취소뿐이다.</summary>
        SwapCandidates,
        /// <summary>아이템 후보: 지금 보고 있는 캐릭터가 쓸 수 있는 아이템들([101]부터, skill_code가 있는 보유 아이템)과 [106] 취소.</summary>
        ItemCandidates,
        /// <summary>대상 후보: 고른 스킬의 타겟 대상(skill_master의 target_type)에 맞는 살아 있는 유닛만 슬롯이 흰색(리더는 연두색)이 되어 슬롯 번호로 고르고, 커맨드는 [101] 취소뿐이다.</summary>
        TargetCandidates,
    }

    /// <summary>행동 후보의 스킬 하나: 장착한 스킬 슬롯의 코드와 스킬 코드, 이름.</summary>
    public sealed record BattleSkillOption(long SlotCode, long SkillCode, string Name);

    /// <summary>
    /// 유닛이 쓸 수 있는 스킬: 캐릭터가 스킬 슬롯에 장착한 스킬들(스킬 슬롯 코드순, 스킬 풀에 있는 것만). 이것이 행동 후보이고
    /// 적은 이 중에서 랜덤으로 쓴다. 장착한 스킬이 없는 유닛은 행동을 설정하지 않아도 되고(실행을 막지 않음) 행동하지 않는다.
    /// </summary>
    List<BattleSkillOption> BattleSkillsOf(Combatant combatant)
    {
        if (UnitOf(combatant) is not { } unit || CreateUnitStore().CharacterOf(unit) is not { } character) return [];
        return character.SkillSlots.OrderBy(s => s.Key)
            .Where(s => s.Value >= 0 && s.Value < character.SkillPool.Count)
            .Select(s => new BattleSkillOption(s.Key, character.SkillPool[(int)s.Value].SkillCode, SkillName(character.SkillPool[(int)s.Value].SkillCode))).ToList();
    }

    /// <summary>
    /// 아이템 후보의 아이템 하나: 커먼(Key = 아이템 코드)이나 유니크(Key = 인스턴스 ID), 이름, 쓰면 실행하는 스킬 코드, 남은 수량(유니크는 1).
    /// </summary>
    public sealed record BattleItemOption(bool IsUnique, string Key, string Name, long SkillCode, long Count)
    {
        /// <summary>항목의 인자와 같은 참조 문자열: 커먼 "C코드", 유니크 "U인스턴스ID".</summary>
        public string Ref => (IsUnique ? "U" : "C") + Key;
    }

    /// <summary>이번 턴에 아군 유닛(파티 슬롯 번호)마다 쓰기로 한 아이템의 참조("C코드"/"U인스턴스ID"). 스킬 대신 아이템으로 행동을 정한 유닛만 있다.</summary>
    readonly Dictionary<int, string> battleItems = [];

    /// <summary>대상 후보에서 고르는 중인 아이템의 참조(아이템 후보에서 고른 뒤). 없으면 null.</summary>
    string? battlePendingItem;

    /// <summary>
    /// 보는 캐릭터가 지금 고를 수 있는 아이템들: skill_code가 있는 커먼 아이템(보유량에서 장비에 낀 것과 다른 유닛이 쓰기로 한 수량을 뺀 것이 1 이상)과
    /// 같은 조건의 유니크 아이템(다른 유닛이 쓰기로 한 것 제외; 스킬 인스턴스를 가진 것). 커먼은 코드순, 유니크는 보유 순서.
    /// </summary>
    public IReadOnlyList<BattleItemOption> BattleItems()
    {
        var reserved = battleItems.Where(kv => kv.Key != BattleCurrentSlot).Select(kv => kv.Value).ToList();
        var result = new List<BattleItemOption>();
        foreach (var (code, held) in InventoryItems.OrderBy(kv => kv.Key))
        {
            if (Pack.FindItem(code) is not { SkillCode: { } skill } entry) continue;
            var free = held.Amount - EquippedCount(EquipRef.Common(code)) - reserved.Count(r => r == "C" + code.ToString(CultureInfo.InvariantCulture));
            if (free > 0) result.Add(new BattleItemOption(false, code.ToString(CultureInfo.InvariantCulture), entry.Name, skill, free));
        }
        foreach (var item in OwnedUniqueItems)
        {
            if (item.Skill is not { } skill || reserved.Contains("U" + item.InstanceId)) continue;
            result.Add(new BattleItemOption(true, item.InstanceId, Pack.FindUniqueItem(item.ItemCode)?.ItemName ?? Text(item.ItemCode), skill.SkillCode, 1));
        }
        return result;
    }

    /// <summary>슬롯의 아군 유닛이 이번 턴에 쓰기로 한 아이템의 참조. 아이템으로 정하지 않았으면 null.</summary>
    public string? BattleItemOf(int slot) => battleItems.GetValueOrDefault(slot);

    /// <summary>지금 보고 있는(행동을 설정하는) 아군 캐릭터의 파티 슬롯 번호. 파티에 유닛이 없으면 null.</summary>
    public int? BattleCurrentSlot { get; private set; }

    /// <summary>지금 작전 화면 아래의 커맨드 모습.</summary>
    public BattleCommandMode BattleCommands { get; private set; }

    /// <summary>이번 턴에 아군 유닛(파티 슬롯 번호)마다 설정한 행동(스킬 코드). 설정하지 않은 유닛은 없다.</summary>
    readonly Dictionary<int, long> battleActions = [];

    /// <summary>이번 턴에 아군 유닛(파티 슬롯 번호)마다 고른 메인 타겟(어느 편의 몇 번 슬롯). 스킬과 함께 정해진다.</summary>
    readonly Dictionary<int, Combatant> battleTargets = [];

    /// <summary>대상 후보에서 고르는 중인 스킬 번호(행동 후보에서 스킬을 고른 뒤). 없으면 null.</summary>
    long? battlePendingSkill;

    /// <summary>슬롯의 아군 유닛이 이번 턴에 정한 메인 타겟의 슬롯 번호. 정하지 않았으면 null.</summary>
    public int? BattleTargetOf(int slot) => battleTargets.TryGetValue(slot, out var target) ? target.Slot : null;

    /// <summary>슬롯의 아군 유닛이 정한 메인 타겟이 적 편인가(정하지 않았으면 null).</summary>
    public bool? BattleTargetIsEnemyOf(int slot) => battleTargets.TryGetValue(slot, out var target) ? target.IsEnemy : null;

    /// <summary>슬롯의 아군 유닛이 이번 턴에 설정한 행동(스킬 코드). 설정하지 않았으면 null.</summary>
    public long? BattleActionOf(int slot) => battleActions.TryGetValue(slot, out var skill) ? skill : null;

    /// <summary>파티의 모든 유닛이 이번 턴의 행동을 설정했는가([106] 실행은 이때만 누를 수 있다). 장착한 스킬이 없는 유닛은 설정하지 않아도 된다.</summary>
    public bool BattleAllActionsSet => BattleUnitSlots().Where(s => BattleSkillsOf(new Combatant(false, s)).Count > 0).All(battleActions.ContainsKey);

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
                    enemyItems[id] = new UniqueItemAfterCreateCircuit(Pack, Pack.NewUniqueItemInstance(id, equip.ItemCode, 0)).Execute();
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
        battleItems.Clear();
        battlePendingSkill = null;
        battlePendingItem = null;
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
        battlePendingItem = null;
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

    /// <summary>[105] 교체: 보는 캐릭터와 자리를 바꿀 아군 슬롯을 고르는 교체 후보로 바뀌고 화면은 적 1 / 아군 3으로 바뀐다.</summary>
    void OpenBattleSwap()
    {
        if (BattleCurrentSlot is null) return;
        BattleCommands = BattleCommandMode.SwapCandidates;
        BattleTop = BattleTopAllyHeavy;
    }

    /// <summary>교체 후보에서 고를 수 있는 아군 슬롯들: 보는 캐릭터의 슬롯을 뺀 모든 칸(빈 칸 포함).</summary>
    List<int> BattleSwapSlots() =>
        BattleCurrentSlot is { } current ? Enumerable.Range(0, PartySlotCount).Where(i => i != current).ToList() : [];

    /// <summary>
    /// 보는 캐릭터의 유닛과 고른 슬롯의 유닛(빈 칸이면 그 칸으로 옮김)의 자리를 바꾼다. 바로 실행되고 행동을 쓰지 않으며,
    /// 이번 턴에 정한 행동·아이템·타겟은 유닛과 함께 따라가고 아군을 가리킨 타겟도 새 자리로 바뀐다. 보는 캐릭터는 그대로 같은 유닛이다.
    /// </summary>
    void SwapBattleUnits(int other)
    {
        if (BattleCurrentSlot is not { } current || !BattleSwapSlots().Contains(other)) return;
        var units = PartyInstance.Units;
        (units[current], units[other]) = (units[other], units[current]);

        int Moved(int slot) => slot == current ? other : slot == other ? current : slot;
        Dictionary<int, T> Remap<T>(Dictionary<int, T> map, Func<T, T> value) =>
            map.ToDictionary(kv => Moved(kv.Key), kv => value(kv.Value));
        Combatant Retarget(Combatant c) => c.IsEnemy ? c : c with { Slot = Moved(c.Slot) };
        var actions = Remap(battleActions, v => v);
        var items = Remap(battleItems, v => v);
        var targets = Remap(battleTargets, Retarget);
        battleActions.Clear(); battleItems.Clear(); battleTargets.Clear();
        foreach (var (k, v) in actions) battleActions[k] = v;
        foreach (var (k, v) in items) battleItems[k] = v;
        foreach (var (k, v) in targets) battleTargets[k] = v;

        BattleCurrentSlot = other;
        battlePendingSkill = null;
        battlePendingItem = null;
        BattleCommands = BattleCommandMode.Basic;
    }

    /// <summary>[103] 아이템: 커맨드가 보는 캐릭터의 아이템 후보로 바뀌고 화면은 적 3 / 아군 1로 바뀐다.</summary>
    void OpenBattleItemCandidates()
    {
        if (BattleCurrentSlot is null) return;
        BattleCommands = BattleCommandMode.ItemCandidates;
        BattleTop = BattleTopEnemyHeavy;
    }

    /// <summary>아이템 후보에서 아이템을 고르면 그 아이템의 스킬로 메인 타겟을 고르는 대상 후보로 바뀐다(아직 행동은 정해지지 않는다). 지금 고를 수 있는 아이템만 된다.</summary>
    void ChooseBattleItem(string reference)
    {
        if (BattleItems().FirstOrDefault(o => o.Ref == reference) is not { } option) return;
        battlePendingSkill = option.SkillCode;
        battlePendingItem = option.Ref;
        BattleCommands = BattleCommandMode.TargetCandidates;
    }

    /// <summary>행동 후보에서 스킬을 고르면 메인 타겟을 고르는 대상 후보로 바뀐다(아직 행동은 정해지지 않는다). 보는 캐릭터가 장착한 스킬만 고를 수 있다.</summary>
    void ChooseBattleSkill(long skill)
    {
        if (BattleCurrentSlot is not { } current || !BattleSkillsOf(new Combatant(false, current)).Any(o => o.SkillCode == skill)) return;
        battlePendingSkill = skill;
        battlePendingItem = null;
        BattleCommands = BattleCommandMode.TargetCandidates;
    }

    /// <summary>대상 후보에서 유닛(Arg: "E슬롯"은 적, "A슬롯"은 아군)을 고르면 보는 캐릭터의 행동(스킬과 메인 타겟)이 정해지고 다음 캐릭터로 넘어간다.</summary>
    void ChooseBattleTarget(string arg)
    {
        var target = new Combatant(arg[0] == 'E', int.Parse(arg.AsSpan(1), CultureInfo.InvariantCulture));
        if (BattleCurrentSlot is { } slot && battlePendingSkill is { } skill && BattleTargets().Contains(target))
        {
            battleActions[slot] = skill;
            battleTargets[slot] = target;
            if (battlePendingItem is { } item) battleItems[slot] = item;
            else battleItems.Remove(slot);
        }
        SelectBattleCharacter(1);
    }

    /// <summary>스킬의 타겟 대상. 스킬 마스터에 없으면 상대편(Enemy).</summary>
    SkillTargetType SkillTargetTypeOf(long skillCode) =>
        Pack.FindSkill(skillCode)?.TargetType ?? SkillTargetType.Enemy;

    /// <summary>시전자가 그 타겟 대상의 스킬로 메인 타겟으로 삼을 수 있는 살아 있는 유닛들(적 편 슬롯 순, 아군 편 슬롯 순).</summary>
    List<Combatant> TargetCandidatesOf(Combatant caster, SkillTargetType type) => type switch
    {
        SkillTargetType.Enemy => LivingOf(!caster.IsEnemy),
        SkillTargetType.Ally => LivingOf(caster.IsEnemy),
        SkillTargetType.Self => IsAlive(caster) ? [caster] : [],
        _ => [.. LivingOf(true), .. LivingOf(false)],
    };

    /// <summary>대상 후보에서 고를 수 있는 유닛들: 보는 캐릭터가 고른 스킬의 타겟 대상에 맞는 살아 있는 유닛. 고른 스킬이 없으면 비어 있다.</summary>
    List<Combatant> BattleTargets() =>
        BattleCurrentSlot is { } slot && battlePendingSkill is { } skill
            ? TargetCandidatesOf(new Combatant(false, slot), SkillTargetTypeOf(skill))
            : [];

    /// <summary>적 슬롯 하나의 표시: 임시 캐릭터 인스턴스의 현재 값을 보여 준다. 빈 칸은 빈 슬롯.</summary>
    PartySlotView EnemySlotView(int slot)
    {
        if (!enemyInstances.TryGetValue(slot, out var state)) return PartySlotView.Empty(slot);
        return new PartySlotView(
            slot, state.CharacterCode, $"Lv.{state.Level}", CharacterName(state.CharacterCode),
            Gauge.Render(state.Hp, state.MaxHp, gray: state.IsDead),
            Gauge.Render(state.Xp, state.XpMax, gray: state.IsDead),
            AttributeBadges(state.CharacterCode), [], IsLeader: enemyUnits[slot]?.IsLeader == true);
    }

    static string BattleRowTitle(int rowInSide) => rowInSide switch { 0 => "전위", 1 => "중위", _ => "후위" };

    /// <summary>작전 화면의 일곱 줄 전부(위에서부터 적 후위, 적 중위, 적 전위, 경계선, 아군 전위, 아군 중위, 아군 후위).</summary>
    IReadOnlyList<BattleRowView> AllBattleRows()
    {
        var enemy = CurrentEnemyPartyCode is { } code ? Pack.FindPresetParty(code) : null;
        // 행동 후보에서는 모든 슬롯이 회색으로 고를 수 없고, 대상 후보에서는 고를 수 있는 적만 흰색(리더는 연두색)이다
        var targets = BattleCommands == BattleCommandMode.TargetCandidates ? BattleTargets() : [];
        bool Gray(bool isEnemy, int slot) => BattleCommands switch
        {
            BattleCommandMode.ActionCandidates or BattleCommandMode.ItemCandidates => true,
            BattleCommandMode.SwapCandidates => isEnemy || !BattleSwapSlots().Contains(slot),
            BattleCommandMode.TargetCandidates => !targets.Contains(new Combatant(isEnemy, slot)),
            _ => false,
        };
        var enemySlots = Enumerable.Range(0, PartySlotCount).Select(EnemySlotView).Select(v => v with { IsGray = Gray(true, v.Slot) }).ToList();
        var allySlots = PartySlotViews.Select(v => v with { IsSelected = v.Slot == BattleCurrentSlot, IsGray = Gray(false, v.Slot), Name = BattleActionOf(v.Slot) is { } skill ? SkillName(skill) : v.Name }).ToList();     // 보는 캐릭터의 이름은 파란색

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
        if (BattleCommands == BattleCommandMode.ItemCandidates)
        {
            // 쓸 수 있는 아이템들이 [101]부터 차례로 나온다([106]은 취소라 건너뛴다). 커먼 아이템은 "이름 ×수량"
            var items = BattleItems();
            for (var i = 0; i < items.Count; i++)
                yield return new ScreenEntry((101 + i + (i >= 5 ? 1 : 0)).ToString(CultureInfo.InvariantCulture),
                    items[i].IsUnique ? items[i].Name : $"{items[i].Name} ×{items[i].Count}", Screen.Battle, EntryAction.BattleChooseItem, items[i].Ref);
            yield return new ScreenEntry("106", "취소", Screen.Battle, EntryAction.BattleCancelActions);
            yield break;
        }
        if (BattleCommands == BattleCommandMode.ActionCandidates)
        {
            // 장착한 스킬들이 [101]부터 차례로 나온다([106]은 취소라 건너뛴다)
            var skills = BattleCurrentSlot is { } current ? BattleSkillsOf(new Combatant(false, current)) : [];
            for (var i = 0; i < skills.Count; i++)
                yield return new ScreenEntry((101 + i + (i >= 5 ? 1 : 0)).ToString(CultureInfo.InvariantCulture), skills[i].Name, Screen.Battle,
                    EntryAction.BattleChooseSkill, skills[i].SkillCode.ToString(CultureInfo.InvariantCulture));
            yield return new ScreenEntry("106", "취소", Screen.Battle, EntryAction.BattleCancelActions);
            yield break;
        }
        if (BattleCommands is BattleCommandMode.TargetCandidates or BattleCommandMode.SwapCandidates)
        {
            yield return new ScreenEntry("101", "취소", Screen.Battle, EntryAction.BattleCancelActions);
            yield break;
        }
        yield return new ScreenEntry("101", "다음 캐릭터", Screen.Battle, EntryAction.BattleSelectCharacter, "1");
        yield return new ScreenEntry("102", "행동 설정", Screen.Battle, EntryAction.BattleOpenActions);
        yield return new ScreenEntry("103", "아이템", Screen.Battle, EntryAction.BattleOpenItems);
        yield return new ScreenEntry("104", "이전 캐릭터", Screen.Battle, EntryAction.BattleSelectCharacter, "-1");
        yield return new ScreenEntry("105", "교체", Screen.Battle, EntryAction.BattleOpenSwap);
        yield return new ScreenEntry("106", "실행", Screen.Battle, EntryAction.BattleExecute, Enabled: BattleAllActionsSet);
        yield return new ScreenEntry("107", "도망", Screen.Battle, EntryAction.BattleRun);
    }

    /// <summary>▲(음수)는 적 3 / 아군 1 구성으로, ▼(양수)는 적 1 / 아군 3 구성으로 바로 넘어간다(그 사이 구성은 보여 주지 않는다).</summary>
    void ScrollBattle(int delta) =>
        BattleTop = delta < 0 ? BattleTopEnemyHeavy : BattleTopAllyHeavy;
}
