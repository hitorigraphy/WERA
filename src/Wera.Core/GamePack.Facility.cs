using System.IO.Compression;

namespace Wera.Core;

/// <summary>시설 마스터의 한 행: 시설 코드, 이름과 설치 조건(시설 조건 마스터. 그룹 안은 AND, 그룹끼리는 OR, 조건이 없으면 늘 설치할 수 있다).</summary>
public sealed record FacilityEntry(long Code, string Name, IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    public bool IsAvailable(IReadOnlyDictionary<long, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}

/// <summary>시설 설치 재료 마스터의 한 행: 시설(FacilityCode)을 짓는 데 GroupCode 그룹이 요구하는 커먼 아이템(ItemCode) Amount개.</summary>
public sealed record FacilityBuildCost(long FacilityCode, long GroupCode, long ItemCode, long Amount);

/// <summary>시설 하나를 지을 때 쓸 수 있는 재료 한 벌(그룹): 그룹 안은 모두(AND), 그룹끼리는 하나만(OR) 갖추면 된다.</summary>
public sealed record FacilityBuildGroup(long GroupCode, IReadOnlyList<FacilityBuildCost> Costs);

/// <summary>아지트 층계 마스터의 한 행: FloorOrder번째로 생긴 층(1은 처음부터 있는 층, 2는 첫 확장으로 생긴 층…)의 기본 시설.</summary>
/// <param name="HideoutCode">이 행을 쓰는 아지트(null이면 모든 아지트가 함께 쓴다).</param>
public sealed record HideoutFloorEntry(long FloorOrder, long FacilityCode, long? HideoutCode = null);

// facility_master(code, name)와 facility_condition_master(facility_code, group_number, flag_code, condition_type, condition_value; 복합키 (facility_code, group_number, flag_code)),
// facility_build_master(facility_code, group_code, item_code; 세 열이 키, amount), hideout_floor_master(floor_order(키), facility_code, 선택 열 hideout_code; (hideout_code, floor_order)가 키). 모두 선택이다.
public sealed partial class GamePack
{
    public const string FacilityMasterPath = "master/facility_master_v1.tsv";
    public const string FacilityConditionMasterPath = "master/facility_condition_master_v1.tsv";
    public const string FacilityBuildMasterPath = "master/facility_build_master_v1.tsv";
    public const string HideoutFloorMasterPath = "master/hideout_floor_master_v1.tsv";

    /// <summary>시설 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<FacilityEntry>? Facilities { get; init; }

    /// <summary>시설 설치 재료 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<FacilityBuildCost>? FacilityBuildCosts { get; init; }

    /// <summary>아지트 층계 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<HideoutFloorEntry>? HideoutFloors { get; init; }

    public FacilityEntry? FindFacility(long code) => Facilities?.FirstOrDefault(f => f.Code == code);

    /// <summary>이 시설을 짓는 데 쓸 수 있는 재료 그룹들(그룹 코드는 파일에 처음 나온 순서). 비어 있으면 재료 없이 지을 수 있다.</summary>
    public IReadOnlyList<FacilityBuildGroup> FacilityBuildGroupsOf(long facilityCode) =>
        (FacilityBuildCosts ?? []).Where(c => c.FacilityCode == facilityCode).GroupBy(c => c.GroupCode)
            .Select(g => new FacilityBuildGroup(g.Key, g.ToList())).ToList();

    /// <summary>시작 아지트에서 FloorOrder번째로 생긴 층의 기본 시설 코드. 정하지 않았으면 null.</summary>
    public long? BaseFacilityOf(long floorOrder) => BaseFacilityOf(StartHideoutCode, floorOrder);

    /// <summary>이 아지트에서 FloorOrder번째로 생긴 층의 기본 시설 코드(그 아지트 전용 행이 공용 행을 대신한다). 정하지 않았으면 null.</summary>
    public long? BaseFacilityOf(long hideoutCode, long floorOrder) =>
        ForHideout(HideoutFloors ?? [], hideoutCode, f => f.HideoutCode, f => f.FloorOrder).FirstOrDefault(f => f.FloorOrder == floorOrder)?.FacilityCode;

    /// <summary>시설 설치 재료 마스터: 시설은 시설 마스터에, 아이템은 커먼 아이템 인벤토리에 있어야 하고 개수는 1 이상이다. 세 열이 키라 같은 키를 두 번 쓸 수 없다.</summary>
    static List<FacilityBuildCost>? LoadFacilityBuildCosts(ZipArchive zip, List<FacilityEntry>? facilities, List<ItemEntry>? items)
    {
        const string file = FacilityBuildMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (facilities is null) throw new PackFormatException($"{file}가 있으려면 {FacilityMasterPath}도 필요합니다.");
        if (items is null) throw new PackFormatException($"{file}가 있으려면 {CommonItemInventoryMasterPath}도 필요합니다.");

        var facilityCodes = facilities.Select(f => f.Code).ToHashSet();
        var itemCodes = items.Select(i => i.Code).ToHashSet();
        var rows = new List<FacilityBuildCost>();
        var seen = new HashSet<(long, long, long)>();
        foreach (var r in Body(file, text, "facility_code", "group_code", "item_code", "amount"))
        {
            var facility = ParseLong(file, "시설 코드", r[0]);
            var who = $"시설 '{facility}'의";
            var group = ParseLong(file, $"{who} 그룹 코드", r[1]);
            var item = ParseLong(file, $"{who} 그룹 '{group}'의 아이템 코드", r[2]);
            var amount = ParseLong(file, $"{who} 그룹 '{group}' 아이템 '{item}'의 개수", r[3]);
            if (!facilityCodes.Contains(facility))
                throw new PackFormatException($"{file}: 시설 '{facility}'가 {FacilityMasterPath}에 없습니다.");
            if (!itemCodes.Contains(item))
                throw new PackFormatException($"{file}: {who} 그룹 '{group}'의 아이템 '{item}'가 {CommonItemInventoryMasterPath}에 없습니다.");
            if (amount < 1)
                throw new PackFormatException($"{file}: {who} 그룹 '{group}' 아이템 '{item}'의 개수 '{amount}'는 1 이상이어야 합니다.");
            if (!seen.Add((facility, group, item)))
                throw new PackFormatException($"{file}: 같은 키(시설 {facility}, 그룹 {group}, 아이템 {item})가 두 번 있습니다.");
            rows.Add(new FacilityBuildCost(facility, group, item, amount));
        }
        return rows;
    }

    /// <summary>아지트 층계 마스터: 층 순서는 1 이상이고 (아지트, 층 순서)마다 한 번씩만, 시설은 시설 마스터에 있어야 한다. 선택 열 hideout_code는 <see cref="HideoutBody"/>를 따른다.</summary>
    static List<HideoutFloorEntry>? LoadHideoutFloors(ZipArchive zip, List<FacilityEntry>? facilities, IReadOnlySet<long>? hideoutCodes)
    {
        const string file = HideoutFloorMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (facilities is null) throw new PackFormatException($"{file}가 있으려면 {FacilityMasterPath}도 필요합니다.");

        var facilityCodes = facilities.Select(f => f.Code).ToHashSet();
        var rows = new List<HideoutFloorEntry>();
        var seen = new HashSet<(long?, long)>();
        foreach (var (r, hideout) in HideoutBody(file, text, hideoutCodes, "floor_order", "facility_code"))
        {
            var order = ParseLong(file, "층 순서", r[0]);
            var facility = ParseLong(file, $"층 {order}의 기본 시설 코드", r[1]);
            if (order < 1)
                throw new PackFormatException($"{file}: 층 순서 '{order}'는 1 이상이어야 합니다.");
            if (!seen.Add((hideout, order)))
                throw new PackFormatException($"{file}: {HideoutLabel(hideout)}층 순서 '{order}'가 두 번 있습니다.");
            if (!facilityCodes.Contains(facility))
                throw new PackFormatException($"{file}: 층 {order}의 기본 시설 '{facility}'가 {FacilityMasterPath}에 없습니다.");
            rows.Add(new HideoutFloorEntry(order, facility, hideout));
        }
        return rows;
    }
}
