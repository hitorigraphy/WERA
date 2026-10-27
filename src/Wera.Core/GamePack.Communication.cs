using System.IO.Compression;

namespace Wera.Core;

/// <summary>교류 그룹 마스터의 한 행(예: 대화). 교류 화면 왼쪽에 "[코드×100] 이름"으로 나열된다.</summary>
public sealed record CommunicationGroupEntry(long Code, string Name);

/// <summary>교류 마스터의 한 행(예: 대화 그룹의 인사하기). GroupCode는 교류 그룹 마스터의 코드다.</summary>
public sealed record CommunicationEntry(long Code, long GroupCode, string Name);

/// <summary>
/// 교류 흐름 한 칸이 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다.
/// 숫자 값은 정해진 번호다: None 0. 그 밖의 종류는 아직 정해지지 않았다.
/// </summary>
public enum CommunicationFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
}

/// <summary>
/// 교류 흐름 마스터의 한 행: 교류(CommunicationCode)에서 캐릭터(CharacterCode)별로 흐름 번호 순으로 이어지는 칸.
/// 인자(Param1~3)와 Text의 뜻은 종류(FlowType)마다 정해진다(아직 종류가 None뿐이라 쓰이지 않는다).
/// </summary>
public sealed record CommunicationFlow(
    long CommunicationCode, long CharacterCode, long FlowNumber, CommunicationFlowType FlowType,
    long Param1, long Param2, long Param3, string Text);

// 교류: communication_group_master(code, name; code가 키), communication_master(code, group_code, name; code가 키),
// communication_flow_master(communication_code, character_code, flow_number, flow_type, param_1~3, text; 앞의 세 열이 키).
public sealed partial class GamePack
{
    public const string CommunicationGroupMasterPath = "master/communication_group_master_v1.tsv";
    public const string CommunicationMasterPath = "master/communication_master_v1.tsv";
    public const string CommunicationFlowMasterPath = "master/communication_flow_master_v1.tsv";

    /// <summary>교류 그룹 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CommunicationGroupEntry>? CommunicationGroups { get; init; }

    /// <summary>교류 화면의 입력 코드는 그룹 코드×100이라, 1 이상이고 100배가 long을 넘지 않아야 한다.</summary>
    static List<CommunicationGroupEntry>? LoadCommunicationGroups(ZipArchive zip)
    {
        var groups = LoadCodeNames(zip, CommunicationGroupMasterPath, "code", "name", "교류 그룹",
            (code, name) => new CommunicationGroupEntry(code, name));
        foreach (var g in groups ?? [])
            if (g.Code < 1 || g.Code > long.MaxValue / 100)
                throw new PackFormatException(
                    $"{CommunicationGroupMasterPath}: 교류 그룹 코드 '{g.Code}'는 1 이상 {long.MaxValue / 100} 이하여야 합니다(화면의 코드는 그룹 코드×100).");
        return groups;
    }

    /// <summary>교류 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CommunicationEntry>? Communications { get; init; }

    /// <summary>교류 흐름 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CommunicationFlow>? CommunicationFlows { get; init; }

    /// <summary>교류 마스터: 그룹 코드는 교류 그룹 마스터에 있어야 한다(그래서 교류 그룹 마스터가 있어야 한다).</summary>
    static List<CommunicationEntry>? LoadCommunications(ZipArchive zip, List<CommunicationGroupEntry>? groups)
    {
        const string file = CommunicationMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (groups is null) throw new PackFormatException($"{file}가 있으려면 {CommunicationGroupMasterPath}도 필요합니다.");

        var groupCodes = groups.Select(g => g.Code).ToHashSet();
        var rows = new List<CommunicationEntry>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, "code", "group_code", "name"))
        {
            var code = ParseLong(file, "교류 코드", r[0]);
            var groupCode = ParseLong(file, $"교류 '{code}'의 그룹 코드", r[1]);
            if (!seen.Add(code))
                throw new PackFormatException($"{file}: 교류 코드 '{code}'가 중복됩니다.");
            if (!groupCodes.Contains(groupCode))
                throw new PackFormatException($"{file}: 교류 '{code}'의 그룹 코드 '{groupCode}'가 교류 그룹 마스터에 없습니다.");
            rows.Add(new CommunicationEntry(code, groupCode, r[2]));
        }
        return rows;
    }

    /// <summary>교류 흐름 마스터: 교류는 교류 마스터에, 캐릭터는 캐릭터 인벤토리에 있어야 하고 (교류, 캐릭터, 흐름 번호)는 겹칠 수 없다.</summary>
    static List<CommunicationFlow>? LoadCommunicationFlows(
        ZipArchive zip, List<CommunicationEntry>? communications, IReadOnlyDictionary<long, InventoryEntry> inventory)
    {
        const string file = CommunicationFlowMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (communications is null) throw new PackFormatException($"{file}가 있으려면 {CommunicationMasterPath}도 필요합니다.");

        var codes = communications.Select(c => c.Code).ToHashSet();
        var rows = new List<CommunicationFlow>();
        var seen = new HashSet<(long, long, long)>();
        foreach (var r in Body(file, text, "communication_code", "character_code", "flow_number", "flow_type",
                     "param_1", "param_2", "param_3", "text"))
        {
            var code = ParseLong(file, "교류 코드", r[0]);
            var who = $"교류 '{code}'의";
            var character = ParseLong(file, $"{who} 캐릭터 코드", r[1]);
            var flow = ParseLong(file, $"{who} 캐릭터 '{character}'의 흐름 번호", r[2]);
            var where = $"{who} 캐릭터 '{character}'의 흐름 {flow}의";
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<CommunicationFlowType>().Contains(r[3], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<CommunicationFlowType>(r[3], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 흐름 종류 '{r[3]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<CommunicationFlowType>())})");

            if (!codes.Contains(code))
                throw new PackFormatException($"{file}: 교류 '{code}'가 교류 마스터에 없습니다.");
            if (!inventory.TryGetValue(character, out var entry) || entry.Kind != InventoryKind.Character)
                throw new PackFormatException($"{file}: {who} 캐릭터 코드 '{character}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (!seen.Add((code, character, flow)))
                throw new PackFormatException($"{file}: 같은 키(교류 {code}, 캐릭터 {character}, 흐름 {flow})가 두 번 있습니다.");
            rows.Add(new CommunicationFlow(code, character, flow, type,
                OptionalLong(file, $"{where} 인자 1", r[4]),
                OptionalLong(file, $"{where} 인자 2", r[5]),
                OptionalLong(file, $"{where} 인자 3", r[6]), r[7]));
        }
        return rows;
    }

    /// <summary>한 교류의 한 캐릭터의 흐름 칸들(키 순서: 흐름 번호).</summary>
    public IReadOnlyList<CommunicationFlow> CommunicationFlowsOf(long communicationCode, long characterCode) =>
        (CommunicationFlows ?? [])
            .Where(f => f.CommunicationCode == communicationCode && f.CharacterCode == characterCode)
            .OrderBy(f => f.FlowNumber).ToList();
}
