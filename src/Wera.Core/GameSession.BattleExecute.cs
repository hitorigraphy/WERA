namespace Wera.Core;

// 전투 [106] 실행: 생존한 전원의 스킬 순서 서킷을 계산해 OrderPoint가 가장 큰 1명이 스킬을 쓰고, 남은 인원으로 다시 계산해 전원이 1회씩 행동하면 1턴이 끝난다.
public sealed partial class GameSession
{
    /// <summary>전투의 결과: 아직 진행 중이면 None.</summary>
    public enum BattleOutcomeKind { None, Win, Lose }

    /// <summary>진행 중인 전투의 결과. 적이 모두 쓰러지면 Win, 아군이 모두 쓰러지면 Lose, 전투를 시작하면 None으로 돌아간다.</summary>
    public BattleOutcomeKind BattleOutcome { get; private set; }

    /// <summary>
    /// 행동 후보의 스킬 번호(1~4)에 대응하는 스킬 마스터 코드. 사용 가능한 스킬 목록이 생기기 전까지의 임시 규칙으로 1000 + 번호이다
    /// (스킬 마스터에 없는 코드는 이펙트가 없어 아무 일도 하지 않는다).
    /// </summary>
    public static long BattleSkillCode(int skillNumber) => 1000 + skillNumber;

    /// <summary>이번 턴에 마지막으로 실행한 행동들(행동한 순서대로). 다음 턴을 시작하면 비워진다.</summary>
    public IReadOnlyList<BattleActed> BattleLog => battleLog;
    readonly List<BattleActed> battleLog = [];

    /// <summary>실행한 행동 한 건: 시전자 쪽(적 여부)과 슬롯, 스킬 번호, 대상 쪽의 슬롯, 그때의 OrderPoint, 대상이 입은 피해(행동 전후 대상의 hp 차이; 음수면 회복).</summary>
    public readonly record struct BattleActed(bool IsEnemy, int Slot, int SkillNumber, int TargetSlot, decimal OrderPoint, long Damage);

    /// <summary>이번 턴 행동 기록을 화면에 보여 줄 한 줄씩("시전자 → 대상 : 스킬N (피해 N)", 행동한 순서). 적의 이름 앞에는 "적 "이 붙는다.</summary>
    public IReadOnlyList<string> BattleLogLines => battleLog.Select(a =>
    {
        string Name(bool enemy, int slot) => (enemy ? "적 " : "") +
            (UnitOf(new Combatant(enemy, slot)) is { } unit ? CharacterName(unit.CharacterCode) : "?");
        var result = a.Damage > 0 ? $"피해 {a.Damage}" : a.Damage < 0 ? $"회복 {-a.Damage}" : "피해 없음";
        return $"{Name(a.IsEnemy, a.Slot)} → {Name(!a.IsEnemy, a.TargetSlot)} : {BattleSkillNames[a.SkillNumber - 1]} ({result})";
    }).ToList();

    readonly record struct Combatant(bool IsEnemy, int Slot);

    PartyUnit? UnitOf(Combatant c) => c.IsEnemy ? enemyUnits[c.Slot] : PartyInstance.Units[c.Slot];

    bool IsAlive(Combatant c) => UnitOf(c) is { } unit && CreateUnitStore().CharacterOf(unit) is { IsDead: false };

    List<Combatant> LivingOf(bool enemy) =>
        Enumerable.Range(0, PartySlotCount).Select(i => new Combatant(enemy, i)).Where(IsAlive).ToList();

