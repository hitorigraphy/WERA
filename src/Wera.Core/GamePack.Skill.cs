using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>스킬 슬롯 마스터의 한 행.</summary>
public sealed record SkillSlotEntry(long Code, string Name);

/// <summary>
/// 캐릭터 스킬 슬롯 태그 마스터의 한 행: 캐릭터의 스킬 슬롯에 그룹 단위로 걸린 태그 규칙.
/// 앞의 네 열(캐릭터, 스킬 슬롯, 그룹, 태그)이 복합키다.
/// </summary>
public sealed record CharacterSkillSlotTag(long CharacterCode, long SkillSlotCode, long GroupCode, long TagCode);

/// <summary>스킬 마스터의 한 행.</summary>
public sealed record SkillEntry(long Code, string Name);

/// <summary>스킬 태그 마스터의 한 행.</summary>
public sealed record SkillTag(long SkillCode, long TagCode);

/// <summary>
/// 스킬 이펙트가 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다.
/// 마스터만으로는 스킬의 동작을 정할 수 없으므로 종류마다 동작을 코드로 구현하고, 마스터는 어느 종류를 어떤 값(value_1~3)으로 쓸지만 적는다.
/// 숫자 값은 정해진 번호다: None 0, CacheToTargetModValue 1, CacheToTargetSetValue 2, CacheToCasterModValue 3, CacheToCasterSetValue 4, CasterValueToCache 5, TargetValueToCache 6, CacheAdd 7, CacheSubtract 8, CacheMultiply 9, CacheDivide 10, SetCacheValue 11, Jump 12, Finish 13, JumpCacheEqual 14, JumpCacheNotEqual 15, JumpCacheGreaterThan 16, JumpCacheGreaterOrEqual 17, JumpCacheLessThan 18, JumpCacheLessOrEqual 19.
/// 이펙트는 스킬 서킷(<see cref="SkillCircuit"/>)에서 실행 번호 순서로 실행된다.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EffectType>))]
public enum EffectType
{
    /// <summary>아무 일도 하지 않는다. 값은 보지 않는다.</summary>
    None = 0,
    /// <summary>
    /// 서킷 캐시의 값을 타겟의 스테이터스에 더한다(음수면 뺀다). value_1이 status_code(스테이터스 마스터에 있어야 함),
    /// value_2가 cache_index(0 이상)이고 value_3은 보지 않는다. 결과는 그 캐릭터 스테이터스의 최솟값~최댓값으로 맞춘다.
    /// 타겟에게 그 스테이터스가 없거나 캐시에 그 번호의 값이 없으면 아무 일도 하지 않는다.
    /// </summary>
    CacheToTargetModValue = 1,
    /// <summary>서킷 캐시의 값으로 타겟의 스테이터스를 바꾼다. 인자와 예외는 <see cref="CacheToTargetModValue"/>와 같다.</summary>
    CacheToTargetSetValue = 2,
    /// <summary>서킷 캐시의 값을 시전자의 스테이터스에 더한다. 인자와 예외는 <see cref="CacheToTargetModValue"/>와 같다.</summary>
    CacheToCasterModValue = 3,
    /// <summary>서킷 캐시의 값으로 시전자의 스테이터스를 바꾼다. 인자와 예외는 <see cref="CacheToTargetModValue"/>와 같다.</summary>
    CacheToCasterSetValue = 4,
    /// <summary>
    /// 시전자의 스테이터스 값을 서킷 캐시의 cache_index번에 쓴다(이전 값은 덮어씀). value_1이 status_code, value_2가 cache_index(0~<see cref="MaxCacheIndex"/>)이고
    /// value_3은 보지 않는다. 캐시가 그 번호보다 짧으면 모자란 칸을 0으로 채워 늘린다. 시전자에게 그 스테이터스가 없으면 캐시를 건드리지 않는다.
    /// </summary>
    CasterValueToCache = 5,
    /// <summary>타겟의 스테이터스 값을 서킷 캐시의 cache_index번에 쓴다. 인자와 예외는 <see cref="CasterValueToCache"/>와 같다.</summary>
    TargetValueToCache = 6,
    /// <summary>
    /// 캐시 계산: cache[value_1] = cache[value_2] + cache[value_3]. 세 값 모두 cache_index(0~<see cref="MaxCacheIndex"/>)다.
    /// 결과를 쓸 번호가 캐시보다 뒤면 모자란 칸을 0으로 채워 늘린다. 계산할 칸(value_2나 value_3)이 캐시에 없거나 결과가 decimal 범위를 넘으면 아무 일도 하지 않는다.
    /// </summary>
    CacheAdd = 7,
    /// <summary>cache[value_1] = cache[value_2] - cache[value_3]. 인자와 예외는 <see cref="CacheAdd"/>와 같다.</summary>
    CacheSubtract = 8,
    /// <summary>cache[value_1] = cache[value_2] * cache[value_3]. 인자와 예외는 <see cref="CacheAdd"/>와 같다.</summary>
    CacheMultiply = 9,
    /// <summary>cache[value_1] = cache[value_2] / cache[value_3](나눗셈은 decimal 그대로, 반올림 없음). 인자와 예외는 <see cref="CacheAdd"/>와 같고, value_3의 칸이 0이면 아무 일도 하지 않는다.</summary>
    CacheDivide = 10,
    /// <summary>
    /// 캐시에 상수를 쓴다: cache[value_1] = value_2 / 10^value_3. value_1이 cache_index(0~<see cref="MaxCacheIndex"/>), value_2가 정수 값,
    /// value_3이 소수 자릿수(0~28)다(15, 2는 0.15). 캐시가 짧으면 모자란 칸을 0으로 채워 늘린다.
    /// </summary>
    SetCacheValue = 11,
    /// <summary>
    /// 조건 없이 점프한다. value_1이 점프할 execute_number이고, 같은 스킬에 있어야 하며 자신의 execute_number보다 커야 한다(뒤로만 가서 무한 반복이 없다).
    /// </summary>
    Jump = 12,
    /// <summary>스킬의 이펙트 실행을 끝낸다(뒤의 이펙트는 실행하지 않는다).</summary>
    Finish = 13,
    /// <summary>
    /// cache[value_1]과 cache[value_2]를 견주어(같다) 참이면 value_3의 execute_number로 점프하고, 거짓이면 다음 이펙트로 간다. value_1과 value_2는 cache_index이고
    /// 둘 중 하나가 캐시에 없으면 거짓이다. 점프 대상의 규칙은 <see cref="Jump"/>와 같다.
    /// </summary>
    JumpCacheEqual = 14,
    /// <summary>같지 않다. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheNotEqual = 15,
    /// <summary>cache[value_1] &gt; cache[value_2]. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheGreaterThan = 16,
    /// <summary>cache[value_1] &gt;= cache[value_2]. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheGreaterOrEqual = 17,
    /// <summary>cache[value_1] &lt; cache[value_2]. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheLessThan = 18,
    /// <summary>cache[value_1] &lt;= cache[value_2]. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheLessOrEqual = 19,
}

