namespace Wera.Core;

public sealed partial class GameSession
{
    /// <summary>
    /// 캐릭터의 XPP가 바뀌었음을 알려 XPP 변경 서킷(<see cref="XppChangedCircuit"/>)을 실행하고, 그 결과(XPP를 바탕으로 계산한 각 XP)를 캐릭터에 저장한다.
    /// 캐릭터를 보유하지 않았거나 XP 종류가 없으면 아무것도 하지 않고 false.
    /// </summary>
    public bool RunXppChanged(long characterCode)
    {
        if (!Characters.TryGetValue(characterCode, out var character) || (Pack.FindCharacter(characterCode)?.XpTypeCode ?? 0) == 0) return false;
        Characters[characterCode] = new XppChangedCircuit(Pack, character with { CharacterCode = characterCode }).Execute();
        return true;
    }
}
