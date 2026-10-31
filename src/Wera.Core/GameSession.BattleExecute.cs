using System.Globalization;

namespace Wera.Core;

// 전투 [107] 실행: 생존한 전원의 스킬 순서 서킷을 계산해 OrderPoint가 가장 큰 1명이 스킬을 쓰고, 남은 인원으로 다시 계산해 전원이 1회씩 행동하면 1턴이 끝난다.
public sealed partial class GameSession
{
    /// <summary>전투의 결과: 아직 진행 중이면 None.</summary>
    public enum BattleOutcomeKind { None, Win, Lose, Run }

    /// <summary>진행 중인 전투의 결과. 적이 모두 쓰러지면 Win, 아군이 모두 쓰러지면 Lose, 도망치면 Run, 전투를 시작하면 None으로 돌아간다.</summary>
    public BattleOutcomeKind BattleOutcome { get; private set; }

    /// <summary>이번 턴에 마지막으로 실행한 행동들(행동한 순서대로). 다음 턴을 시작하면 비워진다.</summary>
    public IReadOnlyList<BattleActed> BattleLog => battleLog;
    readonly List<BattleActed> battleLog = [];

    /// <summary>
    /// 실행한 행동 한 건: 시전자 쪽(적 여부)과 슬롯, 스킬 코드, 대상 쪽(적 여부)과 슬롯, 그때의 OrderPoint, 대상이 입은 피해(행동 전후 대상의 hp 차이; 음수면 회복).
    /// 이동(<paramref name="IsMove"/>)이면 아군 유닛이 Slot에서 TargetSlot으로 옮겨 간 것이고 나머지 값은 쓰지 않는다.
    /// 서브 타겟(<paramref name="IsSub"/>)이면 같은 행동으로 맞은 서브 타겟 하나의 기록이고 대상과 피해는 그 서브 타겟의 것이다(메인 타겟의 기록 바로 뒤에 서브 타겟 순서로 이어진다).
    /// </summary>
    public readonly record struct BattleActed(bool IsEnemy, int Slot, long SkillCode, bool TargetIsEnemy, int TargetSlot, decimal OrderPoint, long Damage, bool IsMove = false, bool IsSub = false);

    /// <summary>이번 턴 행동 기록을 화면에 보여 줄 한 줄씩("시전자 → 대상 : 스킬N (피해 N)", 행동한 순서). 적의 이름 앞에는 "적 "이 붙는다.</summary>
    public IReadOnlyList<string> BattleLogLines => battleLog.Select(a =>
    {
        if (a.IsMove)                                                              // 옮겨 간 유닛은 이제 도착한 슬롯에 있다
            return $"{(UnitOf(new Combatant(false, a.TargetSlot)) is { } mover ? CharacterName(mover.CharacterCode) : "?")} : 이동 → [{BattleSlotNumber(false, a.TargetSlot)}]";
        string Name(bool enemy, int slot) => (enemy ? "적 " : "") +
            (UnitOf(new Combatant(enemy, slot)) is { } unit ? CharacterName(unit.CharacterCode) : "?");
        var result = a.Damage > 0 ? $"피해 {a.Damage}" : a.Damage < 0 ? $"회복 {-a.Damage}" : "피해 없음";
        if (a.IsSub) return $"↳ {Name(a.TargetIsEnemy, a.TargetSlot)} ({result})";
        return $"{Name(a.IsEnemy, a.Slot)} → {Name(a.TargetIsEnemy, a.TargetSlot)} : {SkillName(a.SkillCode)} ({result})";
    }).ToList();

    readonly record struct Combatant(bool IsEnemy, int Slot);

    PartyUnit? UnitOf(Combatant c) => c.IsEnemy ? enemyUnits[c.Slot] : PartyInstance.Units[c.Slot];

    bool IsAlive(Combatant c) => UnitOf(c) is { } unit && CreateUnitStore().CharacterOf(unit) is { IsDead: false };

