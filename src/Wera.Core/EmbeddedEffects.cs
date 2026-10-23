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
/// 상태이상 이펙트 마스터의 이펙트 코드와 이펙트 밸류 1~3이 <see cref="Execute"/>에 그대로 전달된다.
/// </summary>
public abstract class EmbeddedEffect
{
    /// <summary>이 이펙트의 코드. 임베디드 이펙트 마스터 안에서 유일해야 한다.</summary>
    public abstract long EffectCode { get; }

    /// <summary>타겟 캐릭터에게 이펙트를 실행한다. 값의 의미는 이펙트마다 정한다.</summary>
    public abstract void Execute(Character target, long value1, long value2, long value3);
}

/// <summary>
/// 임베디드 이펙트 마스터: 이펙트 코드 → 이펙트. 기본 마스터(<see cref="Default"/>)는 구동기에 하드코딩된 목록이다.
/// 게임팩을 읽을 때 상태이상 이펙트 마스터의 이펙트 코드가 여기에 있는지 검증한다.
/// </summary>
public sealed class EmbeddedEffectMaster
{
    readonly Dictionary<long, EmbeddedEffect> byCode = [];

    public EmbeddedEffectMaster(IEnumerable<EmbeddedEffect> effects)
    {
        foreach (var effect in effects)
            if (!byCode.TryAdd(effect.EffectCode, effect))
                throw new ArgumentException($"임베디드 이펙트 코드 {effect.EffectCode}가 중복됩니다.", nameof(effects));
    }

    public IReadOnlyCollection<EmbeddedEffect> All => byCode.Values;

    public bool Contains(long effectCode) => byCode.ContainsKey(effectCode);

    public EmbeddedEffect? Find(long effectCode) => byCode.GetValueOrDefault(effectCode);

    /// <summary>구동기에 하드코딩된 임베디드 이펙트들. 새 이펙트는 클래스를 만들고 여기에 추가한다.</summary>
    public static EmbeddedEffectMaster Default { get; } = new(
    [
        // 아직 정의된 이펙트가 없다.
    ]);
}
