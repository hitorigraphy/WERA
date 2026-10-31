namespace Wera.Core;

/// <summary>
/// XPP 변경 서킷: 캐릭터의 XPP가 바뀌면 그 캐릭터의 XP 종류(xp_type_code)의 XPP 변경 이펙트(마스터의 execute_number 순서)를 실행해,
/// XPP를 바탕으로 각 XP(MP 등)를 계산한다. 그 캐릭터가 시전자이자 메인 타겟이고(장비 없는 유닛) 처음부터 포커스되어 있으며 서브 타겟은 없다.
/// 이펙트의 종류와 동작은 <see cref="UnitCircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// 실행이 끝나면 바뀐 스테이터스를 정수(소수는 버림)로 각 스테이터스의 범위에 맞춰 인스턴스에 되돌린다.
/// </summary>
public sealed class XppChangedCircuit : UnitCircuitBase
{
    public XppChangedCircuit(GamePack pack, CharacterInstance character)
        : base(pack, UnitStore.Of(character), new PartyUnit(character.CharacterCode), null) =>
        PresetFocus(character: character);

    /// <summary>이펙트를 실행 번호 순서로 실행하고 조작이 끝난 캐릭터를 돌려준다. XP 종류가 없거나 이펙트가 없으면 원래 인스턴스를 그대로 돌려준다.</summary>
    public CharacterInstance Execute()
    {
        var xpType = Pack.FindCharacter(Caster!.CharacterCode)?.XpTypeCode ?? 0;
        if (xpType != 0)
            Run(Pack.XppChangedEffectsOf(xpType).Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
        return Store.CharacterOf(Caster!)!;
    }
}
