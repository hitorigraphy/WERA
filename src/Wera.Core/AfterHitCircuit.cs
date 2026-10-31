namespace Wera.Core;

/// <summary>
/// 피격 후 서킷: 스킬 실행 서킷(<see cref="SkillExecuteCircuit"/>) 다음에 실행된다. 시전자와 타겟(맞은 쪽), 캐시(<see cref="CacheValues"/>)를 갖고
/// 피격 후 이펙트 마스터를 실행 번호 순서로 실행한다. 이펙트의 종류와 동작은 <see cref="SkillExecuteCircuit"/>과 같다.
/// </summary>
public sealed class AfterHitCircuit(GamePack pack, BattleUnit caster, BattleUnit target)
{
    public BattleUnit Caster { get; } = caster;
    public BattleUnit Target { get; } = target;

    /// <summary>이펙트들이 공유하는 값 목록(cache_index가 이 목록의 번호).</summary>
    public List<decimal> CacheValues { get; } = [];

    /// <summary>이펙트를 실행 번호 순서로 실행한다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다.</summary>
    public void Execute()
    {
        var effects = pack.AfterHitEffectsInOrder();
        for (var at = 0; at < effects.Count; at++)
        {
            var effect = effects[at];
            if (effect.EffectType == AfterHitEffectType.Finish) return;
            if (JumpTarget(effect) is { } target)
            {
                var to = effects.ToList().FindIndex(e => e.ExecuteNumber == target);
                if (to > at) at = to - 1;
            }
            else Apply(effect);
        }
    }

    long? JumpTarget(AfterHitEffect effect)
    {
        if (effect.EffectType == AfterHitEffectType.Jump) return effect.Value1;
        if (AfterHitJump.Of(effect.EffectType) is not { } comparison) return null;
        return CacheAt(effect.Value1) is { } left && CacheAt(effect.Value2) is { } right && SkillJump.Test(comparison, left, right)
            ? effect.Value3
            : null;
    }

    void Apply(AfterHitEffect effect)
    {
        switch (effect.EffectType)
        {
            case AfterHitEffectType.None:
                break;
            case AfterHitEffectType.CacheToTargetModValue:
                if (CacheAt(effect.Value2) is { } mod) Target.ModValue(effect.Value1, mod);
                break;
            case AfterHitEffectType.CacheToTargetSetValue:
                if (CacheAt(effect.Value2) is { } set) Target.SetValue(effect.Value1, set);
                break;
            case AfterHitEffectType.CacheToCasterModValue:
                if (CacheAt(effect.Value2) is { } casterMod) Caster.ModValue(effect.Value1, casterMod);
                break;
            case AfterHitEffectType.CacheToCasterSetValue:
                if (CacheAt(effect.Value2) is { } casterSet) Caster.SetValue(effect.Value1, casterSet);
                break;
            case AfterHitEffectType.CasterValueToCache:
                if (Caster.ValueOf(effect.Value1) is { } casterValue) SkillCache.Write(CacheValues, effect.Value2, casterValue);
                break;
            case AfterHitEffectType.TargetValueToCache:
                if (Target.ValueOf(effect.Value1) is { } targetValue) SkillCache.Write(CacheValues, effect.Value2, targetValue);
                break;
            case AfterHitEffectType.SetCacheValue:
                SkillCache.Write(CacheValues, effect.Value1, effect.Value2 / SkillCache.Pow10(effect.Value3));
                break;
            case AfterHitEffectType.CacheAdd:
            case AfterHitEffectType.CacheSubtract:
            case AfterHitEffectType.CacheMultiply:
            case AfterHitEffectType.CacheDivide:
                Calculate(effect);
                break;
        }
    }

    /// <summary>캐시 계산: cache[value_1] = cache[value_2] (연산) cache[value_3]. 칸이 없거나 0으로 나누거나 decimal 범위를 넘으면 아무 일도 하지 않는다.</summary>
    void Calculate(AfterHitEffect effect)
    {
        if (CacheAt(effect.Value2) is not { } left || CacheAt(effect.Value3) is not { } right) return;
        var op = effect.EffectType switch
        {
            AfterHitEffectType.CacheAdd => SkillCache.Op.Add,
            AfterHitEffectType.CacheSubtract => SkillCache.Op.Subtract,
            AfterHitEffectType.CacheMultiply => SkillCache.Op.Multiply,
            _ => SkillCache.Op.Divide,
        };
        if (SkillCache.Calculate(op, left, right) is { } result) SkillCache.Write(CacheValues, effect.Value1, result);
    }

    decimal? CacheAt(long index) => SkillCache.At(CacheValues, index);
}
