namespace Wera.Core;

/// <summary>XP 계산 서킷의 결과: 화면에 XP로 표기하는 값과 최댓값(소수는 버린 정수).</summary>
public sealed record XpResult(long Value, long Max);

/// <summary>
/// XP 계산 서킷: 캐릭터의 XPP가 바뀌면(= 보여 줄 때마다 XPP를 바탕으로 다시) 그 캐릭터의 XP 종류(xp_type_code)의 XP 계산 이펙트(마스터의 execute_number 순서)를 실행해
/// XP를 계산한다. 그 캐릭터가 시전자이자 메인 타겟이고(장비 없는 유닛) 처음부터 포커스되어 있으며 서브 타겟은 없다.
/// 이펙트의 종류와 동작은 <see cref="UnitCircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// 결과(<see cref="XpResult"/>)는 실행이 끝난 서킷 캐시의 0번(XP 값)과 1번(XP 최댓값)이다(비어 있으면 0, 소수는 버림). 캐릭터의 스테이터스는 바꾸지 않는다.
/// </summary>
public sealed class XpCalculateCircuit : UnitCircuitBase
{
    /// <summary>결과의 XP 값을 담는 캐시 번호.</summary>
    public const int ValueCacheIndex = 0;

    /// <summary>결과의 XP 최댓값을 담는 캐시 번호.</summary>
    public const int MaxCacheIndex = 1;

    public XpCalculateCircuit(GamePack pack, CharacterInstance character)
        : base(pack, UnitStore.Of(character), new PartyUnit(character.CharacterCode), null) =>
        PresetFocus(character: character);

    /// <summary>이펙트를 실행 번호 순서로 실행하고 결과를 돌려준다. XP 종류가 없으면 null이고(XP가 없는 캐릭터), 이펙트가 없으면 0/0이다.</summary>
    public XpResult? Execute()
    {
        var xpType = Pack.FindCharacter(Caster!.CharacterCode)?.XpTypeCode ?? 0;
        if (xpType == 0) return null;
        Run(Pack.XpCalculateEffectsOf(xpType).Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
        return new XpResult(Cached(ValueCacheIndex), Cached(MaxCacheIndex));
    }

    long Cached(int index) => index < CacheValues.Count ? (long)decimal.Truncate(CacheValues[index]) : 0;
}
