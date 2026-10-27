namespace Wera.Core;

/// <summary>
/// 보유 캐릭터 인스턴스: 인스턴스 ID(ULID 문자열)와 마스터 코드(캐릭터 인벤토리 코드)를 함께 가지고, 목록 순서(OrderNumber)와 현재 상태를 가진다.
/// XpMax는 다음 레벨까지 필요한 경험치. 얻을 때 OrderNumber는 소지 캐릭터 수 + 1이고(순서를 바꿀 때는 OrderNumber를 조작한다), 목록은 OrderNumber순, 같으면 InstanceId순이다.
/// </summary>
public readonly record struct CharacterInstance(
    string InstanceId, long CharacterCode, long OrderNumber, long Level, long Hp, long MaxHp, long Xp, long XpMax)
{
    /// <summary>상태만 아는 인스턴스(시험, 임베디드 이펙트): ID는 비어 있고 코드와 순서는 0이다. 세션은 보유할 때 ID와 코드를 채운다.</summary>
    public static CharacterInstance Stats(long level, long hp, long maxHp, long xp, long xpMax) =>
        new("", 0, 0, level, hp, maxHp, xp, xpMax);

    public bool IsDead => Hp <= 0;

    /// <summary>세이브가 받아들이는 범위로 맞춘다: 레벨 1 이상, HP 0~최대 HP, XP 0~필요 XP.</summary>
    public CharacterInstance Clamped()
    {
        var maxHp = Math.Max(0, MaxHp);
        var xpMax = Math.Max(0, XpMax);
        return this with { Level = Math.Max(1, Level), Hp = Math.Clamp(Hp, 0, maxHp), MaxHp = maxHp, Xp = Math.Clamp(Xp, 0, xpMax), XpMax = xpMax };
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

    /// <summary>빈 슬롯의 표시(회색 0/0). 슬롯 번호가 없는 슬롯(파트너, 어시스트)은 -1.</summary>
    public static PartySlotView Empty(int slot = -1)
    {
        var empty = Gauge.Render(0, 0, gray: true);
        return new PartySlotView(slot, null, "", EmptyName, empty, empty, [], []);
    }

    /// <summary>줄 이름: 위 줄(슬롯 0~2) 전위, 가운데 줄(3~5) 중위, 아래 줄(6~8) 후위.</summary>
    public static IReadOnlyList<string> RowNames { get; } = ["전위", "중위", "후위"];

    public string RowName => RowNames[Slot / 3];
    public bool IsEmpty => CharacterCode is null;
}

public enum CharacterListColor
{
    /// <summary>배치할 수 있다.</summary>
    White,
    /// <summary>배치할 수 없다(보유하지 않음).</summary>
    Gray,
    /// <summary>다른 슬롯에 배치된 파티원. 고르면 위치를 서로 바꾼다.</summary>
    Green,
}

public sealed record CharacterListItem(long Code, string Name, CharacterListColor Color)
{
    public bool Selectable => Color != CharacterListColor.Gray;
}