    /// <summary>
    /// [106] 실행: 전원이 1회씩 행동할 때까지(또는 한쪽이 모두 쓰러질 때까지) 순서를 다시 계산하며 스킬을 쓴다. 아군은 설정한 스킬과 메인 타겟을 쓰고,
    /// 적은 스킬과 메인 타겟을 랜덤으로 정한다(자동 지정 서킷은 나중에). 메인 타겟이 쓰러졌으면 같은 쪽의 살아 있는 유닛 중 랜덤으로 바꾼다.
    /// 끝나면 승패를 판정하고 이긴 쪽이 없으면 다음 턴(행동 설정)을 시작한다.
    /// </summary>
    void ExecuteBattleTurn()
    {
        if (BattleOutcome != BattleOutcomeKind.None || !BattleAllActionsSet) return;
        battleLog.Clear();
        var store = CreateUnitStore();

        var skillOf = new Dictionary<Combatant, int>();
        var targetOf = new Dictionary<Combatant, int>();
        foreach (var slot in BattleUnitSlots())
        {
            var ally = new Combatant(false, slot);
            skillOf[ally] = battleActions[slot];
            targetOf[ally] = battleTargets[slot];
        }
        foreach (var enemy in LivingOf(true))
        {
            skillOf[enemy] = (int)NextRandom(BattleSkillNames.Count) + 1;
            var allies = LivingOf(false);
            if (allies.Count > 0) targetOf[enemy] = allies[(int)NextRandom(allies.Count)].Slot;
        }

        var waiting = skillOf.Keys.Where(IsAlive).ToList();
        while (waiting.Count > 0 && BattleOutcome == BattleOutcomeKind.None)
        {
            waiting.RemoveAll(c => !IsAlive(c));
            if (waiting.Count == 0) break;

            // 서킷을 다시 만들어 매번 OrderPoint를 새로 계산하고, 가장 큰 한 명(같으면 먼저 나온 쪽)이 행동한다
            var actor = waiting[0];
            var best = decimal.MinValue;
            foreach (var c in waiting)
            {
                var target = ResolveBattleTarget(c, targetOf);
                var order = new SkillOrderCircuit(Pack, store, BattleSkillCode(skillOf[c]), UnitOf(c), target is { } t ? UnitOf(t) : null);
                order.Execute();
                if (order.Result > best) { best = order.Result; actor = c; }
            }
            waiting.Remove(actor);

            var targetCombatant = ResolveBattleTarget(actor, targetOf);
            if (targetCombatant is not { } victim) break;                      // 상대가 없으면 이미 끝난 전투다
            targetOf[actor] = victim.Slot;
            var caster = UnitOf(actor)!;
            var main = UnitOf(victim)!;
            var hpBefore = store.CharacterOf(main)!.Hp;
            new SkillExecuteCircuit(Pack, store, BattleSkillCode(skillOf[actor]), caster, main).Execute();
            new AfterHitCircuit(Pack, store, caster, main).Execute();
            new AfterAttackCircuit(Pack, store, caster, main).Execute();
            battleLog.Add(new BattleActed(actor.IsEnemy, actor.Slot, skillOf[actor], victim.Slot, best, hpBefore - store.CharacterOf(main)!.Hp));

            if (LivingOf(true).Count == 0) BattleOutcome = BattleOutcomeKind.Win;
            else if (LivingOf(false).Count == 0) BattleOutcome = BattleOutcomeKind.Lose;
        }

        if (BattleOutcome == BattleOutcomeKind.None) { BeginTurn(); return; }
        foreach (var slot in BattleUnitSlots())
            if (BattleOutcome == BattleOutcomeKind.Win) new AfterBattleWinCircuit(Pack, store, PartyInstance.Units[slot]!).Execute();
            else new AfterBattleLoseCircuit(Pack, store, PartyInstance.Units[slot]!).Execute();
        BattleCommands = BattleCommandMode.Basic;
    }

    /// <summary>행동하는 유닛이 노릴 상대: 정해 둔 대상이 살아 있으면 그대로, 쓰러졌으면 상대 쪽의 살아 있는 유닛 중 랜덤. 상대가 모두 쓰러졌으면 null.</summary>
    Combatant? ResolveBattleTarget(Combatant actor, Dictionary<Combatant, int> targetOf)
    {
        var side = !actor.IsEnemy;
        if (targetOf.TryGetValue(actor, out var slot) && new Combatant(side, slot) is var chosen && IsAlive(chosen)) return chosen;
        var living = LivingOf(side);
        if (living.Count == 0) return null;
        return living[(int)NextRandom(living.Count)];
    }
}
