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

    /// <summary>스킬의 이펙트를 실행 번호 순서로 모두 실행한다.</summary>
    public void Execute()
    {
        foreach (var effect in pack.SkillEffectsOf(SkillCode)) Apply(effect);
    }

    void Apply(SkillEffect effect)
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
        }
    }

    decimal? CacheAt(long index) => index >= 0 && index < CacheValues.Count ? CacheValues[(int)index] : null;
}
