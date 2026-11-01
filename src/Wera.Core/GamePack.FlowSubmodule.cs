using System.IO.Compression;

namespace Wera.Core;

// 흐름 서킷의 서브모듈: 서킷 이펙트 서브모듈(GamePack.Submodule.cs)의 흐름 번호판이다. 흐름 서킷(이벤트, 던전 이벤트, ADV, 교류, 시설, 방어력)마다 서브모듈 마스터가 따로 있다(선택):
// master/<흐름 마스터 이름에서 _flow_master를 뗀 것>_submodule_master_v1.tsv (이벤트는 event_, 던전 이벤트는 dungeon_event_, 교류의 듀오/솔로/기본은 하나의 communication_submodule_master).
// 열은 그 흐름 마스터와 같고 첫 열만 소유자 코드 대신 submodule_code다(예: submodule_code, flow_number, flow_type, param_1, param_2, param_3). 키는 (submodule_code, flow_number)다.
// 흐름 종류와 인자 검증은 그 흐름 마스터와 같고, 서브모듈 안에서는 JumpToSubmodule을 쓸 수 없다(서브모듈이 서브모듈을 부를 수 없어 재귀가 없다).
// 점프는 같은 서브모듈 안의 자신보다 큰 흐름 번호로만 가고, Finish는 서브모듈만 끝내고 부른 곳으로 돌아간다.
// 서브모듈은 부른 서킷의 상태(플래그, 인벤토리, 오퍼레이터, 방어력 합 등)를 그대로 쓴다. 이벤트, ADV, 교류의 서브모듈 안에서 대사, 선택지, 전투를 만나 멈춰도 다음에 그 자리에서 이어 가고, 끝나면 부른 칸의 다음으로 돌아온다.
public sealed partial class GamePack
{
    public const string EventSubmoduleMasterPath = "master/event_submodule_master_v1.tsv";
    public const string DungeonEventSubmoduleMasterPath = "master/dungeon_event_submodule_master_v1.tsv";
    public const string AdvSubmoduleMasterPath = "master/adv_submodule_master_v1.tsv";
    public const string CommunicationSubmoduleMasterPath = "master/communication_submodule_master_v1.tsv";
    public const string FacilitySubmoduleMasterPath = "master/facility_submodule_master_v1.tsv";
    public const string DefenseSubmoduleMasterPath = "master/operator_slot_defense_submodule_master_v1.tsv";

    /// <summary>이벤트 서브모듈(서브모듈 코드 → 흐름 칸들, 흐름 번호 순). 칸의 EventCode가 서브모듈 코드다. 마스터가 없으면 null.</summary>
    public IReadOnlyDictionary<long, IReadOnlyList<EventFlow>>? EventSubmodules { get; init; }

    /// <summary>던전 이벤트 서브모듈. 마스터가 없으면 null.</summary>
    public IReadOnlyDictionary<long, IReadOnlyList<EventFlow>>? DungeonEventSubmodules { get; init; }

    /// <summary>ADV 서브모듈(칸의 AdvCode가 서브모듈 코드). 마스터가 없으면 null.</summary>
    public IReadOnlyDictionary<long, IReadOnlyList<AdvFlow>>? AdvSubmodules { get; init; }

    /// <summary>교류 서브모듈(듀오, 솔로, 기본 흐름이 함께 부른다). 마스터가 없으면 null.</summary>
    public IReadOnlyDictionary<long, IReadOnlyList<CommunicationFlowStep>>? CommunicationSubmodules { get; init; }

    /// <summary>시설 서브모듈(칸의 FacilityCode가 서브모듈 코드). 마스터가 없으면 null.</summary>
    public IReadOnlyDictionary<long, IReadOnlyList<FacilityFlow>>? FacilitySubmodules { get; init; }

    /// <summary>방어력 계산 서브모듈(칸의 OperatorSlotCode가 서브모듈 코드). 마스터가 없으면 null.</summary>
    public IReadOnlyDictionary<long, IReadOnlyList<DefenseFlow>>? DefenseSubmodules { get; init; }

    /// <summary>소유자 코드(서브모듈 코드)별로 묶고 흐름 번호 순으로 정렬한다. 행이 없으면 null.</summary>
    static Dictionary<long, List<T>>? GroupSubmodules<T>(IEnumerable<T>? rows, Func<T, long> code, Func<T, long> number) =>
        rows?.GroupBy(code).ToDictionary(g => g.Key, g => g.OrderBy(number).ToList());

    static Dictionary<long, IReadOnlyList<T>>? ReadOnly<T>(Dictionary<long, List<T>>? grouped) =>
        grouped?.ToDictionary(g => g.Key, g => (IReadOnlyList<T>)g.Value);

    /// <summary>
    /// 흐름 마스터의 JumpToSubmodule 칸(param_1이 서브모듈 코드)이 가리키는 서브모듈이 그 서킷 종류의 서브모듈 마스터에 있는지 본다. 서브모듈 마스터가 아예 없으면 부를 곳이 없으므로 오류다.
    /// </summary>
    static void CheckFlowSubmoduleCalls<T>(string mainFile, string submoduleFile, IEnumerable<T>? rows, IEnumerable<long>? catalog,
        Func<T, bool> isCall, Func<T, long> code, Func<T, string> where)
    {
        foreach (var row in rows ?? [])
        {
            if (!isCall(row)) continue;
            if (catalog is null) throw new PackFormatException($"{mainFile}: {where(row)} JumpToSubmodule에는 {submoduleFile}가 필요합니다.");
            if (!catalog.Contains(code(row)))
                throw new PackFormatException($"{mainFile}: {where(row)} JumpToSubmodule의 서브모듈 '{code(row)}'가 {submoduleFile}에 없습니다.");
        }
    }

