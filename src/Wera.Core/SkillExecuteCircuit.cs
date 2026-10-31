namespace Wera.Core;

/// <summary>
/// 스킬 실행 서킷: 시전자가 메인 타겟과 서브 타겟들에게 스킬을 쓰면 그 파티 유닛들이 서킷에 등록되고, 그 스킬의 이펙트(스킬 이펙트 마스터)가 실행 번호 순서로 실행된다.
/// 이펙트는 Focus* 종류로 유닛의 캐릭터, 장비, 스킬을 포커스하고 포커스한 것과 <see cref="CircuitBase.CacheValues"/>로 값을 주고받는다.
/// 이펙트 종류마다의 동작은 <see cref="CircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// </summary>
public sealed class SkillExecuteCircuit(GamePack pack, UnitStore store, long skillCode, PartyUnit? caster, PartyUnit? mainTarget, IEnumerable<PartyUnit>? subTarget = null)
    : CircuitBase(pack, store, caster, mainTarget, subTarget)
{
    public long SkillCode { get; } = skillCode;

    /// <summary>스킬의 이펙트를 실행 번호 순서로 실행한다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다(점프는 뒤로만 가므로 반드시 끝난다).</summary>
    public void Execute() =>
        Run(Pack.SkillExecuteEffectsOf(SkillCode).Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
}
