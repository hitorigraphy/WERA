using System.IO.Compression;

namespace Wera.Core;

// 장비: 슬롯 마스터, 태그 마스터, 장비 마스터, 장비 태그 마스터, 장비 슬롯 태그 마스터.
public sealed partial class GamePack
{
    public const string EquipmentSlotMasterPath = "master/equipment_slot_master_v1.tsv";
    public const string TagMasterPath = "master/tag_master_v1.tsv";
    public const string EquipmentMasterPath = "master/equipment_master_v1.tsv";
    public const string EquipmentTagMasterPath = "master/equipment_tag_master_v1.tsv";
    public const string EquipmentSlotTagMasterPath = "master/equipment_slot_tag_master_v1.tsv";

    /// <summary>장비 슬롯 마스터가 없으면 null.</summary>
    public IReadOnlyList<EquipmentSlotEntry>? EquipmentSlots { get; init; }

    /// <summary>태그 마스터가 없으면 null.</summary>
    public IReadOnlyList<TagEntry>? Tags { get; init; }

    /// <summary>장비 마스터가 없으면 null.</summary>
    public IReadOnlyList<EquipmentItem>? EquipmentItems { get; init; }

    /// <summary>장비 태그 마스터가 없으면 null.</summary>
    public IReadOnlyList<EquipmentTag>? EquipmentTags { get; init; }

    /// <summary>장비 슬롯 태그 마스터가 없으면 null. 게임팩에 적힌 순서 그대로다.</summary>
    public IReadOnlyList<EquipmentSlotTag>? EquipmentSlotTags { get; init; }

    /// <summary>캐릭터가 가진 장비 슬롯들. 장비 슬롯 태그 마스터에 그 캐릭터로 나오는 슬롯이며, 처음 나온 순서다.</summary>
    public IReadOnlyList<EquipmentSlotEntry> EquipmentSlotsOf(long characterCode)
    {
        // 보조키 (캐릭터, 슬롯)의 범위 조회로 그 캐릭터의 규칙 행들을 모은 뒤, 파일에 처음 나온 순서로 슬롯을 고른다.
        var rows = Masters.EquipmentSlotTagRowTable
            .FindRangeByCharacterCodeAndEquipmentSlotCode((characterCode, long.MinValue), (characterCode, long.MaxValue))
            .OrderBy(r => r.Order);
        return rows.Select(r => r.EquipmentSlotCode).Distinct().Select(code => FindEquipmentSlot(code)!).ToList();
    }

    /// <summary>장비가 이 캐릭터의 이 슬롯에 낄 수 있는가: 슬롯 태그 마스터의 규칙에 장비의 태그가 부합해야 한다.</summary>
    public bool CanEquip(long characterCode, long equipmentSlotCode, long itemCode)
    {
        var rules = SlotTagRulesOf(characterCode, equipmentSlotCode);
        if (rules.Count == 0 || FindEquipmentItem(itemCode) is null) return false;

        return EquipmentRules.Matches(rules, TagsOfEquipment(itemCode));
    }

    /// <summary>이 캐릭터의 이 슬롯에 낄 수 있는 장비들(장비 마스터 순서).</summary>
    public IReadOnlyList<EquipmentItem> EquippableItems(long characterCode, long equipmentSlotCode) =>
        (EquipmentItems ?? []).Where(i => CanEquip(characterCode, equipmentSlotCode, i.ItemCode)).ToList();

    // ---- 로더 ----

    /// <summary>"코드, 이름" 두 열짜리 마스터를 읽는다. 파일이 없으면 null.</summary>
    static List<T>? LoadCodeNames<T>(
        ZipArchive zip, string path, string codeColumn, string nameColumn, string noun, Func<long, string, T> create)
    {
        var text = TryReadEntry(zip, path);
        if (text is null) return null;

        var rows = new List<T>();
        var seen = new HashSet<long>();
        foreach (var r in Body(path, text, codeColumn, nameColumn))
        {
            var code = ParseLong(path, $"{noun} 코드", r[0]);
            if (!seen.Add(code))
                throw new PackFormatException($"{path}: {noun} 코드 '{code}'가 중복됩니다.");
            rows.Add(create(code, r[1]));
        }
        return rows;
    }

    static List<EquipmentSlotEntry>? LoadEquipmentSlots(ZipArchive zip) =>
        LoadCodeNames(zip, EquipmentSlotMasterPath, "equipment_slot_code", "equipment_slot_name", "장비 슬롯",
            (code, name) => new EquipmentSlotEntry(code, name));

    static List<TagEntry>? LoadTags(ZipArchive zip) =>
        LoadCodeNames(zip, TagMasterPath, "tag_code", "tag_name", "태그", (code, name) => new TagEntry(code, name));

    static List<EquipmentItem>? LoadEquipmentItems(ZipArchive zip) =>
        LoadCodeNames(zip, EquipmentMasterPath, "item_code", "item_name", "장비 아이템",
            (code, name) => new EquipmentItem(code, name));