/// <summary>스킬 이펙트의 JumpCache* 종류가 쓰는 규칙: 이름이 JumpCache로 시작하는 종류의 비교 방법.</summary>
public static class SkillJump
{
    const string Prefix = "JumpCache";

    public static FlagComparison? Of(EffectType type) =>
        type.ToString() is var name && name.StartsWith(Prefix, StringComparison.Ordinal)
        && Enum.TryParse<FlagComparison>(name[Prefix.Length..], out var comparison)
            ? comparison
            : null;

    /// <summary>점프하는 종류(Jump, JumpCache*)인가.</summary>
    public static bool IsJump(EffectType type) => type == EffectType.Jump || Of(type) is not null;

    /// <summary>점프할 execute_number가 어느 값(value_1, value_3)에 있는가. 점프하는 종류가 아니면 null.</summary>
    public static long? TargetOf(SkillEffect effect) =>
        effect.EffectType == EffectType.Jump ? effect.Value1 : Of(effect.EffectType) is not null ? effect.Value3 : null;

    public static bool Test(FlagComparison comparison, decimal left, decimal right) => comparison switch
    {
        FlagComparison.Equal => left == right,
        FlagComparison.NotEqual => left != right,
        FlagComparison.GreaterThan => left > right,
        FlagComparison.GreaterOrEqual => left >= right,
        FlagComparison.LessThan => left < right,
        FlagComparison.LessOrEqual => left <= right,
        _ => false,
    };
}

/// <summary>스킬 이펙트 마스터의 한 행: 스킬을 쓰면 execute_number 순서대로 실행하는 이펙트 하나(종류와 값 셋).</summary>
public sealed record SkillEffect(long SkillCode, long ExecuteNumber, EffectType EffectType, long Value1, long Value2, long Value3);

