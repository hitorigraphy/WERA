namespace Wera.Core;

/// <summary>
/// 로드 후 캐릭터 서킷: 세이브를 불러온 뒤 보유 캐릭터 하나에 대해 로드 후 캐릭터 흐름(마스터의 execute_number 순서)을 실행해
/// 게임팩의 정의에 따라 캐릭터 데이터를 조작한다. 그 캐릭터가 시전자이자 메인 타겟이고(장비 없는 유닛) 처음부터 포커스되어 있으며 서브 타겟은 없다.
/// 흐름의 종류와 동작은 <see cref="UnitCircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// 실행이 끝나면 바뀐 스테이터스를 정수(소수는 버림)로 각 스테이터스의 범위에 맞춰 인스턴스에 되돌린다.
/// </summary>
public sealed class AfterLoadCharacterCircuit : UnitCircuitBase
{
    public AfterLoadCharacterCircuit(GamePack pack, CharacterInstance character)
        : this(pack, character, UnitStore.Of(character))
    {
    }

    AfterLoadCharacterCircuit(GamePack pack, CharacterInstance character, UnitStore store)
        : base(pack, store, new PartyUnit(character.CharacterCode), null) =>
        PresetFocus(character: character);

    /// <summary>흐름을 실행 번호 순서로 실행하고 조작이 끝난 캐릭터를 돌려준다. 흐름이 없으면 원래 인스턴스를 그대로 돌려준다.</summary>
    public CharacterInstance Execute()
    {
        Run(Pack.AfterLoadCharacterFlowsInOrder().Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.FlowType, e.Value1, e.Value2, e.Value3)).ToList());
        return Store.CharacterOf(Caster!)!;
    }
}
