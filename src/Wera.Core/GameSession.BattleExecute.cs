using System.Globalization;

namespace Wera.Core;

// 전투 [106] 실행: 생존한 전원의 스킬 순서 서킷을 계산해 OrderPoint가 가장 큰 1명이 스킬을 쓰고, 남은 인원으로 다시 계산해 전원이 1회씩 행동하면 1턴이 끝난다.
public sealed partial class GameSession
{
    /// <summary>전투의 결과: 아직 진행 중이면 None.</summary>
    public enum BattleOutcomeKind { None, Win, Lose, Run }

    /// <summary>진행 중인 전투의 결과. 적이 모두 쓰러지면 Win, 아군이 모두 쓰러지면 Lose, 도망치면 Run, 전투를 시작하면 None으로 돌아간다.</summary>
    public BattleOutcomeKind BattleOutcome { get; private set; }

    /// <summary>이번 턴에 마지막으로 실행한 행동들(행동한 순서대로). 다음 턴을 시작하면 비워진다.</summary>
    public IReadOnlyList<BattleActed> BattleLog => battleLog;
    readonly List<BattleActed> battleLog = [];

    /// <summary>실행한 행동 한 건: 시전자 쪽(적 여부)과 슬롯, 스킬 코드, 대상 쪽(적 여부)과 슬롯, 그때의 OrderPoint, 대상이 입은 피해(행동 전후 대상의 hp 차이; 음수면 회복).</summary>
    public readonly record struct BattleActed(bool IsEnemy, int Slot, long SkillCode, bool TargetIsEnemy, int TargetSlot, decimal OrderPoint, long Damage);

    /// <summary>이번 턴 행동 기록을 화면에 보여 줄 한 줄씩("시전자 → 대상 : 스킬N (피해 N)", 행동한 순서). 적의 이름 앞에는 "적 "이 붙는다.</summary>
    public IReadOnlyList<string> BattleLogLines => battleLog.Select(a =>
    {
        string Name(bool enemy, int slot) => (enemy ? "적 " : "") +
            (UnitOf(new Combatant(enemy, slot)) is { } unit ? CharacterName(unit.CharacterCode) : "?");
        var result = a.Damage > 0 ? $"피해 {a.Damage}" : a.Damage < 0 ? $"회복 {-a.Damage}" : "피해 없음";
        return $"{Name(a.IsEnemy, a.Slot)} → {Name(a.TargetIsEnemy, a.TargetSlot)} : {SkillName(a.SkillCode)} ({result})";
    }).ToList();

    readonly record struct Combatant(bool IsEnemy, int Slot);

    PartyUnit? UnitOf(Combatant c) => c.IsEnemy ? enemyUnits[c.Slot] : PartyInstance.Units[c.Slot];

    bool IsAlive(Combatant c) => UnitOf(c) is { } unit && CreateUnitStore().CharacterOf(unit) is { IsDead: false };

    List<Combatant> LivingOf(bool enemy) =>
        Enumerable.Range(0, PartySlotCount).Select(i => new Combatant(enemy, i)).Where(IsAlive).ToList();