    /// <summary>
    /// 흐름 서킷들의 서브모듈 마스터를 읽고(종류별 검증은 그 흐름 마스터와 같다), 흐름 마스터의 JumpToSubmodule 호출이 가리키는 서브모듈이 있는지 확인한다.
    /// </summary>
    static (
        Dictionary<long, IReadOnlyList<EventFlow>>? Event, Dictionary<long, IReadOnlyList<EventFlow>>? DungeonEvent, Dictionary<long, IReadOnlyList<AdvFlow>>? Adv,
        Dictionary<long, IReadOnlyList<CommunicationFlowStep>>? Communication, Dictionary<long, IReadOnlyList<FacilityFlow>>? Facility,
        Dictionary<long, IReadOnlyList<DefenseFlow>>? Defense) LoadFlowSubmodules(
        ZipArchive zip, List<AdvEntry>? advs, List<BattleEntry>? battles, IReadOnlyList<TagEntry>? tags, IReadOnlyDictionary<long, InventoryEntry> inventory,
        List<StatusEntry>? statuses, List<FacilityOperatorSlot>? facilityOperatorSlots,
        List<EventFlow>? eventFlows, List<EventFlow>? dungeonEventFlows, List<AdvFlow>? advFlows,
        List<CommunicationSoloFlow>? soloFlows, List<CommunicationDuoFlow>? duoFlows, List<CommunicationDefaultFlow>? defaultFlows,
        List<FacilityFlow>? facilityFlows, List<DefenseFlow>? defenseFlows)
    {
        var events = ReadOnly(GroupSubmodules(LoadEventFlows(zip, null, advs, battles, tags, inventory, submodule: true), f => f.EventCode, f => f.FlowNumber));
        var dungeonEvents = ReadOnly(GroupSubmodules(LoadEventFlows(zip, null, advs, battles, tags, inventory, dungeon: true, submodule: true), f => f.EventCode, f => f.FlowNumber));
        var adv = ReadOnly(GroupSubmodules(LoadAdvFlows(zip, advs, inventory, tags, submodule: true), f => f.AdvCode, f => f.FlowNumber));
        var communication = LoadCommunicationSubmodules(zip, inventory, tags);
        var facility = ReadOnly(GroupSubmodules(LoadFacilityFlows(zip, null, tags, inventory, facilityOperatorSlots, submodule: true), f => f.FacilityCode, f => f.FlowNumber));
        var defense = ReadOnly(GroupSubmodules(LoadDefenseFlows(zip, null, statuses, tags, submodule: true), f => f.OperatorSlotCode, f => f.FlowNumber));

        CheckFlowSubmoduleCalls(EventFlowMasterPath, EventSubmoduleMasterPath, eventFlows, events?.Keys,
            f => f.FlowType == EventFlowType.JumpToSubmodule, f => f.Param1, f => $"이벤트 '{f.EventCode}'의 흐름 {f.FlowNumber}의");
        CheckFlowSubmoduleCalls(DungeonEventFlowMasterPath, DungeonEventSubmoduleMasterPath, dungeonEventFlows, dungeonEvents?.Keys,
            f => f.FlowType == EventFlowType.JumpToSubmodule, f => f.Param1, f => $"던전 이벤트 '{f.EventCode}'의 흐름 {f.FlowNumber}의");
        CheckFlowSubmoduleCalls(AdvFlowMasterPath, AdvSubmoduleMasterPath, advFlows, adv?.Keys,
            f => f.FlowType == AdvFlowType.JumpToSubmodule, f => f.Param1, f => $"ADV '{f.AdvCode}'의 흐름 {f.FlowNumber}의");
        CheckFlowSubmoduleCalls(CommunicationSoloFlowMasterPath, CommunicationSubmoduleMasterPath, soloFlows, communication?.Keys,
            f => f.FlowType == CommunicationFlowType.JumpToSubmodule, f => f.Param1, f => $"교류 '{f.CommunicationCode}'의 흐름 {f.FlowNumber}의");
        CheckFlowSubmoduleCalls(CommunicationDuoFlowMasterPath, CommunicationSubmoduleMasterPath, duoFlows, communication?.Keys,
            f => f.FlowType == CommunicationFlowType.JumpToSubmodule, f => f.Param1, f => $"교류 '{f.CommunicationCode}'의 흐름 {f.FlowNumber}의");
        CheckFlowSubmoduleCalls(CommunicationDefaultFlowMasterPath, CommunicationSubmoduleMasterPath, defaultFlows, communication?.Keys,
            f => f.FlowType == CommunicationFlowType.JumpToSubmodule, f => f.Param1, f => $"교류 '{f.CommunicationCode}'의 흐름 {f.FlowNumber}의");
        CheckFlowSubmoduleCalls(FacilityFlowMasterPath, FacilitySubmoduleMasterPath, facilityFlows, facility?.Keys,
            f => f.FlowType == FacilityFlowType.JumpToSubmodule, f => f.Param1, f => $"시설 '{f.FacilityCode}'의 흐름 {f.FlowNumber}의");
        CheckFlowSubmoduleCalls(DefenseFlowMasterPath, DefenseSubmoduleMasterPath, defenseFlows, defense?.Keys,
            f => f.FlowType == DefenseFlowType.JumpToSubmodule, f => f.Param1, f => $"오퍼레이터 슬롯 '{f.OperatorSlotCode}'의 흐름 {f.FlowNumber}의");
        return (events, dungeonEvents, adv, communication, facility, defense);
    }
}
