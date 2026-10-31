using System.IO.Compression;

namespace Wera.Core;

// 스테이터스: 스테이터스 종류(status_master: level, hp, xpp 등)와 캐릭터별 레벨별 값(character_status_master).
public sealed partial class GamePack
{
    public const string StatusMasterPath = "master/status_master_v1.tsv";
    public const string CharacterStatusMasterPath = "master/character_status_master_v1.tsv";

    /// <summary>레벨 칸이 이 값인 행은 레벨별 행이 없을 때 쓰는 기본값이고, 캐릭터를 만들 때(레벨이 정해지기 전)의 값이다.</summary>
    public const long BaseLevel = 0;

    /// <summary>엔진이 아는 스테이터스 코드: 스테이터스 마스터에 이 번호로 적어야 캐릭터 상태(CharacterInstance)를 채우는 데 쓰인다.</summary>
    public const long LevelStatusCode = 1;
    /// <summary>HP: 초깃값이 현재 HP, 최댓값이 최대 HP.</summary>
    public const long HpStatusCode = 2;
    /// <summary>경험치: 초깃값이 현재 경험치, 최댓값이 다음 레벨까지 필요한 경험치.</summary>
    public const long XppStatusCode = 3;
    /// <summary>최대 HP: 초깃값이 새 캐릭터의 최대 HP. 마스터에 이 스테이터스의 행이 없으면 hp 행의 최댓값을 쓴다.</summary>
    public const long MaxHpStatusCode = 4;
    /// <summary>유니크 아이템(장비)의 강화 횟수: 유니크 아이템 스테이터스 마스터에 이 코드로 적은 값이 초깃값이다. 장비 상세 화면의 "+{강화 횟수}"가 이 값을 보여 주고, 아이템에 이 스테이터스가 없으면 0이다.</summary>
    public const long EnhanceCountStatusCode = 8;
    /// <summary>다음 레벨까지 필요한 경험치: 초깃값이 새 캐릭터의 필요 경험치. 마스터에 이 스테이터스의 행이 없으면 xpp 행의 최댓값을 쓴다(그것도 없으면 0).</summary>
    public const long XppMaxStatusCode = 5;
    /// <summary>경험치(Exp): 초깃값이 현재 경험치, 최댓값이 다음 레벨까지 필요한 경험치. XPP(XP로 바꾸기 전 단계의 값)와는 별개이고, 캐릭터 상세 화면의 Exp 바가 이 값을 보여 준다.</summary>
    public const long ExpStatusCode = 6;
    /// <summary>다음 레벨까지 필요한 경험치: 초깃값이 새 캐릭터의 필요 경험치. 마스터에 이 스테이터스의 행이 없으면 exp 행의 최댓값을 쓴다(그것도 없으면 0).</summary>
    public const long ExpMaxStatusCode = 7;

    /// <summary>스테이터스 마스터가 없으면 null.</summary>
    public IReadOnlyList<StatusEntry>? Statuses { get; init; }

    /// <summary>캐릭터 스테이터스 마스터가 없으면 null. 게임팩에 적힌 순서 그대로다.</summary>
    public IReadOnlyList<CharacterStatus>? CharacterStatuses { get; init; }

    /// <summary>
    /// 스테이터스 마스터: level(1)과 hp(2)는 엔진이 예약한 코드라 반드시 있어야 한다.
    /// 세 번째 열에 선택 열 character_display_order(0 이상의 정수)를 둘 수 있다: 0(또는 열이 없음)이면 캐릭터 상세에 표시하지 않고, 1 이상이면 작은 수부터 표시한다.
    /// </summary>
    static List<StatusEntry>? LoadStatuses(ZipArchive zip)
    {
        const string file = StatusMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        var table = Tsv.WithoutMemo(Tsv.Parse(text));
        var hasOrder = table.Count > 0 && table[0].Length > 2 && table[0][2] == "character_display_order";
        var statuses = new List<StatusEntry>();
        var seen = new HashSet<long>();
        foreach (var r in hasOrder ? Body(file, text, "status_code", "status_name", "character_display_order") : Body(file, text, "status_code", "status_name"))
        {
            var code = ParseLong(file, "스테이터스 코드", r[0]);
            if (!seen.Add(code))
                throw new PackFormatException($"{file}: 스테이터스 코드 '{code}'가 중복됩니다.");
            var order = hasOrder && r[2].Trim() != "" ? ParseLong(file, $"스테이터스 '{code}'의 character_display_order", r[2]) : 0;
            if (order < 0)
                throw new PackFormatException($"{file}: 스테이터스 '{code}'의 character_display_order '{order}'는 0 이상이어야 합니다.");
            statuses.Add(new StatusEntry(code, r[1], order));
        }
        foreach (var (code, what) in new[] { (LevelStatusCode, "level"), (HpStatusCode, "hp") })
            if (statuses.All(s => s.Code != code))
                throw new PackFormatException($"{StatusMasterPath}: 예약된 스테이터스 코드 {code}({what})가 없습니다. level은 {LevelStatusCode}, hp는 {HpStatusCode}로 적어야 합니다.");
        return statuses;
    }

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

