using System.Globalization;
using System.IO.Compression;

namespace Wera.Core;

/// <summary>
/// 이벤트 흐름 한 칸이 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다.
/// 숫자 값은 정해진 번호다: None 0, Finish 1, Jump 2, Adv 3, Battle 4.
/// </summary>
public enum EventFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
    /// <summary>이벤트를 끝낸다.</summary>
    Finish = 1,
    /// <summary>다른 흐름 칸으로 점프한다. param_1이 점프할 flow_number(같은 이벤트의 흐름)다.</summary>
    Jump = 2,
    /// <summary>ADV를 재생한다. param_1이 재생할 adv_code(ADV 마스터)다.</summary>
    Adv = 3,
    /// <summary>전투를 개시한다. param_1이 개시할 battle_code(전투 마스터)다.</summary>
    Battle = 4,
}

/// <summary>이벤트 마스터의 한 행. 이벤트 조건 마스터의 그룹 규칙(그룹 안은 AND, 그룹끼리는 OR)으로 열리고, 조건이 없으면 늘 열린다.</summary>
/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음.</param>
public sealed record GameEvent(long Code, string Name, IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    public bool IsAvailable(IReadOnlyDictionary<long, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}

/// <summary>
/// 이벤트 흐름 마스터의 한 행. 이벤트(EventCode)의 흐름 번호(FlowNumber) 칸이 하는 일(FlowType)과 그 인자. 키는 (EventCode, FlowNumber)다.
/// Param1은 종류에 따라 점프할 흐름 번호(Jump), 재생할 adv 코드(Adv), 개시할 전투 코드(Battle)로 쓰이고,
/// Param2와 Param3은 지금은 쓰이지 않는다(앞으로를 위해 남겨 둔다). 흐름 조건 마스터의 조건을 만족해야 실행할 수 있고, 조건이 없으면 늘 실행할 수 있다.
/// </summary>
/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음.</param>
public sealed record EventFlow(
    long EventCode, long FlowNumber, EventFlowType FlowType, long Param1, long Param2, long Param3,
    IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    public bool IsAvailable(IReadOnlyDictionary<long, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}

/// <summary>ADV 마스터의 한 행.</summary>
public sealed record AdvEntry(long Code, string Name);

/// <summary>전투 마스터의 한 행.</summary>
public sealed record BattleEntry(long Code, string Name);

/// <summary>흐름 조건표의 한 행(파일 순서 그대로): 어느 (이벤트, 흐름)의 몇 번 그룹에 어떤 조건이 있는가.</summary>
internal readonly record struct FlatFlowCondition(long EventCode, long FlowNumber, int GroupNumber, FlagCondition Condition);

// 이벤트, 이벤트 조건, 이벤트 흐름, 이벤트 흐름 조건, ADV, 전투 마스터. 이 마스터들은 선택이고, 아직 게임 진행에 쓰이지 않는다.
public sealed partial class GamePack
{
    public const string EventMasterPath = "master/event_master_v1.tsv";
    public const string EventConditionMasterPath = "master/event_condition_master_v1.tsv";
    public const string EventFlowMasterPath = "master/event_flow_master_v1.tsv";
    public const string EventFlowConditionMasterPath = "master/event_flow_condition_master_v1.tsv";
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
    /// 이벤트 흐름 마스터와 흐름 조건 마스터를 읽는다.
    /// 흐름: 이벤트는 이벤트 마스터에 있어야 하고, 키 (이벤트, 흐름 번호)를 두 번 쓸 수 없다. 흐름 번호와 인자는 정수(long, 인자는 비우면 0),
    /// 종류는 이름으로 적는다. 종류별 인자 검증: Jump의 param_1은 같은 이벤트의 흐름 번호, Adv의 param_1은 ADV 마스터의 코드,
    /// Battle의 param_1은 전투 마스터의 코드여야 한다(Adv와 Battle은 그 마스터가 있어야 한다). Finish와 None은 인자를 보지 않는다.
    /// 흐름 조건: (이벤트, 흐름)은 흐름 마스터에 있어야 하고 키 (이벤트, 흐름, 그룹넘버, 플래그)를 두 번 쓸 수 없다.
    /// </summary>
    static List<EventFlow>? LoadEventFlows(
        ZipArchive zip, List<GameEvent>? events, List<AdvEntry>? advs, List<BattleEntry>? battles, List<FlatFlowCondition> flat)
    {
        const string file = EventFlowMasterPath;
        const string conditionFile = EventFlowConditionMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null)
        {
            if (FindEntry(zip, conditionFile) is not null)
                throw new PackFormatException($"{conditionFile}가 있으려면 {file}도 필요합니다.");
            return null;
        }
        if (events is null) throw new PackFormatException($"{file}가 있으려면 {EventMasterPath}도 필요합니다.");

        var eventCodes = events.Select(e => e.Code).ToHashSet();
        var rows = new List<(long Event, long Flow, EventFlowType Type, long P1, long P2, long P3)>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "event_code", "flow_number", "flow_type", "param_1", "param_2", "param_3"))
        {
            var eventCode = ParseLong(file, "이벤트 코드", r[0]);
            var who = $"이벤트 '{eventCode}'의";
            var flow = ParseLong(file, $"{who} 흐름 번호", r[1]);
            var where = $"{who} 흐름 {flow}의";
            // 숫자("1")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<EventFlowType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<EventFlowType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 흐름 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<EventFlowType>())})");

            if (!eventCodes.Contains(eventCode))
                throw new PackFormatException($"{file}: 이벤트 '{eventCode}'가 이벤트 마스터에 없습니다.");
            if (!seen.Add((eventCode, flow)))
                throw new PackFormatException($"{file}: 같은 키(이벤트 {eventCode}, 흐름 {flow})가 두 번 있습니다.");
            rows.Add((eventCode, flow, type,
                OptionalLong(file, $"{where} 인자 1", r[3]),
                OptionalLong(file, $"{where} 인자 2", r[4]),
                OptionalLong(file, $"{where} 인자 3", r[5])));
        }

        // 종류별 인자 검증(param_2, param_3은 쓰지 않는다)
        var advCodes = advs?.Select(a => a.Code).ToHashSet();
        var battleCodes = battles?.Select(b => b.Code).ToHashSet();
        foreach (var (eventCode, flow, type, p1, _, _) in rows)
        {
            var where = $"{file}: 이벤트 '{eventCode}'의 흐름 {flow}의";
            switch (type)
            {
                case EventFlowType.Jump when !seen.Contains((eventCode, p1)):
                    throw new PackFormatException($"{where} 점프할 흐름 번호 '{p1}'가 같은 이벤트의 흐름에 없습니다.");
                case EventFlowType.Adv when advCodes is null:
                    throw new PackFormatException($"{where} Adv가 있으려면 {AdvMasterPath}도 필요합니다.");
                case EventFlowType.Adv when !advCodes!.Contains(p1):
                    throw new PackFormatException($"{where} 재생할 ADV '{p1}'가 ADV 마스터에 없습니다.");
                case EventFlowType.Battle when battleCodes is null:
                    throw new PackFormatException($"{where} Battle이 있으려면 {BattleMasterPath}도 필요합니다.");
                case EventFlowType.Battle when !battleCodes!.Contains(p1):
                    throw new PackFormatException($"{where} 개시할 전투 '{p1}'가 전투 마스터에 없습니다.");
            }
        }

        // 흐름 조건: (이벤트, 흐름) → (그룹넘버 → 조건들)
        var groups = rows.ToDictionary(r => (r.Event, r.Flow), _ => new SortedDictionary<int, List<FlagCondition>>());
        var conditionText = TryReadEntry(zip, conditionFile);
        if (conditionText is not null)
        {
            var seenKeys = new HashSet<(long, long, int, long)>();       // 복합키는 (이벤트, 흐름, 그룹넘버, 플래그 코드)
            foreach (var r in Body(conditionFile, conditionText,
                         "event_code", "flow_number", "group_number", "flag_code", "condition_type", "condition_value"))
            {
                var eventCode = ParseLong(conditionFile, "이벤트 코드", r[0]);
                var flow = ParseLong(conditionFile, $"이벤트 '{eventCode}'의 흐름 번호", r[1]);
                var who = $"이벤트 '{eventCode}'의 흐름 {flow}의";
                if (!groups.TryGetValue((eventCode, flow), out var byGroup))
                    throw new PackFormatException($"{conditionFile}: 알 수 없는 흐름(이벤트 '{eventCode}', 흐름 {flow}).");
                if (!int.TryParse(r[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupNumber))
                    throw new PackFormatException($"{conditionFile}: {who} 그룹넘버 '{r[2]}'이(가) 정수가 아닙니다.");
                // 숫자("1")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
                if (!Enum.GetNames<ConditionType>().Contains(r[4], StringComparer.OrdinalIgnoreCase)
                    || !Enum.TryParse<ConditionType>(r[4], ignoreCase: true, out var conditionType))
                    throw new PackFormatException(
                        $"{conditionFile}: {who} 컨디션 타입 '{r[4]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<ConditionType>())})");
                // None은 조건 없음이므로 플래그 코드와 밸류를 비워 둘 수 있다.
                long flagCode = 0, value = 0;
                if (conditionType != ConditionType.None)
                {
                    flagCode = ParseLong(conditionFile, $"{who} 플래그 코드", r[3]);
                    value = ParseLong(conditionFile, $"{who} 컨디션 밸류", r[5]);
                }

                if (!seenKeys.Add((eventCode, flow, groupNumber, flagCode)))
                    throw new PackFormatException(
                        $"{conditionFile}: {who} 그룹 {groupNumber}에 플래그 '{flagCode}'가 두 번 있습니다.");
                if (!byGroup.TryGetValue(groupNumber, out var list)) byGroup[groupNumber] = list = [];
                var condition = new FlagCondition(flagCode, conditionType, value);
                list.Add(condition);
                flat.Add(new FlatFlowCondition(eventCode, flow, groupNumber, condition));
            }
        }

        return rows
            .Select(r => new EventFlow(
                r.Event, r.Flow, r.Type, r.P1, r.P2, r.P3,
                groups[(r.Event, r.Flow)].Values.Select(l => (IReadOnlyList<FlagCondition>)l).ToList()))
            .ToList();
    }

    // ---- 키로 찾기 ----

    public GameEvent? FindEvent(long code) => Events?.FirstOrDefault(e => e.Code == code);

    public AdvEntry? FindAdv(long code) =>
        Masters.AdvRowTable.TryFindByCode(code, out var r) ? new AdvEntry(r.Code, r.Name) : null;

    public BattleEntry? FindBattle(long code) =>
        Masters.BattleRowTable.TryFindByCode(code, out var r) ? new BattleEntry(r.Code, r.Name) : null;

    /// <summary>이벤트의 흐름 칸 하나(조건 포함). 없으면 null.</summary>
    public EventFlow? FindEventFlow(long eventCode, long flowNumber) =>
        EventFlows?.FirstOrDefault(f => f.EventCode == eventCode && f.FlowNumber == flowNumber);

    /// <summary>한 이벤트의 흐름 칸들(키 순서: 흐름 번호).</summary>
    public IReadOnlyList<EventFlow> FlowsOf(long eventCode) =>
        (EventFlows ?? []).Where(f => f.EventCode == eventCode).OrderBy(f => f.FlowNumber).ToList();
}
