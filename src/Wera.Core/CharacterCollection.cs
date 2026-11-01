namespace Wera.Core;

/// <summary>
/// 보유 캐릭터 인스턴스들: 인스턴스 ID(ULID 문자열) → 인스턴스. 같은 캐릭터 코드의 인스턴스가 여럿일 수 있다(캐릭터 인벤토리 마스터의 carry_limit까지).
/// 편의로 캐릭터 코드(long)로도 읽고 쓴다: 코드는 "그 코드의 첫 인스턴스"(OrderNumber순, 같으면 InstanceId순)를 뜻한다.
/// </summary>
public sealed class CharacterCollection : Dictionary<string, CharacterInstance>
{
    public CharacterCollection() { }

    public CharacterCollection(IEnumerable<KeyValuePair<string, CharacterInstance>> source) : base(source) { }

    /// <summary>이 코드의 인스턴스들: OrderNumber순, 같으면 InstanceId순.</summary>
    public IReadOnlyList<CharacterInstance> InstancesOf(long code) =>
        Values.Where(c => c.CharacterCode == code).OrderBy(c => c.OrderNumber).ThenBy(c => c.InstanceId, StringComparer.Ordinal).ToList();

    /// <summary>이 코드의 첫 인스턴스(없으면 null).</summary>
    public CharacterInstance? FirstOf(long code) =>
        Values.Where(c => c.CharacterCode == code).MinBy(c => (c.OrderNumber, c.InstanceId), Comparer<(long, string)>.Create(
            (a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : string.CompareOrdinal(a.Item2, b.Item2)));

    /// <summary>이 코드의 인스턴스 수.</summary>
    public int CountOf(long code) => Values.Count(c => c.CharacterCode == code);

    /// <summary>
    /// 이 코드의 첫 인스턴스를 읽고, 쓰면 그 인스턴스를 바꾼다(없으면 읽기는 예외). 쓰는 값의 ID가 비어 있으면(상태만 아는 인스턴스)
    /// 바꾸는 인스턴스의 ID와 순서를 이어받고, 그 코드의 인스턴스가 아직 없으면 새 ID와 다음 순서(소지 캐릭터 수)를 준다.
    /// </summary>
    public CharacterInstance this[long code]
    {
        get => FirstOf(code) ?? throw new KeyNotFoundException($"캐릭터 코드 '{code}'의 인스턴스가 없습니다.");
        set
        {
            var existing = FirstOf(code);
            if (value.InstanceId.Length == 0)
                value = existing is not null
                    ? value with { InstanceId = existing.InstanceId, OrderNumber = existing.OrderNumber }
                    : value with { InstanceId = GameSession.NextInstanceId(Keys), OrderNumber = Count };
            base[existing?.InstanceId ?? value.InstanceId] = value with { CharacterCode = code };
        }
    }

    public bool ContainsKey(long code) => FirstOf(code) is not null;

    public bool TryGetValue(long code, out CharacterInstance instance)
    {
        instance = FirstOf(code)!;
        return instance is not null;
    }

    /// <summary>이 코드의 첫 인스턴스를 뺀다.</summary>
    public bool Remove(long code) => FirstOf(code) is { } first && Remove(first.InstanceId);
}
