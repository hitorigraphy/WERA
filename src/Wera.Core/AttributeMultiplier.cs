namespace Wera.Core;

/// <summary>
/// 속성 배율: 스킬의 속성(공격 속성)과 시전자·타겟의 유리/불리속성으로 정해지는 곱셈 보정(데미지와 명중률에 같이 쓰는 규칙).
/// 시전자: 공격 속성이 자신의 유리속성이면 ×유리속성 배율, 불리속성이면 ×불리속성 배율. 타겟: 공격 속성이 자신의 유리속성이면 ×불리속성 배율, 불리속성이면 ×유리속성 배율.
/// 두 보정은 곱한다(기본 설정 1.5, 0.5에서 유리속성 시전자가 불리속성 타겟을 치면 1.5 × 1.5 = 2.25). 배율은 게임 설정 AttributeAdvantageMultiplier, AttributeDisadvantageMultiplier다.
/// 공격 속성이 없거나 시전자나 타겟을 모르면 그쪽 보정은 없다(1).
/// </summary>
public static class AttributeMultiplier
{
    /// <param name="attackAttribute">스킬의 속성(속성 마스터의 코드). null이면 속성이 없는 스킬이라 1.</param>
    /// <param name="casterCharacter">시전자의 캐릭터 코드. null이면 시전자 보정이 없다.</param>
    /// <param name="targetCharacter">타겟의 캐릭터 코드. null이면 타겟 보정이 없다.</param>
    public static decimal Of(GamePack pack, long? attackAttribute, long? casterCharacter, long? targetCharacter)
    {
        if (attackAttribute is not { } attribute) return 1m;
        var advantage = pack.AttributeAdvantageMultiplier;
        var disadvantage = pack.AttributeDisadvantageMultiplier;
        var result = 1m;
        if (casterCharacter is { } caster && pack.FindCharacter(caster) is { } attacker)
        {
            if (attribute == attacker.AdvantageAttributeCode) result *= advantage;
            else if (attribute == attacker.DisadvantageAttributeCode) result *= disadvantage;
        }
        if (targetCharacter is { } target && pack.FindCharacter(target) is { } defender)
        {
            if (attribute == defender.AdvantageAttributeCode) result *= disadvantage;
            else if (attribute == defender.DisadvantageAttributeCode) result *= advantage;
        }
        return result;
    }
}
