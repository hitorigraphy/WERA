using System.IO.Compression;

namespace Wera.Core;

/// <summary>아지트 마스터의 한 행: 아지트 코드와 이름.</summary>
public sealed record HideoutEntry(long Code, string Name);

// hideout_master(hideout_code(키), name). 선택이다. 없으면 아지트가 하나(코드 DefaultHideoutCode)뿐이라 지금까지와 똑같이 동작한다.
// 아지트가 여럿이면 플레이어는 한 번에 한 아지트(현재 아지트)만 쓰고, 이벤트의 SwitchHideout 흐름으로 바꾼다. 아지트마다 층, 시설, 오퍼레이터, 방어 파티를 따로 가진다.
//
// 아지트의 배치와 규칙을 말하는 마스터 셋(hideout_floor_master, hideout_expand_master, hideout_defense_enemy_master)은 선택 열 hideout_code를 가질 수 있다.
// 칸이 비었거나 열이 없는 행은 모든 아지트가 함께 쓰고, hideout_code가 있는 행은 그 아지트만 쓴다. 같은 항목(아래 "덮어쓰는 단위")을 아지트 전용 행이 다시 적으면
// 그 아지트에서는 전용 행이 공용 행을 대신한다: 층계는 floor_order 한 행, 확장 재료는 expand_count의 모든 행(그 횟수는 전용 행만 쓴다), 방어전 적은 code 한 행이다.
// 시설(facility_master)과 그 하위 마스터(설치 조건·재료·오퍼레이터 슬롯·흐름·방어력), 오퍼레이터 슬롯, 방어 흐름은 아지트와 상관없이 하나라서 hideout_code가 없다
// (아지트마다 다른 시설이 필요하면 다른 코드의 시설을 만들고 층계 마스터로 아지트별 기본 시설을 정한다).
public sealed partial class GamePack
{
    public const string HideoutMasterPath = "master/hideout_master_v1.tsv";

    /// <summary>아지트 마스터가 없을 때의 유일한 아지트의 코드.</summary>
    public const long DefaultHideoutCode = 1;

    /// <summary>아지트 마스터(게임팩 파일 순서). 마스터가 없으면 null(아지트는 코드 <see cref="DefaultHideoutCode"/> 하나).</summary>
    public IReadOnlyList<HideoutEntry>? Hideouts { get; init; }

    /// <summary>게임 시작 때의 현재 아지트 코드(게임 설정 StartHideoutCode, 없으면 아지트 마스터의 첫 행, 마스터도 없으면 <see cref="DefaultHideoutCode"/>).</summary>
    public long StartHideoutCode { get; init; } = DefaultHideoutCode;

    /// <summary>이 게임팩의 아지트 코드들(아지트 마스터 순서, 마스터가 없으면 <see cref="DefaultHideoutCode"/> 하나).</summary>
    public IReadOnlyList<long> HideoutCodes => Hideouts?.Select(h => h.Code).ToList() ?? [DefaultHideoutCode];

    public HideoutEntry? FindHideout(long code) => Hideouts?.FirstOrDefault(h => h.Code == code);

    /// <summary>아지트의 이름. 아지트 마스터가 없거나 그 코드가 없으면 null.</summary>
    public string? HideoutNameOf(long code) => FindHideout(code)?.Name;

    static List<HideoutEntry>? LoadHideouts(ZipArchive zip) =>
        LoadCodeNames(zip, HideoutMasterPath, "hideout_code", "name", "아지트", (code, name) => new HideoutEntry(code, name));

    /// <summary>게임 설정 StartHideoutCode(있으면)를 확인하고 시작 아지트 코드를 정한다: 아지트 마스터에 있는 코드(마스터가 없으면 <see cref="DefaultHideoutCode"/>)여야 한다.</summary>
    static long ResolveStartHideout(List<GameSettingEntry>? settings, List<HideoutEntry>? hideouts)
    {
        var codes = hideouts?.Select(h => h.Code).ToList() ?? [DefaultHideoutCode];
        if (settings?.FirstOrDefault(s => s.SettingType == GameSettingType.StartHideoutCode) is not { ValueType: GameSettingValueType.Long } setting) return codes[0];
        if (!codes.Contains(setting.LongValue))
            throw new PackFormatException(hideouts is null
                ? $"{GameSettingMasterPath}: StartHideoutCode '{setting.LongValue}'가 있으려면 {HideoutMasterPath}에 그 아지트가 있어야 합니다(마스터가 없으면 아지트는 {DefaultHideoutCode} 하나입니다)."
                : $"{GameSettingMasterPath}: StartHideoutCode '{setting.LongValue}'가 {HideoutMasterPath}에 없습니다.");
        return setting.LongValue;
    }