// 스킬 마스터: skill_slot_master(code, name; code가 키), character_skill_slot_tag_master(character_code, skill_slot_code, group_code, tag_code; 네 열이 키),
// skill_master(code, name; code가 키), skill_tag_master(skill_code, tag_code; 두 열이 키), skill_effect_master(skill_code, execute_number, effect_type, value_1, value_2, value_3; 앞의 두 열이 키).
// 모두 선택이다.
public sealed partial class GamePack
{
    /// <summary>스킬 이펙트의 cache_index가 가질 수 있는 가장 큰 값(서킷 캐시가 끝없이 늘어나지 않게 한다).</summary>
    public const long MaxCacheIndex = 255;

    public const string SkillSlotMasterPath = "master/skill_slot_master_v1.tsv";
    public const string CharacterSkillSlotTagMasterPath = "master/character_skill_slot_tag_master_v1.tsv";
    public const string SkillMasterPath = "master/skill_master_v1.tsv";
    public const string SkillTagMasterPath = "master/skill_tag_master_v1.tsv";
    public const string SkillEffectMasterPath = "master/skill_effect_master_v1.tsv";

    /// <summary>스킬 슬롯 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<SkillSlotEntry>? SkillSlots { get; init; }

    /// <summary>캐릭터 스킬 슬롯 태그 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CharacterSkillSlotTag>? CharacterSkillSlotTags { get; init; }

    /// <summary>스킬 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<SkillEntry>? Skills { get; init; }

    /// <summary>스킬 태그 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<SkillTag>? SkillTags { get; init; }

    /// <summary>스킬 이펙트 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<SkillEffect>? SkillEffects { get; init; }

    static List<SkillSlotEntry>? LoadSkillSlots(ZipArchive zip) =>
        LoadCodeNames(zip, SkillSlotMasterPath, "code", "name", "스킬 슬롯", (code, name) => new SkillSlotEntry(code, name));

    static List<SkillEntry>? LoadSkills(ZipArchive zip) =>
        LoadCodeNames(zip, SkillMasterPath, "code", "name", "스킬", (code, name) => new SkillEntry(code, name));

    /// <summary>캐릭터 스킬 슬롯 태그 마스터: 캐릭터는 캐릭터 인벤토리에, 스킬 슬롯은 스킬 슬롯 마스터에, 태그는 태그 마스터에 있어야 하고 네 열이 키라 같은 키를 두 번 쓸 수 없다.</summary>
    static List<CharacterSkillSlotTag>? LoadCharacterSkillSlotTags(
        ZipArchive zip, List<CharacterEntry>? characters, List<SkillSlotEntry>? skillSlots, List<TagEntry>? tags)
    {
        const string file = CharacterSkillSlotTagMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (characters is null) throw new PackFormatException($"{file}가 있으려면 {CharacterInventoryMasterPath}도 필요합니다.");
        if (skillSlots is null) throw new PackFormatException($"{file}가 있으려면 {SkillSlotMasterPath}도 필요합니다.");
        if (tags is null) throw new PackFormatException($"{file}가 있으려면 {TagMasterPath}도 필요합니다.");

        var characterCodes = characters.Select(c => c.Code).ToHashSet();
        var slotCodes = skillSlots.Select(s => s.Code).ToHashSet();
        var tagCodes = tags.Select(t => t.Code).ToHashSet();
        var rows = new List<CharacterSkillSlotTag>();
        var seen = new HashSet<(long, long, long, long)>();
        foreach (var r in Body(file, text, "character_code", "skill_slot_code", "group_code", "tag_code"))
        {
            var character = ParseLong(file, "캐릭터 코드", r[0]);
            var slot = ParseLong(file, $"캐릭터 '{character}'의 스킬 슬롯 코드", r[1]);
            var group = ParseLong(file, $"캐릭터 '{character}' 스킬 슬롯 '{slot}'의 그룹 코드", r[2]);
            var tag = ParseLong(file, $"캐릭터 '{character}' 스킬 슬롯 '{slot}'의 태그 코드", r[3]);
            var who = $"캐릭터 '{character}' 스킬 슬롯 '{slot}'의";
            if (!characterCodes.Contains(character))
                throw new PackFormatException($"{file}: 캐릭터 '{character}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: {who} 스킬 슬롯 '{slot}'가 스킬 슬롯 마스터에 없습니다.");
            if (!tagCodes.Contains(tag))
                throw new PackFormatException($"{file}: {who} 태그 '{tag}'가 태그 마스터에 없습니다.");
            if (!seen.Add((character, slot, group, tag)))
                throw new PackFormatException($"{file}: 같은 키(캐릭터 {character}, 스킬 슬롯 {slot}, 그룹 {group}, 태그 {tag})가 두 번 있습니다.");
            rows.Add(new CharacterSkillSlotTag(character, slot, group, tag));
        }
        return rows;
    }

