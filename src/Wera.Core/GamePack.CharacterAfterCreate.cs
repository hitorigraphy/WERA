using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 캐릭터 생성 후 이펙트가 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다. <see cref="CharacterAfterCreateCircuit"/>이 실행한다(그 캐릭터가 시전자이자 메인 타겟이고 처음부터 포커스되어 있다).
/// 구성은 스킬 실행 이펙트(<see cref="EffectType"/>)와 같다: 번호와 이름, 인자가 같다.
/// 숫자 값은 정해진 번호다: None 0, FocusCasterCharacter 1, FocusMainTargetCharacter 2, FocusSubTargetCharacter 3, FocusCasterEquipment 4, FocusMainTargetEquipment 5, FocusSubTargetEquipment 6, FocusCasterSkill 7, FocusMainTargetSkill 8, FocusSubTargetSkill 9, CacheToFocusedCharacterModValue 10, CacheToFocusedCharacterSetValue 11, FocusedCharacterValueToCache 12, CacheToFocusedItemModValue 13, CacheToFocusedItemSetValue 14, FocusedItemValueToCache 15, CacheToFocusedSkillModValue 16, CacheToFocusedSkillSetValue 17, FocusedSkillValueToCache 18, CacheAdd 19, CacheSubtract 20, CacheMultiply 21, CacheDivide 22, SetCacheValue 23, Jump 24, Finish 25, JumpCacheEqual 26, JumpCacheNotEqual 27, JumpCacheGreaterThan 28, JumpCacheGreaterOrEqual 29, JumpCacheLessThan 30, JumpCacheLessOrEqual 31.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CharacterAfterCreateEffectType>))]
public enum CharacterAfterCreateEffectType
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

/// <summary>캐릭터 생성 후 이펙트 마스터의 한 행: 캐릭터가 만들어지면 서킷이 character_code가 같은 행을 execute_number 순서대로 실행하는 이펙트 하나(종류와 값 셋).</summary>
public sealed record CharacterAfterCreateEffect(long CharacterCode, long ExecuteNumber, CharacterAfterCreateEffectType EffectType, long Value1, long Value2, long Value3);

// character_after_create_effect_master(character_code, execute_number, effect_type, value_1, value_2, value_3; 앞의 두 열이 키). 선택이다.
// 캐릭터가 만들어지면(새로 얻을 때) character_code가 같은 이펙트들이 실행되어, 게임팩의 정의에 따라 그 캐릭터의 초기 데이터를 조작한다.
public sealed partial class GamePack
{
    public const string CharacterAfterCreateEffectMasterPath = "master/character_after_create_effect_master_v1.tsv";

    /// <summary>캐릭터 생성 후 이펙트 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CharacterAfterCreateEffect>? CharacterAfterCreateEffects { get; init; }

    /// <summary>
    /// 캐릭터 생성 후 이펙트 마스터: character_code는 캐릭터 인벤토리 마스터에 있어야 하고 키 (캐릭터, 실행 번호)를 두 번 쓸 수 없다. 종류는 이름으로 적고 값 셋은 비우면 0이다.
    /// 종류별 인자 검증은 <see cref="EffectArgs.Check"/>를 따른다. 점프 대상은 같은 캐릭터의, 자신보다 큰 execute_number여야 한다.
    /// </summary>
    static List<CharacterAfterCreateEffect>? LoadCharacterAfterCreateEffects(ZipArchive zip, IEnumerable<long>? ownerCodes, EffectCheck check)
    {
        const string file = CharacterAfterCreateEffectMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (ownerCodes is null) throw new PackFormatException($"{file}가 있으려면 {CharacterInventoryMasterPath}도 필요합니다.");

        var codes = ownerCodes.ToHashSet();
        var rows = new List<CharacterAfterCreateEffect>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "character_code", "execute_number", "effect_type", "value_1", "value_2", "value_3"))
        {
            var owner = ParseLong(file, "캐릭터 코드", r[0]);
            var who = $"캐릭터 '{owner}'의";
            var number = ParseLong(file, $"{who} 실행 번호", r[1]);
            var where = $"{who} 실행 {number}의";
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<CharacterAfterCreateEffectType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<CharacterAfterCreateEffectType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 이펙트 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<CharacterAfterCreateEffectType>())})");
            var value1 = r[3].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_1", r[3]);
            var value2 = r[4].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_2", r[4]);
            var value3 = r[5].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_3", r[5]);

            if (!codes.Contains(owner))
                throw new PackFormatException($"{file}: 캐릭터 코드 '{owner}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (!seen.Add((owner, number)))
                throw new PackFormatException($"{file}: 캐릭터 '{owner}'의 실행 번호 '{number}'가 두 번 적혀 있습니다.");
            EffectArgs.Check(file, where, (EffectType)(int)type, value1, value2, value3, check);
            rows.Add(new CharacterAfterCreateEffect(owner, number, type, value1, value2, value3));
        }

        // 점프는 같은 캐릭터에 있는, 자신보다 큰 execute_number로만 갈 수 있다.
        var numbers = rows.GroupBy(e => e.CharacterCode).ToDictionary(g => g.Key, g => g.Select(e => e.ExecuteNumber).ToHashSet());
        foreach (var e in rows)
            if (EffectArgs.JumpTargetOf((EffectType)(int)e.EffectType, e.Value1, e.Value3) is { } target)
            {
                var where = $"캐릭터 '{e.CharacterCode}'의 실행 {e.ExecuteNumber}의";
                if (target <= e.ExecuteNumber)
                    throw new PackFormatException($"{file}: {where} 점프 대상 '{target}'는 자신의 실행 번호보다 커야 합니다.");
                if (!numbers[e.CharacterCode].Contains(target))
                    throw new PackFormatException($"{file}: {where} 점프 대상 '{target}'가 같은 캐릭터에 없습니다.");
            }
        return rows;
    }

    /// <summary>캐릭터의 캐릭터 생성 후 이펙트 마스터(실행 번호 순). 없으면 빈 목록.</summary>
    public IReadOnlyList<CharacterAfterCreateEffect> CharacterAfterCreateEffectsOf(long code) =>
        Masters.CharacterAfterCreateEffectRowTable.FindByCharacterCode(code)
            .OrderBy(r => r.ExecuteNumber)
            .Select(r => new CharacterAfterCreateEffect(r.CharacterCode, r.ExecuteNumber, r.EffectType, r.Value1, r.Value2, r.Value3)).ToList();
}
