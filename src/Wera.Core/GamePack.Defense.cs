using System.IO.Compression;

namespace Wera.Core;

/// <summary>
/// 방어력 계산 흐름 칸의 종류(flow_type 열에 이름으로 적는다). 방어전에서 오퍼레이터 슬롯에 선 캐릭터 한 명의 최대 방어력을 정하는 서킷이다.
/// 흐름 번호 순으로 달리며 AddDefense 칸들이 방어력을 쌓고, 점프하는 칸은 같은 슬롯의 자신보다 큰 흐름 번호로만 갈 수 있다.
/// </summary>
public enum DefenseFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
    /// <summary>서킷을 끝낸다.</summary>
    Finish = 1,
    /// <summary>조건 없이 뒤쪽의 흐름 칸으로 점프한다. param_1이 점프할 flow_number다.</summary>
    Jump = 2,
    /// <summary>방어력에 고정값을 더한다(음수면 뺀다). param_1이 더할 값이다.</summary>
    AddDefense = 3,
    /// <summary>방어력에 캐릭터의 스테이터스 값 × 배율(%)을 더한다(소수는 버림). param_1이 status_code, param_2가 배율(%)이다.</summary>
    AddDefenseByStatus = 4,
    /// <summary>플래그 값이 flag_value와 비교해 참이면 target_flow_number로 점프한다. param_1 flag_code, param_2 flag_value, param_3 target_flow_number.</summary>
    JumpFlagEqual = 5,
    JumpFlagNotEqual = 6,
    JumpFlagGreaterThan = 7,
    JumpFlagGreaterOrEqual = 8,
    JumpFlagLessThan = 9,
    JumpFlagLessOrEqual = 10,
    /// <summary>오퍼레이터 캐릭터가 태그를 가지면 점프한다. param_1 tag_code, param_3 target_flow_number.</summary>
    JumpOperatorHaveTag = 11,
    /// <summary>확률로 점프한다. param_1 chance, param_2 bound(1 이상), param_3 target_flow_number.</summary>
    JumpRandom = 12,
    /// <summary>
    /// 서브모듈 호출: param_1의 서브모듈(이 종류의 서브모듈 마스터)을 부른 흐름의 같은 상태로 실행하고 끝나면 다음 칸으로 돌아온다. 서브모듈 안의 Finish는 서브모듈만 끝낸다.
    /// 서브모듈 안에서는 쓸 수 없다(서브모듈이 서브모듈을 부를 수 없다). 서브모듈 마스터에 그 코드가 있어야 한다(로드 검증). 값은 부르는 쪽과 공유하는 상태(플래그 등)로 주고받는다.
    /// </summary>
    JumpToSubmodule = 13,
}

/// <summary>오퍼레이터 슬롯 방어력 흐름 마스터의 한 칸.</summary>
public sealed record DefenseFlow(long OperatorSlotCode, long FlowNumber, DefenseFlowType FlowType, long Param1, long Param2, long Param3);

/// <summary>시설 방어력 마스터의 한 행: 시설(FacilityCode)의 최대 방어력. 오퍼레이터가 모두 부딪친 뒤 시설이 이 값으로 부딪친다.</summary>
public sealed record FacilityDefense(long FacilityCode, long Defense);

// operator_slot_defense_flow_master(operator_slot_code, flow_number; 키, flow_type, param_1~param_3), facility_defense_master(facility_code(키), defense). 모두 선택이다.
// 방어력 흐름이 없는 오퍼레이터 슬롯의 방어력은 0이고, 방어력이 없는 시설은 부딪치지 못한다.
public sealed partial class GamePack
{
    public const string DefenseFlowMasterPath = "master/operator_slot_defense_flow_master_v1.tsv";
    public const string FacilityDefenseMasterPath = "master/facility_defense_master_v1.tsv";

    /// <summary>오퍼레이터 슬롯 방어력 흐름 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<DefenseFlow>? DefenseFlows { get; init; }

    /// <summary>시설 방어력 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<FacilityDefense>? FacilityDefenses { get; init; }

    /// <summary>한 오퍼레이터 슬롯의 방어력 흐름 칸들(흐름 번호 순).</summary>
    public IReadOnlyList<DefenseFlow> DefenseFlowsOf(long operatorSlotCode) =>
        (DefenseFlows ?? []).Where(f => f.OperatorSlotCode == operatorSlotCode).OrderBy(f => f.FlowNumber).ToList();

    /// <summary>시설의 최대 방어력(방어력 행이 없으면 0).</summary>
    public long FacilityDefenseOf(long facilityCode) => FacilityDefenses?.FirstOrDefault(d => d.FacilityCode == facilityCode)?.Defense ?? 0;

    static bool IsDefenseConditionalJump(DefenseFlowType type) =>
        FlagJump.Of(type.ToString()) is not null || type is DefenseFlowType.JumpOperatorHaveTag or DefenseFlowType.JumpRandom;

