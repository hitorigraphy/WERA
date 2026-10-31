using System.IO.Compression;

namespace Wera.Core;

/// <summary>부품 슬롯 마스터의 한 행: 장비(유니크 아이템)가 가진 부품을 끼우는 슬롯. 부품에는 커먼 아이템이 들어온다.</summary>
public sealed record PartSlotEntry(long Code, string Name);

/// <summary>
/// 부품 슬롯 태그 마스터의 한 행. 유니크 아이템(ItemCode)의 부품 슬롯(PartSlotCode)에 부품(커먼 아이템)을 끼울 수 있는 조건의 일부:
/// 같은 (유니크 아이템, 슬롯)에서 같은 GroupNumber인 행들이 한 그룹이고, 그룹 안은 모두 만족해야 한다(AND).
/// IsPositive가 참이면 부품이 그 태그를 가져야 하고, 거짓이면 가지면 안 된다(장비 슬롯 태그, 스킬 슬롯 태그와 같다).
/// 복합키는 (ItemCode, PartSlotCode, GroupNumber, TagCode) 네 열이고 IsPositive는 값이다.
/// </summary>
public sealed record UniqueItemPartSlotTag(long ItemCode, long PartSlotCode, int GroupNumber, long TagCode, bool IsPositive);

/// <summary>"이 부품이 이 슬롯의 태그 조건에 부합하는가"의 판정(<see cref="EquipmentRules"/>와 같은 규칙).</summary>
public static class PartSlotRules
{
    /// <summary>그룹 안은 AND(양성 태그는 있어야 하고 음성 태그는 없어야 함), 그룹끼리는 OR이다. 규칙이 하나도 없으면 아무것도 끼울 수 없다.</summary>
    public static bool Matches(IEnumerable<UniqueItemPartSlotTag> rules, IReadOnlySet<long> tags) =>
        rules.GroupBy(r => r.GroupNumber)
            .Any(group => group.All(r => tags.Contains(r.TagCode) == r.IsPositive));
}

// part_slot_master(code, name)와 part_slot_tag_master(item_code, part_slot_code, group_number, tag_code, is_positive; 앞의 네 열이 키). 둘 다 선택이다.
public sealed partial class GamePack
{
    public const string PartSlotMasterPath = "master/part_slot_master_v1.tsv";
    public const string PartSlotTagMasterPath = "master/part_slot_tag_master_v1.tsv";

    /// <summary>부품 슬롯 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<PartSlotEntry>? PartSlots { get; init; }

    /// <summary>부품 슬롯 태그 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<UniqueItemPartSlotTag>? PartSlotTags { get; init; }

    public PartSlotEntry? FindPartSlot(long code) => PartSlots?.FirstOrDefault(s => s.Code == code);

    /// <summary>장비가 가진 부품 슬롯들: 부품 슬롯 마스터의 모든 슬롯(슬롯 코드 순). 태그 마스터는 슬롯의 유무가 아니라 무엇을 끼울 수 있는지만 정한다.</summary>
    public IReadOnlyList<PartSlotEntry> PartSlotsOf(long uniqueItemCode) =>
        (PartSlots ?? []).OrderBy(s => s.Code).ToList();

    /// <summary>유니크 아이템 하나의 부품 슬롯 태그 규칙(슬롯, 그룹, 태그 코드 순).</summary>
    public IReadOnlyList<UniqueItemPartSlotTag> PartSlotTagsOf(long uniqueItemCode) =>
        (PartSlotTags ?? []).Where(t => t.ItemCode == uniqueItemCode)
            .OrderBy(t => t.PartSlotCode).ThenBy(t => t.GroupNumber).ThenBy(t => t.TagCode).ToList();

    /// <summary>합친 태그(장비의 태그 + 부품의 태그)가 이 장비의 이 슬롯 규칙에 부합하는가. 규칙이 하나도 없는 슬롯에는 아무것도 끼울 수 없다.</summary>
    public bool PartFits(long uniqueItemCode, long partSlotCode, IReadOnlySet<long> tags)
    {
        var rules = PartSlotTagsOf(uniqueItemCode).Where(t => t.PartSlotCode == partSlotCode).ToList();
        return rules.Count > 0 && PartSlotRules.Matches(rules, tags);
    }

