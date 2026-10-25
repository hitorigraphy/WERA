namespace Wera.Core;

/// <summary>장비 슬롯 마스터의 한 행(예: 무기, 방어구, 장신구).</summary>
public sealed record EquipmentSlotEntry(long Code, string Name);

/// <summary>태그 마스터의 한 행. 태그는 장비에 붙고, 장비 슬롯 태그 마스터가 "이 슬롯에는 어떤 태그의 장비가 들어가는가"를 정한다.</summary>
public sealed record TagEntry(long Code, string Name);

/// <summary>유니크 아이템 마스터의 한 행. 유니크 아이템의 코드 공간은 아이템 인벤토리와 따로다.</summary>
public sealed record UniqueItem(long ItemCode, string ItemName);

/// <summary>유니크 아이템 태그 마스터의 한 행: 유니크 아이템(ItemCode)이 가진 태그.</summary>
public sealed record UniqueItemTag(long ItemCode, long TagCode);

/// <summary>커먼 아이템 태그 마스터의 한 행: 아이템 인벤토리의 아이템(ItemCode)이 가진 태그.</summary>
public sealed record CommonItemTag(long ItemCode, long TagCode);

/// <summary>캐릭터 태그 마스터의 한 행: 캐릭터(CharacterCode)가 가진 태그.</summary>
public sealed record CharacterTag(long CharacterCode, long TagCode);

/// <summary>장비 슬롯에 들어갈 수 있는 것의 종류. 코드 공간이 종류마다 따로라서 코드만으로는 구분되지 않는다.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<EquipKind>))]
public enum EquipKind
{
    /// <summary>유니크 아이템 인스턴스(EquipRef.Code는 인스턴스 ID).</summary>
    UniqueItem,
    /// <summary>커먼 아이템(아이템 인벤토리. 보유 개수는 인벤토리 수량).</summary>
    CommonItem,
    /// <summary>캐릭터(펫, 소환수 같은 것. 보유 캐릭터여야 한다).</summary>
    Character,
}

/// <summary>
/// 장비 슬롯에 끼는 것 하나: 종류와 그 종류 안의 코드. 유니크 아이템은 아이템 코드가 아니라
/// 인스턴스 ID(<see cref="UniqueItemInstance"/>)로 가리킨다.
/// </summary>
public readonly record struct EquipRef(EquipKind Kind, long Code)
{
    public static EquipRef Unique(long instanceId) => new(EquipKind.UniqueItem, instanceId);
    public static EquipRef Common(long code) => new(EquipKind.CommonItem, code);
    public static EquipRef Char(long code) => new(EquipKind.Character, code);
}

/// <summary>
/// 유니크 아이템 인스턴스: 보유한 유니크 아이템 한 개. 만들어질 때 유니크 아이템 태그 마스터의 태그를 복사해 가지고,
/// 게임 중에 태그가 사라지거나 추가될 수 있다(그 태그가 세이브에 저장된다).
/// </summary>
public sealed record UniqueItemInstance(long ItemCode, List<long>? Tags = null);

/// <summary>낄 수 있는 것의 목록 항목.</summary>
public sealed record EquipCandidate(EquipRef Ref, string Name);

/// <summary>
/// 장비 슬롯 태그 마스터의 한 행. 캐릭터(CharacterCode)의 장비 슬롯(EquipmentSlotCode)에 장비를 낄 수 있는 조건의 일부:
/// 같은 (캐릭터, 슬롯)에서 같은 GroupNumber인 행들이 한 그룹이고, 그룹 안은 모두 만족해야 한다(AND).
/// IsPositive가 참이면 장비가 그 태그를 가져야 하고, 거짓이면 가지면 안 된다.
/// 복합키는 (CharacterCode, EquipmentSlotCode, TagCode, GroupNumber) 네 열이고 IsPositive는 값이다.
/// </summary>
public sealed record EquipmentSlotTag(long CharacterCode, long EquipmentSlotCode, long TagCode, int GroupNumber, bool IsPositive);

/// <summary>"이 장비가 이 슬롯의 태그 조건에 부합하는가"의 판정.</summary>
public static class EquipmentRules
{
    /// <summary>
    /// 한 (캐릭터, 슬롯)의 태그 규칙에 장비의 태그가 부합하는지. 그룹 안은 AND(양성 태그는 있어야 하고 음성 태그는 없어야 함),
    /// 그룹끼리는 OR이다. 규칙이 하나도 없으면 아무것도 낄 수 없다.
    /// </summary>
    public static bool Matches(IEnumerable<EquipmentSlotTag> rules, IReadOnlySet<long> equipmentTags) =>
        rules.GroupBy(r => r.GroupNumber)
            .Any(group => group.All(r => equipmentTags.Contains(r.TagCode) == r.IsPositive));
}

/// <summary>캐릭터 스테이터스 마스터의 한 행(예: 레벨, 최대 HP, 경험치 상한).</summary>
public sealed record CharacterStatusEntry(long Code, string Name);

/// <summary>캐릭터 초기 스테이터스 마스터의 한 행: 캐릭터(CharacterCode)의 스테이터스(StatusCode) 초깃값.</summary>
public sealed record CharacterInitialStatus(long CharacterCode, long StatusCode, long InitialValue);
