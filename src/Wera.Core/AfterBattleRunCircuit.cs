namespace Wera.Core;

/// <summary>
/// 전투 도망 후 서킷: 전투에서 도망치면 전투에 참가한 아군 유닛(<see cref="PartyUnit"/>) 하나에 대해 전투 도망 후 이펙트(마스터의 execute_number 순서)를 실행해
/// 게임팩의 정의에 따라 그 유닛의 캐릭터, 장비, 스킬을 조작한다. 그 유닛이 시전자이자 메인 타겟이고 서브 타겟은 없으며, 캐릭터는 처음부터 포커스되어 있다
/// (장비와 스킬은 Focus* 이펙트로 포커스한다). 이펙트의 종류와 동작은 <see cref="CircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// 실행이 끝나면 바뀐 스테이터스를 정수(소수는 버림)로 각 스테이터스의 범위에 맞춰 저장소에 되돌린다.
/// </summary>
public sealed class AfterBattleRunCircuit : CircuitBase
{
    public AfterBattleRunCircuit(GamePack pack, UnitStore store, PartyUnit unit) : base(pack, store, unit, unit) =>
        PresetFocus(character: store.CharacterOf(unit));

    /// <summary>캐릭터 인스턴스 하나만으로(장비 없이) 실행한다.</summary>
    public AfterBattleRunCircuit(GamePack pack, CharacterInstance character)
        : this(pack, UnitStore.Of(character), new PartyUnit(character.CharacterCode))
    {
    }

    /// <summary>이펙트를 실행 번호 순서로 실행하고 조작이 끝난 캐릭터를 돌려준다. 이펙트가 없으면 원래 인스턴스를 그대로 돌려준다.</summary>
    public CharacterInstance Execute()
    {
        Run(Pack.AfterBattleRunEffectsInOrder().Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
        return Store.CharacterOf(Caster!)!;
    }
}
