namespace Wera.Core;

public sealed partial class GameSession
{
    /// <summary>
    /// 캐릭터의 XP 이름(XP 종류 마스터의 이름)과 XP 계산 서킷(<see cref="XpCalculateCircuit"/>)의 결과. 이름과 결과는 XPP를 바탕으로 보여 줄 때 계산한다.
    /// XP 종류가 없는 캐릭터(또는 XP 종류 마스터에 없는 코드)는 null.
    /// </summary>
    public (string Name, XpResult Result)? XpOf(CharacterInstance character)
    {
        var xpType = Pack.FindCharacter(character.CharacterCode)?.XpTypeCode ?? 0;
        if (xpType == 0 || Pack.FindXpType(xpType) is not { } type) return null;
        return new XpCalculateCircuit(Pack, character).Execute() is { } result ? (type.Name, result) : null;
    }

    /// <summary>XP 줄의 앞머리 "{XP 이름} : ". 이름은 3칸 이상으로 맞춰 "HP  : "와 같은 6칸이 되게 한다("MP  : "). XP가 없는 캐릭터는 같은 길이의 빈칸이다.</summary>
    public static string XpLabel(string xpName) =>
        xpName == "" ? new string(' ', 6) : PadColumns(xpName, 3) + " : ";

    /// <summary>XP 게이지 한 줄: 이름과 게이지. XP가 없는 캐릭터는 이름이 빈 글이고 빈 회색 게이지다.</summary>
    (string Name, GaugeView Gauge) XpLine(CharacterInstance character, int width = Gauge.Width) =>
        XpOf(character) is { } xp
            ? (xp.Name, Gauge.Render(xp.Result.Value, xp.Result.Max, gray: character.IsDead, width))
            : ("", Gauge.Render(0, 0, gray: true, width));
}