    /// <summary>스킬 태그 마스터: 스킬은 스킬 마스터에, 태그는 태그 마스터에 있어야 하고 같은 (스킬, 태그)는 한 번만 쓸 수 있다.</summary>
    static List<SkillTag>? LoadSkillTags(ZipArchive zip, List<SkillEntry>? skills, List<TagEntry>? tags)
    {
        const string file = SkillTagMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (skills is null) throw new PackFormatException($"{file}가 있으려면 {SkillMasterPath}도 필요합니다.");
        if (tags is null) throw new PackFormatException($"{file}가 있으려면 {TagMasterPath}도 필요합니다.");

        var skillCodes = skills.Select(s => s.Code).ToHashSet();
        var tagCodes = tags.Select(t => t.Code).ToHashSet();
        var rows = new List<SkillTag>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "skill_code", "tag_code"))
        {
            var skill = ParseLong(file, "스킬 코드", r[0]);
            var tag = ParseLong(file, $"스킬 '{skill}'의 태그 코드", r[1]);
            if (!skillCodes.Contains(skill))
                throw new PackFormatException($"{file}: 스킬 코드 '{skill}'가 스킬 마스터에 없습니다.");
            if (!tagCodes.Contains(tag))
                throw new PackFormatException($"{file}: 스킬 '{skill}'의 태그 코드 '{tag}'가 태그 마스터에 없습니다.");
            if (!seen.Add((skill, tag)))
                throw new PackFormatException($"{file}: 스킬 '{skill}'의 태그 '{tag}'가 두 번 적혀 있습니다.");
            rows.Add(new SkillTag(skill, tag));
        }
        return rows;
    }

    /// <summary>스킬 이펙트 마스터: 스킬은 스킬 마스터에 있어야 하고 키 (스킬, 실행 번호)를 두 번 쓸 수 없다. 종류는 이름으로 적고 값 셋은 비우면 0이다.</summary>
    static List<SkillEffect>? LoadSkillEffects(ZipArchive zip, List<SkillEntry>? skills, List<StatusEntry>? statuses)
    {
        const string file = SkillEffectMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (skills is null) throw new PackFormatException($"{file}가 있으려면 {SkillMasterPath}도 필요합니다.");

        var skillCodes = skills.Select(s => s.Code).ToHashSet();
        var rows = new List<SkillEffect>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "skill_code", "execute_number", "effect_type", "value_1", "value_2", "value_3"))
        {
            var skill = ParseLong(file, "스킬 코드", r[0]);
            var who = $"스킬 '{skill}'의";
            var number = ParseLong(file, $"{who} 실행 번호", r[1]);
            var where = $"{who} 실행 {number}의";
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<EffectType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<EffectType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 이펙트 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<EffectType>())})");
            var value1 = r[3].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_1", r[3]);
            var value2 = r[4].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_2", r[4]);
            var value3 = r[5].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_3", r[5]);

            if (!skillCodes.Contains(skill))
                throw new PackFormatException($"{file}: 스킬 코드 '{skill}'가 스킬 마스터에 없습니다.");
            if (!seen.Add((skill, number)))
                throw new PackFormatException($"{file}: 스킬 '{skill}'의 실행 번호 '{number}'가 두 번 적혀 있습니다.");
            if (type == EffectType.SetCacheValue)
            {
                if (value1 < 0 || value1 > MaxCacheIndex)
                    throw new PackFormatException($"{file}: {where} value_1(cache_index) '{value1}'는 0~{MaxCacheIndex}이어야 합니다.");
                if (value3 < 0 || value3 > 28)
                    throw new PackFormatException($"{file}: {where} value_3(소수 자릿수) '{value3}'는 0~28이어야 합니다.");
            }
            else if (type == EffectType.Jump || type == EffectType.Finish)
            {
                // 점프 대상은 모든 행을 읽은 뒤에 확인한다.
            }
            else if (SkillJump.Of(type) is not null)
            {
                foreach (var (index, column) in new[] { (value1, "value_1"), (value2, "value_2") })
                    if (index < 0 || index > MaxCacheIndex)
                        throw new PackFormatException($"{file}: {where} {column}(cache_index) '{index}'는 0~{MaxCacheIndex}이어야 합니다.");
            }
            else if (type is >= EffectType.CacheAdd and <= EffectType.CacheDivide)
            {
                foreach (var (index, column) in new[] { (value1, "value_1"), (value2, "value_2"), (value3, "value_3") })
                    if (index < 0 || index > MaxCacheIndex)
                        throw new PackFormatException($"{file}: {where} {column}(cache_index) '{index}'는 0~{MaxCacheIndex}이어야 합니다.");
            }
            else if (type != EffectType.None)
            {
                if (statuses is null) throw new PackFormatException($"{file}: {where} {type}에는 {StatusMasterPath}가 필요합니다.");
                if (statuses.All(st => st.Code != value1))
                    throw new PackFormatException($"{file}: {where} value_1(status_code) '{value1}'가 스테이터스 마스터에 없습니다.");
                if (value2 < 0 || value2 > MaxCacheIndex)
                    throw new PackFormatException($"{file}: {where} value_2(cache_index) '{value2}'는 0~{MaxCacheIndex}이어야 합니다.");
            }
            rows.Add(new SkillEffect(skill, number, type, value1, value2, value3));
        }

        // 점프는 같은 스킬에 있는, 자신보다 큰 execute_number로만 갈 수 있다.
        var numbers = rows.GroupBy(e => e.SkillCode).ToDictionary(g => g.Key, g => g.Select(e => e.ExecuteNumber).ToHashSet());
        foreach (var e in rows)
            if (SkillJump.TargetOf(e) is { } target)
            {
                var where = $"스킬 '{e.SkillCode}'의 실행 {e.ExecuteNumber}의";
                if (target <= e.ExecuteNumber)
                    throw new PackFormatException($"{file}: {where} 점프 대상 '{target}'는 자신의 실행 번호보다 커야 합니다.");
                if (!numbers[e.SkillCode].Contains(target))
                    throw new PackFormatException($"{file}: {where} 점프 대상 '{target}'가 같은 스킬에 없습니다.");
            }
        return rows;
    }

    // ---- 조회 ----

    public SkillSlotEntry? FindSkillSlot(long code) =>
        Masters.SkillSlotRowTable.TryFindByCode(code, out var r) ? new SkillSlotEntry(r.Code, r.Name) : null;

    public SkillEntry? FindSkill(long code) =>
        Masters.SkillRowTable.TryFindByCode(code, out var r) ? new SkillEntry(r.Code, r.Name) : null;

    /// <summary>캐릭터의 스킬 슬롯 태그 전부(스킬 슬롯, 그룹, 태그 코드 순).</summary>
    public IReadOnlyList<CharacterSkillSlotTag> SkillSlotTagsOf(long characterCode) =>
        Masters.CharacterSkillSlotTagRowTable.FindByCharacterCode(characterCode)
            .OrderBy(r => r.SkillSlotCode).ThenBy(r => r.GroupCode).ThenBy(r => r.TagCode)
            .Select(r => new CharacterSkillSlotTag(r.CharacterCode, r.SkillSlotCode, r.GroupCode, r.TagCode)).ToList();

    /// <summary>캐릭터의 스킬 슬롯 하나의 한 그룹에 걸린 태그(태그 코드 순).</summary>
    public IReadOnlyList<CharacterSkillSlotTag> SkillSlotTagsOf(long characterCode, long skillSlotCode, long groupCode) =>
        Masters.CharacterSkillSlotTagRowTable.FindByCharacterCodeAndSkillSlotCodeAndGroupCode((characterCode, skillSlotCode, groupCode))
            .OrderBy(r => r.TagCode)
            .Select(r => new CharacterSkillSlotTag(r.CharacterCode, r.SkillSlotCode, r.GroupCode, r.TagCode)).ToList();

    /// <summary>스킬에 붙은 태그 코드(코드 순).</summary>
    public IReadOnlyList<long> TagsOfSkill(long skillCode) =>
        Masters.SkillTagRowTable.FindBySkillCode(skillCode).OrderBy(r => r.TagCode).Select(r => r.TagCode).ToList();

    /// <summary>스킬의 이펙트(실행 번호 순).</summary>
    public IReadOnlyList<SkillEffect> SkillEffectsOf(long skillCode) =>
        Masters.SkillEffectRowTable.FindBySkillCode(skillCode)
            .OrderBy(r => r.ExecuteNumber)
            .Select(r => new SkillEffect(r.SkillCode, r.ExecuteNumber, r.EffectType, r.Value1, r.Value2, r.Value3)).ToList();
}