    /// <summary>
    /// 오퍼레이터 슬롯 방어력 흐름 마스터: 오퍼레이터 슬롯은 오퍼레이터 슬롯 마스터에 있어야 하고 키 (슬롯, 흐름 번호)를 두 번 쓸 수 없다.
    /// Jump의 param_1과 점프하는 칸의 param_3은 자신보다 큰 같은 슬롯의 흐름 번호, AddDefenseByStatus의 스테이터스와 JumpOperatorHaveTag의 태그는 각각의 마스터에,
    /// JumpRandom의 모수는 1 이상이어야 한다.
    /// </summary>
    static List<DefenseFlow>? LoadDefenseFlows(ZipArchive zip, List<OperatorSlotEntry>? slots, List<StatusEntry>? statuses, IReadOnlyList<TagEntry>? tags, bool submodule = false)
    {
        // 서브모듈 마스터(submodule: true)는 같은 규칙인데 소유자가 서브모듈 코드(오퍼레이터 슬롯 마스터에 있을 필요 없음)이고 JumpToSubmodule을 쓸 수 없다.
        var file = submodule ? DefenseSubmoduleMasterPath : DefenseFlowMasterPath;
        var label = submodule ? "서브모듈" : "오퍼레이터 슬롯";
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (!submodule && slots is null) throw new PackFormatException($"{file}가 있으려면 {OperatorSlotMasterPath}도 필요합니다.");

        var slotCodes = slots?.Select(s => s.Code).ToHashSet() ?? [];
        var rows = new List<DefenseFlow>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, submodule ? "submodule_code" : "operator_slot_code", "flow_number", "flow_type", "param_1", "param_2", "param_3"))
        {
            var slot = ParseLong(file, $"{label} 코드", r[0]);
            var who = $"{label} '{slot}'의";
            var flow = ParseLong(file, $"{who} 흐름 번호", r[1]);
            var where = $"{who} 흐름 {flow}의";
            if (!Enum.GetNames<DefenseFlowType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<DefenseFlowType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException($"{file}: {where} 흐름 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<DefenseFlowType>())})");
            if (!submodule && !slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: 오퍼레이터 슬롯 '{slot}'가 {OperatorSlotMasterPath}에 없습니다.");
            if (!seen.Add((slot, flow)))
                throw new PackFormatException($"{file}: 같은 키({label} {slot}, 흐름 {flow})가 두 번 있습니다.");
            rows.Add(new DefenseFlow(slot, flow, type,
                OptionalLong(file, $"{where} 인자 1", r[3]), OptionalLong(file, $"{where} 인자 2", r[4]), OptionalLong(file, $"{where} 인자 3", r[5])));
        }

        foreach (var (slot, flow, type, p1, p2, p3) in rows)
        {
            var where = $"{file}: {label} '{slot}'의 흐름 {flow}의";
            if (submodule && type == DefenseFlowType.JumpToSubmodule)
                throw new PackFormatException($"{where} 서브모듈 안에서는 JumpToSubmodule을 쓸 수 없습니다(서브모듈이 서브모듈을 부를 수 없습니다).");
            if (type == DefenseFlowType.AddDefenseByStatus && (statuses is null || !statuses.Any(s => s.Code == p1)))
                throw new PackFormatException($"{where} AddDefenseByStatus의 스테이터스 코드 '{p1}'가 스테이터스 마스터에 없습니다.");
            if (type == DefenseFlowType.JumpOperatorHaveTag && (tags is null || !tags.Any(t => t.Code == p1)))
                throw new PackFormatException($"{where} JumpOperatorHaveTag의 태그 코드 '{p1}'가 태그 마스터에 없습니다.");
            if (type == DefenseFlowType.JumpRandom && p2 < 1)
                throw new PackFormatException($"{where} JumpRandom의 모수 '{p2}'는 1 이상이어야 합니다.");
            var target = type == DefenseFlowType.Jump ? (long?)p1 : IsDefenseConditionalJump(type) ? p3 : null;
            if (target is { } to)
            {
                if (to <= flow)
                    throw new PackFormatException(
                        $"{where} 점프할 흐름 번호 '{to}'는 자신의 흐름 번호 {flow}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                if (!seen.Contains((slot, to)))
                    throw new PackFormatException($"{where} 점프할 흐름 번호 '{to}'가 같은 슬롯의 흐름에 없습니다.");
            }
        }
        return rows;
    }

    /// <summary>시설 방어력 마스터: 시설은 시설 마스터에 있어야 하고 시설 코드를 두 번 쓸 수 없으며 defense는 0 이상이어야 한다.</summary>
    static List<FacilityDefense>? LoadFacilityDefenses(ZipArchive zip, List<FacilityEntry>? facilities)
    {
        const string file = FacilityDefenseMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (facilities is null) throw new PackFormatException($"{file}가 있으려면 {FacilityMasterPath}도 필요합니다.");

        var facilityCodes = facilities.Select(f => f.Code).ToHashSet();
        var rows = new List<FacilityDefense>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, "facility_code", "defense"))
        {
            var facility = ParseLong(file, "시설 코드", r[0]);
            var defense = ParseLong(file, $"시설 '{facility}'의 defense", r[1]);
            if (!facilityCodes.Contains(facility))
                throw new PackFormatException($"{file}: 시설 '{facility}'가 {FacilityMasterPath}에 없습니다.");
            if (!seen.Add(facility))
                throw new PackFormatException($"{file}: 시설 '{facility}'가 두 번 있습니다.");
            if (defense < 0)
                throw new PackFormatException($"{file}: 시설 '{facility}'의 defense '{defense}'은(는) 0 이상이어야 합니다.");
            rows.Add(new FacilityDefense(facility, defense));
        }
        return rows;
    }
}
