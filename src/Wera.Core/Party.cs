namespace Wera.Core;

/// <summary>보유 캐릭터의 현재 상태. XpMax는 다음 레벨까지 필요한 경험치.</summary>
public readonly record struct CharacterState(long Level, long Hp, long MaxHp, long Xp, long XpMax)
{
    public bool IsDead => Hp <= 0;

    /// <summary>세이브가 받아들이는 범위로 맞춘다: 레벨 1 이상, HP 0~최대 HP, XP 0~필요 XP.</summary>
    public CharacterState Clamped()
    {
        var maxHp = Math.Max(0, MaxHp);
        var xpMax = Math.Max(0, XpMax);
        return new CharacterState(Math.Max(1, Level), Math.Clamp(Hp, 0, maxHp), maxHp, Math.Clamp(Xp, 0, xpMax), xpMax);
    }
}

public enum GaugeColor
{
    White,
    Yellow,
    Red,
    Gray,
}

/// <summary>화면에 그릴 게이지 한 줄. Text는 게이지 칸 수와 같은 길이의 문자열.</summary>
public sealed record GaugeView(string Text, GaugeColor Color);

public static class Gauge
{
    /// <summary>"HP : " 같은 앞머리를 뺀 게이지 칸 수.</summary>
    public const int Width = 13;

    /// <summary>
    /// 게이지를 width칸으로 그린다. 채운 칸은 '*', 빈 칸은 '-'이고 가운데에 "현재/최대"를 겹쳐 쓴다.
    /// 예: 36/40 → "****36/40***-", 3/30 → "*----3/30----".
    /// 색은 50% 이상 흰색, 25% 이상 노랑, 그 아래는 빨강이고, <paramref name="gray"/>(사망, 빈 슬롯)면 회색이다.
    /// </summary>
    public static GaugeView Render(long current, long max, bool gray, int width = Width)
    {
        current = Math.Max(0, current);
        max = Math.Max(0, max);
        var ratio = max == 0 ? 0.0 : Math.Min(1.0, (double)current / max);
        var filled = (int)Math.Round(width * ratio, MidpointRounding.AwayFromZero);

        var cells = new char[width];
        for (var i = 0; i < width; i++) cells[i] = i < filled ? '*' : '-';

        var label = $"{current}/{max}";
        if (label.Length > width) label = label[..width];
        var start = (width - label.Length + 1) / 2;   // 칸이 남으면 오른쪽으로 치우친다
        label.CopyTo(0, cells, start, label.Length);

        var color = gray ? GaugeColor.Gray
            : ratio >= 0.5 ? GaugeColor.White
            : ratio >= 0.25 ? GaugeColor.Yellow
            : GaugeColor.Red;
        return new GaugeView(new string(cells), color);
    }
}

/// <summary>
/// 슬롯 4번째 줄의 속성 표시: 속성 아이콘 옆에 유리속성이면 ⭕, 불리속성이면 ❌.
/// 속성 아이콘 이미지는 아직 경로 규칙이 없어 화면에서는 No Image로 그린다.
/// </summary>
public sealed record AttributeBadge(long AttributeCode, string Name, bool Advantage)
{
    public string Marker => Advantage ? "⭕" : "❌";
}

/// <summary>슬롯 4번째 줄의 상태이상 표시: 상태이상 아이콘(지금은 No Image)만 나온다.</summary>
public sealed record StatusBadge(long StatusEffectCode, string Name);

/// <summary>
/// 파티 설정 화면의 슬롯 하나. 슬롯 0~2가 전위, 3~5가 중위, 6~8이 후위.
/// Badges(속성)와 StatusBadges(상태이상)가 4번째(아이콘) 줄을 이룬다.
/// </summary>
public sealed record PartySlotView(
    int Slot, long? CharacterCode, string LevelText, string Name, GaugeView Hp, GaugeView Xp,
    IReadOnlyList<AttributeBadge> Badges, IReadOnlyList<StatusBadge> StatusBadges)
{
    public const string EmptyName = "빈 슬롯";
    public bool IsEmpty => CharacterCode is null;
}

public enum CharacterListColor
{
    /// <summary>배치할 수 있다.</summary>
    White,
    /// <summary>배치할 수 없다(보유하지 않음).</summary>
    Gray,
    /// <summary>지금 이 슬롯에 배치되어 있는 캐릭터.</summary>
    SkyBlue,
    /// <summary>다른 슬롯에 배치된 파티원. 고르면 위치를 서로 바꾼다.</summary>
    Green,
}

public sealed record CharacterListItem(long Code, string Name, CharacterListColor Color)
{
    public bool Selectable => Color != CharacterListColor.Gray;
}
