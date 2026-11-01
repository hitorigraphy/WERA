using System.IO.Compression;

namespace Wera.Core;

/// <summary>
/// 시설 흐름 칸의 종류(flow_type 열에 이름으로 적는다). 턴 종료 뒤 시설마다 흐름 번호 순으로 실행해 아이템 등을 생산한다.
/// 이름이 같은 칸의 규칙과 인자는 이벤트 흐름(<see cref="EventFlowType"/>)과 같다. 점프하는 칸은 같은 시설의 자신보다 큰 흐름 번호로만 갈 수 있다.
/// </summary>
public enum FacilityFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
    /// <summary>시설 서킷을 끝낸다.</summary>
    Finish = 1,
    /// <summary>조건 없이 뒤쪽의 흐름 칸으로 점프한다. param_1이 점프할 flow_number다.</summary>
    Jump = 2,
    /// <summary>플레이어 플래그를 정한 값으로 바꾼다. param_1이 flag_code, param_2가 value다.</summary>
    SetFlag = 3,
    /// <summary>플레이어 플래그에 값을 더한다(음수면 뺀다). param_1이 flag_code, param_2가 더할 value다.</summary>
    ModFlag = 4,
    /// <summary>인벤토리에 개수를 더한다(음수면 뺀다. 생산과 재료 소모에 쓴다). param_1이 inventory_code, param_2가 개수다. 유니크 아이템과 캐릭터는 1 또는 -1이어야 한다.</summary>
    AddInventory = 5,
    /// <summary>플래그 값이 flag_value와 비교해 참이면 target_flow_number로 점프한다. param_1 flag_code, param_2 flag_value, param_3 target_flow_number.</summary>
    JumpFlagEqual = 6,
    JumpFlagNotEqual = 7,
    JumpFlagGreaterThan = 8,
    JumpFlagGreaterOrEqual = 9,
    JumpFlagLessThan = 10,
    JumpFlagLessOrEqual = 11,
    /// <summary>인벤토리 보유 개수가 value와 비교해 참이면 target_flow_number로 점프한다(재료가 있는지 보는 데 쓴다). param_1 inventory_code, param_2 count, param_3 target_flow_number.</summary>
    JumpInventoryEqual = 12,
    JumpInventoryNotEqual = 13,
    JumpInventoryGreaterThan = 14,
    JumpInventoryGreaterOrEqual = 15,
    JumpInventoryLessThan = 16,
    JumpInventoryLessOrEqual = 17,
    /// <summary>캐릭터가 태그를 가지면 점프한다. param_1 캐릭터 코드, param_2 tag_code, param_3 target_flow_number.</summary>
    JumpCharacterHaveTag = 18,
    /// <summary>유니크 아이템이 태그를 가지면 점프한다. param_1 유니크 아이템 코드, param_2 tag_code, param_3 target_flow_number.</summary>
    JumpUniqueItemHaveTag = 19,
    /// <summary>확률로 점프한다. param_1 chance, param_2 bound(1 이상), param_3 target_flow_number(0 이상 bound 미만의 난수가 chance보다 작으면 점프).</summary>
    JumpRandom = 20,
    /// <summary>이 시설의 오퍼레이터 슬롯(param_1: operator_slot_code)에 캐릭터가 있으면 target_flow_number로 점프한다. param_3 target_flow_number.</summary>
    JumpOperatorExists = 21,
    /// <summary>이 시설의 오퍼레이터 슬롯(param_1)에 캐릭터가 없으면 target_flow_number로 점프한다. param_3 target_flow_number.</summary>
    JumpOperatorNotExists = 22,
    /// <summary>이 시설의 오퍼레이터 슬롯(param_1)에 선 캐릭터가 태그(param_2: tag_code)를 가지면 점프한다(슬롯이 비었거나 태그가 없으면 다음 칸으로). param_3 target_flow_number.</summary>
    JumpOperatorHaveTag = 23,
    /// <summary>
    /// 서브모듈 호출: param_1의 서브모듈(이 종류의 서브모듈 마스터)을 부른 흐름의 같은 상태로 실행하고 끝나면 다음 칸으로 돌아온다. 서브모듈 안의 Finish는 서브모듈만 끝낸다.
    /// 서브모듈 안에서는 쓸 수 없다(서브모듈이 서브모듈을 부를 수 없다). 서브모듈 마스터에 그 코드가 있어야 한다(로드 검증). 값은 부르는 쪽과 공유하는 상태(플래그 등)로 주고받는다.
    /// </summary>
    JumpToSubmodule = 24,
}

