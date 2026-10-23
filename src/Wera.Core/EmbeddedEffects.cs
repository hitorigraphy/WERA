namespace Wera.Core;

/// <summary>
/// 이펙트의 타겟이 되는 캐릭터. 마스터 정의(<see cref="Entry"/>: 코드, 이름, 소지제한, 속성)와
/// 현재 상태(<see cref="State"/>: 레벨, HP, XP)를 함께 들고 있고, 이펙트는 State를 바꿀 수 있다.
/// </summary>
public sealed class Character(CharacterEntry entry, CharacterState state)
{
    public CharacterEntry Entry { get; } = entry;
    public long Code => Entry.Code;
    public string Name => Entry.Name;
    public CharacterState State { get; set; } = state;
}

/// <summary>
/// 임베디드 이펙트: 구동기에 하드코딩된 효과. 데이터(TSV)로 정의하지 않고 이 클래스를 상속해서 코드로 만든다.
/// 생성자에서 타겟 캐릭터와 이펙트 밸류 1~3을 받아 저장해 두고, <see cref="Execute"/>는 파라미터 없이 실행만 한다.
/// 밸류의 의미는 이펙트마다 정한다.
/// </summary>
public abstract class EmbeddedEffect(Character target, long value1, long value2, long value3)
{
    public Character Target { get; } = target;
    public long Value1 { get; } = value1;
    public long Value2 { get; } = value2;
    public long Value3 { get; } = value3;

    /// <summary>이 이펙트의 코드. 임베디드 이펙트 마스터에 등록한 코드와 같아야 한다.</summary>
    public abstract long EffectCode { get; }

    /// <summary>저장해 둔 타겟과 밸류로 이펙트를 실행한다.</summary>
    public abstract void Execute();
}

/// <summary>저장할 타겟과 밸류로 이펙트 인스턴스를 만드는 방법. 보통 <c>(t, a, b, c) => new MyEffect(t, a, b, c)</c>.</summary>
public delegate EmbeddedEffect EmbeddedEffectFactory(Character target, long value1, long value2, long value3);

/// <summary>임베디드 이펙트 마스터의 한 항목: 이펙트 코드와 그 이펙트를 만드는 방법.</summary>
public sealed record EmbeddedEffectDefinition(long EffectCode, EmbeddedEffectFactory Create);

/// <summary>
/// 임베디드 이펙트 마스터: 이펙트 코드 → 이펙트를 만드는 방법. 기본 마스터(<see cref="Default"/>)는 구동기에 하드코딩된 목록이다.
/// 게임팩을 읽을 때 상태이상 이펙트 마스터의 이펙트 코드가 여기에 있는지 검증한다.
/// </summary>
public sealed class EmbeddedEffectMaster
{
    readonly Dictionary<long, EmbeddedEffectDefinition> byCode = [];

    public EmbeddedEffectMaster(IEnumerable<EmbeddedEffectDefinition> definitions)
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
    public static EmbeddedEffectMaster Default { get; } = new(
    [
        // 아직 정의된 이펙트가 없다. 예: new(1, (t, a, b, c) => new HealEffect(t, a, b, c)),
    ]);
}
