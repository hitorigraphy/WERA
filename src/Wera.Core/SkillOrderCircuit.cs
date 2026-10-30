namespace Wera.Core;

/// <summary>
/// 스킬 순서 서킷: 스킬을 쓰는 순서를 정하는 OrderPoint를 계산한다. 시전자와 타겟, 캐시(<see cref="CacheValues"/>), 그리고 <see cref="Result"/>를 갖고,
/// 스킬의 순서 이펙트(스킬 순서 이펙트 마스터)를 실행 번호 순서로 실행한 뒤의 Result가 OrderPoint다. 순서 이펙트는 유닛의 스테이터스를 읽기만 하고 바꾸지 않는다.
/// 점프와 Finish 규칙은 <see cref="SkillCircuit"/>과 같다.
/// </summary>
public sealed class SkillOrderCircuit(GamePack pack, long skillCode, BattleUnit caster, BattleUnit target)
{
    public long SkillCode { get; } = skillCode;
    public BattleUnit Caster { get; } = caster;
    public BattleUnit Target { get; } = target;

    /// <summary>이펙트들이 공유하는 값 목록(cache_index가 이 목록의 번호).</summary>
    public List<decimal> CacheValues { get; } = [];

    /// <summary>서킷의 결과: 실행이 끝난 뒤의 값이 OrderPoint다. CacheToResult가 바꾸고, 바꾸지 않으면 0이다.</summary>
    public decimal Result { get; private set; }

    /// <summary>스킬의 순서 이펙트를 실행 번호 순서로 실행한다(Jump와 JumpCache*는 뒤로 건너뛰고 Finish는 거기서 끝낸다).</summary>
    public void Execute()
    {
        var effects = pack.SkillOrderEffectsOf(SkillCode);
        for (var at = 0; at < effects.Count; at++)
        {
            var effect = effects[at];
            if (effect.EffectType == OrderEffectType.Finish) return;
            if (JumpTarget(effect) is { } target)
            {
                var to = effects.ToList().FindIndex(e => e.ExecuteNumber == target);
                if (to > at) at = to - 1;
            }
            else Apply(effect);
        }
    }

    long? JumpTarget(SkillOrderEffect effect)
    {
        if (effect.EffectType == OrderEffectType.Jump) return effect.Value1;
        if (SkillJump.Of(effect.EffectType) is not { } comparison) return null;
        return SkillCache.At(CacheValues, effect.Value1) is { } left && SkillCache.At(CacheValues, effect.Value2) is { } right
            && SkillJump.Test(comparison, left, right)
                ? effect.Value3
                : null;
    }

    void Apply(SkillOrderEffect effect)
    {
        switch (effect.EffectType)
        {
            case OrderEffectType.CasterValueToCache:
                if (Caster.ValueOf(effect.Value1) is { } casterValue) SkillCache.Write(CacheValues, effect.Value2, casterValue);
                break;
            case OrderEffectType.TargetValueToCache:
                if (Target.ValueOf(effect.Value1) is { } targetValue) SkillCache.Write(CacheValues, effect.Value2, targetValue);
                break;
            case OrderEffectType.SetCacheValue:
                SkillCache.Write(CacheValues, effect.Value1, effect.Value2 / SkillCache.Pow10(effect.Value3));
                break;
            case OrderEffectType.CacheAdd:
                Calculate(effect, SkillCache.Op.Add);
                break;
            case OrderEffectType.CacheSubtract:
                Calculate(effect, SkillCache.Op.Subtract);
                break;
            case OrderEffectType.CacheMultiply:
                Calculate(effect, SkillCache.Op.Multiply);
                break;
            case OrderEffectType.CacheDivide:
                Calculate(effect, SkillCache.Op.Divide);
                break;
            case OrderEffectType.CacheToResult:
                if (SkillCache.At(CacheValues, effect.Value1) is { } value) Result = value;
                break;
        }
    }

    void Calculate(SkillOrderEffect effect, SkillCache.Op op)
    {
        if (SkillCache.At(CacheValues, effect.Value2) is not { } left || SkillCache.At(CacheValues, effect.Value3) is not { } right) return;
        if (SkillCache.Calculate(op, left, right) is { } result) SkillCache.Write(CacheValues, effect.Value1, result);
    }
}