/// <summary>시설 흐름 마스터의 한 칸.</summary>
public sealed record FacilityFlow(long FacilityCode, long FlowNumber, FacilityFlowType FlowType, long Param1, long Param2, long Param3);

// facility_flow_master(facility_code, flow_number; 키, flow_type, param_1~param_3). 선택이다. 흐름이 없는 시설은 아무것도 생산하지 않는다.
public sealed partial class GamePack
{
    public const string FacilityFlowMasterPath = "master/facility_flow_master_v1.tsv";

    /// <summary>시설 흐름 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<FacilityFlow>? FacilityFlows { get; init; }

    /// <summary>한 시설의 흐름 칸들(흐름 번호 순).</summary>
    public IReadOnlyList<FacilityFlow> FacilityFlowsOf(long facilityCode) =>
        (FacilityFlows ?? []).Where(f => f.FacilityCode == facilityCode).OrderBy(f => f.FlowNumber).ToList();

    internal static bool IsFacilityConditionalJump(FacilityFlowType type) =>
        FlagJump.Of(type.ToString()) is not null || InventoryJump.Of(type.ToString()) is not null
        || type is FacilityFlowType.JumpCharacterHaveTag or FacilityFlowType.JumpUniqueItemHaveTag or FacilityFlowType.JumpRandom
            or FacilityFlowType.JumpOperatorExists or FacilityFlowType.JumpOperatorNotExists or FacilityFlowType.JumpOperatorHaveTag;

