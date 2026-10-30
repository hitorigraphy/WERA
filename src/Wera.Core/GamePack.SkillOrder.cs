using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 스킬 순서 이펙트가 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다. 순서 서킷(<see cref="SkillOrderCircuit"/>)이 실행하고,
/// 유닛의 스테이터스는 읽기만 하며 마지막에 Result(행동 순서를 정하는 OrderPoint)를 계산하는 것이 목적이다.
/// 인자 규칙은 실행 이펙트(<see cref="EffectType"/>)의 같은 이름과 같다.
/// 숫자 값은 정해진 번호다: None 0, CasterValueToCache 1, TargetValueToCache 2, SetCacheValue 3, CacheAdd 4, CacheSubtract 5, CacheMultiply 6, CacheDivide 7,
/// Jump 8, Finish 9, JumpCacheEqual 10, JumpCacheNotEqual 11, JumpCacheGreaterThan 12, JumpCacheGreaterOrEqual 13, JumpCacheLessThan 14, JumpCacheLessOrEqual 15, CacheToResult 16.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderEffectType>))]
public enum OrderEffectType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
    /// <summary>시전자의 스테이터스 값을 캐시에 쓴다. value_1이 status_code, value_2가 cache_index다(<see cref="EffectType.CasterValueToCache"/>와 같다).</summary>
    CasterValueToCache = 1,
    /// <summary>타겟의 스테이터스 값을 캐시에 쓴다. 인자는 <see cref="CasterValueToCache"/>와 같다.</summary>
    TargetValueToCache = 2,
    /// <summary>캐시에 상수를 쓴다: cache[value_1] = value_2 / 10^value_3(<see cref="EffectType.SetCacheValue"/>와 같다).</summary>
    SetCacheValue = 3,
    /// <summary>cache[value_1] = cache[value_2] + cache[value_3]. 세 값 모두 cache_index다.</summary>
    CacheAdd = 4,
    /// <summary>cache[value_1] = cache[value_2] - cache[value_3].</summary>
    CacheSubtract = 5,
    /// <summary>cache[value_1] = cache[value_2] * cache[value_3].</summary>
    CacheMultiply = 6,
    /// <summary>cache[value_1] = cache[value_2] / cache[value_3]. 0으로 나누면 아무 일도 하지 않는다.</summary>
    CacheDivide = 7,
    /// <summary>조건 없이 점프한다. value_1이 점프할 execute_number다(<see cref="EffectType.Jump"/>와 같다).</summary>
    Jump = 8,
    /// <summary>이펙트 실행을 끝낸다.</summary>
    Finish = 9,
    /// <summary>cache[value_1] == cache[value_2]이면 value_3의 execute_number로 점프한다(<see cref="EffectType.JumpCacheEqual"/>과 같다).</summary>
    JumpCacheEqual = 10,
    JumpCacheNotEqual = 11,
    JumpCacheGreaterThan = 12,
    JumpCacheGreaterOrEqual = 13,
    JumpCacheLessThan = 14,
    JumpCacheLessOrEqual = 15,
    /// <summary>Result를 캐시의 값으로 바꾼다: Result = cache[value_1]. value_1은 cache_index이고 그 칸이 캐시에 없으면 아무 일도 하지 않는다.</summary>
    CacheToResult = 16,
}

/// <summary>스킬 순서 이펙트 마스터의 한 행: 스킬의 순서 서킷이 execute_number 순서대로 실행하는 이펙트 하나(종류와 값 셋).</summary>
public sealed record SkillOrderEffect(long SkillCode, long ExecuteNumber, OrderEffectType EffectType, long Value1, long Value2, long Value3);

// skill_order_effect_master(skill_code, execute_number, effect_type, value_1, value_2, value_3; 앞의 두 열이 키). 선택이다.
// 스킬을 쓰는 순서를 정하는 OrderPoint를 계산한다: 순서 서킷이 이 이펙트들을 돌리고 마지막 Result가 OrderPoint다.
public sealed partial class GamePack
{
    public const string SkillOrderEffectMasterPath = "master/skill_order_effect_master_v1.tsv";

    /// <summary>스킬 순서 이펙트 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<SkillOrderEffect>? SkillOrderEffects { get; init; }

