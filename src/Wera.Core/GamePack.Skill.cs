using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>스킬 슬롯 마스터의 한 행.</summary>
public sealed record SkillSlotEntry(long Code, string Name);

/// <summary>
/// 캐릭터 스킬 슬롯 태그 마스터의 한 행. 캐릭터(CharacterCode)의 스킬 슬롯(SkillSlotCode)에 스킬을 넣을 수 있는 조건의 일부:
/// 같은 (캐릭터, 슬롯)에서 같은 GroupNumber인 행들이 한 그룹이고, 그룹 안은 모두 만족해야 한다(AND).
/// IsPositive가 참이면 스킬이 그 태그를 가져야 하고, 거짓이면 가지면 안 된다(장비 슬롯 태그와 같다).
/// 복합키는 (CharacterCode, SkillSlotCode, GroupNumber, TagCode) 네 열이고 IsPositive는 값이다.
/// </summary>
public sealed record CharacterSkillSlotTag(long CharacterCode, long SkillSlotCode, int GroupNumber, long TagCode, bool IsPositive);

/// <summary>"이 스킬이 이 슬롯의 태그 조건에 부합하는가"의 판정(<see cref="EquipmentRules"/>와 같은 규칙).</summary>
public static class SkillSlotRules
{
    /// <summary>
    /// 한 (캐릭터, 스킬 슬롯)의 태그 규칙에 태그가 부합하는지. 그룹 안은 AND(양성 태그는 있어야 하고 음성 태그는 없어야 함),
    /// 그룹끼리는 OR이다. 규칙이 하나도 없으면 아무것도 넣을 수 없다.
    /// </summary>
    public static bool Matches(IEnumerable<CharacterSkillSlotTag> rules, IReadOnlySet<long> tags) =>
        rules.GroupBy(r => r.GroupNumber)
            .Any(group => group.All(r => tags.Contains(r.TagCode) == r.IsPositive));
}

/// <summary>스킬 마스터의 한 행.</summary>
public sealed record SkillEntry(long Code, string Name, SkillMainTargetType MainTargetType = SkillMainTargetType.Enemy, SkillSubTargetType SubTargetType = SkillSubTargetType.None);

/// <summary>스킬의 메인 타겟 대상(skill_master의 main_target_type): 시전자 기준으로 메인 타겟이 될 수 있는 유닛. 기절(쓰러진) 유닛을 대상으로 하는 종류(<see cref="AllyFallen"/>, <see cref="EnemyFallen"/>)를 뺀 나머지는 쓰러진 유닛을 제외한다.</summary>
public enum SkillMainTargetType
{
    /// <summary>시전자의 상대편 유닛.</summary>
    Enemy,
    /// <summary>시전자와 같은 편 유닛(시전자 자신 포함).</summary>
    Ally,
    /// <summary>시전자 자신뿐.</summary>
    Self,
    /// <summary>편을 가리지 않고 모든 유닛.</summary>
    Any,
    /// <summary>시전자와 같은 편의 기절(쓰러진) 유닛(부활 스킬 등).</summary>
    AllyFallen,
    /// <summary>시전자의 상대편의 기절(쓰러진) 유닛.</summary>
    EnemyFallen,
    /// <summary>메인 타겟이 없다. 전투에서 직접 쓸 수 없는 스킬(행동 후보와 적의 스킬 선택에서 빠짐)이고 패시브 스킬 등으로만 쓴다.</summary>
    None,
}

/// <summary>스킬 태그 마스터의 한 행.</summary>
public sealed record SkillTag(long SkillCode, long TagCode);

