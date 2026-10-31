namespace Wera.Core;

/// <summary>
/// 스킬 순서 서킷: 스킬을 쓰는 순서를 정하는 OrderPoint를 계산한다. 시전자, 메인 타겟, 서브 타겟들과 캐시(<see cref="UnitCircuitBase.CacheValues"/>), 그리고 <see cref="Result"/>를 갖고,
/// 스킬의 순서 이펙트(스킬 순서 이펙트 마스터)를 실행 번호 순서로 실행한 뒤의 Result가 OrderPoint다. 순서 이펙트는 포커스한 것의 스테이터스를 읽기만 하고 바꾸지 않는다.
/// 점프와 Finish 규칙은 <see cref="SkillExecuteCircuit"/>과 같다.
/// </summary>
public sealed class SkillOrderCircuit(GamePack pack, UnitStore store, long skillCode, PartyUnit? caster, PartyUnit? mainTarget, IEnumerable<PartyUnit>? subTarget = null)
    : UnitCircuitBase(pack, store, caster, mainTarget, subTarget)
{
    public long SkillCode { get; } = skillCode;

    /// <summary>서킷의 결과: 실행이 끝난 뒤의 값이 OrderPoint다. CacheToResult가 바꾸고, 바꾸지 않으면 0이다.</summary>
    public decimal Result { get; private set; }

    /// <summary>스킬의 순서 이펙트를 실행 번호 순서로 실행한다(Jump와 JumpCache*는 뒤로 건너뛰고 Finish는 거기서 끝낸다).</summary>
    public void Execute() =>
        Run(Pack.SkillOrderEffectsOf(SkillCode).Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());

    protected override void ApplyOther(CircuitEffect effect)
    {
        if (effect.TypeNumber == (int)OrderEffectType.CacheToResult && CacheAt(effect.Value1) is { } value) Result = value;
    }
}