    bool IsFallen(Combatant c) => UnitOf(c) is { } unit && CreateUnitStore().CharacterOf(unit) is { IsDead: true };

    List<Combatant> LivingOf(bool enemy) =>
        Enumerable.Range(0, PartySlotCount).Select(i => new Combatant(enemy, i)).Where(IsAlive).ToList();

    /// <summary>그 편에서 쓰러진 유닛들(슬롯 순).</summary>
    List<Combatant> FallenOf(bool enemy) =>
        Enumerable.Range(0, PartySlotCount).Select(i => new Combatant(enemy, i)).Where(IsFallen).ToList();

    /// <summary>그 편에 있는 유닛 전부, 쓰러진 것도 포함(슬롯 순).</summary>
    List<Combatant> ExistingOf(bool enemy) =>
        Enumerable.Range(0, PartySlotCount).Select(i => new Combatant(enemy, i)).Where(c => UnitOf(c) is not null).ToList();

    /// <summary>
    /// [107] 실행: 전원이 1회씩 행동할 때까지(또는 한쪽이 모두 쓰러질 때까지) 순서를 다시 계산하며 스킬을 쓴다. 아군은 설정한 스킬과 메인 타겟을 쓰고,
    /// 적은 스킬과 메인 타겟(스킬의 타겟 대상에 맞는 유닛)을 랜덤으로 정한다(자동 지정 서킷은 나중에). 메인 타겟이 쓰러졌으면 그 스킬의 타겟 대상에 맞는 살아 있는 유닛 중 랜덤으로 바꾼다.
    /// 끝나면 승패를 판정하고 이긴 쪽이 없으면 다음 턴(행동 설정)을 시작한다.
    /// </summary>
    void ExecuteBattleTurn()
    {
        if (BattleOutcome != BattleOutcomeKind.None || !BattleAllActionsSet) return;
        battleLog.Clear();
        var moved = ApplyBattleMoves();
        var store = CreateUnitStore();

        var skillOf = new Dictionary<Combatant, long>();
        var targetOf = new Dictionary<Combatant, Combatant>();
        foreach (var slot in BattleUnitSlots().Where(battleActions.ContainsKey))      // 장착한 스킬이 없는 유닛은 행동하지 않는다
        {
            var ally = new Combatant(false, slot);
            skillOf[ally] = battleActions[slot];
            targetOf[ally] = battleTargets[slot] is { IsEnemy: false } t ? t with { Slot = moved.GetValueOrDefault(t.Slot, t.Slot) } : battleTargets[slot];     // 이동한 아군을 가리켰다면 새 자리로
        }
        foreach (var enemy in LivingOf(true))
        {
            var skills = BattleSkillsOf(enemy);
            if (skills.Count == 0) continue;
            skillOf[enemy] = skills[(int)NextRandom(skills.Count)].SkillCode;
            if (AutoTargetOf(enemy, skillOf[enemy]) is { } auto) targetOf[enemy] = auto;
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
            var aliveBefore = new[] { false, true }.SelectMany(LivingOf).ToList();
            var hpBefore = store.CharacterOf(main)!.Hp;
            var subCombatants = SubTargetsOf(victim, skillOf[actor]);
            var subs = subCombatants.Select(c => UnitOf(c)!).ToList();
            var subHpBefore = subs.Select(u => store.CharacterOf(u)!.Hp).ToList();
            new SkillExecuteCircuit(Pack, store, skillOf[actor], caster, main, subs).Execute();
            new AfterHitCircuit(Pack, store, caster, main).Execute();
            foreach (var sub in subs) new AfterHitCircuit(Pack, store, caster, sub).Execute();     // 서브 타겟도 피격자다(공격 후는 시전자가 한 번만)
            new AfterAttackCircuit(Pack, store, caster, main).Execute();
            if (!actor.IsEnemy && battleItems.TryGetValue(actor.Slot, out var usedItem) && usedItem[0] == 'C')     // 쓴 커먼 아이템은 하나 줄어든다(유니크는 남는다)
                ChangeCommonItem(long.Parse(usedItem.AsSpan(1), CultureInfo.InvariantCulture), -1);
            battleLog.Add(new BattleActed(actor.IsEnemy, actor.Slot, skillOf[actor], victim.IsEnemy, victim.Slot, best, hpBefore - store.CharacterOf(main)!.Hp));
            for (var i = 0; i < subs.Count; i++)
                battleLog.Add(new BattleActed(actor.IsEnemy, actor.Slot, skillOf[actor], subCombatants[i].IsEnemy, subCombatants[i].Slot, best, subHpBefore[i] - store.CharacterOf(subs[i])!.Hp, IsSub: true));

            foreach (var fallen in aliveBefore.Where(c => !IsAlive(c)))                  // 이 행동으로 쓰러진 유닛마다 쓰러진 직후 서킷(시전자는 쓰러진 유닛, 메인 타겟은 행동한 유닛)
                new BattleAfterFallenCircuit(Pack, store, UnitOf(fallen), caster).Execute();

            if (LivingOf(true).Count == 0) BattleOutcome = BattleOutcomeKind.Win;
            else if (LivingOf(false).Count == 0) BattleOutcome = BattleOutcomeKind.Lose;
        }

        if (BattleOutcome == BattleOutcomeKind.None) { BeginTurn(); return; }
        foreach (var slot in BattleUnitSlots())
            if (BattleOutcome == BattleOutcomeKind.Win) new AfterBattleWinCircuit(Pack, store, PartyInstance.Units[slot]!).Execute();
            else new AfterBattleLoseCircuit(Pack, store, PartyInstance.Units[slot]!).Execute();
        BattleCommands = BattleCommandMode.Basic;
    }

