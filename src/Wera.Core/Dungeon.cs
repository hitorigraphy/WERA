namespace Wera.Core;

/// <summary>플래그 조건 비교 방식. TSV에는 이름(대소문자 무시)으로 적는다.</summary>
public enum ConditionType
{
    /// <summary>조건 없음. 항상 만족한다.</summary>
    None = 0,
    Equal,
    NotEqual,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
}

/// <summary>플래그 하나에 대한 조건. 값이 없는 플래그는 0으로 취급한다.</summary>
public sealed record FlagCondition(long FlagCode, ConditionType Type, long Value)
{
    public bool IsMet(IReadOnlyDictionary<long, long> playerFlags) =>
        IsMet(playerFlags.GetValueOrDefault(FlagCode));

    /// <summary>실제 값 하나를 조건과 비교한다.</summary>
    public bool IsMet(long actual) =>
        Type switch
        {
            ConditionType.None => true,
            ConditionType.Equal => actual == Value,
            ConditionType.NotEqual => actual != Value,
            ConditionType.GreaterThan => actual > Value,
            ConditionType.GreaterOrEqual => actual >= Value,
            ConditionType.LessThan => actual < Value,
            ConditionType.LessOrEqual => actual <= Value,
            _ => false,
        };
}

/// <summary>그룹넘버 순으로 정렬된 조건 묶음의 평가. 같은 그룹 안은 AND, 그룹끼리는 OR. 조건이 없으면 항상 만족.</summary>
public static class ConditionGroups
{
    public static bool IsMet(IReadOnlyList<IReadOnlyList<FlagCondition>> groups, IReadOnlyDictionary<long, long> playerFlags) =>
        groups.Count == 0 || groups.Any(g => g.All(c => c.IsMet(playerFlags)));
}

/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음.</param>
public sealed record Dungeon(long Code, string Name, IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    public bool IsAvailable(IReadOnlyDictionary<long, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}

/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음.</param>
public sealed record Shop(long Code, string Name, IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    public bool IsAvailable(IReadOnlyDictionary<long, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}
