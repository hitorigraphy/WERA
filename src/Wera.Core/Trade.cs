namespace Wera.Core;

/// <summary>일반 아이템.</summary>
public sealed record CommonItem(string Code, string Name);

/// <summary>번치 아이템에 포함된 부품. ItemCode는 일반 아이템 코드.</summary>
public sealed record BunchPart(string ItemCode, int Amount);

/// <summary>여러 일반 아이템을 묶은 아이템.</summary>
public sealed record BunchItem(string Code, string Name, IReadOnlyList<BunchPart> Parts);

/// <summary>화면에 보여 줄 교환 한 줄. 아이템은 코드가 아니라 이름.</summary>
public sealed record TradeLine(string Code, string CostItemName, int CostAmount, string ReceiveItemName, int ReceiveAmount);

/// <summary>
/// 상점 교환: 코스트 아이템을 CostAmount개 내면 리시브 아이템을 ReceiveAmount개 받는다.
/// 아이템 코드는 일반 아이템 또는 번치 아이템의 코드.
/// </summary>
/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음. 같은 그룹 안은 AND, 그룹끼리는 OR.</param>
public sealed record ShopTrade(
    string Code,
    string ShopCode,
    string CostItemCode,
    int CostAmount,
    string ReceiveItemCode,
    int ReceiveAmount,
    IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    public bool IsAvailable(IReadOnlyDictionary<string, int> flags) => ConditionGroups.IsMet(Groups, flags);
}
