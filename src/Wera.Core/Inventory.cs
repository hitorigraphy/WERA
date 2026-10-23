namespace Wera.Core;

/// <summary>인벤토리_아이템의 값. 아이템 코드(키)별로 하나씩 가진다.</summary>
public readonly record struct InventoryItem(long Amount);
