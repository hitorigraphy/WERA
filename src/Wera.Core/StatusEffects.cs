namespace Wera.Core;

/// <summary>상태이상 마스터의 한 행.</summary>
public sealed record StatusEffectEntry(long Code, string Name);

/// <summary>
/// 상태이상 이펙트가 발동하는 시점. TSV에는 이름(대소문자 무시)으로 적는다.
/// 아직 시점 목록을 받지 못해 None만 정의되어 있다.
/// </summary>
public enum TriggerType
{
    None = 0,
}

/// <summary>
/// 상태이상 이펙트 마스터의 한 행: 상태이상(<see cref="StatusEffectCode"/>)이 <see cref="Trigger"/> 시점에
/// 임베디드 이펙트(<see cref="EffectCode"/>)를 값 1~3으로 실행한다. 우선도의 해석(정렬 방향)은 호출하는 쪽이 정한다.
/// </summary>
public sealed record StatusEffectEffect(
    long StatusEffectCode, TriggerType Trigger, long Priority, long EffectCode, long Value1, long Value2, long Value3);
