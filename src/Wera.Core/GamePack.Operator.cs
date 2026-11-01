using System.IO.Compression;

namespace Wera.Core;

/// <summary>오퍼레이터 슬롯 마스터의 한 행: 시설에서 일하는 오퍼레이터 슬롯의 종류.</summary>
public sealed record OperatorSlotEntry(long Code, string Name);

/// <summary>시설 오퍼레이터 슬롯 마스터의 한 행: 시설(FacilityCode)이 가진 오퍼레이터 슬롯(OperatorSlotCode).</summary>
public sealed record FacilityOperatorSlot(long FacilityCode, long OperatorSlotCode);

/// <summary>오퍼레이터 슬롯 태그 마스터의 한 행: 오퍼레이터 슬롯(OperatorSlotCode)에 서려면 캐릭터가 가져야 하는 태그(TagCode).</summary>
public sealed record OperatorSlotTag(long OperatorSlotCode, long TagCode);

// operator_slot_master(code, name), facility_operator_slot_master(facility_code, operator_slot_code; 두 열이 키), operator_slot_tag_master(operator_slot_code, tag_code; 두 열이 키). 모두 선택이다.
// 시설이 가진 오퍼레이터 슬롯은 (시설)로, 슬롯에 서는 캐릭터의 조건은 (오퍼레이터 슬롯)의 태그들로 알아낸다: 캐릭터는 그 태그를 모두 가져야 한다.
public sealed partial class GamePack
{
    public const string OperatorSlotMasterPath = "master/operator_slot_master_v1.tsv";
    public const string FacilityOperatorSlotMasterPath = "master/facility_operator_slot_master_v1.tsv";
    public const string OperatorSlotTagMasterPath = "master/operator_slot_tag_master_v1.tsv";

    /// <summary>오퍼레이터 슬롯 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<OperatorSlotEntry>? OperatorSlots { get; init; }

    /// <summary>시설 오퍼레이터 슬롯 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<FacilityOperatorSlot>? FacilityOperatorSlots { get; init; }

    /// <summary>오퍼레이터 슬롯 태그 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<OperatorSlotTag>? OperatorSlotTags { get; init; }

    public OperatorSlotEntry? FindOperatorSlot(long code) => OperatorSlots?.FirstOrDefault(s => s.Code == code);

    /// <summary>시설이 가진 오퍼레이터 슬롯들(오퍼레이터 슬롯 마스터 순서).</summary>
    public IReadOnlyList<OperatorSlotEntry> OperatorSlotsOf(long facilityCode)
    {
        var codes = (FacilityOperatorSlots ?? []).Where(r => r.FacilityCode == facilityCode).Select(r => r.OperatorSlotCode).ToHashSet();
        return (OperatorSlots ?? []).Where(s => codes.Contains(s.Code)).ToList();
    }

    /// <summary>오퍼레이터 슬롯에 서려면 가져야 하는 태그 코드들(파일 순서). 비어 있으면 누구나 설 수 있다.</summary>
    public IReadOnlyList<long> OperatorSlotTagsOf(long operatorSlotCode) =>
        (OperatorSlotTags ?? []).Where(r => r.OperatorSlotCode == operatorSlotCode).Select(r => r.TagCode).ToList();

    static List<OperatorSlotEntry>? LoadOperatorSlots(ZipArchive zip) =>
        LoadCodeNames(zip, OperatorSlotMasterPath, "code", "name", "오퍼레이터 슬롯", (code, name) => new OperatorSlotEntry(code, name));

    /// <summary>시설 오퍼레이터 슬롯 마스터: 시설는 시설 마스터에, 슬롯은 오퍼레이터 슬롯 마스터에 있어야 하고 같은 키를 두 번 쓸 수 없다.</summary>
    static List<FacilityOperatorSlot>? LoadFacilityOperatorSlots(ZipArchive zip, List<FacilityEntry>? facilities, List<OperatorSlotEntry>? slots)
    {
        const string file = FacilityOperatorSlotMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (facilities is null) throw new PackFormatException($"{file}가 있으려면 {FacilityMasterPath}도 필요합니다.");
        if (slots is null) throw new PackFormatException($"{file}가 있으려면 {OperatorSlotMasterPath}도 필요합니다.");

        var facilityCodes = facilities.Select(f => f.Code).ToHashSet();
        var slotCodes = slots.Select(s => s.Code).ToHashSet();
        var rows = new List<FacilityOperatorSlot>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "facility_code", "operator_slot_code"))
        {
            var facility = ParseLong(file, "시설 코드", r[0]);
            var slot = ParseLong(file, $"시설 '{facility}'의 오퍼레이터 슬롯 코드", r[1]);
            if (!facilityCodes.Contains(facility))
                throw new PackFormatException($"{file}: 시설 '{facility}'가 {FacilityMasterPath}에 없습니다.");
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: 시설 '{facility}'의 오퍼레이터 슬롯 '{slot}'가 {OperatorSlotMasterPath}에 없습니다.");
            if (!seen.Add((facility, slot)))
                throw new PackFormatException($"{file}: 같은 키(시설 {facility}, 오퍼레이터 슬롯 {slot})가 두 번 있습니다.");
            rows.Add(new FacilityOperatorSlot(facility, slot));
        }
        return rows;
    }

    /// <summary>오퍼레이터 슬롯 태그 마스터: 슬롯은 오퍼레이터 슬롯 마스터에, 태그는 태그 마스터에 있어야 하고 같은 키를 두 번 쓸 수 없다.</summary>
    static List<OperatorSlotTag>? LoadOperatorSlotTags(ZipArchive zip, List<OperatorSlotEntry>? slots, IReadOnlyList<TagEntry>? tags)
    {
        const string file = OperatorSlotTagMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (slots is null) throw new PackFormatException($"{file}가 있으려면 {OperatorSlotMasterPath}도 필요합니다.");
        if (tags is null) throw new PackFormatException($"{file}가 있으려면 {TagMasterPath}도 필요합니다.");

        var slotCodes = slots.Select(s => s.Code).ToHashSet();
        var tagCodes = tags.Select(t => t.Code).ToHashSet();
        var rows = new List<OperatorSlotTag>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "operator_slot_code", "tag_code"))
        {
            var slot = ParseLong(file, "오퍼레이터 슬롯 코드", r[0]);
            var tag = ParseLong(file, $"오퍼레이터 슬롯 '{slot}'의 태그 코드", r[1]);
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: 오퍼레이터 슬롯 '{slot}'가 {OperatorSlotMasterPath}에 없습니다.");
            if (!tagCodes.Contains(tag))
                throw new PackFormatException($"{file}: 오퍼레이터 슬롯 '{slot}'의 태그 '{tag}'가 태그 마스터에 없습니다.");
            if (!seen.Add((slot, tag)))
                throw new PackFormatException($"{file}: 같은 키(오퍼레이터 슬롯 {slot}, 태그 {tag})가 두 번 있습니다.");
            rows.Add(new OperatorSlotTag(slot, tag));
        }
        return rows;
    }
}