    /// <summary>
    /// 스킬 순서 이펙트 마스터: 스킬은 스킬 마스터에 있어야 하고 키 (스킬, 실행 번호)를 두 번 쓸 수 없다. 종류는 이름으로 적고 값 셋은 비우면 0이다.
    /// 종류별 인자 검증: 스테이터스 값을 캐시로 옮기는 종류는 value_1이 스테이터스 마스터의 코드, value_2가 cache_index(0~<see cref="MaxCacheIndex"/>),
    /// SetCacheValue는 value_1이 cache_index, value_3이 0~28, 캐시 계산은 세 값 모두 cache_index, JumpCache*는 value_1과 value_2가 cache_index,
    /// CacheToResult는 value_1이 cache_index다. 점프 대상은 같은 스킬의, 자신보다 큰 execute_number여야 한다.
    /// </summary>
    static List<SkillOrderEffect>? LoadSkillOrderEffects(ZipArchive zip, List<SkillEntry>? skills, List<StatusEntry>? statuses)
    {
        const string file = SkillOrderEffectMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (skills is null) throw new PackFormatException($"{file}가 있으려면 {SkillMasterPath}도 필요합니다.");

        var skillCodes = skills.Select(s => s.Code).ToHashSet();
        var rows = new List<SkillOrderEffect>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "skill_code", "execute_number", "effect_type", "value_1", "value_2", "value_3"))
        {
            var skill = ParseLong(file, "스킬 코드", r[0]);
            var who = $"스킬 '{skill}'의";
            var number = ParseLong(file, $"{who} 실행 번호", r[1]);
            var where = $"{who} 실행 {number}의";
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<OrderEffectType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<OrderEffectType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 이펙트 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<OrderEffectType>())})");
            var value1 = r[3].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_1", r[3]);
            var value2 = r[4].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_2", r[4]);
            var value3 = r[5].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_3", r[5]);

            if (!skillCodes.Contains(skill))
                throw new PackFormatException($"{file}: 스킬 코드 '{skill}'가 스킬 마스터에 없습니다.");
            if (!seen.Add((skill, number)))
                throw new PackFormatException($"{file}: 스킬 '{skill}'의 실행 번호 '{number}'가 두 번 적혀 있습니다.");

            void CheckCacheIndex(long index, string column)
            {
                if (index < 0 || index > MaxCacheIndex)
                    throw new PackFormatException($"{file}: {where} {column}(cache_index) '{index}'는 0~{MaxCacheIndex}이어야 합니다.");
            }

            switch (type)
            {
                case OrderEffectType.CasterValueToCache:
                case OrderEffectType.TargetValueToCache:
                    if (statuses is null) throw new PackFormatException($"{file}: {where} {type}에는 {StatusMasterPath}가 필요합니다.");
                    if (statuses.All(st => st.Code != value1))
                        throw new PackFormatException($"{file}: {where} value_1(status_code) '{value1}'가 스테이터스 마스터에 없습니다.");
                    CheckCacheIndex(value2, "value_2");
                    break;
                case OrderEffectType.SetCacheValue:
                    CheckCacheIndex(value1, "value_1");
                    if (value3 < 0 || value3 > 28)
                        throw new PackFormatException($"{file}: {where} value_3(소수 자릿수) '{value3}'는 0~28이어야 합니다.");
                    break;
                case OrderEffectType.CacheAdd:
                case OrderEffectType.CacheSubtract:
                case OrderEffectType.CacheMultiply:
                case OrderEffectType.CacheDivide:
                    CheckCacheIndex(value1, "value_1");
                    CheckCacheIndex(value2, "value_2");
                    CheckCacheIndex(value3, "value_3");
                    break;
                case OrderEffectType.CacheToResult:
                    CheckCacheIndex(value1, "value_1");
                    break;
                default:
                    if (SkillJump.Of(type) is not null)
                    {
                        CheckCacheIndex(value1, "value_1");
                        CheckCacheIndex(value2, "value_2");
                    }
                    break;
            }
            rows.Add(new SkillOrderEffect(skill, number, type, value1, value2, value3));
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

    /// <summary>스킬의 순서 이펙트(실행 번호 순).</summary>
    public IReadOnlyList<SkillOrderEffect> SkillOrderEffectsOf(long skillCode) =>
        Masters.SkillOrderEffectRowTable.FindBySkillCode(skillCode)
            .OrderBy(r => r.ExecuteNumber)
            .Select(r => new SkillOrderEffect(r.SkillCode, r.ExecuteNumber, r.EffectType, r.Value1, r.Value2, r.Value3)).ToList();
}
