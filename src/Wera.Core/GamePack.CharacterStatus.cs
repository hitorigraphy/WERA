using System.IO.Compression;

namespace Wera.Core;

// 스테이터스: 스테이터스 종류(status_master: level, hp, xp 등)와 캐릭터별 레벨별 값(character_status_master).
public sealed partial class GamePack
{
    public const string StatusMasterPath = "master/status_master_v1.tsv";
    public const string CharacterStatusMasterPath = "master/character_status_master_v1.tsv";

    /// <summary>레벨 칸이 이 값인 행은 레벨별 행이 없을 때 쓰는 기본값이고, 캐릭터를 만들 때(레벨이 정해지기 전)의 값이다.</summary>
    public const long BaseLevel = 0;

    /// <summary>엔진이 아는 스테이터스 코드: 스테이터스 마스터에 이 번호로 적어야 캐릭터 상태(CharacterState)를 채우는 데 쓰인다.</summary>
    public const long LevelStatusCode = 1;
    /// <summary>HP: 초깃값이 현재 HP, 최댓값이 최대 HP.</summary>
    public const long HpStatusCode = 2;
    /// <summary>경험치: 초깃값이 현재 경험치, 최댓값이 다음 레벨까지 필요한 경험치.</summary>
    public const long XpStatusCode = 3;

    /// <summary>스테이터스 마스터가 없으면 null.</summary>
    public IReadOnlyList<StatusEntry>? Statuses { get; init; }

    /// <summary>캐릭터 스테이터스 마스터가 없으면 null. 게임팩에 적힌 순서 그대로다.</summary>
    public IReadOnlyList<CharacterStatus>? CharacterStatuses { get; init; }

    static List<StatusEntry>? LoadStatuses(ZipArchive zip) =>
        LoadCodeNames(zip, StatusMasterPath, "status_code", "status_name", "스테이터스",
            (code, name) => new StatusEntry(code, name));