    /// <summary>
    /// 시설 흐름 마스터: 시설은 시설 마스터에 있어야 하고, 키 (시설, 흐름 번호)를 두 번 쓸 수 없다. 흐름 번호와 인자는 정수(인자는 비우면 0), 종류는 이름으로 적는다.
    /// 종류별 검증은 이벤트 흐름과 같다: Jump의 param_1과 점프하는 칸의 param_3은 자신보다 큰 같은 시설의 흐름 번호, AddInventory와 JumpInventory*의 인벤토리 코드는 인벤토리 마스터에,
    /// 태그 점프의 코드와 태그는 각각의 마스터에, 오퍼레이터 칸의 param_1은 그 시설이 가진 오퍼레이터 슬롯(시설 오퍼레이터 슬롯 마스터)이어야 하고 JumpOperatorHaveTag의 param_2는 태그 마스터에 있어야 한다.
    /// JumpRandom의 모수는 1 이상이어야 한다.
    /// </summary>
    static List<FacilityFlow>? LoadFacilityFlows(
        ZipArchive zip, List<FacilityEntry>? facilities, IReadOnlyList<TagEntry>? tags, IReadOnlyDictionary<long, InventoryEntry> inventory,
        List<FacilityOperatorSlot>? facilityOperatorSlots, bool submodule = false)
    {
        // 서브모듈 마스터(submodule: true)는 같은 규칙인데 소유자가 서브모듈 코드(시설 마스터에 있을 필요 없음)이고 JumpToSubmodule을 쓸 수 없으며,
        // 오퍼레이터 칸의 슬롯은 어느 시설이 부를지 모르므로 어느 시설에든 있는 슬롯이면 된다.
        var file = submodule ? FacilitySubmoduleMasterPath : FacilityFlowMasterPath;
        var label = submodule ? "서브모듈" : "시설";
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (!submodule && facilities is null) throw new PackFormatException($"{file}가 있으려면 {FacilityMasterPath}도 필요합니다.");

        var facilityCodes = facilities?.Select(f => f.Code).ToHashSet() ?? [];
        var rows = new List<FacilityFlow>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, submodule ? "submodule_code" : "facility_code", "flow_number", "flow_type", "param_1", "param_2", "param_3"))
        {
            var facility = ParseLong(file, $"{label} 코드", r[0]);
            var who = $"{label} '{facility}'의";
            var flow = ParseLong(file, $"{who} 흐름 번호", r[1]);
            var where = $"{who} 흐름 {flow}의";
            if (!Enum.GetNames<FacilityFlowType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<FacilityFlowType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 흐름 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<FacilityFlowType>())})");
            if (!submodule && !facilityCodes.Contains(facility))
                throw new PackFormatException($"{file}: 시설 '{facility}'가 {FacilityMasterPath}에 없습니다.");
            if (!seen.Add((facility, flow)))
                throw new PackFormatException($"{file}: 같은 키(시설 {facility}, 흐름 {flow})가 두 번 있습니다.");
            rows.Add(new FacilityFlow(facility, flow, type,
                OptionalLong(file, $"{where} 인자 1", r[3]), OptionalLong(file, $"{where} 인자 2", r[4]), OptionalLong(file, $"{where} 인자 3", r[5])));
        }

        foreach (var row in rows)
        {
            var (facility, flow, type, p1, p2, p3) = row;
            var where = $"{file}: {label} '{facility}'의 흐름 {flow}의";
            if (submodule && type == FacilityFlowType.JumpToSubmodule)
                throw new PackFormatException($"{where} 서브모듈 안에서는 JumpToSubmodule을 쓸 수 없습니다(서브모듈이 서브모듈을 부를 수 없습니다).");
            if (type == FacilityFlowType.AddInventory) CheckAddInventory(where, p1, p2, inventory);
            if (type == FacilityFlowType.JumpRandom && p2 < 1)
                throw new PackFormatException($"{where} JumpRandom의 모수 '{p2}'는 1 이상이어야 합니다.");
            if (type is FacilityFlowType.JumpCharacterHaveTag or FacilityFlowType.JumpUniqueItemHaveTag) CheckHaveTag(where, type.ToString(), p1, p2, inventory, tags);
            if (type is FacilityFlowType.JumpOperatorExists or FacilityFlowType.JumpOperatorNotExists or FacilityFlowType.JumpOperatorHaveTag
                && !(facilityOperatorSlots ?? []).Any(s => (submodule || s.FacilityCode == facility) && s.OperatorSlotCode == p1))
                throw new PackFormatException($"{where} {type}의 오퍼레이터 슬롯 '{p1}'가 {FacilityOperatorSlotMasterPath}에 {(submodule ? "" : $"시설 '{facility}'의 ")}슬롯으로 없습니다.");
            if (type == FacilityFlowType.JumpOperatorHaveTag && (tags is null || !tags.Any(t => t.Code == p2)))
                throw new PackFormatException($"{where} JumpOperatorHaveTag의 태그 코드 '{p2}'가 태그 마스터에 없습니다.");
            if (InventoryJump.Of(type.ToString()) is not null && !inventory.ContainsKey(p1))
                throw new PackFormatException($"{where} {type}의 인벤토리 코드 '{p1}'가 인벤토리 마스터에 없습니다.");
            var target = type == FacilityFlowType.Jump ? (long?)p1 : IsFacilityConditionalJump(type) ? p3 : null;
            if (target is { } to)
            {
                if (to <= flow)
                    throw new PackFormatException(
                        $"{where} 점프할 흐름 번호 '{to}'는 자신의 흐름 번호 {flow}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                if (!seen.Contains((facility, to)))
                    throw new PackFormatException($"{where} 점프할 흐름 번호 '{to}'가 같은 시설의 흐름에 없습니다.");
            }
        }
        return rows;
    }
}
