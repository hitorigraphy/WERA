namespace Wera.Core;

/// <summary>
/// 공격 후 서킷: 피격 후 서킷(<see cref="AfterHitCircuit"/>) 다음에 실행된다. 시전자와 타겟(스킬을 맞은 쪽), 캐시(<see cref="CacheValues"/>)를 갖고
/// 공격 후 이펙트 마스터를 실행 번호 순서로 실행한다. 이펙트의 종류와 동작은 <see cref="SkillExecuteCircuit"/>과 같다.
/// </summary>
public sealed class AfterAttackCircuit(GamePack pack, BattleUnit caster, BattleUnit target)
{
    public BattleUnit Caster { get; } = caster;
    public BattleUnit Target { get; } = target;

    /// <summary>이펙트들이 공유하는 값 목록(cache_index가 이 목록의 번호).</summary>
    public List<decimal> CacheValues { get; } = [];

    /// <summary>이펙트를 실행 번호 순서로 실행한다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다.</summary>
    public void Execute()
    {
        var effects = pack.AfterAttackEffectsInOrder();
        for (var at = 0; at < effects.Count; at++)
        {
            var effect = effects[at];
            if (effect.EffectType == AfterAttackEffectType.Finish) return;
            if (JumpTarget(effect) is { } target)
            {
                var to = effects.ToList().FindIndex(e => e.ExecuteNumber == target);
                if (to > at) at = to - 1;
            }
            else Apply(effect);
        }
    }

    long? JumpTarget(AfterAttackEffect effect)
    {
        if (effect.EffectType == AfterAttackEffectType.Jump) return effect.Value1;
        if (AfterAttackJump.Of(effect.EffectType) is not { } comparison) return null;
        return CacheAt(effect.Value1) is { } left && CacheAt(effect.Value2) is { } right && SkillJump.Test(comparison, left, right)
            ? effect.Value3
            : null;
    }

    void Apply(AfterAttackEffect effect)
    {
        switch (effect.EffectType)
        {
            case AfterAttackEffectType.None:
                break;
            case AfterAttackEffectType.CacheToTargetModValue:
                if (CacheAt(effect.Value2) is { } mod) Target.ModValue(effect.Value1, mod);
                break;
            case AfterAttackEffectType.CacheToTargetSetValue:
                if (CacheAt(effect.Value2) is { } set) Target.SetValue(effect.Value1, set);
                break;
            case AfterAttackEffectType.CacheToCasterModValue:
                if (CacheAt(effect.Value2) is { } casterMod) Caster.ModValue(effect.Value1, casterMod);
                break;
            case AfterAttackEffectType.CacheToCasterSetValue:
                if (CacheAt(effect.Value2) is { } casterSet) Caster.SetValue(effect.Value1, casterSet);
                break;
            case AfterAttackEffectType.CasterValueToCache:
                if (Caster.ValueOf(effect.Value1) is { } casterValue) SkillCache.Write(CacheValues, effect.Value2, casterValue);
                break;
            case AfterAttackEffectType.TargetValueToCache:
                if (Target.ValueOf(effect.Value1) is { } targetValue) SkillCache.Write(CacheValues, effect.Value2, targetValue);
                break;
            case AfterAttackEffectType.SetCacheValue:
                SkillCache.Write(CacheValues, effect.Value1, effect.Value2 / SkillCache.Pow10(effect.Value3));
                break;
            case AfterAttackEffectType.CacheAdd:
            case AfterAttackEffectType.CacheSubtract:
            case AfterAttackEffectType.CacheMultiply:
            case AfterAttackEffectType.CacheDivide:
                Calculate(effect);
                break;
        }
    }

    /// <summary>캐시 계산: cache[value_1] = cache[value_2] (연산) cache[value_3]. 칸이 없거나 0으로 나누거나 decimal 범위를 넘으면 아무 일도 하지 않는다.</summary>
    void Calculate(AfterAttackEffect effect)
    {
        if (CacheAt(effect.Value2) is not { } left || CacheAt(effect.Value3) is not { } right) return;
        var op = effect.EffectType switch
        {
            AfterAttackEffectType.CacheAdd => SkillCache.Op.Add,
            AfterAttackEffectType.CacheSubtract => SkillCache.Op.Subtract,
            AfterAttackEffectType.CacheMultiply => SkillCache.Op.Multiply,
            _ => SkillCache.Op.Divide,
        };
        if (SkillCache.Calculate(op, left, right) is { } result) SkillCache.Write(CacheValues, effect.Value1, result);
    }

    decimal? CacheAt(long index) => SkillCache.At(CacheValues, index);
}
