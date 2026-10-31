using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 세이브를 불러온 뒤 캐릭터에게 실행하는 흐름의 종류. TSV에는 이름(대소문자 무시)으로 적는다. <see cref="AfterLoadCharacterCircuit"/>이 실행한다.
/// 숫자 값은 정해진 번호다: None 0. (그 밖의 종류는 아직 정해지지 않았다.)
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoadCharacterFlowType>))]
public enum LoadCharacterFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
}

/// <summary>로드 후 캐릭터 흐름 마스터의 한 행: 서킷이 execute_number 순서대로 실행하는 흐름 하나(종류와 값 셋).</summary>
public sealed record LoadCharacterFlow(long ExecuteNumber, LoadCharacterFlowType FlowType, long Value1, long Value2, long Value3);

// after_load_character_flow_master(execute_number, flow_type, value_1, value_2, value_3; 첫 열이 키). 선택이다.
// 세이브를 불러오면 보유한 캐릭터마다 이 흐름을 실행해, 게임팩의 정의에 따라 캐릭터 데이터를 조작한다.
public sealed partial class GamePack
{
    public const string AfterLoadCharacterFlowMasterPath = "master/after_load_character_flow_master_v1.tsv";

    /// <summary>로드 후 캐릭터 흐름 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<LoadCharacterFlow>? AfterLoadCharacterFlows { get; init; }

    /// <summary>
    /// 로드 후 캐릭터 흐름 마스터: 키 execute_number를 두 번 쓸 수 없다. 종류는 이름으로 적고 값 셋은 비우면 0이다.
    /// 종류별 인자 검증은 종류가 정해지면 더한다(지금은 None뿐).
    /// </summary>
    static List<LoadCharacterFlow>? LoadAfterLoadCharacterFlows(ZipArchive zip)
    {
        const string file = AfterLoadCharacterFlowMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;

        var rows = new List<LoadCharacterFlow>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, "execute_number", "flow_type", "value_1", "value_2", "value_3"))
        {
            var number = ParseLong(file, "실행 번호", r[0]);
            var where = $"실행 {number}의";
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<LoadCharacterFlowType>().Contains(r[1], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<LoadCharacterFlowType>(r[1], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 흐름 종류 '{r[1]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<LoadCharacterFlowType>())})");
            var value1 = r[2].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_1", r[2]);
            var value2 = r[3].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_2", r[3]);
            var value3 = r[4].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_3", r[4]);
            if (!seen.Add(number))
                throw new PackFormatException($"{file}: 실행 번호 '{number}'가 두 번 적혀 있습니다.");
            rows.Add(new LoadCharacterFlow(number, type, value1, value2, value3));
        }
        return rows;
    }

    /// <summary>로드 후 캐릭터 흐름(실행 번호 순). 마스터가 없으면 빈 목록.</summary>
    public IReadOnlyList<LoadCharacterFlow> AfterLoadCharacterFlowsInOrder() =>
        Masters.AfterLoadCharacterFlowRowTable.All
            .OrderBy(r => r.ExecuteNumber)
            .Select(r => new LoadCharacterFlow(r.ExecuteNumber, r.FlowType, r.Value1, r.Value2, r.Value3)).ToList();
}
