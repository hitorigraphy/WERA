using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 스킬 이펙트(스킬 실행 이펙트 마스터)가 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다. 0~32는 <see cref="EffectType"/>과 같은 번호와 동작이고,
/// 스킬 실행 서킷에만 있는 종류가 33부터다(33 AttributeMultiplierToCache). 인자와 예외는 각 종류의 설명을 따른다.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SkillExecuteEffectType>))]
public enum SkillExecuteEffectType
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
    /// <summary>
    /// 서브모듈을 부른다: value_1이 submodule_code(이 서킷 종류의 서브모듈 마스터에 있어야 함)이고 value_2, value_3은 보지 않는다.
    /// 서브모듈의 이펙트들이 이 서킷의 캐시, 포커스, 시전자와 타겟을 그대로 써서 실행 번호 순서로 실행되고(그래서 값은 캐시로 주고받는다: 부르기 전에 캐시에 넣은 값이 인자, 서브모듈이 캐시에 쓴 값이 결과),
    /// 끝나면(Finish 또는 마지막 이펙트) 이 이펙트 다음으로 돌아온다. 서브모듈 안에서는 JumpToSubmodule을 쓸 수 없다(서브모듈이 서브모듈을 부를 수 없다).
    /// </summary>
    JumpToSubmodule = 32,
    /// <summary>
    /// 스킬 실행 서킷에만 있는 종류. 이 서킷이 실행하는 스킬의 속성(스킬 마스터의 attribute_code)과 시전자·타겟의 유리/불리속성으로 속성 배율을 계산해 캐시에 쓴다:
    /// cache[value_1] = 배율. value_1이 cache_index(0~<see cref="GamePack.MaxCacheIndex"/>), value_2가 타겟 선택(0이면 메인 타겟, n(1 이상)이면 서브 타겟 n-1번)이고 value_3은 보지 않는다.
    /// 배율은 시전자가 스킬 속성이 자신의 유리속성이면 ×유리속성 배율(게임 설정 AttributeAdvantageMultiplier, 기본 1.5), 불리속성이면 ×불리속성 배율(AttributeDisadvantageMultiplier, 기본 0.5)이고,
    /// 타겟은 스킬 속성이 자신의 유리속성이면 ×불리속성 배율(기본 0.5), 불리속성이면 ×유리속성 배율(기본 1.5)이며, 두 보정은 곱한다(기본 설정에서 최대 2.25).
    /// 스킬에 속성이 없거나 시전자나 그 타겟이 없으면 배율은 1이다. 데미지와 명중률은 이 배율을 곱해 쓰는 것이 규칙이지만 엔진이 자동으로 곱하지는 않는다(CacheMultiply로 게임팩이 곱한다).
    /// </summary>
    AttributeMultiplierToCache = 33,
}