    /// <summary>
    /// 선택 열 hideout_code(헤더 이름으로 찾아 어느 위치에 있어도 된다)를 읽어 행마다 아지트 코드(비었으면 null: 모든 아지트가 쓴다)를 함께 돌려준다.
    /// 나머지 열은 <see cref="Body"/>와 같다. 아지트 코드는 아지트 마스터(<paramref name="hideoutCodes"/>)에 있어야 하고, 마스터가 없는데 코드를 적으면 오류다.
    /// </summary>
    static IEnumerable<(string[] Row, long? Hideout)> HideoutBody(string file, string text, IReadOnlySet<long>? hideoutCodes, params string[] header)
    {
        var rows = Tsv.WithoutMemo(Tsv.Parse(text));
        var at = rows.Count == 0 ? -1 : Array.FindIndex(rows[0], h => string.Equals(h.Trim(), "hideout_code", StringComparison.Ordinal));
        var cells = new List<string>();
        if (at >= 0)
        {
            var stripped = new List<string[]>(rows.Count);
            foreach (var r in rows)
            {
                cells.Add(at < r.Length ? r[at].Trim() : "");
                stripped.Add(at < r.Length ? r.Where((_, i) => i != at).ToArray() : r);
            }
            rows = stripped;
        }

        var index = 0;
        foreach (var row in BodyRows(file, rows, header))
        {
            index++;
            var cell = at >= 0 ? cells[index] : "";
            if (cell.Length == 0) { yield return (row, null); continue; }
            var code = ParseLong(file, "아지트 코드", cell);
            if (hideoutCodes is null)
                throw new PackFormatException($"{file}: hideout_code '{code}'가 있으려면 {HideoutMasterPath}도 필요합니다.");
            if (!hideoutCodes.Contains(code))
                throw new PackFormatException($"{file}: 아지트 '{code}'가 {HideoutMasterPath}에 없습니다.");
            yield return (row, code);
        }
    }

    /// <summary>
    /// 아지트(<paramref name="hideout"/>)가 쓰는 행들(파일 순서): 공용 행(아지트 코드 없음)과 그 아지트 전용 행. 전용 행이 같은 덮어쓰는 단위(<paramref name="parentKey"/>)를 다시 적었으면
    /// 그 단위의 공용 행은 빠진다.
    /// </summary>
    static List<T> ForHideout<T, TKey>(IEnumerable<T> rows, long hideout, Func<T, long?> hideoutOf, Func<T, TKey> parentKey) where TKey : notnull
    {
        var mine = rows.Where(r => hideoutOf(r) is null || hideoutOf(r) == hideout).ToList();
        var overridden = mine.Where(r => hideoutOf(r) == hideout).Select(parentKey).ToHashSet();
        return mine.Where(r => hideoutOf(r) == hideout || !overridden.Contains(parentKey(r))).ToList();
    }

    /// <summary>
    /// 이벤트·던전 이벤트 흐름(과 그 서브모듈)의 SwitchHideout 칸이 가리키는 아지트가 있는지 확인한다(아지트 마스터가 없으면 아지트는 <see cref="DefaultHideoutCode"/> 하나).
    /// </summary>
    static void CheckSwitchHideoutCalls(
        List<EventFlow>? eventFlows, List<EventFlow>? dungeonEventFlows,
        Dictionary<long, IReadOnlyList<EventFlow>>? eventSubmodules, Dictionary<long, IReadOnlyList<EventFlow>>? dungeonEventSubmodules,
        List<HideoutEntry>? hideouts)
    {
        var codes = hideouts?.Select(h => h.Code).ToHashSet() ?? [DefaultHideoutCode];
        void Check(string file, string who, IEnumerable<EventFlow>? flows)
        {
            foreach (var f in (flows ?? []).Where(f => f.FlowType == EventFlowType.SwitchHideout))
                if (!codes.Contains(f.Param1))
                    throw new PackFormatException($"{file}: {who} '{f.EventCode}'의 흐름 {f.FlowNumber}의 SwitchHideout 아지트 '{f.Param1}'가 "
                        + (hideouts is null ? $"없습니다(아지트 마스터가 없으면 아지트는 {DefaultHideoutCode} 하나입니다)." : $"{HideoutMasterPath}에 없습니다."));
        }
        Check(EventFlowMasterPath, "이벤트", eventFlows);
        Check(DungeonEventFlowMasterPath, "던전 이벤트", dungeonEventFlows);
        Check(EventSubmoduleMasterPath, "서브모듈", eventSubmodules?.Values.SelectMany(f => f));
        Check(DungeonEventSubmoduleMasterPath, "서브모듈", dungeonEventSubmodules?.Values.SelectMany(f => f));
    }

    /// <summary>아지트 코드 칸의 글: 공용 행이면 빈 글, 전용 행이면 "아지트 N의 ".</summary>
    static string HideoutLabel(long? hideout) => hideout is { } h ? $"아지트 '{h}'의 " : "";
}