    /// <summary>
    /// 캐릭터 스테이터스 마스터: 캐릭터는 캐릭터 인벤토리에, 스테이터스는 스테이터스 마스터에 있어야 하고,
    /// 복합키 (캐릭터, 스테이터스, 레벨)을 두 번 쓸 수 없다. 레벨은 0 이상이고(0은 기본값 행), 최솟값은 최댓값 이하,
    /// 초깃값은 최솟값과 최댓값 사이여야 한다. 값은 모두 long이다.
    /// </summary>
    static List<CharacterStatus>? LoadCharacterStatuses(
        ZipArchive zip, List<CharacterEntry>? characters, List<StatusEntry>? statuses)
    {
        const string file = CharacterStatusMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (characters is null) throw new PackFormatException($"{file}가 있으려면 {CharacterInventoryMasterPath}도 필요합니다.");
        if (statuses is null) throw new PackFormatException($"{file}가 있으려면 {StatusMasterPath}도 필요합니다.");

        var characterCodes = characters.Select(c => c.Code).ToHashSet();
        var statusCodes = statuses.Select(s => s.Code).ToHashSet();
        var rows = new List<CharacterStatus>();
        var seen = new HashSet<(long, long, long)>();
        foreach (var r in Body(file, text, "character_code", "status_code", "level", "initial_value", "min_value", "max_value"))
        {
            var character = ParseLong(file, "캐릭터 코드", r[0]);
            var status = ParseLong(file, $"캐릭터 '{character}'의 스테이터스 코드", r[1]);
            var level = ParseLong(file, $"캐릭터 '{character}' 스테이터스 '{status}'의 레벨", r[2]);
            var who = $"캐릭터 '{character}' 스테이터스 '{status}' 레벨 {level}의";
            var initial = ParseLong(file, $"{who} 초깃값", r[3]);
            var min = ParseLong(file, $"{who} 최솟값", r[4]);
            var max = ParseLong(file, $"{who} 최댓값", r[5]);

            if (!characterCodes.Contains(character))
                throw new PackFormatException($"{file}: 캐릭터 '{character}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (!statusCodes.Contains(status))
                throw new PackFormatException($"{file}: 캐릭터 '{character}'의 스테이터스 '{status}'가 스테이터스 마스터에 없습니다.");
            if (level < 0)
                throw new PackFormatException($"{file}: 캐릭터 '{character}' 스테이터스 '{status}'의 레벨 {level}이(가) 음수입니다.");
            if (min > max)
                throw new PackFormatException($"{file}: {who} 최솟값 {min}이(가) 최댓값 {max}보다 큽니다.");
            if (initial < min || initial > max)
                throw new PackFormatException($"{file}: {who} 초깃값 {initial}이(가) 최솟값 {min}~최댓값 {max} 범위 밖입니다.");
            if (!seen.Add((character, status, level)))
                throw new PackFormatException(
                    $"{file}: 같은 키(캐릭터 {character}, 스테이터스 {status}, 레벨 {level})가 두 번 있습니다.");
            rows.Add(new CharacterStatus(character, status, level, initial, min, max));
        }
        return rows;
    }

    // ---- 키로 찾기 ----

    public StatusEntry? FindStatus(long code) =>
        Masters.StatusRowTable.TryFindByCode(code, out var r) ? new StatusEntry(r.Code, r.Name) : null;

    /// <summary>
    /// 캐릭터의 스테이터스 값(초깃값, 최솟값, 최댓값)을 레벨로 찾는다. 그 레벨의 행이 있으면 그것을,
    /// 없으면 레벨 0(<see cref="BaseLevel"/>)의 행을 쓴다(폴백). 둘 다 없으면 null.
    /// </summary>
    public CharacterStatus? CharacterStatusAt(long characterCode, long statusCode, long level) =>
        TryCharacterStatus(characterCode, statusCode, level) ?? (level == BaseLevel ? null : TryCharacterStatus(characterCode, statusCode, BaseLevel));

    /// <summary>캐릭터를 만들 때의 스테이터스 값: 레벨이 정해지지 않았으므로 레벨 0으로 찾는다. 없으면 null.</summary>
    public CharacterStatus? InitialCharacterStatus(long characterCode, long statusCode) =>
        TryCharacterStatus(characterCode, statusCode, BaseLevel);

    CharacterStatus? TryCharacterStatus(long characterCode, long statusCode, long level) =>
        Masters.CharacterStatusRowTable.TryFindByCharacterCodeAndStatusCodeAndLevel((characterCode, statusCode, level), out var r)
            ? new CharacterStatus(r.CharacterCode, r.StatusCode, r.Level, r.InitialValue, r.MinValue, r.MaxValue)
            : null;

    /// <summary>캐릭터에게 적힌 모든 행(모든 스테이터스, 모든 레벨. 게임팩 파일 순서). 보조키 캐릭터 코드로 찾는다.</summary>
    public IReadOnlyList<CharacterStatus> CharacterStatusesOf(long characterCode) =>
        Masters.CharacterStatusRowTable.FindByCharacterCode(characterCode)
            .OrderBy(r => r.Order)
            .Select(r => new CharacterStatus(r.CharacterCode, r.StatusCode, r.Level, r.InitialValue, r.MinValue, r.MaxValue))
            .ToList();

    /// <summary>
    /// 새로 얻는 캐릭터의 상태. 레벨이 정해지기 전이므로 레벨 0의 행으로 채운다: 레벨 = level의 초깃값,
    /// HP = hp의 초깃값(최대 HP는 최댓값), 경험치 = xp의 초깃값(필요 경험치는 최댓값).
    /// 행이 없는 스테이터스는 레벨 1, HP 1/1, 경험치 0/1로 시작한다.
    /// </summary>
    public CharacterState NewCharacterState(long characterCode)
    {
        var level = InitialCharacterStatus(characterCode, LevelStatusCode);
        var hp = InitialCharacterStatus(characterCode, HpStatusCode);
        var xp = InitialCharacterStatus(characterCode, XpStatusCode);
        return new CharacterState(
            level?.InitialValue ?? 1,
            hp?.InitialValue ?? 1, hp?.MaxValue ?? 1,
            xp?.InitialValue ?? 0, xp?.MaxValue ?? 1).Clamped();
    }
}
