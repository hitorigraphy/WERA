using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 캐릭터가 익힌 스킬 하나: 스킬 코드(스킬 마스터)와 <see cref="Statuses"/>(스테이터스 코드 → 값).
/// Statuses는 스킬 스테이터스 마스터의 초깃값에서 복사해 가지고 게임 중에 바뀔 수 있다. 세이브에 없으면(null) 마스터에서 복사한다.
/// </summary>
public sealed record SkillInstance(long SkillCode, Dictionary<long, long>? Statuses = null)
{
    public bool Equals(SkillInstance? other) =>
        other is not null && SkillCode == other.SkillCode && UniqueItemInstance.StatusesEqual(Statuses, other.Statuses);

    public override int GetHashCode() => SkillCode.GetHashCode();
}

/// <summary>
/// 보유 캐릭터 인스턴스: 인스턴스 ID(ULID 문자열)와 마스터 코드(캐릭터 인벤토리 코드)를 함께 가지고, 목록 순서(OrderNumber)와 현재 상태를 가진다.
/// 얻을 때 OrderNumber는 소지 캐릭터 수(0부터 시작)이고(순서를 바꿀 때는 OrderNumber를 조작한다), 목록은 OrderNumber순, 같으면 InstanceId순이다.
/// 상태는 <see cref="Statuses"/>(스테이터스 코드 → 값)에 있다: 예약된 코드는 level(1), hp(2), xpp(3), max_hp(4), xpp_max(5)이고
/// <see cref="Level"/>, <see cref="Hp"/>, <see cref="MaxHp"/>, <see cref="Xpp"/>, <see cref="XppMax"/>는 그 값을 읽는 편의 속성이다.
/// 스킬은 <see cref="SkillPool"/>(익힌 스킬들)에 있고 <see cref="SkillSlots"/>가 스킬 슬롯 코드 → SkillPool의 번호(0부터)로 어느 슬롯에 어느 스킬을 넣어 두었는지 가진다.
/// 값을 바꿀 때는 <see cref="WithStatus"/>처럼 새 인스턴스를 만들어 돌려주는 메서드를 쓴다(딕셔너리를 직접 고치면 복사본과 공유한다).
/// </summary>
public sealed record CharacterInstance
{
    public string InstanceId { get; init; } = "";
    public long CharacterCode { get; init; }
    public long OrderNumber { get; init; }

    /// <summary>스테이터스 코드 → 현재 값.</summary>
    public Dictionary<long, long> Statuses { get; init; } = [];

    /// <summary>익힌 스킬들.</summary>
    public List<SkillInstance> SkillPool { get; init; } = [];

    /// <summary>스킬 슬롯 코드 → <see cref="SkillPool"/>의 번호.</summary>
    public Dictionary<long, long> SkillSlots { get; init; } = [];

    // 예전 세이브의 고정 필드(Level, Hp, MaxHp, Xpp, XppMax). 읽을 때만 쓰고 세이브에는 쓰지 않는다(불러오면 Statuses로 옮긴다).
    [JsonPropertyName("Level")] public long? LegacyLevel { get; init; }
    [JsonPropertyName("Hp")] public long? LegacyHp { get; init; }
    [JsonPropertyName("MaxHp")] public long? LegacyMaxHp { get; init; }
    [JsonPropertyName("Xpp")] public long? LegacyXpp { get; init; }
    [JsonPropertyName("XppMax")] public long? LegacyXppMax { get; init; }

    [JsonIgnore] public long Level => Statuses.GetValueOrDefault(GamePack.LevelStatusCode);
    [JsonIgnore] public long Hp => Statuses.GetValueOrDefault(GamePack.HpStatusCode);
    [JsonIgnore] public long MaxHp => Statuses.GetValueOrDefault(GamePack.MaxHpStatusCode);
    [JsonIgnore] public long Xpp => Statuses.GetValueOrDefault(GamePack.XppStatusCode);
    [JsonIgnore] public long XppMax => Statuses.GetValueOrDefault(GamePack.XppMaxStatusCode);

    /// <summary>상태만 아는 인스턴스(시험, 임베디드 이펙트): ID는 비어 있고 코드와 순서는 0이다. 세션은 보유할 때 ID와 코드를 채운다.</summary>
    public static CharacterInstance Stats(long level, long hp, long maxHp, long xpp, long xppMax) =>
        new()
        {
            Statuses = new Dictionary<long, long>
            {
                [GamePack.LevelStatusCode] = level, [GamePack.HpStatusCode] = hp, [GamePack.MaxHpStatusCode] = maxHp,
                [GamePack.XppStatusCode] = xpp, [GamePack.XppMaxStatusCode] = xppMax,
            },
        };

    public bool IsDead => Hp <= 0;

    /// <summary>스테이터스 하나를 바꾼 새 인스턴스(원래 인스턴스는 그대로).</summary>
    public CharacterInstance WithStatus(long statusCode, long value) =>
        this with { Statuses = new Dictionary<long, long>(Statuses) { [statusCode] = value } };

    /// <summary>세이브가 받아들이는 범위로 맞춘다: 레벨 1 이상, 최대 HP와 필요 XPP 0 이상, HP 0~최대 HP, XPP 0~필요 XPP.</summary>
    public CharacterInstance Clamped()
    {
        var maxHp = Math.Max(0, MaxHp);
        var xppMax = Math.Max(0, XppMax);
        var statuses = new Dictionary<long, long>(Statuses)
        {
            [GamePack.LevelStatusCode] = Math.Max(1, Level),
            [GamePack.MaxHpStatusCode] = maxHp,
            [GamePack.HpStatusCode] = Math.Clamp(Hp, 0, maxHp),
            [GamePack.XppMaxStatusCode] = xppMax,
            [GamePack.XppStatusCode] = Math.Clamp(Xpp, 0, xppMax),
        };
        return this with { Statuses = statuses };
    }

    // 딕셔너리와 목록은 내용으로 견준다(레코드의 기본 동등성은 참조를 견주므로 직접 쓴다).
    public bool Equals(CharacterInstance? other) =>
        other is not null
        && InstanceId == other.InstanceId && CharacterCode == other.CharacterCode && OrderNumber == other.OrderNumber
        && SameEntries(Statuses, other.Statuses) && SameEntries(SkillSlots, other.SkillSlots)
        && SkillPool.SequenceEqual(other.SkillPool);

    public override int GetHashCode() => HashCode.Combine(InstanceId, CharacterCode, OrderNumber, Statuses.Count, SkillPool.Count, SkillSlots.Count);

    static bool SameEntries(Dictionary<long, long> a, Dictionary<long, long> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);
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
    /// <summary>"HP  : " 같은 앞머리를 뺀 게이지 칸 수.</summary>
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
    int Slot, long? CharacterCode, string LevelText, string Name, GaugeView Hp, GaugeView Xpp,
    IReadOnlyList<AttributeBadge> Badges, IReadOnlyList<StatusBadge> StatusBadges, bool IsLeader = false, bool IsSelected = false, bool IsGray = false)
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
