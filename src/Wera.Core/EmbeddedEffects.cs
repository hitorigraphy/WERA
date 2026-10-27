namespace Wera.Core;

/// <summary>
/// 이펙트의 타겟이 되는 캐릭터. 마스터 정의(<see cref="Entry"/>: 코드, 이름, 소지제한, 속성)와
/// 현재 상태(<see cref="State"/>: 레벨, HP, XP)를 함께 들고 있고, 이펙트는 State를 바꿀 수 있다.
/// </summary>
public sealed class Character(CharacterEntry entry, CharacterInstance state)
{
    public CharacterEntry Entry { get; } = entry;
    public long Code => Entry.Code;
    public string Name => Entry.Name;
    public CharacterInstance State { get; set; } = state;
}

/// <summary>
/// 임베디드 이펙트: 구동기에 하드코딩된 효과. 데이터(TSV)로 정의하지 않고 이 클래스를 상속해서 코드로 만든다.
/// 생성자에서 타겟 캐릭터와 이펙트 밸류 1~3을 받아 저장해 두고, <see cref="Execute"/>는 파라미터 없이 실행만 한다.
/// 밸류의 의미는 이펙트마다 정하지만, 밸류 2는 모든 이펙트에서 "최대 실행 횟수"로 쓴다(<see cref="EffectExecutor"/>).
/// </summary>
public abstract class EmbeddedEffect(Character target, long value1, long value2, long value3)
{
    public Character Target { get; } = target;
    public long Value1 { get; } = value1;
    public long Value2 { get; } = value2;
    public long Value3 { get; } = value3;

    /// <summary>내부 카운터: 익스큐터가 이 이펙트를 실행할 때마다 1씩 올린다.</summary>
    public long Counter { get; internal set; }

    /// <summary>
    /// 유효 플래그. 처음엔 유효하고, 카운터가 밸류 2 이상이 되면 익스큐터가 무효로 바꾼다.
    /// 무효인 이펙트는 다시 실행되지 않고 익스큐터에서 폐기된다.
    /// </summary>
    public bool IsValid { get; internal set; } = true;

    /// <summary>이 이펙트의 코드. 임베디드 이펙트 등록부에 등록한 코드와 같아야 한다.</summary>
    public abstract long EffectCode { get; }

    /// <summary>저장해 둔 타겟과 밸류로 이펙트를 실행한다.</summary>
    public abstract void Execute();
}

/// <summary>저장할 타겟과 밸류로 이펙트 인스턴스를 만드는 방법. 보통 <c>(t, a, b, c) => new MyEffect(t, a, b, c)</c>.</summary>
public delegate EmbeddedEffect EmbeddedEffectFactory(Character target, long value1, long value2, long value3);

/// <summary>임베디드 이펙트 등록부의 한 항목: 이펙트 코드와 그 이펙트를 만드는 방법.</summary>
public sealed record EmbeddedEffectDefinition(long EffectCode, EmbeddedEffectFactory Create);

/// <summary>
/// 임베디드 이펙트 등록부: 이펙트 코드 → 이펙트를 만드는 방법. 기본 등록부(<see cref="Default"/>)는 구동기에 하드코딩된 목록이다.
/// 게임팩을 읽을 때 상태이상 이펙트 마스터의 이펙트 코드가 여기에 있는지 검증한다.
/// </summary>
public sealed class EmbeddedEffectRegistry
{
    readonly Dictionary<long, EmbeddedEffectDefinition> byCode = [];

    public EmbeddedEffectRegistry(IEnumerable<EmbeddedEffectDefinition> definitions)
    {
        foreach (var definition in definitions)
            if (!byCode.TryAdd(definition.EffectCode, definition))
                throw new ArgumentException($"임베디드 이펙트 코드 {definition.EffectCode}가 중복됩니다.", nameof(definitions));
    }

    public IReadOnlyCollection<EmbeddedEffectDefinition> All => byCode.Values;

    public bool Contains(long effectCode) => byCode.ContainsKey(effectCode);

    /// <summary>
    /// 이펙트를 만든다(타겟과 밸류를 저장할 뿐 실행은 하지 않는다. 실행은 반환된 이펙트의 Execute()). 없는 코드면 null.
    /// </summary>
    /// <exception cref="InvalidOperationException">만들어진 이펙트의 EffectCode가 등록한 코드와 다를 때.</exception>
    public EmbeddedEffect? Create(long effectCode, Character target, long value1 = 0, long value2 = 0, long value3 = 0)
    {
        if (!byCode.TryGetValue(effectCode, out var definition)) return null;
        var effect = definition.Create(target, value1, value2, value3);
        return effect.EffectCode == effectCode
            ? effect
            : throw new InvalidOperationException(
                $"임베디드 이펙트 {effect.GetType().Name}의 EffectCode({effect.EffectCode})가 등록한 코드({effectCode})와 다릅니다.");
    }

    /// <summary>구동기에 하드코딩된 임베디드 이펙트들. 새 이펙트는 클래스를 만들고 여기에 등록한다.</summary>
    public static EmbeddedEffectRegistry Default { get; } = new(
    [
        new(HealEffect.Code, (t, a, b, c) => new HealEffect(t, a, b, c)),
    ]);
}

/// <summary>
/// 익스큐터: 이펙트들을 순서대로 실행한다. 한 번 실행할 때마다 이펙트의 카운터를 올리고,
/// 카운터가 밸류 2 이상이 되면 그 이펙트를 무효로 만든다. 무효가 된 이펙트는 다음 실행부터 돌지 않고 목록에서 폐기된다.
/// 이펙트의 실행 순서는 추가한 순서 그대로다(우선도 정렬은 하지 않는다).
/// </summary>
public sealed class EffectExecutor
{
    readonly List<EmbeddedEffect> effects = [];

    /// <summary>아직 폐기되지 않은 이펙트들.</summary>
    public IReadOnlyList<EmbeddedEffect> Effects => effects;

    public void Add(EmbeddedEffect effect) => effects.Add(effect);

    /// <summary>
    /// 유효한 이펙트를 한 번씩 실행한다. 실행한 이펙트는 카운터가 1 오르고, 밸류 2 이상이 되면 무효가 된다.
    /// 이미 무효인 이펙트는 실행하지 않는다. 실행이 끝나면 무효인 이펙트를 모두 폐기한다.
    /// 실행 중에 추가한 이펙트는 다음 실행부터 돈다.
    /// </summary>
    public void Run()
    {
        foreach (var effect in effects.ToArray())
        {
            if (!effect.IsValid) continue;
            effect.Execute();
            effect.Counter++;
            if (effect.Counter >= effect.Value2) effect.IsValid = false;
        }
        effects.RemoveAll(e => !e.IsValid);
    }
}
