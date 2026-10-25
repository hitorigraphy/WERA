using System.Globalization;
using System.IO.Compression;

namespace Wera.Core;

/// <summary>
/// ADV 흐름 한 칸이 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다.
/// 숫자 값은 정해진 번호다: None 0, Finish 1, Jump 2, Text 3, Choice 4.
/// </summary>
public enum AdvFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
    /// <summary>ADV를 끝낸다.</summary>
    Finish = 1,
    /// <summary>뒤쪽의 흐름 칸으로 점프한다. param_1이 점프할 flow_number이고, 같은 ADV에 있어야 하며 자신의 flow_number보다 커야 한다.</summary>
    Jump = 2,
    /// <summary>텍스트를 보여 준다. 인자는 아직 정해지지 않아 쓰이지 않는다.</summary>
    Text = 3,
    /// <summary>
    /// 선택지 하나. param_1이 선택지 그룹 아이디(같은 그룹의 행이 한 번의 선택지 묶음이고, flow_number 순으로 연속해야 한다),
    /// param_2가 이 선택지를 고르면 점프할 flow_number다(같은 ADV에 있어야 하며 자신의 flow_number보다 커야 한다).
    /// </summary>
    Choice = 4,
}

/// <summary>
/// ADV 흐름 마스터의 한 행. ADV(AdvCode)의 흐름 번호(FlowNumber) 칸이 하는 일(FlowType)과 그 인자. 키는 (AdvCode, FlowNumber)다.
/// 흐름 조건 마스터의 조건을 만족해야 실행할 수 있고, 조건이 없으면 늘 실행할 수 있다.
/// </summary>
/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음.</param>
public sealed record AdvFlow(
    long AdvCode, long FlowNumber, AdvFlowType FlowType, long Param1, long Param2, long Param3,
    IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    public bool IsAvailable(IReadOnlyDictionary<long, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}

/// <summary>ADV 흐름 조건표의 한 행(파일 순서 그대로).</summary>
internal readonly record struct FlatAdvFlowCondition(long AdvCode, long FlowNumber, int GroupNumber, FlagCondition Condition);

// ADV 흐름, ADV 흐름 조건 마스터. 이 마스터들은 선택이고, 아직 게임 진행에 쓰이지 않는다.
public sealed partial class GamePack
{
    public const string AdvFlowMasterPath = "master/adv_flow_master_v1.tsv";
    public const string AdvFlowConditionMasterPath = "master/adv_flow_condition_master_v1.tsv";

    /// <summary>ADV 흐름 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<AdvFlow>? AdvFlows { get; init; }

    /// <summary>
    /// ADV 흐름 마스터와 흐름 조건 마스터를 읽는다.
    /// 흐름: ADV는 ADV 마스터에 있어야 하고, 키 (ADV, 흐름 번호)를 두 번 쓸 수 없다. 흐름 번호와 인자는 정수(long, 인자는 비우면 0), 종류는 이름으로 적는다.
    /// 종류별 인자 검증: Jump의 param_1과 Choice의 param_2는 자신의 흐름 번호보다 큰 같은 ADV의 흐름 번호(뒤로만 점프해 무한 반복이 없다).
    /// 같은 ADV에서 Choice의 param_1(선택지 그룹 아이디)이 같은 행들은 흐름 번호 순으로 연속해야 한다(다른 칸이 끼어들 수 없다).
    /// Finish, None, Text는 인자를 보지 않는다.
    /// 흐름 조건: (ADV, 흐름)은 흐름 마스터에 있어야 하고 키 (ADV, 흐름, 그룹넘버, 플래그)를 두 번 쓸 수 없다.
    /// </summary>
    static List<AdvFlow>? LoadAdvFlows(ZipArchive zip, List<AdvEntry>? advs, List<FlatAdvFlowCondition> flat)
    {
        const string file = AdvFlowMasterPath;
        const string conditionFile = AdvFlowConditionMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null)
        {
            if (FindEntry(zip, conditionFile) is not null)
                throw new PackFormatException($"{conditionFile}가 있으려면 {file}도 필요합니다.");
            return null;
        }
        if (advs is null) throw new PackFormatException($"{file}가 있으려면 {AdvMasterPath}도 필요합니다.");

        var advCodes = advs.Select(a => a.Code).ToHashSet();
        var rows = new List<(long Adv, long Flow, AdvFlowType Type, long P1, long P2, long P3)>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "adv_code", "flow_number", "flow_type", "param_1", "param_2", "param_3"))
        {
            var advCode = ParseLong(file, "ADV 코드", r[0]);
            var who = $"ADV '{advCode}'의";
            var flow = ParseLong(file, $"{who} 흐름 번호", r[1]);
            var where = $"{who} 흐름 {flow}의";
            // 숫자("1")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<AdvFlowType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<AdvFlowType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 흐름 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<AdvFlowType>())})");

            if (!advCodes.Contains(advCode))
                throw new PackFormatException($"{file}: ADV '{advCode}'가 ADV 마스터에 없습니다.");
            if (!seen.Add((advCode, flow)))
                throw new PackFormatException($"{file}: 같은 키(ADV {advCode}, 흐름 {flow})가 두 번 있습니다.");
            rows.Add((advCode, flow, type,
                OptionalLong(file, $"{where} 인자 1", r[3]),
                OptionalLong(file, $"{where} 인자 2", r[4]),
                OptionalLong(file, $"{where} 인자 3", r[5])));
        }

        // 종류별 인자 검증
        foreach (var (advCode, flow, type, p1, p2, _) in rows)
        {
            var where = $"{file}: ADV '{advCode}'의 흐름 {flow}의";
            switch (type)
            {
                case AdvFlowType.Jump when p1 <= flow:
                    throw new PackFormatException(
                        $"{where} 점프할 흐름 번호 '{p1}'는 자신의 흐름 번호 {flow}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                case AdvFlowType.Jump when !seen.Contains((advCode, p1)):
                    throw new PackFormatException($"{where} 점프할 흐름 번호 '{p1}'가 같은 ADV의 흐름에 없습니다.");
                case AdvFlowType.Choice when p2 <= flow:
                    throw new PackFormatException(
                        $"{where} 선택지가 점프할 흐름 번호 '{p2}'는 자신의 흐름 번호 {flow}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                case AdvFlowType.Choice when !seen.Contains((advCode, p2)):
                    throw new PackFormatException($"{where} 선택지가 점프할 흐름 번호 '{p2}'가 같은 ADV의 흐름에 없습니다.");
            }
        }

        // 같은 선택지 그룹(param_1)의 Choice 행은 흐름 번호 순으로 연속해야 한다.
        foreach (var byAdv in rows.GroupBy(r => r.Adv))
        {
            var started = new HashSet<long>();
            long? last = null;                          // 바로 앞 칸이 Choice일 때 그 선택지 그룹
            foreach (var (advCode, flow, type, p1, _, _) in byAdv.OrderBy(r => r.Flow))
            {
                if (type != AdvFlowType.Choice) { last = null; continue; }
                if (last != p1 && !started.Add(p1))
                    throw new PackFormatException(
                        $"{file}: ADV '{advCode}'의 흐름 {flow}의 선택지 그룹 '{p1}'가 연속하지 않습니다(같은 그룹의 선택지 사이에 다른 칸이 있습니다).");
                last = p1;
            }
        }

        // 흐름 조건: (ADV, 흐름) → (그룹넘버 → 조건들)
        var groups = rows.ToDictionary(r => (r.Adv, r.Flow), _ => new SortedDictionary<int, List<FlagCondition>>());
        var conditionText = TryReadEntry(zip, conditionFile);
        if (conditionText is not null)
        {
            var seenKeys = new HashSet<(long, long, int, long)>();       // 복합키는 (ADV, 흐름, 그룹넘버, 플래그 코드)
            foreach (var r in Body(conditionFile, conditionText,
                         "adv_code", "flow_number", "group_number", "flag_code", "condition_type", "condition_value"))
            {
                var advCode = ParseLong(conditionFile, "ADV 코드", r[0]);
                var flow = ParseLong(conditionFile, $"ADV '{advCode}'의 흐름 번호", r[1]);
                var who = $"ADV '{advCode}'의 흐름 {flow}의";
                if (!groups.TryGetValue((advCode, flow), out var byGroup))
                    throw new PackFormatException($"{conditionFile}: 알 수 없는 흐름(ADV '{advCode}', 흐름 {flow}).");
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

                if (!seenKeys.Add((advCode, flow, groupNumber, flagCode)))
                    throw new PackFormatException(
                        $"{conditionFile}: {who} 그룹 {groupNumber}에 플래그 '{flagCode}'가 두 번 있습니다.");
                if (!byGroup.TryGetValue(groupNumber, out var list)) byGroup[groupNumber] = list = [];
                var condition = new FlagCondition(flagCode, conditionType, value);
                list.Add(condition);
                flat.Add(new FlatAdvFlowCondition(advCode, flow, groupNumber, condition));
            }
        }

        return rows
            .Select(r => new AdvFlow(
                r.Adv, r.Flow, r.Type, r.P1, r.P2, r.P3,
                groups[(r.Adv, r.Flow)].Values.Select(l => (IReadOnlyList<FlagCondition>)l).ToList()))
            .ToList();
    }

    /// <summary>ADV의 흐름 칸 하나(조건 포함). 없으면 null.</summary>
    public AdvFlow? FindAdvFlow(long advCode, long flowNumber) =>
        AdvFlows?.FirstOrDefault(f => f.AdvCode == advCode && f.FlowNumber == flowNumber);

    /// <summary>한 ADV의 흐름 칸들(키 순서: 흐름 번호).</summary>
    public IReadOnlyList<AdvFlow> AdvFlowsOf(long advCode) =>
        (AdvFlows ?? []).Where(f => f.AdvCode == advCode).OrderBy(f => f.FlowNumber).ToList();
}