    /// <summary>마스터 태그 기준으로 이 커먼 아이템을 이 장비의 이 부품 슬롯에 끼울 수 있는가(장비의 마스터 태그와 부품의 태그를 합쳐서 본다).</summary>
    public bool CanInstallPart(long uniqueItemCode, long partSlotCode, long commonItemCode)
    {
        if (FindItem(commonItemCode) is null) return false;
        var tags = MasterTagsOfCommonItem(commonItemCode).ToHashSet();
        tags.UnionWith(MasterTagsOfUniqueItem(uniqueItemCode));
        return PartFits(uniqueItemCode, partSlotCode, tags);
    }

    static List<PartSlotEntry>? LoadPartSlots(ZipArchive zip) =>
        LoadCodeNames(zip, PartSlotMasterPath, "code", "name", "부품 슬롯", (code, name) => new PartSlotEntry(code, name));

    /// <summary>부품 슬롯 태그 마스터: 유니크 아이템은 유니크 아이템 인벤토리에, 부품 슬롯은 부품 슬롯 마스터에, 태그는 태그 마스터에 있어야 하고 네 열이 키라 같은 키를 두 번 쓸 수 없다.</summary>
    static List<UniqueItemPartSlotTag>? LoadPartSlotTags(
        ZipArchive zip, List<UniqueItem>? uniqueItems, List<PartSlotEntry>? partSlots, List<TagEntry>? tags)
    {
        const string file = PartSlotTagMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (uniqueItems is null) throw new PackFormatException($"{file}가 있으려면 {UniqueItemInventoryMasterPath}도 필요합니다.");
        if (partSlots is null) throw new PackFormatException($"{file}가 있으려면 {PartSlotMasterPath}도 필요합니다.");
        if (tags is null) throw new PackFormatException($"{file}가 있으려면 {TagMasterPath}도 필요합니다.");

        var itemCodes = uniqueItems.Select(u => u.ItemCode).ToHashSet();
        var slotCodes = partSlots.Select(s => s.Code).ToHashSet();
        var tagCodes = tags.Select(t => t.Code).ToHashSet();
        var rows = new List<UniqueItemPartSlotTag>();
        var seen = new HashSet<(long, long, int, long)>();
        foreach (var r in Body(file, text, "item_code", "part_slot_code", "group_number", "tag_code", "is_positive"))
        {
            var item = ParseLong(file, "유니크 아이템 코드", r[0]);
            var slot = ParseLong(file, $"유니크 아이템 '{item}'의 부품 슬롯 코드", r[1]);
            var who = $"유니크 아이템 '{item}' 부품 슬롯 '{slot}'의";
            if (!int.TryParse(r[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var group))
                throw new PackFormatException($"{file}: {who} 그룹넘버 '{r[2]}'이(가) 정수가 아닙니다.");
            var tag = ParseLong(file, $"{who} 태그 코드", r[3]);
            var positive = ParseBool(file, $"{who} is_positive", r[4]);
            if (!itemCodes.Contains(item))
                throw new PackFormatException($"{file}: 유니크 아이템 '{item}'가 유니크 아이템 인벤토리 마스터에 없습니다.");
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: {who} 부품 슬롯 '{slot}'가 부품 슬롯 마스터에 없습니다.");
            if (!tagCodes.Contains(tag))
                throw new PackFormatException($"{file}: {who} 태그 '{tag}'가 태그 마스터에 없습니다.");
            if (!seen.Add((item, slot, group, tag)))
                throw new PackFormatException($"{file}: 같은 키(유니크 아이템 {item}, 부품 슬롯 {slot}, 그룹 {group}, 태그 {tag})가 두 번 있습니다.");
            rows.Add(new UniqueItemPartSlotTag(item, slot, group, tag, positive));
        }
        return rows;
    }
}