/// <summary>
/// 스킬 이펙트가 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다. 이펙트는 스킬 서킷(<see cref="SkillExecuteCircuit"/>)에서 실행 번호 순서로 실행된다.
/// 마스터만으로는 동작을 정할 수 없으므로 종류마다 동작을 코드로 구현하고, 마스터는 어느 종류를 어떤 값(value_1~3)으로 쓸지만 적는다.
/// 서킷은 시전자(Caster), 메인 타겟(MainTarget), 서브 타겟들(SubTarget)이라는 파티 유닛을 가지고, Focus* 종류가 그 유닛의 캐릭터, 장비(유니크 아이템), 스킬을
/// 포커스(FocusedCharacterInstance, FocusedUniqueItemInstance, FocusedSkill)하며, 스테이터스는 포커스한 것과 캐시 사이에서만 주고받는다.
/// 숫자 값은 정해진 번호다: None 0, FocusCasterCharacter 1, FocusMainTargetCharacter 2, FocusSubTargetCharacter 3, FocusCasterEquipment 4, FocusMainTargetEquipment 5, FocusSubTargetEquipment 6, FocusCasterSkill 7, FocusMainTargetSkill 8, FocusSubTargetSkill 9, CacheToFocusedCharacterModValue 10, CacheToFocusedCharacterSetValue 11, FocusedCharacterValueToCache 12, CacheToFocusedItemModValue 13, CacheToFocusedItemSetValue 14, FocusedItemValueToCache 15, CacheToFocusedSkillModValue 16, CacheToFocusedSkillSetValue 17, FocusedSkillValueToCache 18, CacheAdd 19, CacheSubtract 20, CacheMultiply 21, CacheDivide 22, SetCacheValue 23, Jump 24, Finish 25, JumpCacheEqual 26, JumpCacheNotEqual 27, JumpCacheGreaterThan 28, JumpCacheGreaterOrEqual 29, JumpCacheLessThan 30, JumpCacheLessOrEqual 31.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EffectType>))]
public enum EffectType
{
    /// <summary>아무 일도 하지 않는다. 값은 보지 않는다.</summary>
    None = 0,
    /// <summary>시전자의 캐릭터를 포커스한다(FocusedCharacterInstance). 값은 보지 않는다. 시전자가 없거나 그 캐릭터를 찾을 수 없으면 포커스는 비워진다.</summary>
    FocusCasterCharacter = 1,
    /// <summary>메인 타겟의 캐릭터를 포커스한다. 인자와 예외는 <see cref="FocusCasterCharacter"/>와 같다.</summary>
    FocusMainTargetCharacter = 2,
    /// <summary>서브 타겟의 캐릭터를 포커스한다. value_1이 서브 타겟 번호(0부터, SubTarget 목록의 번호)이고, 그 번호의 서브 타겟이 없으면 포커스는 비워진다.</summary>
    FocusSubTargetCharacter = 3,
    /// <summary>시전자가 장비 슬롯에 낀 유니크 아이템을 포커스한다(FocusedUniqueItemInstance). value_1이 장비 슬롯 코드(장비 슬롯 마스터에 있어야 함)이고, 슬롯이 비었거나 낀 것이 유니크 아이템이 아니면(커먼 아이템, 캐릭터) 포커스는 비워진다.</summary>
    FocusCasterEquipment = 4,
    /// <summary>메인 타겟의 장비를 포커스한다. 인자와 예외는 <see cref="FocusCasterEquipment"/>와 같다.</summary>
    FocusMainTargetEquipment = 5,
    /// <summary>서브 타겟의 장비를 포커스한다. value_1이 서브 타겟 번호, value_2가 장비 슬롯 코드이고 예외는 <see cref="FocusCasterEquipment"/>와 같다(그 번호의 서브 타겟이 없어도 포커스는 비워진다).</summary>
    FocusSubTargetEquipment = 6,
    /// <summary>시전자의 캐릭터가 스킬 슬롯에 장착한 스킬을 포커스한다(FocusedSkill). value_1이 스킬 슬롯 코드(스킬 슬롯 마스터에 있어야 함)이고, 슬롯이 비었으면 포커스는 비워진다.</summary>
    FocusCasterSkill = 7,
    /// <summary>메인 타겟의 캐릭터가 장착한 스킬을 포커스한다. 인자와 예외는 <see cref="FocusCasterSkill"/>과 같다.</summary>
    FocusMainTargetSkill = 8,
    /// <summary>서브 타겟의 캐릭터가 장착한 스킬을 포커스한다. value_1이 서브 타겟 번호, value_2가 스킬 슬롯 코드이고 예외는 <see cref="FocusCasterSkill"/>과 같다.</summary>
    FocusSubTargetSkill = 9,
    /// <summary>서킷 캐시의 값을 포커스한 캐릭터의 스테이터스에 더한다(음수면 뺀다). value_1이 status_code(스테이터스 마스터에 있어야 함), value_2가 cache_index(0 이상)이고 value_3은 보지 않는다. 결과는 그 스테이터스의 최솟값~최댓값으로 맞춘다. 포커스가 비었거나 그 스테이터스가 없거나 캐시에 그 번호의 값이 없으면 아무 일도 하지 않는다.</summary>
    CacheToFocusedCharacterModValue = 10,
    /// <summary>서킷 캐시의 값으로 포커스한 캐릭터의 스테이터스를 바꾼다. 인자와 예외는 <see cref="CacheToFocusedCharacterModValue"/>와 같다.</summary>
    CacheToFocusedCharacterSetValue = 11,
    /// <summary>포커스한 캐릭터의 스테이터스 값을 서킷 캐시의 cache_index번에 쓴다(이전 값은 덮어씀). value_1이 status_code, value_2가 cache_index(0~<see cref="GamePack.MaxCacheIndex"/>)이고 value_3은 보지 않는다. 캐시가 그 번호보다 짧으면 모자란 칸을 0으로 채워 늘린다. 포커스가 비었거나 그 스테이터스가 없으면 캐시를 건드리지 않는다.</summary>
    FocusedCharacterValueToCache = 12,
    /// <summary>서킷 캐시의 값을 포커스한 유니크 아이템의 스테이터스에 더한다. 인자와 예외는 <see cref="CacheToFocusedCharacterModValue"/>와 같다(범위는 유니크 아이템 스테이터스 마스터의 최솟값~최댓값).</summary>
    CacheToFocusedItemModValue = 13,
    /// <summary>서킷 캐시의 값으로 포커스한 유니크 아이템의 스테이터스를 바꾼다. 인자와 예외는 <see cref="CacheToFocusedItemModValue"/>와 같다.</summary>
    CacheToFocusedItemSetValue = 14,
    /// <summary>포커스한 유니크 아이템의 스테이터스 값을 서킷 캐시에 쓴다. 인자와 예외는 <see cref="FocusedCharacterValueToCache"/>와 같다.</summary>
    FocusedItemValueToCache = 15,
    /// <summary>서킷 캐시의 값을 포커스한 스킬의 스테이터스에 더한다. 인자와 예외는 <see cref="CacheToFocusedCharacterModValue"/>와 같다(범위는 스킬 스테이터스 마스터의 최솟값~최댓값).</summary>
    CacheToFocusedSkillModValue = 16,
    /// <summary>서킷 캐시의 값으로 포커스한 스킬의 스테이터스를 바꾼다. 인자와 예외는 <see cref="CacheToFocusedSkillModValue"/>와 같다.</summary>
    CacheToFocusedSkillSetValue = 17,
    /// <summary>포커스한 스킬의 스테이터스 값을 서킷 캐시에 쓴다. 인자와 예외는 <see cref="FocusedCharacterValueToCache"/>와 같다.</summary>
    FocusedSkillValueToCache = 18,
    /// <summary>캐시 계산: cache[value_1] = cache[value_2] + cache[value_3]. 세 값 모두 cache_index(0~<see cref="GamePack.MaxCacheIndex"/>)다. 결과를 쓸 번호가 캐시보다 뒤면 모자란 칸을 0으로 채워 늘린다. 계산할 칸(value_2나 value_3)이 캐시에 없거나 결과가 decimal 범위를 넘으면 아무 일도 하지 않는다.</summary>
    CacheAdd = 19,
    /// <summary>cache[value_1] = cache[value_2] - cache[value_3]. 인자와 예외는 <see cref="CacheAdd"/>와 같다.</summary>
    CacheSubtract = 20,
    /// <summary>cache[value_1] = cache[value_2] * cache[value_3]. 인자와 예외는 <see cref="CacheAdd"/>와 같다.</summary>
    CacheMultiply = 21,
    /// <summary>cache[value_1] = cache[value_2] / cache[value_3](나눗셈은 decimal 그대로, 반올림 없음). 인자와 예외는 <see cref="CacheAdd"/>와 같고, value_3의 칸이 0이면 아무 일도 하지 않는다.</summary>
    CacheDivide = 22,
    /// <summary>캐시에 상수를 쓴다: cache[value_1] = value_2 / 10^value_3. value_1이 cache_index(0~<see cref="GamePack.MaxCacheIndex"/>), value_2가 정수 값, value_3이 소수 자릿수(0~28)다(15, 2는 0.15). 캐시가 짧으면 모자란 칸을 0으로 채워 늘린다.</summary>
    SetCacheValue = 23,
    /// <summary>조건 없이 점프한다. value_1이 점프할 execute_number이고, 같은 마스터의 같은 소유자(스킬, 캐릭터, 아이템별 마스터에서는 같은 코드)에 있어야 하며 자신의 execute_number보다 커야 한다(뒤로만 가서 무한 반복이 없다).</summary>
    Jump = 24,
    /// <summary>이펙트 실행을 끝낸다(뒤의 이펙트는 실행하지 않는다).</summary>
    Finish = 25,
    /// <summary>cache[value_1]과 cache[value_2]를 견주어(같다) 참이면 value_3의 execute_number로 점프하고, 거짓이면 다음 이펙트로 간다. value_1과 value_2는 cache_index이고 둘 중 하나가 캐시에 없으면 거짓이다. 점프 대상의 규칙은 <see cref="Jump"/>와 같다.</summary>
    JumpCacheEqual = 26,
    /// <summary>같지 않다. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheNotEqual = 27,
    /// <summary>cache[value_1] &gt; cache[value_2]. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheGreaterThan = 28,
    /// <summary>cache[value_1] &gt;= cache[value_2]. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheGreaterOrEqual = 29,
    /// <summary>cache[value_1] &lt; cache[value_2]. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheLessThan = 30,
    /// <summary>cache[value_1] &lt;= cache[value_2]. 인자와 규칙은 <see cref="JumpCacheEqual"/>과 같다.</summary>
    JumpCacheLessOrEqual = 31,
}

