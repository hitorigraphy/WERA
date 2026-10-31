using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 전투 패배 후 이펙트가 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다. <see cref="AfterBattleLoseCircuit"/>이 실행한다.
/// 구성은 스킬 실행 이펙트(<see cref="EffectType"/>)와 같다: 번호와 이름, 인자가 같다다만 서킷에 유닛이 하나뿐이라 이름에 Caster나 Target이 들어간 종류는 모두 그 유닛을 가리킨다(CacheToTargetModValue와 CacheToCasterModValue는 같은 일을 한다).
/// 숫자 값은 정해진 번호다: None 0, CacheToTargetModValue 1, CacheToTargetSetValue 2, CacheToCasterModValue 3, CacheToCasterSetValue 4, CasterValueToCache 5, TargetValueToCache 6, CacheAdd 7, CacheSubtract 8, CacheMultiply 9, CacheDivide 10, SetCacheValue 11, Jump 12, Finish 13, JumpCacheEqual 14, JumpCacheNotEqual 15, JumpCacheGreaterThan 16, JumpCacheGreaterOrEqual 17, JumpCacheLessThan 18, JumpCacheLessOrEqual 19.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AfterBattleLoseEffectType>))]
public enum AfterBattleLoseEffectType
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
    /// 조건 없이 점프한다. value_1이 점프할 execute_number이고, 같은 마스터에 있어야 하며 자신의 execute_number보다 커야 한다(뒤로만 가서 무한 반복이 없다).
    /// </summary>
    Jump = 12,
    /// <summary>이펙트 실행을 끝낸다(뒤의 이펙트는 실행하지 않는다).</summary>
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

/// <summary>전투 패배 후 이펙트의 JumpCache* 종류가 쓰는 규칙(<see cref="SkillJump"/>와 같다).</summary>
public static class AfterBattleLoseJump
{
    const string Prefix = "JumpCache";

    public static FlagComparison? Of(AfterBattleLoseEffectType type) =>
        type.ToString().StartsWith(Prefix, StringComparison.Ordinal) && Enum.TryParse<FlagComparison>(type.ToString()[Prefix.Length..], out var c) ? c : null;

    /// <summary>점프할 execute_number가 어느 값(value_1, value_3)에 있는가. 점프하는 종류가 아니면 null.</summary>
    public static long? TargetOf(AfterBattleLoseEffect effect) =>
        effect.EffectType == AfterBattleLoseEffectType.Jump ? effect.Value1 : Of(effect.EffectType) is not null ? effect.Value3 : null;
}

/// <summary>전투 패배 후 이펙트 마스터의 한 행: 전투에서 지면(아군이 모두 쓰러지면) 서킷이 execute_number 순서대로 실행하는 이펙트 하나(종류와 값 셋).</summary>
public sealed record AfterBattleLoseEffect(long ExecuteNumber, AfterBattleLoseEffectType EffectType, long Value1, long Value2, long Value3);

// after_battle_lose_effect_master(execute_number, effect_type, value_1, value_2, value_3; 첫 열이 키). 선택이다.
// 전투에서 지면(아군이 모두 쓰러지면) 전투에 참가한 아군 유닛마다 실행되어, 게임팩의 정의에 따라 그 유닛의 데이터를 조작한다.
public sealed partial class GamePack
{
    public const string AfterBattleLoseEffectMasterPath = "master/after_battle_lose_effect_master_v1.tsv";

    /// <summary>전투 패배 후 이펙트 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<AfterBattleLoseEffect>? AfterBattleLoseEffects { get; init; }

