namespace Wera.Core;

/// <summary>아이템 인벤토리 마스터의 한 행. CarryLimit은 한 번에 소지할 수 있는 최대 수량.</summary>
public sealed record ItemEntry(string Code, string Name, int CarryLimit);

/// <summary>캐릭터 인벤토리 마스터의 한 행. CarryLimit은 한 번에 소지할 수 있는 최대 수량.</summary>
public sealed record CharacterEntry(string Code, string Name, int CarryLimit);

/// <summary>번치 인벤토리의 부품. ItemCode는 아이템 인벤토리 마스터의 아이템 코드.</summary>
public sealed record BunchPart(string ItemCode, int Amount);

/// <summary>번치 인벤토리 마스터의 한 항목: 여러 아이템을 묶은 것. 소지제한은 없다.</summary>
public sealed record BunchEntry(string Code, string Name, IReadOnlyList<BunchPart> Parts);

/// <summary>화면에 보여 줄 교환 한 줄. 아이템은 코드가 아니라 이름.</summary>
public sealed record TradeLine(string Code, string CostItemName, int CostAmount, string ReceiveItemName, int ReceiveAmount)
{
    /// <summary>"금화 ×100 → 약초 ×5"</summary>
    public string Text => $"{CostItemName} ×{CostAmount} → {ReceiveItemName} ×{ReceiveAmount}";
}

/// <summary>
/// 상점 교환: 코스트 아이템을 CostAmount개 내면 리시브 아이템을 ReceiveAmount개 받는다.
/// 아이템 코드는 아이템 인벤토리 또는 번치 인벤토리의 코드.
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
    public bool IsAvailable(IReadOnlyDictionary<string, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}
