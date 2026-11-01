using System.Globalization;

namespace Wera.Core;

// 아지트 방어전: 턴 종료 때 위협도로 침입 판정(RollDefenseEnemy)을 해서 적 파티가 나오면 방어전 화면(Screen.Defense)이 그 결과를 로그로 한 줄씩 보여 준다.
// 결과는 그 자리에서 모두 계산하고(상태 변경은 즉시 적용), 화면은 Enter(코드 1)마다 다음 줄을 보여 줄 뿐이다. 마지막 줄 다음 Enter가 홈으로 돌아가고 시설 서킷을 돌린다.
public sealed partial class GameSession
{
    /// <summary>방어전 화면의 "[1] 다음".</summary>
    public const string NextDefenseStepCode = "1";

    /// <summary>한 번의 방어전에서 보스(층의 방어 파티)가 이기지도 지지도 못하고 끝나지 않을 때 자르는 턴 수(넘으면 보스가 그 층을 내준 것으로 본다).</summary>
    public const int DefenseBossTurnLimit = 50;

    // 침입한 적 파티: 프리셋 파티로 만든 임시 인스턴스(HP는 층을 지나도 이어진다). 쓰러지면 구금된 적으로 보유 캐릭터가 된다.
    sealed class DefenseInvader(int number, PresetParty preset)
    {
        public int Number { get; } = number;
        public PresetParty Preset { get; } = preset;
        public Dictionary<int, CharacterInstance> Instances { get; set; } = [];
        public PartyUnit?[] Units { get; set; } = new PartyUnit?[PartySlotCount];
        public Dictionary<string, UniqueItemInstance> Items { get; set; } = [];
        public string Name => $"침입 파티 {Number}({Preset.Name})";
        public IEnumerable<int> Slots => Instances.Keys.Order();
        public bool IsDefeated => Instances.Values.All(c => c.IsDead);
    }

    readonly List<string> defenseLog = [];
    int defenseShown;

    /// <summary>방어전 화면이 지금까지 보여 준 로그 줄들(오래된 것부터, 마지막이 최신). 방어전 화면이 아니면 빈 목록.</summary>
    public IReadOnlyList<string> DefenseLog => Screen == Screen.Defense ? defenseLog.Take(defenseShown).ToList() : [];

    /// <summary>방어전 화면에서 로그가 더 남았는가(false면 다음 Enter가 방어전을 끝내고 홈으로 돌아간다).</summary>
    public bool DefenseHasMore => Screen == Screen.Defense && defenseShown < defenseLog.Count;

    /// <summary>방어전 전체를 계산해 로그로 돌려준다(화면 없이). 적이 나오지 않으면 빈 목록. 상태 변경(구금, 체력, 위협도, 침입 여부)은 즉시 적용된다.</summary>
    public IReadOnlyList<string> RunDefense()
    {
        var log = new List<string>();
        ResolveDefense(log);
        return log;
    }

    /// <summary>턴 종료 뒤의 방어전: 적이 나오면 로그를 만들어 방어전 화면으로 가고 true, 아니면 아무 일도 하지 않고 false.</summary>
    bool BeginDefenseScreen()
    {
        defenseLog.Clear();
        defenseShown = 0;
        ResolveDefense(defenseLog);
        if (defenseLog.Count == 0) return false;
        defenseShown = 1;
        Screen = Screen.Defense;
        return true;
    }

    /// <summary>방어전 화면의 다음 줄. 줄이 더 없으면 방어전을 끝내고 홈으로 돌아가 시설 서킷을 돌린다.</summary>
    void NextDefenseStep()
    {
        if (defenseShown < defenseLog.Count)
        {
            defenseShown++;
            return;
        }
        defenseLog.Clear();
        defenseShown = 0;
        Screen = Screen.Home;
        Message = $"방어전 종료: {defenseSummary}";
        if (RunFacilityCircuits() is { } production) Message += $" / {production}";
    }

    string defenseSummary = "";

    IReadOnlyList<ScreenEntry> DefenseEntries() => [new ScreenEntry(NextDefenseStepCode, "다음", Screen.Defense, EntryAction.NextDefenseStep)];

