using System.IO.Compression;

namespace Wera.Core;

// 캐릭터 스테이터스: 스테이터스 종류(character_status_master)와 캐릭터별 초깃값(character_initial_status_master).
public sealed partial class GamePack
{
    public const string CharacterStatusMasterPath = "master/character_status_master_v1.tsv";
    public const string CharacterInitialStatusMasterPath = "master/character_initial_status_master_v1.tsv";

    /// <summary>캐릭터 스테이터스 마스터가 없으면 null.</summary>
    public IReadOnlyList<CharacterStatusEntry>? CharacterStatuses { get; init; }

    /// <summary>캐릭터 초기 스테이터스 마스터가 없으면 null. 게임팩에 적힌 순서 그대로다.</summary>
    public IReadOnlyList<CharacterInitialStatus>? CharacterInitialStatuses { get; init; }

    static List<CharacterStatusEntry>? LoadCharacterStatuses(ZipArchive zip) =>
        LoadCodeNames(zip, CharacterStatusMasterPath, "status_code", "status_name", "캐릭터 스테이터스",
            (code, name) => new CharacterStatusEntry(code, name));

    /// <summary>
    /// 캐릭터 초기 스테이터스 마스터: 캐릭터는 캐릭터 인벤토리에, 스테이터스는 캐릭터 스테이터스 마스터에 있어야 하고,
    /// 같은 (캐릭터, 스테이터스)를 두 번 쓸 수 없다. 초깃값은 long이다.
    /// </summary>
    static List<CharacterInitialStatus>? LoadCharacterInitialStatuses(
        ZipArchive zip, List<CharacterEntry>? characters, List<CharacterStatusEntry>? statuses)
    {
        const string file = CharacterInitialStatusMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (characters is null) throw new PackFormatException($"{file}가 있으려면 {CharacterInventoryMasterPath}도 필요합니다.");
        if (statuses is null) throw new PackFormatException($"{file}가 있으려면 {CharacterStatusMasterPath}도 필요합니다.");

        var characterCodes = characters.Select(c => c.Code).ToHashSet();
        var statusCodes = statuses.Select(s => s.Code).ToHashSet();
        var rows = new List<CharacterInitialStatus>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "character_code", "status_code", "initial_value"))
        {
            var character = ParseLong(file, "캐릭터 코드", r[0]);
            var status = ParseLong(file, $"캐릭터 '{character}'의 스테이터스 코드", r[1]);
            var value = ParseLong(file, $"캐릭터 '{character}' 스테이터스 '{status}'의 초깃값", r[2]);
            if (!characterCodes.Contains(character))
                throw new PackFormatException($"{file}: 캐릭터 '{character}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (!statusCodes.Contains(status))
                throw new PackFormatException($"{file}: 캐릭터 '{character}'의 스테이터스 '{status}'가 캐릭터 스테이터스 마스터에 없습니다.");
            if (!seen.Add((character, status)))
                throw new PackFormatException($"{file}: 캐릭터 '{character}'에 스테이터스 '{status}'가 두 번 있습니다.");
            rows.Add(new CharacterInitialStatus(character, status, value));
        }
        return rows;
    }

    // ---- 키로 찾기 ----

    public CharacterStatusEntry? FindCharacterStatus(long code) =>
        Masters.CharacterStatusRowTable.TryFindByCode(code, out var r) ? new CharacterStatusEntry(r.Code, r.Name) : null;

    /// <summary>캐릭터의 초기 스테이터스 한 개. 정해 두지 않았으면 null.</summary>
    public long? InitialStatusOf(long characterCode, long statusCode) =>
        Masters.CharacterInitialStatusRowTable.TryFindByCharacterCodeAndStatusCode((characterCode, statusCode), out var r)
            ? r.InitialValue : null;

    /// <summary>캐릭터의 초기 스테이터스들(게임팩 파일 순서). 보조키 캐릭터 코드로 찾는다.</summary>
    public IReadOnlyList<CharacterInitialStatus> InitialStatusesOf(long characterCode) =>
        Masters.CharacterInitialStatusRowTable.FindByCharacterCode(characterCode)
            .OrderBy(r => r.Order)
            .Select(r => new CharacterInitialStatus(r.CharacterCode, r.StatusCode, r.InitialValue))
            .ToList();
}