        // 캐릭터를 만들 때는 레벨 0 행을 읽는다. level과 hp는 폴백이 없으므로 모든 캐릭터에게 레벨 0 행이 있어야 한다(xp는 없으면 0).
        var baseRows = rows.Where(r => r.Level == BaseLevel).Select(r => (r.CharacterCode, r.StatusCode)).ToHashSet();
        foreach (var character in characters)
            foreach (var (code, what) in new[] { (LevelStatusCode, "level"), (HpStatusCode, "hp") })
                if (!baseRows.Contains((character.Code, code)))
                    throw new PackFormatException(
                        $"{file}: 캐릭터 '{character.Code}'에 {what}({code})의 레벨 {BaseLevel} 행이 없습니다.");
        return rows;
    }

    // ---- 키로 찾기 ----

    public StatusEntry? FindStatus(long code) =>
        Masters.StatusRowTable.TryFindByCode(code, out var r) ? new StatusEntry(r.Code, r.Name, r.CharacterDisplayOrder) : null;

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

    /// <summary>캐릭터에게 적힌 모든 행(모든 스테이터스, 모든 레벨. 키 순서: 스테이터스, 레벨). 보조키 캐릭터 코드로 찾는다.</summary>
    public IReadOnlyList<CharacterStatus> CharacterStatusesOf(long characterCode) =>
        Masters.CharacterStatusRowTable.FindByCharacterCode(characterCode)
            .OrderBy(r => r.StatusCode).ThenBy(r => r.Level)
            .Select(r => new CharacterStatus(r.CharacterCode, r.StatusCode, r.Level, r.InitialValue, r.MinValue, r.MaxValue))
            .ToList();

    /// <summary>
    /// 새로 얻는 캐릭터의 상태. 레벨이 정해지기 전이므로 레벨 0의 행으로 채운다: 마스터에 그 캐릭터의 행이 있는 모든 스테이터스를 초깃값으로
    /// <see cref="CharacterInstance.Statuses"/>에 넣는다. 예약된 일곱 코드(level 1, hp 2, xpp 3, max_hp 4, xpp_max 5, exp 6, exp_max 7)는 늘 들어간다:
    /// 행이 없으면 level 1, hp 1, xpp 0, exp 0이고, max_hp는 hp 행의 최댓값(없으면 1), xpp_max는 xpp 행의 최댓값(없으면 0), exp_max는 exp 행의 최댓값(없으면 0)이다.
    /// 캐릭터 스테이터스 마스터가 있으면 level과 hp의 레벨 0 행이 반드시 있다(로드 때 검증). 스킬은 아직 하나도 익히지 않았다.
    /// </summary>
    public CharacterInstance NewCharacterInstance(long characterCode, string instanceId = "", long orderNumber = 0)
    {
        var statuses = new Dictionary<long, long>();
        foreach (var code in CharacterStatusesOf(characterCode).Select(r => r.StatusCode).Distinct())
            if (InitialCharacterStatus(characterCode, code) is { } row)
                statuses[code] = row.InitialValue;
        statuses.TryAdd(LevelStatusCode, 1);
        statuses.TryAdd(HpStatusCode, 1);
        statuses.TryAdd(XppStatusCode, 0);
        statuses.TryAdd(MaxHpStatusCode, InitialCharacterStatus(characterCode, HpStatusCode)?.MaxValue ?? 1);
        statuses.TryAdd(XppMaxStatusCode, InitialCharacterStatus(characterCode, XppStatusCode)?.MaxValue ?? 0);
        statuses.TryAdd(ExpStatusCode, 0);
        statuses.TryAdd(ExpMaxStatusCode, InitialCharacterStatus(characterCode, ExpStatusCode)?.MaxValue ?? 0);
        return new CharacterInstance { InstanceId = instanceId, CharacterCode = characterCode, OrderNumber = orderNumber, Statuses = statuses }.Clamped();
    }
}