    /// <summary>
    /// 방어전을 계산한다. 침입 파티들의 대기열(파티, 층)을 앞에서부터 처리한다: 맨 위 층(0번, 가장 최근에 확장한 층)에서 시작해 아래로 내려오고 가장 처음 있던 층(마지막 번호)이 최종층이다.
    /// 한 층을 지나갈 때마다 추가 침입 판정을 다시 한다(통과하면 새 파티가 맨 위 층에서 대기열 뒤에 선다). 최종층을 지나면 아지트가 침입당한다(<see cref="HideoutInvaded"/>).
    /// </summary>
    void ResolveDefense(List<string> log)
    {
        defenseSummary = "";
        if (RollDefenseEnemy() is not { } first) return;
        var floors = HideoutFloorCount;
        var number = 0;
        var queue = new Queue<(DefenseInvader Party, int Floor)>();
        int invaders = 0, defeated = 0, captured = 0;
        void Enter(DefenseEnemy enemy, string headline)
        {
            if (Pack.FindPresetParty(enemy.PresetPartyCode) is not { } preset) return;
            var party = MakeInvader(++number, preset);
            invaders++;
            log.Add($"{headline} {party.Name}: {string.Join(", ", party.Slots.Select(s => CharacterName(party.Instances[s].CharacterCode) + $"(HP {party.Instances[s].Hp})"))}");
            queue.Enqueue((party, 0));
        }

        log.Add($"적이 아지트에 침입했습니다! (위협도 {first.ConsumeThreat} 소모, 남은 위협도 {Threat})");
        Enter(first, "침입!");
        if (floors == 0)
        {
            log.Add("지킬 층이 없어 아지트가 그대로 침입당했습니다.");
            HideoutInvaded = true;
            queue.Clear();
        }

        while (queue.Count > 0)
        {
            var (party, floor) = queue.Dequeue();
            var floorName = $"{floor + 1}층";
            log.Add($"— {party.Name}이(가) {floorName}에 들어왔습니다.");
            var stopped = ResolveFloor(party, floor, log);
            if (stopped)
            {
                defeated++;
                foreach (var slot in party.Slots)
                {
                    var character = party.Instances[slot];
                    if (AddCapturedEnemy(character.CharacterCode) is { } id) { captured++; log.Add($"  구금: {CharacterName(character.CharacterCode)}을(를) 붙잡았습니다."); }
                    else log.Add($"  {CharacterName(character.CharacterCode)}은(는) 붙잡을 수 없어 사라졌습니다.");
                }
                log.Add($"{party.Name}이(가) {floorName}에서 막혔습니다.");
                continue;
            }

            log.Add($"{party.Name}이(가) {floorName}을(를) 지나갔습니다.");
            if (floor + 1 >= floors)
            {
                log.Add($"{party.Name}이(가) 최종층을 뚫었습니다! 아지트가 침입당했습니다.");
                HideoutInvaded = true;
            }
            else queue.Enqueue((party, floor + 1));
            if (RollDefenseEnemy() is { } extra)
            {
                log.Add($"추가 침입! (위협도 {Threat})");
                Enter(extra, "침입!");
            }
        }

        defenseSummary = $"침입 {invaders}팀, 격퇴 {defeated}팀, 구금 {captured}명" + (HideoutInvaded ? ", 아지트 침입당함" : "");
        log.Add(defenseSummary);
    }

    /// <summary>프리셋 파티로 침입 파티(임시 캐릭터 인스턴스들)를 만든다.</summary>
    DefenseInvader MakeInvader(int number, PresetParty preset)
    {
        CurrentEnemyPartyCode = preset.Code;
        enemyInstances.Clear();
        enemyItems.Clear();
        Array.Clear(enemyUnits);
        CreateEnemyInstances();
        var party = new DefenseInvader(number, preset)
        {
            Instances = new Dictionary<int, CharacterInstance>(enemyInstances),
            Units = enemyUnits.ToArray(),
            Items = new Dictionary<string, UniqueItemInstance>(enemyItems),
        };
        ClearBattleState();
        return party;
    }

    void ClearBattleState()
    {
        CurrentEnemyPartyCode = null;
        enemyInstances.Clear();
        enemyItems.Clear();
        Array.Clear(enemyUnits);
        battleLog.Clear();
        BattleOutcome = BattleOutcomeKind.None;
    }

    /// <summary>
    /// 한 층에서 침입 파티가 막혔는가(true면 쓰러져 구금된다). 순서: 기본 시설 → 지은 시설(오퍼레이터가 차례로 한 번씩, 그다음 시설이 한 번 부딪쳐 적들의 체력을 깎는다) → 보스(층의 방어 파티와 자동 전투).
    /// </summary>
    bool ResolveFloor(DefenseInvader party, int floor, List<string> log)
    {
        foreach (var built in new[] { false, true })
        {
            if (FacilityOfFloor(floor, built) is not { } facility) continue;
            log.Add($"  [{(built ? "지은 시설" : "기본 시설")}] {facility.Name}");
            foreach (var slot in Pack.OperatorSlotsOf(facility.Code))
            {
                if (HideoutOperatorInstanceOf(floor, built, slot.Code) is not { } operatorId || !Characters.TryGetValue(operatorId, out var op)) continue;
                var damage = DefenseDamage.Roll(OperatorDefenseOf(operatorId, slot.Code), NextRandom);
                RecordDefense(operatorId);
                if (Hit(party, damage, $"{CharacterName(op.CharacterCode)}({slot.Name})", log)) return true;
            }
            var defense = Pack.FacilityDefenseOf(facility.Code);
            if (defense > 0 && Hit(party, DefenseDamage.Roll(defense, NextRandom), facility.Name, log)) return true;
        }

        var boss = hideoutParties[floor];
        var fighters = boss.Units.OfType<PartyUnit>().Select(IdOf).OfType<string>()
            .Where(id => Characters.TryGetValue(id, out var c) && !c.IsDead).ToList();
        if (fighters.Count == 0)
        {
            log.Add("  [보스] 맞서 싸울 방어 파티가 없어 그대로 지나갑니다.");
            return false;
        }
        log.Add($"  [보스] 방어 파티: {string.Join(", ", fighters.Select(id => CharacterName(Characters[id].CharacterCode)))}");
        foreach (var id in fighters) RecordDefense(id);
        return FightBoss(party, floor, log);
    }