/// <summary>JumpCache* 종류가 쓰는 비교.</summary>
public static class SkillJump
{
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
public sealed record SkillExecuteEffect(long SkillCode, long ExecuteNumber, EffectType EffectType, long Value1, long Value2, long Value3);

// 스킬 마스터: skill_slot_master(code, name; code가 키), character_skill_slot_tag_master(character_code, skill_slot_code, group_number, tag_code, is_positive; 앞의 네 열이 키),
// skill_master(code, name; code가 키), skill_tag_master(skill_code, tag_code; 두 열이 키), skill_execute_effect_master(skill_code, execute_number, effect_type, value_1, value_2, value_3; 앞의 두 열이 키).
// 모두 선택이다.
public sealed partial class GamePack
{
    /// <summary>스킬 이펙트의 cache_index가 가질 수 있는 가장 큰 값(서킷 캐시가 끝없이 늘어나지 않게 한다).</summary>
    public const long MaxCacheIndex = 255;

    public const string SkillSlotMasterPath = "master/skill_slot_master_v1.tsv";
    public const string CharacterSkillSlotTagMasterPath = "master/character_skill_slot_tag_master_v1.tsv";
    public const string SkillMasterPath = "master/skill_master_v1.tsv";
    public const string SkillTagMasterPath = "master/skill_tag_master_v1.tsv";
    public const string SkillExecuteEffectMasterPath = "master/skill_execute_effect_master_v1.tsv";