    /// <summary>
    /// 전투 패배 후 이펙트 마스터: 키 execute_number를 두 번 쓸 수 없다. 종류는 이름으로 적고 값 셋은 비우면 0이다.
    /// 종류별 인자 검증은 스킬 이펙트와 같다: 스테이터스 값을 캐시와 주고받는 종류(1~6)는 value_1이 스테이터스 마스터의 코드, value_2가 cache_index(0~<see cref="MaxCacheIndex"/>),
    /// SetCacheValue는 value_1이 cache_index, value_3이 0~28, 캐시 계산은 세 값 모두 cache_index, JumpCache*는 value_1과 value_2가 cache_index다.
    /// 점프 대상(Jump의 value_1, JumpCache*의 value_3)은 마스터에 있는, 자신보다 큰 execute_number여야 한다.
    /// </summary>
    static List<AfterBattleLoseEffect>? LoadAfterBattleLoseEffects(ZipArchive zip, List<StatusEntry>? statuses)
    {
        const string file = AfterBattleLoseEffectMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;

        var rows = new List<AfterBattleLoseEffect>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, "execute_number", "effect_type", "value_1", "value_2", "value_3"))
        {
            var number = ParseLong(file, "실행 번호", r[0]);
            var where = $"실행 {number}의";
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<AfterBattleLoseEffectType>().Contains(r[1], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<AfterBattleLoseEffectType>(r[1], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 이펙트 종류 '{r[1]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<AfterBattleLoseEffectType>())})");
            var value1 = r[2].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_1", r[2]);
            var value2 = r[3].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_2", r[3]);
            var value3 = r[4].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_3", r[4]);
            if (!seen.Add(number))
                throw new PackFormatException($"{file}: 실행 번호 '{number}'가 두 번 적혀 있습니다.");

            void CheckCacheIndex(long index, string column)
            {
                if (index < 0 || index > MaxCacheIndex)
                    throw new PackFormatException($"{file}: {where} {column}(cache_index) '{index}'는 0~{MaxCacheIndex}이어야 합니다.");
            }

            if (type == AfterBattleLoseEffectType.SetCacheValue)
            {
                CheckCacheIndex(value1, "value_1");
                if (value3 < 0 || value3 > 28)
                    throw new PackFormatException($"{file}: {where} value_3(소수 자릿수) '{value3}'는 0~28이어야 합니다.");
            }
            else if (type is AfterBattleLoseEffectType.Jump or AfterBattleLoseEffectType.Finish)
            {
                // 점프 대상은 모든 행을 읽은 뒤에 확인한다.
            }
            else if (AfterBattleLoseJump.Of(type) is not null)
            {
                CheckCacheIndex(value1, "value_1");
                CheckCacheIndex(value2, "value_2");
            }
            else if (type is >= AfterBattleLoseEffectType.CacheAdd and <= AfterBattleLoseEffectType.CacheDivide)
            {
                CheckCacheIndex(value1, "value_1");
                CheckCacheIndex(value2, "value_2");
                CheckCacheIndex(value3, "value_3");
            }
            else if (type != AfterBattleLoseEffectType.None)
            {
                if (statuses is null) throw new PackFormatException($"{file}: {where} {type}에는 {StatusMasterPath}가 필요합니다.");
                if (statuses.All(st => st.Code != value1))
                    throw new PackFormatException($"{file}: {where} value_1(status_code) '{value1}'가 스테이터스 마스터에 없습니다.");
                CheckCacheIndex(value2, "value_2");
            }
            rows.Add(new AfterBattleLoseEffect(number, type, value1, value2, value3));
        }

        // 점프는 마스터에 있는, 자신보다 큰 execute_number로만 갈 수 있다.
        foreach (var e in rows)
            if (AfterBattleLoseJump.TargetOf(e) is { } target)
            {
                var where = $"실행 {e.ExecuteNumber}의";
                if (target <= e.ExecuteNumber)
                    throw new PackFormatException($"{file}: {where} 점프 대상 '{target}'는 자신의 실행 번호보다 커야 합니다.");
                if (!seen.Contains(target))
                    throw new PackFormatException($"{file}: {where} 점프 대상 '{target}'가 마스터에 없습니다.");
            }
        return rows;
    }

    /// <summary>전투 패배 후 이펙트(실행 번호 순). 마스터가 없으면 빈 목록.</summary>
    public IReadOnlyList<AfterBattleLoseEffect> AfterBattleLoseEffectsInOrder() =>
        Masters.AfterBattleLoseEffectRowTable.All
            .OrderBy(r => r.ExecuteNumber)
            .Select(r => new AfterBattleLoseEffect(r.ExecuteNumber, r.EffectType, r.Value1, r.Value2, r.Value3)).ToList();
}
