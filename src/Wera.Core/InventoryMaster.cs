namespace Wera.Core;

/// <summary>인벤토리 마스터의 종류. 네 종류 모두 하나의 인벤토리 코드 공간을 나눠 쓴다.</summary>
public enum InventoryKind
{
    /// <summary>커먼 아이템 인벤토리 마스터.</summary>
    CommonItem,
    /// <summary>유니크 아이템 인벤토리 마스터.</summary>
    UniqueItem,
    /// <summary>번치 아이템 인벤토리 마스터.</summary>
    BunchItem,
    /// <summary>캐릭터 인벤토리 마스터.</summary>
    Character,
}

/// <summary>
/// 인벤토리 코드(<c>inventory_code</c>) 하나에 대한 공통 항목: 어느 인벤토리 마스터의 것(<see cref="Kind"/>)이고 이름이 무엇인가.
/// 게임팩을 읽을 때 네 인벤토리 마스터를 합쳐 코드로 찾는 사전을 만든다. 같은 코드가 두 마스터에 있으면 읽지 못한다.
/// </summary>
public sealed record InventoryEntry(long Code, InventoryKind Kind, string Name);
