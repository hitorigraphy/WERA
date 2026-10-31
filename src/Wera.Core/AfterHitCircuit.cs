namespace Wera.Core;

/// <summary>
/// 피격 후 서킷: 스킬 실행 서킷(<see cref="SkillExecuteCircuit"/>) 다음에 실행된다. 시전자(시전자는 때린 쪽, 메인 타겟은 맞은 쪽)와 서브 타겟들, 캐시(<see cref="CircuitBase.CacheValues"/>)를 갖고
/// 피격 후 이펙트 마스터를 실행 번호 순서로 실행한다. 이펙트의 종류와 동작은 <see cref="CircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// </summary>
public sealed class AfterHitCircuit(GamePack pack, UnitStore store, PartyUnit? caster, PartyUnit? mainTarget, IEnumerable<PartyUnit>? subTarget = null)
    : CircuitBase(pack, store, caster, mainTarget, subTarget)
{
    /// <summary>이펙트를 실행 번호 순서로 실행한다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다.</summary>
    public void Execute() =>
        Run(Pack.AfterHitEffectsInOrder().Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
}