    /// <summary>
    /// 이번 턴에 정한 이동을 스킬보다 먼저 한다: 서로를 가리키는 이동은 두 유닛의 자리를 바꾸고, 홀로 빈 칸을 가리키는 이동은 그 칸으로 옮긴다.
    /// 돌려주는 값은 자리가 바뀐 유닛의 (원래 슬롯 → 새 슬롯)이고, 이동을 정한 기록은 비워진다.
    /// </summary>
    Dictionary<int, int> ApplyBattleMoves()
    {
        var units = PartyInstance.Units;
        var result = new Dictionary<int, int>();
        foreach (var (from, to) in battleMoves.OrderBy(m => m.Key).ToList())
        {
            if (units[from] is null || result.ContainsKey(from)) continue;              // 이미 상대의 이동으로 처리됐다
            if (battleMoves.TryGetValue(to, out var back) && back == from && units[to] is not null)
            {
                (units[from], units[to]) = (units[to], units[from]);
                result[from] = to;
                result[to] = from;
                battleLog.Add(new BattleActed(false, from, 0, false, to, 0, 0, IsMove: true));
                battleLog.Add(new BattleActed(false, to, 0, false, from, 0, 0, IsMove: true));
            }
            else if (units[to] is null)
            {
                units[to] = units[from];
                units[from] = null;
                result[from] = to;
                battleLog.Add(new BattleActed(false, from, 0, false, to, 0, 0, IsMove: true));
            }
        }
        battleMoves.Clear();
        return result;
    }

    /// <summary>행동하는 유닛이 노릴 대상: 정해 둔 대상이 아직 그 스킬의 메인 타겟 대상에 맞으면 그대로, 아니면 자동 타겟(<see cref="AutoTargetOf"/>). 없으면 null.</summary>
    Combatant? ResolveBattleTarget(Combatant actor, long skillCode, Dictionary<Combatant, Combatant> targetOf)
    {
        if (targetOf.TryGetValue(actor, out var chosen) && TargetCandidatesOf(actor, SkillTargetTypeOf(skillCode)).Contains(chosen)) return chosen;     // 정해 둔 대상이 아직 그 스킬의 대상이 될 수 있으면 그대로
        return AutoTargetOf(actor, skillCode);
    }
}
