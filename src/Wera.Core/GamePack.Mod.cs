using System.IO.Compression;

namespace Wera.Core;

/// <summary>개조 슬롯 마스터의 한 행: 장비(유니크 아이템)에 개조로 아이템을 끼우는 슬롯의 종류.</summary>
public sealed record ModSlotEntry(long Code, string Name);

/// <summary>유니크 아이템 개조 스테이터스 마스터의 한 행: 유니크 아이템(UniqueItemCode)의 개조 슬롯(ModSlotCode)에 커먼 아이템(ItemCode)을 끼우면 이 스테이터스(StatusCode)에 더하는 값(Value).</summary>
public sealed record UniqueItemModStatus(long UniqueItemCode, long ModSlotCode, long ItemCode, long StatusCode, long Value);

// mod_slot_master(code, name)와 unique_item_mod_status_master(unique_item_code, mod_slot_code, item_code, status_code; 네 열이 키, value). 둘 다 선택이다.
// 개조는 코드와 파일 이름에서 mod로 부른다.
// 장비가 가진 개조 슬롯은 (유니크 아이템)으로, 슬롯에 끼울 수 있는 아이템은 (유니크 아이템, 개조 슬롯)으로 묶어 중복 없이 알아낸다.
public sealed partial class GamePack
{
    public const string ModSlotMasterPath = "master/mod_slot_master_v1.tsv";
    public const string UniqueItemModStatusMasterPath = "master/unique_item_mod_status_master_v1.tsv";

    /// <summary>개조 슬롯 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<ModSlotEntry>? ModSlots { get; init; }

    /// <summary>유니크 아이템 개조 스테이터스 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<UniqueItemModStatus>? UniqueItemModStatuses { get; init; }

    public ModSlotEntry? FindModSlot(long code) => ModSlots?.FirstOrDefault(s => s.Code == code);

    /// <summary>장비가 가진 개조 슬롯들: 이 유니크 아이템의 개조 스테이터스 행에 나오는 슬롯을 중복 없이(개조 슬롯 마스터 순서).</summary>
    public IReadOnlyList<ModSlotEntry> ModSlotsOf(long uniqueItemCode)
    {
        var codes = (UniqueItemModStatuses ?? []).Where(r => r.UniqueItemCode == uniqueItemCode).Select(r => r.ModSlotCode).ToHashSet();
        return (ModSlots ?? []).Where(s => codes.Contains(s.Code)).ToList();
    }

    /// <summary>장비의 개조 슬롯에 끼울 수 있는 커먼 아이템의 코드들: (유니크 아이템, 개조 슬롯)의 행에 나오는 아이템을 중복 없이(파일 순서).</summary>
    public IReadOnlyList<long> ModItemsOf(long uniqueItemCode, long modSlotCode) =>
        (UniqueItemModStatuses ?? []).Where(r => r.UniqueItemCode == uniqueItemCode && r.ModSlotCode == modSlotCode).Select(r => r.ItemCode).Distinct().ToList();

    static List<ModSlotEntry>? LoadModSlots(ZipArchive zip) =>
        LoadCodeNames(zip, ModSlotMasterPath, "code", "name", "개조 슬롯", (code, name) => new ModSlotEntry(code, name));

    /// <summary>
    /// 유니크 아이템 개조 스테이터스 마스터: 유니크 아이템은 유니크 아이템 인벤토리에, 개조 슬롯은 개조 슬롯 마스터에, 아이템은 커먼 아이템 인벤토리에, 스테이터스는
    /// 스테이터스 마스터에 있어야 하고 그 유니크 아이템의 유니크 아이템 스테이터스 마스터 행(초깃값에 더해 저장하므로)도 있어야 한다. 네 열이 키라 같은 키를 두 번 쓸 수 없다. 값은 long이다.
    /// </summary>
    static List<UniqueItemModStatus>? LoadUniqueItemModStatuses(
        ZipArchive zip, IEnumerable<long>? uniqueItemCodes, List<ModSlotEntry>? modSlots, List<ItemEntry>? items, List<StatusEntry>? statuses, List<OwnerStatus>? uniqueItemStatuses)
    {
        const string file = UniqueItemModStatusMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (uniqueItemCodes is null) throw new PackFormatException($"{file}가 있으려면 {UniqueItemInventoryMasterPath}도 필요합니다.");
        if (modSlots is null) throw new PackFormatException($"{file}가 있으려면 {ModSlotMasterPath}도 필요합니다.");
        if (items is null) throw new PackFormatException($"{file}가 있으려면 {CommonItemInventoryMasterPath}도 필요합니다.");
        if (statuses is null) throw new PackFormatException($"{file}가 있으려면 {StatusMasterPath}도 필요합니다.");
        if (uniqueItemStatuses is null) throw new PackFormatException($"{file}가 있으려면 {UniqueItemStatusMasterPath}도 필요합니다.");

        var uniques = uniqueItemCodes.ToHashSet();
        var slotCodes = modSlots.Select(s => s.Code).ToHashSet();
        var itemCodes = items.Select(i => i.Code).ToHashSet();
        var statusCodes = statuses.Select(s => s.Code).ToHashSet();
        var owned = uniqueItemStatuses.Select(s => (s.OwnerCode, s.StatusCode)).ToHashSet();
        var rows = new List<UniqueItemModStatus>();
        var seen = new HashSet<(long, long, long, long)>();
        foreach (var r in Body(file, text, "unique_item_code", "mod_slot_code", "item_code", "status_code", "value"))
        {
            var unique = ParseLong(file, "유니크 아이템 코드", r[0]);
            var who = $"유니크 아이템 '{unique}'의";
            var slot = ParseLong(file, $"{who} 개조 슬롯 코드", r[1]);
            var item = ParseLong(file, $"{who} 개조 슬롯 '{slot}'의 아이템 코드", r[2]);
            var status = ParseLong(file, $"{who} 개조 슬롯 '{slot}' 아이템 '{item}'의 스테이터스 코드", r[3]);
            var value = ParseLong(file, $"{who} 개조 슬롯 '{slot}' 아이템 '{item}' 스테이터스 '{status}'의 값", r[4]);
            if (!uniques.Contains(unique))
                throw new PackFormatException($"{file}: 유니크 아이템 '{unique}'가 {UniqueItemInventoryMasterPath}에 없습니다.");
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: {who} 개조 슬롯 '{slot}'가 {ModSlotMasterPath}에 없습니다.");
            if (!itemCodes.Contains(item))
                throw new PackFormatException($"{file}: {who} 개조 슬롯 '{slot}'의 아이템 '{item}'가 {CommonItemInventoryMasterPath}에 없습니다.");
            if (!statusCodes.Contains(status))
                throw new PackFormatException($"{file}: {who} 스테이터스 '{status}'가 스테이터스 마스터에 없습니다.");
            if (!owned.Contains((unique, status)))
                throw new PackFormatException($"{file}: {who} 스테이터스 '{status}'가 {UniqueItemStatusMasterPath}에 없습니다.");
            if (!seen.Add((unique, slot, item, status)))
                throw new PackFormatException($"{file}: 같은 키(유니크 아이템 {unique}, 개조 슬롯 {slot}, 아이템 {item}, 스테이터스 {status})가 두 번 있습니다.");
            rows.Add(new UniqueItemModStatus(unique, slot, item, status, value));
        }
        return rows;
    }
}
