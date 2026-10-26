namespace Wera.Core;

/// <summary>플래그 값과 정해 둔 값을 견주는 방법.</summary>
public enum FlagComparison
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
}

/// <summary>
/// 이벤트와 ADV의 JumpRandom 칸이 함께 쓰는 규칙. 칸의 param_1이 chance(확률), param_2가 bound(모수), param_3이 target_flow_number다:
/// 0 이상 bound 미만의 무작위 정수 r을 뽑아 r &lt; chance면 target_flow_number로 점프하고, 아니면 다음 칸으로 간다
/// (30, 100, 5 → rand(100) &lt; 30 이면 5번 칸으로, 아니면 다음 칸으로). chance가 0 이하면 절대, bound 이상이면 늘 점프한다.
/// bound는 1 이상이어야 하고(로드 검증), 점프는 JumpFlag*와 같이 뒤로만 갈 수 있다.
/// </summary>
public static class RandomJump
{
    public const string Name = "JumpRandom";

    /// <summary>무작위로 점프하는 종류인가.</summary>
    public static bool Is(string flowTypeName) => flowTypeName == Name;

    /// <summary>r이 [0, bound)에서 뽑은 값일 때 점프하는가: r &lt; chance.</summary>
    public static bool Hit(long chance, long roll) => roll < chance;

    /// <summary>[0, bound)의 무작위 정수. bound가 1 이상일 때만 부른다.</summary>
    public static long Roll(long bound, Func<long, long>? random) => (random ?? Default)(bound);

    static long Default(long bound) => Random.Shared.NextInt64(bound);
}

/// <summary>
/// 이벤트와 ADV의 JumpCharacterHaveTag, JumpUniqueItemHaveTag 칸이 함께 쓰는 규칙. 칸의 param_1이 코드(캐릭터 코드 또는 유니크 아이템 코드),
/// param_2가 tag_code, param_3이 target_flow_number다: 현재 태그(캐릭터와 유니크 아이템 인스턴스의 태그는 게임을 하는 동안 바뀔 수 있다)에
/// 그 태그가 있으면 target_flow_number로 점프하고, 없으면 다음 칸으로 간다. 점프는 JumpFlag*와 같이 뒤로만 갈 수 있다.
/// </summary>
public static class HaveTagJump
{
    public const string Character = "JumpCharacterHaveTag";
    public const string UniqueItem = "JumpUniqueItemHaveTag";

    public static bool IsCharacter(string flowTypeName) => flowTypeName == Character;

    public static bool IsUniqueItem(string flowTypeName) => flowTypeName == UniqueItem;

    /// <summary>태그를 보고 점프하는 종류인가.</summary>
    public static bool Is(string flowTypeName) => IsCharacter(flowTypeName) || IsUniqueItem(flowTypeName);

    /// <summary>점프하는 종류(JumpFlag*, JumpInventory*, 태그 점프, JumpRandom)인가: 이런 칸은 param_3이 target_flow_number다.</summary>
    public static bool IsConditionalJump(string flowTypeName) =>
        FlagJump.Of(flowTypeName) is not null || InventoryJump.Of(flowTypeName) is not null || Is(flowTypeName) || RandomJump.Is(flowTypeName);
}

/// <summary>
/// 이벤트와 ADV의 JumpInventory* 칸이 함께 쓰는 규칙. 칸의 param_1이 inventory_code, param_2가 value(개수), param_3이 target_flow_number다:
/// 플레이어가 가진 그 인벤토리 코드의 개수를 value와 견주어 참이면 target_flow_number로 점프하고, 거짓이면 다음 칸으로 간다.
/// 개수는 커먼 아이템은 보유 개수, 번치 아이템은 부품이 모두 갖춰진 세트 수(부품별 보유 개수 ÷ 부품 어마운트의 최솟값),
/// 유니크 아이템은 그 코드의 인스턴스 수, 캐릭터는 보유하면 1 아니면 0이다. 점프는 JumpFlag*와 같이 뒤로만 갈 수 있다.
/// </summary>
public static class InventoryJump
{
    const string Prefix = "JumpInventory";

    /// <summary>흐름 종류의 이름이 JumpInventory로 시작하는 점프 종류면 그 비교 방법, 아니면 null.</summary>
    public static FlagComparison? Of(string flowTypeName) =>
        flowTypeName.StartsWith(Prefix, StringComparison.Ordinal)
        && Enum.TryParse<FlagComparison>(flowTypeName[Prefix.Length..], out var comparison)
            ? comparison
            : null;

    public static FlagComparison? Of(EventFlowType type) => Of(type.ToString());

    public static FlagComparison? Of(AdvFlowType type) => Of(type.ToString());
}

/// <summary>
/// 이벤트와 ADV의 JumpFlag* 칸이 함께 쓰는 규칙. 칸의 param_1이 flag_code, param_2가 flag_value, param_3이 target_flow_number다:
/// 플레이어 플래그(없으면 0)를 flag_value와 견주어 참이면 target_flow_number로 점프하고, 거짓이면 다음 칸으로 간다.
/// 점프는 뒤로(자신의 흐름 번호보다 큰, 같은 이벤트/ADV의 흐름 번호로)만 갈 수 있어 무한 반복이 생기지 않는다.
/// 조건 없이 점프하려면 Jump를 쓴다(param_1이 target_flow_number).
/// </summary>
public static class FlagJump
{
    const string Prefix = "JumpFlag";

    /// <summary>흐름 종류의 이름이 JumpFlag로 시작하는 점프 종류(JumpFlagEqual 등)면 그 비교 방법, 아니면 null.</summary>
    public static FlagComparison? Of(string flowTypeName) =>
        flowTypeName.StartsWith(Prefix, StringComparison.Ordinal)
        && Enum.TryParse<FlagComparison>(flowTypeName[Prefix.Length..], out var comparison)
            ? comparison
            : null;

    public static FlagComparison? Of(EventFlowType type) => Of(type.ToString());

    public static FlagComparison? Of(AdvFlowType type) => Of(type.ToString());

    /// <summary>플래그 값이 기준 값과 견주어 참인가.</summary>
    public static bool Test(FlagComparison comparison, long flagValue, long value) => comparison switch
    {
        FlagComparison.Equal => flagValue == value,
        FlagComparison.NotEqual => flagValue != value,
        FlagComparison.GreaterThan => flagValue > value,
        FlagComparison.GreaterOrEqual => flagValue >= value,
        FlagComparison.LessThan => flagValue < value,
        FlagComparison.LessOrEqual => flagValue <= value,
        _ => false,
    };
}
