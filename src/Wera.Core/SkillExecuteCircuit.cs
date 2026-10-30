namespace Wera.Core;

/// <summary>
/// 스킬 실행 서킷: 캐릭터가 타겟에게 스킬을 쓰면 둘이 서킷에 등록되고, 그 스킬의 이펙트(스킬 이펙트 마스터)가 실행 번호 순서로 실행된다.
/// 이펙트끼리는 <see cref="CacheValues"/>로 값을 주고받는다. 이펙트 종류마다의 동작은 <see cref="Apply"/>의 switch에 있다.
/// </summary>
public sealed class SkillExecuteCircuit(GamePack pack, long skillCode, BattleUnit caster, BattleUnit target)
{
    public long SkillCode { get; } = skillCode;
    public BattleUnit Caster { get; } = caster;
    public BattleUnit Target { get; } = target;

    /// <summary>이펙트들이 공유하는 값 목록(cache_index가 이 목록의 번호).</summary>
    public List<decimal> CacheValues { get; } = [];

    /// <summary>
    /// 스킬의 이펙트를 실행 번호 순서로 실행한다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다
    /// (점프는 뒤로만 가므로 반드시 끝난다).
    /// </summary>
    public void Execute()
    {
        var effects = pack.SkillExecuteEffectsOf(SkillCode);
        for (var at = 0; at < effects.Count; at++)
        {
            var effect = effects[at];
            if (effect.EffectType == EffectType.Finish) return;
            if (JumpTarget(effect) is { } target)
            {
                var to = effects.ToList().FindIndex(e => e.ExecuteNumber == target);
                if (to > at) at = to - 1;
            }
            else Apply(effect);
        }
    }

    /// <summary>점프하는 이펙트가 이번에 점프하면 그 대상의 execute_number, 점프하지 않으면(조건이 거짓이거나 점프하는 종류가 아니면) null.</summary>
    long? JumpTarget(SkillExecuteEffect effect)
    {
        if (effect.EffectType == EffectType.Jump) return effect.Value1;
        if (SkillJump.Of(effect.EffectType) is not { } comparison) return null;
        return CacheAt(effect.Value1) is { } left && CacheAt(effect.Value2) is { } right && SkillJump.Test(comparison, left, right)
            ? effect.Value3
            : null;
    }

    void Apply(SkillExecuteEffect effect)
    {
        switch (effect.EffectType)
        {
            case EffectType.None:
                break;
            case EffectType.CacheToTargetModValue:
                if (CacheAt(effect.Value2) is { } mod) Target.ModValue(effect.Value1, mod);
                break;
            case EffectType.CacheToTargetSetValue:
                if (CacheAt(effect.Value2) is { } set) Target.SetValue(effect.Value1, set);
                break;
            case EffectType.CacheToCasterModValue:
                if (CacheAt(effect.Value2) is { } casterMod) Caster.ModValue(effect.Value1, casterMod);
                break;
            case EffectType.CacheToCasterSetValue:
                if (CacheAt(effect.Value2) is { } casterSet) Caster.SetValue(effect.Value1, casterSet);
                break;
            case EffectType.CasterValueToCache:
                if (Caster.ValueOf(effect.Value1) is { } casterValue) WriteCache(effect.Value2, casterValue);
                break;
            case EffectType.TargetValueToCache:
                if (Target.ValueOf(effect.Value1) is { } targetValue) WriteCache(effect.Value2, targetValue);
                break;
            case EffectType.SetCacheValue:
                WriteCache(effect.Value1, effect.Value2 / Pow10(effect.Value3));
                break;
            case EffectType.CacheAdd:
            case EffectType.CacheSubtract:
            case EffectType.CacheMultiply:
            case EffectType.CacheDivide:
                Calculate(effect);
                break;
        }
    }

    /// <summary>캐시 계산 이펙트: cache[value_1] = cache[value_2] (연산) cache[value_3]. 칸이 없거나 0으로 나누거나 decimal 범위를 넘으면 아무 일도 하지 않는다.</summary>
    void Calculate(SkillExecuteEffect effect)
    {
        if (CacheAt(effect.Value2) is not { } left || CacheAt(effect.Value3) is not { } right) return;
        var op = effect.EffectType switch
        {
            EffectType.CacheAdd => SkillCache.Op.Add,
            EffectType.CacheSubtract => SkillCache.Op.Subtract,
            EffectType.CacheMultiply => SkillCache.Op.Multiply,
            _ => SkillCache.Op.Divide,
        };
        if (SkillCache.Calculate(op, left, right) is { } result) WriteCache(effect.Value1, result);
    }

    void WriteCache(long index, decimal value) => SkillCache.Write(CacheValues, index, value);

    static decimal Pow10(long digits) => SkillCache.Pow10(digits);

    decimal? CacheAt(long index) => SkillCache.At(CacheValues, index);
}
