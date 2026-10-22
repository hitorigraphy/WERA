namespace Wera.Core;

/// <summary>플래그 조건 비교 방식. TSV에는 이름(대소문자 무시)으로 적는다.</summary>
public enum ConditionType
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
}

/// <summary>플래그 하나에 대한 조건. 값이 없는 플래그는 0으로 취급한다.</summary>
public sealed record FlagCondition(string FlagCode, ConditionType Type, int Value)
{
    public bool IsMet(IReadOnlyDictionary<string, int> flags)
    {
        var actual = flags.GetValueOrDefault(FlagCode);
        return Type switch
        {
            ConditionType.Equal => actual == Value,
            ConditionType.NotEqual => actual != Value,
            ConditionType.GreaterThan => actual > Value,
            ConditionType.GreaterOrEqual => actual >= Value,
            ConditionType.LessThan => actual < Value,
            ConditionType.LessOrEqual => actual <= Value,
            _ => false,
        };
    }
}

/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음. 같은 그룹 안은 AND, 그룹끼리는 OR.</param>
public sealed record Dungeon(string Code, string Name, IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    /// <summary>조건이 하나도 없으면 항상 입장 가능.</summary>
    public bool IsAvailable(IReadOnlyDictionary<string, int> flags) =>
        Groups.Count == 0 || Groups.Any(g => g.All(c => c.IsMet(flags)));
}
