using System.IO.Compression;

namespace Wera.Core;

/// <summary>교류 그룹 마스터의 한 행(예: 대화). 교류 화면 왼쪽에 "[코드×100] 이름"으로 나열된다.</summary>
public sealed record CommunicationGroupEntry(long Code, string Name);

// 교류 그룹: communication_group_master(code, name; code가 키).
public sealed partial class GamePack
{
    public const string CommunicationGroupMasterPath = "master/communication_group_master_v1.tsv";

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
}