    /// <summary>장비 태그 마스터: 장비는 장비 마스터에, 태그는 태그 마스터에 있어야 하고, 같은 (장비, 태그)를 두 번 쓸 수 없다.</summary>
    static List<EquipmentTag>? LoadEquipmentTags(ZipArchive zip, List<EquipmentItem>? items, List<TagEntry>? tags)
    {
        const string file = EquipmentTagMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (items is null) throw new PackFormatException($"{file}가 있으려면 {EquipmentMasterPath}도 필요합니다.");
        if (tags is null) throw new PackFormatException($"{file}가 있으려면 {TagMasterPath}도 필요합니다.");

        var itemCodes = items.Select(i => i.ItemCode).ToHashSet();
        var tagCodes = tags.Select(t => t.Code).ToHashSet();
        var rows = new List<EquipmentTag>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "item_code", "tag_code"))
        {
            var item = ParseLong(file, "장비 아이템 코드", r[0]);
            var tag = ParseLong(file, $"장비 '{item}'의 태그 코드", r[1]);
            if (!itemCodes.Contains(item))
                throw new PackFormatException($"{file}: 장비 '{item}'가 장비 마스터에 없습니다.");
            if (!tagCodes.Contains(tag))
                throw new PackFormatException($"{file}: 장비 '{item}'의 태그 '{tag}'가 태그 마스터에 없습니다.");
            if (!seen.Add((item, tag)))
                throw new PackFormatException($"{file}: 장비 '{item}'에 태그 '{tag}'가 두 번 있습니다.");
            rows.Add(new EquipmentTag(item, tag));
        }
        return rows;
    }

    /// <summary>
    /// 장비 슬롯 태그 마스터: 캐릭터는 캐릭터 인벤토리에, 슬롯은 장비 슬롯 마스터에, 태그는 태그 마스터에 있어야 한다.
    /// 앞의 네 열(캐릭터, 슬롯, 태그, 그룹)이 복합키라 같은 키를 두 번 쓸 수 없다(is_positive는 키가 아니라 값).
    /// </summary>
    static List<EquipmentSlotTag>? LoadEquipmentSlotTags(
        ZipArchive zip, List<CharacterEntry>? characters, List<EquipmentSlotEntry>? slots, List<TagEntry>? tags)
    {
        const string file = EquipmentSlotTagMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (characters is null) throw new PackFormatException($"{file}가 있으려면 {CharacterInventoryMasterPath}도 필요합니다.");
        if (slots is null) throw new PackFormatException($"{file}가 있으려면 {EquipmentSlotMasterPath}도 필요합니다.");
        if (tags is null) throw new PackFormatException($"{file}가 있으려면 {TagMasterPath}도 필요합니다.");

        var characterCodes = characters.Select(c => c.Code).ToHashSet();
        var slotCodes = slots.Select(s => s.Code).ToHashSet();
        var tagCodes = tags.Select(t => t.Code).ToHashSet();
        var rows = new List<EquipmentSlotTag>();
        var seen = new HashSet<(long Character, long Slot, long Tag, int Group)>();
        foreach (var r in Body(file, text, "character_code", "equipment_slot_code", "tag_code", "group_number", "is_positive"))
        {
            var character = ParseLong(file, "캐릭터 코드", r[0]);
            var slot = ParseLong(file, $"캐릭터 '{character}'의 장비 슬롯 코드", r[1]);
            var tag = ParseLong(file, $"캐릭터 '{character}' 슬롯 '{slot}'의 태그 코드", r[2]);
            var who = $"캐릭터 '{character}' 슬롯 '{slot}'의";
            if (!int.TryParse(r[3], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var group))
                throw new PackFormatException($"{file}: {who} 그룹넘버 '{r[3]}'이(가) 정수가 아닙니다.");
            var positive = ParseBool(file, $"{who} is_positive", r[4]);

            if (!characterCodes.Contains(character))
                throw new PackFormatException($"{file}: 캐릭터 '{character}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: {who} 장비 슬롯 '{slot}'가 장비 슬롯 마스터에 없습니다.");
            if (!tagCodes.Contains(tag))
                throw new PackFormatException($"{file}: {who} 태그 '{tag}'가 태그 마스터에 없습니다.");

            // 복합키는 앞의 네 열이다. is_positive만 다른 행도 같은 키이므로 중복이다.
            if (!seen.Add((character, slot, tag, group)))
                throw new PackFormatException(
                    $"{file}: 같은 키(캐릭터 {character}, 슬롯 {slot}, 태그 {tag}, 그룹 {group})가 두 번 있습니다.");
            rows.Add(new EquipmentSlotTag(character, slot, tag, group, positive));
        }
        return rows;
    }

    /// <summary>참/거짓 칸: true/false(대소문자 무시) 또는 1/0.</summary>
    static bool ParseBool(string file, string what, string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            "" => throw new PackFormatException($"{file}: {what}이(가) 비어 있습니다."),
            _ => throw new PackFormatException($"{file}: {what} '{value}'이(가) true/false(또는 1/0)가 아닙니다."),
        };
}
