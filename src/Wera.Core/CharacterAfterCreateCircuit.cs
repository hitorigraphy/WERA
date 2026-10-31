namespace Wera.Core;

/// <summary>
/// 캐릭터 생성 후 서킷: 캐릭터를 새로 얻으면(또는 적 유닛의 임시 캐릭터를 만들면) 그 캐릭터에 대해 캐릭터 생성 후 이펙트(마스터의 execute_number 순서)을 실행해
/// 게임팩의 정의에 따라 캐릭터 데이터를 조작한다. 그 캐릭터가 시전자이자 메인 타겟이고(장비 없는 유닛) 처음부터 포커스되어 있으며 서브 타겟은 없다.
/// 이펙트의 종류와 동작은 <see cref="CircuitBase"/>와 <see cref="EffectType"/>을 따르고, 여기에 생성 후에만 있는 종류
/// (<see cref="CharacterAfterCreateEffectType.AddSkillToPool"/>, <see cref="CharacterAfterCreateEffectType.SetSkillToSlot"/>)가 더해진다.
/// 실행이 끝나면 바뀐 스테이터스를 정수(소수는 버림)로 각 스테이터스의 범위에 맞춰 인스턴스에 되돌린다.
/// </summary>
public sealed class CharacterAfterCreateCircuit : CircuitBase
{
    public CharacterAfterCreateCircuit(GamePack pack, CharacterInstance character)
        : base(pack, UnitStore.Of(character), new PartyUnit(character.CharacterCode), null) =>
        PresetFocus(character: character);

    /// <summary>이펙트을 실행 번호 순서로 실행하고 조작이 끝난 캐릭터를 돌려준다. 이펙트이 없으면 원래 인스턴스를 그대로 돌려준다.</summary>
    public CharacterInstance Execute()
    {
        Run(Pack.CharacterAfterCreateEffectsOf(Caster!.CharacterCode).Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
        return Store.CharacterOf(Caster!)!;
    }

    protected override void ApplyOther(CircuitEffect effect)
    {
        switch ((CharacterAfterCreateEffectType)effect.TypeNumber)
        {
            case CharacterAfterCreateEffectType.AddSkillToPool:
                AddSkillToFocusedPool(effect.Value1);
                break;
            case CharacterAfterCreateEffectType.SetSkillToSlot:
                SetFocusedSkillToSlot(effect.Value1);
                break;
        }
    }
}
