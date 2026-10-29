using System.Collections;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 유닛: 캐릭터와 그 캐릭터가 낀 장비. 장비는 슬롯 코드 → 낀 것이고 유닛에 속한다(유닛이 파티에서 빠지면 장비는 보유로 돌아간다).
/// 적 파티의 프리셋 유닛(<see cref="PresetUnit"/>)도 같은 모양이다.
/// </summary>
[method: JsonConstructor]
public sealed record PartyUnit(long CharacterCode, Dictionary<long, EquipRef> Equipment)
{
    /// <summary>장비 없는 새 유닛.</summary>
    public PartyUnit(long characterCode) : this(characterCode, []) { }

    /// <summary>저장용 복사본(장비 사전도 새로 만든다).</summary>
    public PartyUnit Copy() => new(CharacterCode, new Dictionary<long, EquipRef>(Equipment));
}

/// <summary>
/// 파티 인스턴스: 이름과 유닛 아홉 칸(칸 번호 0~2 전위, 3~5 중위, 6~8 후위). 빈 칸은 null이다.
/// 유닛 = 캐릭터 + 장비, 파티 = 유닛들.
/// </summary>
public sealed record PartyInstance(string Name, PartyUnit?[] Units)
{
    /// <summary>파티 슬롯 수: 전위/중위/후위 3줄 × 3칸.</summary>
    public const int SlotCount = 9;

    public static PartyInstance Empty(string name) => new(name, new PartyUnit?[SlotCount]);

    /// <summary>이 캐릭터의 유닛이 있는 칸(없으면 -1).</summary>
    public int IndexOf(long characterCode)
    {
        for (var i = 0; i < Units.Length; i++)
            if (Units[i]?.CharacterCode == characterCode) return i;
        return -1;
    }

    /// <summary>이 캐릭터의 유닛. 파티에 없으면 null.</summary>
    public PartyUnit? UnitOf(long characterCode) => IndexOf(characterCode) is var at and >= 0 ? Units[at] : null;

    /// <summary>저장용 복사본(유닛과 장비도 새로 만든다).</summary>
    public PartyInstance Copy() => new(Name, Units.Select(u => u?.Copy()).ToArray());
}

/// <summary>
/// 파티 칸의 캐릭터 코드를 칸 번호로 읽고 쓰는 얇은 창: <c>Party[0] = 7001</c>은 그 칸에 그 캐릭터의 새 유닛(장비 없음)을 두고,
/// 다른 캐릭터가 있었다면 그 유닛은 장비와 함께 빠진다(장비는 보유로 돌아간다). 이미 그 캐릭터가 그 칸에 있으면 아무것도 바꾸지 않는다.
/// null을 쓰면 칸이 빈다. 읽으면 칸의 캐릭터 코드(빈 칸은 null)다. 세션의 <see cref="PartyInstance"/>를 그대로 들여다본다.
/// </summary>
public sealed class PartySlots(PartyInstance party) : IEnumerable<long?>
{
    public int Length => party.Units.Length;

    public long? this[int slot]
    {
        get => party.Units[slot]?.CharacterCode;
        set
        {
            if (party.Units[slot]?.CharacterCode == value) return;
            party.Units[slot] = value is { } code ? new PartyUnit(code) : null;
        }
    }

    /// <summary>이 캐릭터가 있는 칸(없으면 -1).</summary>
    public int IndexOf(long characterCode) => party.IndexOf(characterCode);

    public IEnumerator<long?> GetEnumerator() => party.Units.Select(u => u?.CharacterCode).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