    /// <summary>
    /// [106] 실행: 전원이 1회씩 행동할 때까지(또는 한쪽이 모두 쓰러질 때까지) 순서를 다시 계산하며 스킬을 쓴다. 아군은 설정한 스킬과 메인 타겟을 쓰고,
    /// 적은 스킬과 메인 타겟(스킬의 타겟 대상에 맞는 유닛)을 랜덤으로 정한다(자동 지정 서킷은 나중에). 메인 타겟이 쓰러졌으면 그 스킬의 타겟 대상에 맞는 살아 있는 유닛 중 랜덤으로 바꾼다.
    /// 끝나면 승패를 판정하고 이긴 쪽이 없으면 다음 턴(행동 설정)을 시작한다.
    /// </summary>
    void ExecuteBattleTurn()
    {
        if (BattleOutcome != BattleOutcomeKind.None || !BattleAllActionsSet) return;
        battleLog.Clear();
        var store = CreateUnitStore();

        var skillOf = new Dictionary<Combatant, long>();
        var targetOf = new Dictionary<Combatant, Combatant>();
        foreach (var slot in BattleUnitSlots().Where(battleActions.ContainsKey))      // 장착한 스킬이 없는 유닛은 행동하지 않는다
        {
            var ally = new Combatant(false, slot);
            skillOf[ally] = battleActions[slot];
            targetOf[ally] = battleTargets[slot];
        }
        foreach (var enemy in LivingOf(true))
        {
            var skills = BattleSkillsOf(enemy);
            if (skills.Count == 0) continue;
            skillOf[enemy] = skills[(int)NextRandom(skills.Count)].SkillCode;
            var candidates = TargetCandidatesOf(enemy, SkillTargetTypeOf(skillOf[enemy]));
            if (candidates.Count > 0) targetOf[enemy] = candidates[(int)NextRandom(candidates.Count)];
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
                var target = ResolveBattleTarget(c, skillOf[c], targetOf);
                var order = new SkillOrderCircuit(Pack, store, skillOf[c], UnitOf(c), target is { } t ? UnitOf(t) : null);
                order.Execute();
                if (order.Result > best) { best = order.Result; actor = c; }
            }
            waiting.Remove(actor);

            var targetCombatant = ResolveBattleTarget(actor, skillOf[actor], targetOf);
            if (targetCombatant is not { } victim) break;                      // 상대가 없으면 이미 끝난 전투다
            targetOf[actor] = victim;
            var caster = UnitOf(actor)!;
            var main = UnitOf(victim)!;
            var hpBefore = store.CharacterOf(main)!.Hp;
            new SkillExecuteCircuit(Pack, store, skillOf[actor], caster, main).Execute();
            new AfterHitCircuit(Pack, store, caster, main).Execute();
            new AfterAttackCircuit(Pack, store, caster, main).Execute();
            if (!actor.IsEnemy && battleItems.TryGetValue(actor.Slot, out var usedItem) && usedItem[0] == 'C')     // 쓴 커먼 아이템은 하나 줄어든다(유니크는 남는다)
                ChangeCommonItem(long.Parse(usedItem.AsSpan(1), CultureInfo.InvariantCulture), -1);
            battleLog.Add(new BattleActed(actor.IsEnemy, actor.Slot, skillOf[actor], victim.IsEnemy, victim.Slot, best, hpBefore - store.CharacterOf(main)!.Hp));

            if (LivingOf(true).Count == 0) BattleOutcome = BattleOutcomeKind.Win;
            else if (LivingOf(false).Count == 0) BattleOutcome = BattleOutcomeKind.Lose;
        }

        if (BattleOutcome == BattleOutcomeKind.None) { BeginTurn(); return; }
        foreach (var slot in BattleUnitSlots())
            if (BattleOutcome == BattleOutcomeKind.Win) new AfterBattleWinCircuit(Pack, store, PartyInstance.Units[slot]!).Execute();
            else new AfterBattleLoseCircuit(Pack, store, PartyInstance.Units[slot]!).Execute();
        BattleCommands = BattleCommandMode.Basic;
    }

    /// <summary>행동하는 유닛이 노릴 대상: 정해 둔 대상이 살아 있으면 그대로, 쓰러졌으면 그 스킬의 타겟 대상에 맞는 살아 있는 유닛 중 랜덤. 없으면 null.</summary>
    Combatant? ResolveBattleTarget(Combatant actor, long skillCode, Dictionary<Combatant, Combatant> targetOf)
    {
        if (targetOf.TryGetValue(actor, out var chosen) && IsAlive(chosen)) return chosen;
        var candidates = TargetCandidatesOf(actor, SkillTargetTypeOf(skillCode));
        if (candidates.Count == 0) return null;
        return candidates[(int)NextRandom(candidates.Count)];
    }
}
