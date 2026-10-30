namespace Wera.Core;

/// <summary>
/// 전투에서 스킬의 이펙트가 읽고 쓰는 유닛. 스테이터스 값은 캐릭터 스테이터스 마스터의 행(초깃값, 최솟값, 최댓값)에서 만들고
/// decimal로 다룬다. 값을 바꾸면 그 스테이터스의 최솟값~최댓값으로 맞춘다.
/// </summary>
public sealed class BattleUnit
{
    readonly Dictionary<long, (decimal Value, decimal Min, decimal Max)> statuses = [];

    public long CharacterCode { get; }

    BattleUnit(long characterCode) => CharacterCode = characterCode;

    /// <summary>
    /// 캐릭터의 마스터로 유닛을 만든다: 마스터에 행이 있는 모든 스테이터스를 그 레벨의 행(없으면 레벨 0 행)의 초깃값으로 채운다.
    /// 레벨을 주지 않으면 level 스테이터스의 레벨 0 초깃값이다.
    /// </summary>
    public static BattleUnit Create(GamePack pack, long characterCode, long? level = null)
    {
        var at = level ?? pack.InitialCharacterStatus(characterCode, GamePack.LevelStatusCode)?.InitialValue ?? 1;
        var unit = new BattleUnit(characterCode);
        foreach (var status in pack.CharacterStatusesOf(characterCode).Select(r => r.StatusCode).Distinct())
            if (pack.CharacterStatusAt(characterCode, status, at) is { } row)
                unit.statuses[status] = (row.InitialValue, row.MinValue, row.MaxValue);
        return unit;
    }

    /// <summary>스테이터스 값. 이 유닛에게 그 스테이터스가 없으면 null.</summary>
    public decimal? ValueOf(long statusCode) => statuses.TryGetValue(statusCode, out var s) ? s.Value : null;

    /// <summary>스테이터스 값을 바꾼다(최솟값~최댓값으로 맞춤). 그 스테이터스가 없으면 false.</summary>
    public bool SetValue(long statusCode, decimal value)
    {
        if (!statuses.TryGetValue(statusCode, out var s)) return false;
        statuses[statusCode] = s with { Value = Math.Clamp(value, s.Min, s.Max) };
        return true;
    }

    /// <summary>스테이터스 값에 더한다(음수면 뺀다). 그 스테이터스가 없으면 false.</summary>
    public bool ModValue(long statusCode, decimal delta) =>
        statuses.TryGetValue(statusCode, out var s) && SetValue(statusCode, s.Value + delta);
}

/// <summary>
/// 스킬 서킷: 캐릭터가 타겟에게 스킬을 쓰면 둘이 서킷에 등록되고, 그 스킬의 이펙트(스킬 이펙트 마스터)가 실행 번호 순서로 실행된다.
/// 이펙트끼리는 <see cref="CacheValues"/>로 값을 주고받는다. 이펙트 종류마다의 동작은 <see cref="Apply"/>의 switch에 있다.
/// </summary>
public sealed class SkillCircuit(GamePack pack, long skillCode, BattleUnit caster, BattleUnit target)
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