    /// <summary>스킬 슬롯 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<SkillSlotEntry>? SkillSlots { get; init; }

    /// <summary>캐릭터 스킬 슬롯 태그 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CharacterSkillSlotTag>? CharacterSkillSlotTags { get; init; }

    /// <summary>스킬 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<SkillEntry>? Skills { get; init; }

    /// <summary>스킬 태그 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<SkillTag>? SkillTags { get; init; }

    /// <summary>스킬 이펙트 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<SkillExecuteEffect>? SkillExecuteEffects { get; init; }

    static List<SkillSlotEntry>? LoadSkillSlots(ZipArchive zip) =>
        LoadCodeNames(zip, SkillSlotMasterPath, "code", "name", "스킬 슬롯", (code, name) => new SkillSlotEntry(code, name));

    /// <summary>
    /// 스킬 마스터: code, name과 선택 열 main_target_type(<see cref="SkillMainTargetType"/>: Enemy/Ally/Self/Any/AllyFallen/EnemyFallen/None, 비우거나 열이 없으면 Enemy),
    /// 그 다음의 선택 열 sub_target_type(<see cref="SkillSubTargetType"/> 이름, 비우거나 열이 없으면 None). 이름만 쓰고 대소문자는 무시한다.
    /// </summary>
    static List<SkillEntry>? LoadSkills(ZipArchive zip)
    {
        var text = TryReadEntry(zip, SkillMasterPath);
        if (text is null) return null;
        var rows = Tsv.WithoutMemo(Tsv.Parse(text));
        var withMain = rows.Count > 0 && rows[0].Length > 2 && rows[0][2] == "main_target_type";
        var subAt = withMain ? 3 : 2;
        var withSub = rows.Count > 0 && rows[0].Length > subAt && rows[0][subAt] == "sub_target_type";
        string[] header = ["code", "name", .. withMain ? new[] { "main_target_type" } : [], .. withSub ? new[] { "sub_target_type" } : []];

        static T ParseName<T>(string cell, long code, string column) where T : struct, Enum
        {
            if (cell == "") return default;
            if (!Enum.GetNames<T>().Contains(cell, StringComparer.OrdinalIgnoreCase) || !Enum.TryParse(cell, ignoreCase: true, out T value))
                throw new PackFormatException($"{SkillMasterPath}: 스킬 코드 '{code}'의 {column} '{cell}'을(를) 알 수 없습니다({string.Join(", ", Enum.GetNames<T>())}).");
            return value;
        }

        var skills = new List<SkillEntry>();
        var seen = new HashSet<long>();
        foreach (var r in Body(SkillMasterPath, text, header))
        {
            var code = ParseLong(SkillMasterPath, "스킬 코드", r[0]);
            if (!seen.Add(code))
                throw new PackFormatException($"{SkillMasterPath}: 스킬 코드 '{code}'가 중복됩니다.");
            var main = withMain ? ParseName<SkillMainTargetType>(r[2], code, "main_target_type") : SkillMainTargetType.Enemy;
            var subType = withSub ? ParseName<SkillSubTargetType>(r[subAt], code, "sub_target_type") : SkillSubTargetType.None;
            skills.Add(new SkillEntry(code, r[1], main, subType));
        }
        return skills;
    }

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
        var seen = new HashSet<(long, long, int, long)>();
        foreach (var r in Body(file, text, "character_code", "skill_slot_code", "group_number", "tag_code", "is_positive"))
        {
            var character = ParseLong(file, "캐릭터 코드", r[0]);
            var slot = ParseLong(file, $"캐릭터 '{character}'의 스킬 슬롯 코드", r[1]);
            var who = $"캐릭터 '{character}' 스킬 슬롯 '{slot}'의";
            if (!int.TryParse(r[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var group))
                throw new PackFormatException($"{file}: {who} 그룹넘버 '{r[2]}'이(가) 정수가 아닙니다.");
            var tag = ParseLong(file, $"캐릭터 '{character}' 스킬 슬롯 '{slot}'의 태그 코드", r[3]);
            var positive = ParseBool(file, $"{who} is_positive", r[4]);
            if (!characterCodes.Contains(character))
                throw new PackFormatException($"{file}: 캐릭터 '{character}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: {who} 스킬 슬롯 '{slot}'가 스킬 슬롯 마스터에 없습니다.");
            if (!tagCodes.Contains(tag))
                throw new PackFormatException($"{file}: {who} 태그 '{tag}'가 태그 마스터에 없습니다.");
            if (!seen.Add((character, slot, group, tag)))
                throw new PackFormatException($"{file}: 같은 키(캐릭터 {character}, 스킬 슬롯 {slot}, 그룹 {group}, 태그 {tag})가 두 번 있습니다.");
            rows.Add(new CharacterSkillSlotTag(character, slot, group, tag, positive));
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

    /// <summary>
    /// 스킬 이펙트 마스터: skill_code는 스킬 마스터에 있어야 하고 키 (스킬, 실행 번호)를 두 번 쓸 수 없다. 종류는 이름으로 적고 값 셋은 비우면 0이다.
    /// 종류별 인자 검증은 <see cref="EffectArgs.Check"/>를 따른다. 점프 대상은 같은 스킬의, 자신보다 큰 execute_number여야 한다.
    /// </summary>
    static List<SkillExecuteEffect>? LoadSkillExecuteEffects(ZipArchive zip, List<SkillEntry>? skills, EffectCheck check)
    {
        const string file = SkillExecuteEffectMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (skills is null) throw new PackFormatException($"{file}가 있으려면 {SkillMasterPath}도 필요합니다.");

        var codes = skills.Select(s => s.Code).ToHashSet();
        var rows = new List<SkillExecuteEffect>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "skill_code", "execute_number", "effect_type", "value_1", "value_2", "value_3"))
        {
            var owner = ParseLong(file, "스킬 코드", r[0]);
            var who = $"스킬 '{owner}'의";
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

            if (!codes.Contains(owner))
                throw new PackFormatException($"{file}: 스킬 코드 '{owner}'가 스킬 마스터에 없습니다.");
            if (!seen.Add((owner, number)))
                throw new PackFormatException($"{file}: 스킬 '{owner}'의 실행 번호 '{number}'가 두 번 적혀 있습니다.");
            EffectArgs.Check(file, where, (EffectType)(int)type, value1, value2, value3, check);
            rows.Add(new SkillExecuteEffect(owner, number, type, value1, value2, value3));
        }

        // 점프는 같은 스킬에 있는, 자신보다 큰 execute_number로만 갈 수 있다.
        var numbers = rows.GroupBy(e => e.SkillCode).ToDictionary(g => g.Key, g => g.Select(e => e.ExecuteNumber).ToHashSet());
        foreach (var e in rows)
            if (EffectArgs.JumpTargetOf((EffectType)(int)e.EffectType, e.Value1, e.Value3) is { } target)
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
        Masters.SkillRowTable.TryFindByCode(code, out var r) ? new SkillEntry(r.Code, r.Name, (SkillMainTargetType)r.MainTargetType, (SkillSubTargetType)r.SubTargetType) : null;

    /// <summary>캐릭터가 가진 스킬 슬롯들: 스킬 슬롯 마스터의 모든 슬롯(슬롯 코드 순). 태그 마스터는 슬롯의 유무가 아니라 무엇을 넣을 수 있는지만 정한다.</summary>
    public IReadOnlyList<SkillSlotEntry> SkillSlotsOf(long characterCode) =>
        (SkillSlots ?? []).OrderBy(s => s.Code).ToList();

    /// <summary>캐릭터의 스킬 슬롯 태그 전부(스킬 슬롯, 그룹, 태그 코드 순).</summary>
    public IReadOnlyList<CharacterSkillSlotTag> SkillSlotTagsOf(long characterCode) =>
        Masters.CharacterSkillSlotTagRowTable.FindByCharacterCode(characterCode)
            .OrderBy(r => r.SkillSlotCode).ThenBy(r => r.GroupNumber).ThenBy(r => r.TagCode)
            .Select(r => new CharacterSkillSlotTag(r.CharacterCode, r.SkillSlotCode, r.GroupNumber, r.TagCode, r.IsPositive)).ToList();

    /// <summary>캐릭터의 스킬 슬롯 하나의 한 그룹에 걸린 태그(태그 코드 순).</summary>
    public IReadOnlyList<CharacterSkillSlotTag> SkillSlotTagsOf(long characterCode, long skillSlotCode, int groupNumber) =>
        Masters.CharacterSkillSlotTagRowTable.FindByCharacterCodeAndSkillSlotCodeAndGroupNumber((characterCode, skillSlotCode, groupNumber))
            .OrderBy(r => r.TagCode)
            .Select(r => new CharacterSkillSlotTag(r.CharacterCode, r.SkillSlotCode, r.GroupNumber, r.TagCode, r.IsPositive)).ToList();

    /// <summary>스킬에 붙은 태그 코드(코드 순).</summary>
    public IReadOnlyList<long> TagsOfSkill(long skillCode) =>
        Masters.SkillTagRowTable.FindBySkillCode(skillCode).OrderBy(r => r.TagCode).Select(r => r.TagCode).ToList();

    /// <summary>스킬의 이펙트(실행 번호 순).</summary>
    public IReadOnlyList<SkillExecuteEffect> SkillExecuteEffectsOf(long skillCode) =>
        Masters.SkillExecuteEffectRowTable.FindBySkillCode(skillCode)
            .OrderBy(r => r.ExecuteNumber)
            .Select(r => new SkillExecuteEffect(r.SkillCode, r.ExecuteNumber, r.EffectType, r.Value1, r.Value2, r.Value3)).ToList();

    /// <summary>
    /// 합친 태그(캐릭터의 태그 + 스킬의 태그)가 이 캐릭터의 이 스킬 슬롯 규칙에 부합하는가. 규칙이 하나도 없는 슬롯에는 아무것도 넣을 수 없다.
    /// </summary>
    public bool SkillFits(long characterCode, long skillSlotCode, IReadOnlySet<long> tags)
    {
        var rules = SkillSlotTagsOf(characterCode).Where(r => r.SkillSlotCode == skillSlotCode).ToList();
        return rules.Count > 0 && SkillSlotRules.Matches(rules, tags);
    }

    /// <summary>마스터 태그 기준으로 이 스킬을 이 캐릭터의 이 스킬 슬롯에 넣을 수 있는가(캐릭터의 마스터 태그와 스킬의 태그를 합쳐서 본다).</summary>
    public bool CanSlotSkill(long characterCode, long skillSlotCode, long skillCode)
    {
        if (FindSkill(skillCode) is null) return false;
        var tags = TagsOfSkill(skillCode).ToHashSet();
        tags.UnionWith(MasterTagsOfCharacter(characterCode));
        return SkillFits(characterCode, skillSlotCode, tags);
    }

    /// <summary>이 캐릭터의 이 스킬 슬롯에 넣을 수 있는 스킬들(스킬 마스터 순서).</summary>
    public IReadOnlyList<SkillEntry> SlottableSkills(long characterCode, long skillSlotCode) =>
        (Skills ?? []).Where(s => CanSlotSkill(characterCode, skillSlotCode, s.Code)).ToList();
}
