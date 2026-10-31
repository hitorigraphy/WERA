namespace Wera.Core;

/// <summary>아이템 인벤토리 마스터의 한 행. CarryLimit은 한 번에 소지할 수 있는 최대 수량.</summary>
public sealed record ItemEntry(long Code, string Name, long CarryLimit, long? SkillCode = null);

/// <summary>
/// 캐릭터 인벤토리 마스터의 한 행. CarryLimit은 한 번에 소지할 수 있는 최대 수량.
/// 모든 캐릭터는 유리속성과 불리속성(속성 마스터의 코드)을 하나씩 가진다. Description은 캐릭터 상세 화면에 나오는 설명문(선택 열 description; 없으면 빈 글).
/// </summary>
public sealed record CharacterEntry(
    long Code, string Name, long CarryLimit, long AdvantageAttributeCode, long DisadvantageAttributeCode, string Description = "");

/// <summary>번치 인벤토리의 부품. ItemCode는 아이템 인벤토리 마스터의 아이템 코드.</summary>
public sealed record BunchPart(long ItemCode, long Amount);

/// <summary>번치 인벤토리 마스터의 한 항목: 여러 아이템을 묶은 것. 소지제한은 없다.</summary>
public sealed record BunchEntry(long Code, string Name, IReadOnlyList<BunchPart> Parts);

/// <summary>화면에 보여 줄 교환 한 줄. 아이템은 코드가 아니라 이름.</summary>
public sealed record TradeLine(long Code, string CostItemName, long CostAmount, string ReceiveItemName, long ReceiveAmount)
{
    /// <summary>"금화 ×100 → 약초 ×5"</summary>
    public string Text => $"{CostItemName} ×{CostAmount} → {ReceiveItemName} ×{ReceiveAmount}";
}

/// <summary>상점 교환의 코스트/리시브가 가리키는 것의 종류. 종류마다 코드 공간이 따로다.</summary>
public enum TradeKind
{
    /// <summary>커먼 아이템 인벤토리 또는 번치 아이템 인벤토리의 코드(TSV에서 비워 두면 이것).</summary>
    CommonItem,
    /// <summary>유니크 아이템 인벤토리의 코드. 받으면 새 인스턴스가 만들어지고, 내면 끼지 않은 인스턴스가 쓰인다.</summary>
    UniqueItem,
    /// <summary>캐릭터 인벤토리의 코드. 어마운트는 1이다. 받으면 새 캐릭터가 되고, 내면 캐릭터가 떠난다.</summary>
    Character,
}

/// <summary>
/// 상점 교환: 코스트를 CostAmount개 내면 리시브를 ReceiveAmount개 받는다.
/// 코스트/리시브는 CostKind/ReceiveKind에 따라 커먼·번치 아이템, 유니크 아이템, 캐릭터의 코드를 가리킨다.
/// </summary>
/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음. 같은 그룹 안은 AND, 그룹끼리는 OR.</param>
public sealed record ShopTrade(
    long Code,
    long ShopCode,
    long CostItemCode,
    long CostAmount,
    long ReceiveItemCode,
    long ReceiveAmount,
    IReadOnlyList<IReadOnlyList<FlagCondition>> Groups,
    TradeKind CostKind = TradeKind.CommonItem,
    TradeKind ReceiveKind = TradeKind.CommonItem)
{
    public bool IsAvailable(IReadOnlyDictionary<long, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}