    /// <summary>한 번 부딪쳐 적들에게 피해를 나눠 주고 로그를 남긴다. 모든 적이 쓰러졌으면 true.</summary>
    bool Hit(DefenseInvader party, long damage, string source, List<string> log)
    {
        var slots = party.Slots.ToList();
        var dealt = DefenseDamage.Distribute(damage, slots.Select(s => party.Instances[s].Hp).ToList(), NextRandom);
        for (var i = 0; i < slots.Count; i++)
            if (dealt[i] > 0) party.Instances[slots[i]].Statuses[GamePack.HpStatusCode] = Math.Max(0, party.Instances[slots[i]].Hp - dealt[i]);
        var detail = string.Join(", ", slots.Where((_, i) => dealt[i] > 0)
            .Select(s => $"{CharacterName(party.Instances[s].CharacterCode)} -{dealt[slots.IndexOf(s)]} (남은 HP {party.Instances[s].Hp})"));
        log.Add($"    {source}: 방어력 {damage}" + (detail.Length == 0 ? " (피해 없음)" : $" → {detail}"));
        if (!party.IsDefeated) return false;
        log.Add($"    {party.Name}의 체력이 모두 바닥났습니다.");
        return true;
    }

    /// <summary>
    /// 보스 전투: 층의 방어 파티(아군 쪽)와 침입 파티가 일반 전투와 같은 규칙으로 자동으로 싸운다(양쪽 모두 스킬은 장착한 것 중 무작위, 대상은 자동 지정).
    /// 침입 파티가 쓰러지면 true(막았다). 방어 파티가 쓰러지거나 <see cref="DefenseBossTurnLimit"/>턴 안에 끝나지 않으면 false(지나간다).
    /// 승패 뒤 서킷은 일반 전투처럼 돌지만 아이템 드롭은 없다(보상은 구금이다).
    /// </summary>
    bool FightBoss(DefenseInvader party, int floor, List<string> log)
    {
        var previousEdit = hideoutEditFloor;
        hideoutEditFloor = floor;                                   // 전투 규칙이 보는 아군 파티는 이 층의 방어 파티다
        CurrentEnemyPartyCode = party.Preset.Code;
        enemyInstances.Clear();
        enemyItems.Clear();
        foreach (var (slot, instance) in party.Instances) enemyInstances[slot] = instance;
        foreach (var (id, item) in party.Items) enemyItems[id] = item;
        for (var slot = 0; slot < PartySlotCount; slot++) enemyUnits[slot] = party.Units[slot];
        BattleOutcome = BattleOutcomeKind.None;
        battleLog.Clear();
        BeginTurn();
        try
        {
            for (var turn = 1; turn <= DefenseBossTurnLimit && BattleOutcome == BattleOutcomeKind.None; turn++)
            {
                ExecuteBattleTurn(autoAllies: true);
                log.Add($"    {turn}턴");
                log.AddRange(BattleLogLines.Select(line => "      " + line));
            }
            party.Instances = new Dictionary<int, CharacterInstance>(enemyInstances);
            party.Items = new Dictionary<string, UniqueItemInstance>(enemyItems);
            var outcome = BattleOutcome;
            log.Add(outcome switch
            {
                BattleOutcomeKind.Win => "    보스 전투 승리: 침입 파티가 쓰러졌습니다.",
                BattleOutcomeKind.Lose => "    보스 전투 패배: 방어 파티가 쓰러졌습니다.",
                _ => $"    보스 전투가 {DefenseBossTurnLimit}턴 안에 끝나지 않아 방어 파티가 층을 내줍니다.",
            });
            return outcome == BattleOutcomeKind.Win;
        }
        finally
        {
            ClearBattleState();
            hideoutEditFloor = previousEdit;
        }
    }
}
