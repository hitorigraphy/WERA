using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>상태이상 마스터의 한 행.</summary>
public sealed record StatusEffectEntry(long Code, string Name);

/// <summary>상태이상 이펙트가 발동하는 시점. TSV에는 이름(대소문자 무시)으로 적는다.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TriggerType>))]
public enum TriggerType
{
    None = 0,
    /// <summary>공격하기 전.</summary>
    BeforeAttack,
    /// <summary>공격한 후.</summary>
    AfterAttack,
    /// <summary>공격을 받기 전.</summary>
    BeforeAttacked,
    /// <summary>공격을 받은 후.</summary>
    AfterAttacked,
    /// <summary>턴 개시 후.</summary>
    AfterTurnStart,
    /// <summary>턴 종료 후.</summary>
    AfterTurnEnd,
    /// <summary>배틀 개시 후.</summary>
    AfterBattleStart,
    /// <summary>배틀 종료 후.</summary>
    AfterBattleEnd,
    /// <summary>이동 후.</summary>
    AfterMove,
}

/// <summary>
/// 상태이상 이펙트 마스터의 한 행: 상태이상(<see cref="StatusEffectCode"/>)이 <see cref="Trigger"/> 시점에
/// 임베디드 이펙트(<see cref="EffectCode"/>)를 값 1~3으로 실행한다. 우선도의 해석(정렬 방향)은 호출하는 쪽이 정한다.
/// </summary>
public sealed record StatusEffectEffect(
    long StatusEffectCode, TriggerType Trigger, long Priority, long EffectCode, long Value1, long Value2, long Value3);

/// <summary>
/// 캐릭터에 걸려 있는 상태이상 이펙트 하나의 <b>복구 가능한 값</b>. 상태이상이 걸린 시점의 마스터 값(시점, 우선도, 이펙트 코드,
/// 밸류 1~3)을 복사해 두고, 진행 상태(카운터, 유효 플래그)와 함께 세이브에 그대로 저장한다.
/// 그래서 게임팩의 마스터가 나중에 바뀌어도 걸려 있던 이펙트는 저장한 값으로 복구된다.
/// </summary>
public sealed record ActiveEffect(
    TriggerType Trigger, long Priority, long EffectCode, long Value1, long Value2, long Value3,
    long Counter = 0, bool IsValid = true)
{
    /// <summary>상태이상 이펙트 마스터의 한 행에서 새로(카운터 0, 유효) 만든다.</summary>
    public static ActiveEffect From(StatusEffectEffect row) =>
        new(row.Trigger, row.Priority, row.EffectCode, row.Value1, row.Value2, row.Value3);

    /// <summary>실행 중인 이펙트의 현재 값(카운터, 유효 플래그 포함)을 저장용으로 뜬다.</summary>
    public static ActiveEffect Capture(TriggerType trigger, long priority, EmbeddedEffect effect) =>
        new(trigger, priority, effect.EffectCode, effect.Value1, effect.Value2, effect.Value3, effect.Counter, effect.IsValid);

    /// <summary>저장한 값 그대로(카운터와 유효 플래그까지) 임베디드 이펙트를 다시 만든다. 등록부에 없는 코드면 null.</summary>
    public EmbeddedEffect? Restore(EmbeddedEffectRegistry registry, Character target)
    {
        var effect = registry.Create(EffectCode, target, Value1, Value2, Value3);
        if (effect is null) return null;
        effect.Counter = Counter;
        effect.IsValid = IsValid;
        return effect;
    }
}

/// <summary>캐릭터에 걸려 있는 상태이상 하나: 상태이상 코드와 그 이펙트들의 복구 가능한 값.</summary>
public sealed record ActiveStatusEffect(long StatusEffectCode, List<ActiveEffect>? Effects = null)
{
    /// <summary>이펙트 목록(없으면 빈 목록). 세이브에는 Effects만 저장한다.</summary>
    [JsonIgnore]
    public IReadOnlyList<ActiveEffect> EffectList => Effects ?? [];
}
