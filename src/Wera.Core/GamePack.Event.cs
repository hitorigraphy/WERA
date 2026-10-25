using System.IO.Compression;

namespace Wera.Core;

/// <summary>이벤트 흐름 한 칸이 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다. 종류는 앞으로 늘어난다.</summary>
public enum EventFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
}

/// <summary>이벤트 마스터의 한 행. 이벤트 조건 마스터의 그룹 규칙(그룹 안은 AND, 그룹끼리는 OR)으로 열리고, 조건이 없으면 늘 열린다.</summary>
/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음.</param>
public sealed record GameEvent(long Code, string Name, IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    public bool IsAvailable(IReadOnlyDictionary<long, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}

/// <summary>
/// 이벤트 흐름 마스터의 한 행. 이벤트(EventCode)의 흐름 번호(FlowNumber) 칸이 하는 일(FlowType)과 그 인자(Param1~3).
/// 복합키는 (EventCode, FlowNumber, GroupNumber)다.
/// </summary>
public sealed record EventFlow(long EventCode, long FlowNumber, int GroupNumber, EventFlowType FlowType, long Param1, long Param2, long Param3);

/// <summary>ADV 마스터의 한 행.</summary>
public sealed record AdvEntry(long Code, string Name);

/// <summary>전투 마스터의 한 행.</summary>
public sealed record BattleEntry(long Code, string Name);

// 이벤트, 이벤트 조건, 이벤트 흐름, ADV, 전투 마스터. 이 마스터들은 선택이고, 아직 게임 진행에 쓰이지 않는다.
public sealed partial class GamePack
{
    public const string EventMasterPath = "master/event_master_v1.tsv";
    public const string EventConditionMasterPath = "master/event_condition_master_v1.tsv";
    public const string EventFlowMasterPath = "master/event_flow_master_v1.tsv";
    public const string AdvMasterPath = "master/adv_master_v1.tsv";
    public const string BattleMasterPath = "master/battle_master_v1.tsv";

    /// <summary>이벤트 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<GameEvent>? Events { get; init; }

    /// <summary>이벤트 흐름 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<EventFlow>? EventFlows { get; init; }

    /// <summary>ADV 마스터가 없으면 null.</summary>
    public IReadOnlyList<AdvEntry>? Advs { get; init; }

    /// <summary>전투 마스터가 없으면 null.</summary>
    public IReadOnlyList<BattleEntry>? Battles { get; init; }

    static List<AdvEntry>? LoadAdvs(ZipArchive zip) =>
        LoadCodeNames(zip, AdvMasterPath, "code", "name", "ADV", (code, name) => new AdvEntry(code, name));

    static List<BattleEntry>? LoadBattles(ZipArchive zip) =>
        LoadCodeNames(zip, BattleMasterPath, "code", "name", "전투", (code, name) => new BattleEntry(code, name));

    /// <summary>
    /// 이벤트 흐름 마스터: 이벤트는 이벤트 마스터에 있어야 하고, 복합키 (이벤트, 흐름 번호, 그룹 넘버)를 두 번 쓸 수 없다.
    /// 흐름 번호와 인자는 정수(long, 인자는 비우면 0), 그룹 넘버는 정수(int), 흐름 종류는 이름으로 적는다.
    /// </summary>
    static List<EventFlow>? LoadEventFlows(ZipArchive zip, List<GameEvent>? events)
    {
        const string file = EventFlowMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (events is null) throw new PackFormatException($"{file}가 있으려면 {EventMasterPath}도 필요합니다.");

        var eventCodes = events.Select(e => e.Code).ToHashSet();
        var rows = new List<EventFlow>();
        var seen = new HashSet<(long, long, int)>();
        foreach (var r in Body(file, text, "event_code", "flow_number", "group_number", "flow_type", "param_1", "param_2", "param_3"))
        {
            var eventCode = ParseLong(file, "이벤트 코드", r[0]);
            var who = $"이벤트 '{eventCode}'의";
            var flow = ParseLong(file, $"{who} 흐름 번호", r[1]);
            if (!int.TryParse(r[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var group))
                throw new PackFormatException($"{file}: {who} 흐름 {flow}의 그룹넘버 '{r[2]}'이(가) 정수가 아닙니다.");
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<EventFlowType>().Contains(r[3], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<EventFlowType>(r[3], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {who} 흐름 {flow}의 흐름 종류 '{r[3]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<EventFlowType>())})");
            var where = $"{who} 흐름 {flow}의";

            if (!eventCodes.Contains(eventCode))
                throw new PackFormatException($"{file}: 이벤트 '{eventCode}'가 이벤트 마스터에 없습니다.");
            if (!seen.Add((eventCode, flow, group)))
                throw new PackFormatException($"{file}: 같은 키(이벤트 {eventCode}, 흐름 {flow}, 그룹 {group})가 두 번 있습니다.");
            rows.Add(new EventFlow(
                eventCode, flow, group, type,
                OptionalLong(file, $"{where} 인자 1", r[4]),
                OptionalLong(file, $"{where} 인자 2", r[5]),
                OptionalLong(file, $"{where} 인자 3", r[6])));
        }
        return rows;
    }

    // ---- 키로 찾기 ----

    public GameEvent? FindEvent(long code) => Events?.FirstOrDefault(e => e.Code == code);

    public AdvEntry? FindAdv(long code) =>
        Masters.AdvRowTable.TryFindByCode(code, out var r) ? new AdvEntry(r.Code, r.Name) : null;

    public BattleEntry? FindBattle(long code) =>
        Masters.BattleRowTable.TryFindByCode(code, out var r) ? new BattleEntry(r.Code, r.Name) : null;

    /// <summary>한 이벤트의 흐름 칸들(키 순서: 흐름 번호, 그룹 넘버). 보조키 이벤트 코드로 찾는다.</summary>
    public IReadOnlyList<EventFlow> FlowsOf(long eventCode) =>
        Masters.EventFlowRowTable.FindByEventCode(eventCode)
            .OrderBy(r => r.FlowNumber).ThenBy(r => r.GroupNumber)
            .Select(r => new EventFlow(r.EventCode, r.FlowNumber, r.GroupNumber, r.FlowType, r.Param1, r.Param2, r.Param3))
            .ToList();
}
