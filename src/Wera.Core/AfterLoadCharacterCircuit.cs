namespace Wera.Core;

/// <summary>
/// 로드 후 캐릭터 서킷: 세이브를 불러온 뒤 보유 캐릭터 하나에 대해 로드 후 캐릭터 흐름(마스터의 execute_number 순서)을 실행해
/// 게임팩의 정의에 따라 캐릭터 데이터를 조작한다. 캐릭터(<see cref="Character"/>)와 캐시(<see cref="CacheValues"/>)를 갖는다.
/// 지금 정해진 종류는 None(아무 일도 하지 않음)뿐이다.
/// </summary>
public sealed class AfterLoadCharacterCircuit(GamePack pack, CharacterInstance character)
{
    /// <summary>흐름이 조작하는 캐릭터. 값을 바꿀 때는 새 인스턴스로 갈아 끼운다(<see cref="CharacterInstance.WithStatus"/>).</summary>
    public CharacterInstance Character { get; private set; } = character;

    /// <summary>흐름들이 공유하는 값 목록(cache_index가 이 목록의 번호).</summary>
    public List<decimal> CacheValues { get; } = [];

    /// <summary>흐름을 실행 번호 순서로 실행하고 조작이 끝난 캐릭터를 돌려준다.</summary>
    public CharacterInstance Execute()
    {
        foreach (var flow in pack.AfterLoadCharacterFlowsInOrder())
            Apply(flow);
        return Character;
    }

    void Apply(LoadCharacterFlow flow)
    {
        switch (flow.FlowType)
        {
            case LoadCharacterFlowType.None:
                break;
        }
    }
}
