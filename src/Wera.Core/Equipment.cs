namespace Wera.Core;

/// <summary>장비 슬롯 마스터의 한 행(예: 무기, 방어구, 장신구).</summary>
public sealed record EquipmentSlotEntry(long Code, string Name);

/// <summary>태그 마스터의 한 행. 태그는 장비에 붙고, 장비 슬롯 태그 마스터가 "이 슬롯에는 어떤 태그의 장비가 들어가는가"를 정한다.</summary>
public sealed record TagEntry(long Code, string Name);

/// <summary>장비 마스터의 한 행.</summary>
public sealed record EquipmentItem(long ItemCode, string ItemName);

/// <summary>장비 태그 마스터의 한 행: 장비(ItemCode)가 가진 태그.</summary>
public sealed record EquipmentTag(long ItemCode, long TagCode);

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
