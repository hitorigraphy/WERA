namespace Wera.Core;

/// <summary>
/// 공격 후 서킷: 공격이 끝난 뒤 실행된다. 시전자(시전자는 공격한 쪽, 메인 타겟은 공격받은 쪽)와 서브 타겟들, 캐시(<see cref="UnitCircuitBase.CacheValues"/>)를 갖고
/// 공격 후 이펙트 마스터를 실행 번호 순서로 실행한다. 이펙트의 종류와 동작은 <see cref="UnitCircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// </summary>
public sealed class AfterAttackCircuit(GamePack pack, UnitStore store, PartyUnit? caster, PartyUnit? mainTarget, IEnumerable<PartyUnit>? subTarget = null)
    : UnitCircuitBase(pack, store, caster, mainTarget, subTarget)
{
    /// <summary>이펙트를 실행 번호 순서로 실행한다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다.</summary>
    public void Execute() =>
        Run(Pack.AfterAttackEffectsInOrder().Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
}
