namespace Wera.Core;

public sealed partial class GameSession
{
    /// <summary>
    /// 이벤트와 ADV의 AddInventory 칸: 인벤토리 코드(커먼, 번치, 유니크 아이템, 캐릭터가 모두 쓰는 하나의 코드 공간)에 개수를 더한다.
    /// 화면에 나오지 않는 칸이라 실패해도 알리지 않고 가능한 만큼만 한다.
    /// <list type="bullet">
    /// <item>커먼 아이템: 개수만큼 더하되 소지 한도를 넘지 않게 멈춘다. 음수면 뺀다(장비 슬롯에 끼워 둔 것은 빼지 않고, 보유한 만큼에서 멈춘다).</item>
    /// <item>번치 아이템: 부품으로 풀어 위와 같이 처리한다.</item>
    /// <item>유니크 아이템(1 또는 -1만): +1은 새 인스턴스를 얻고, -1은 장비에 끼지 않은 인스턴스 하나를 낸다(작은 ID부터, 없으면 아무 일도 없다).</item>
    /// <item>캐릭터(1 또는 -1만): +1은 아직 없으면 얻고, -1은 보유한 캐릭터를 내보낸다(플레이어와 다른 캐릭터에게 장비로 끼워진 캐릭터는 못 내보낸다).</item>
    /// </list>
    /// 마스터에 없는 코드는 아무 일도 없다(게임팩 로드 때 이미 걸러진다).
    /// </summary>
    public void AddInventory(long inventoryCode, long amount)
    {
        if (amount == 0 || Pack.FindInventory(inventoryCode) is not { } entry) return;

        switch (entry.Kind)
        {
            case InventoryKind.CommonItem:
            case InventoryKind.BunchItem:
                Dictionary<long, long> parts;
                try { parts = Pack.Expand(inventoryCode, amount); }
                catch (OverflowException) { return; }
                foreach (var (item, delta) in parts) ChangeCommonItem(item, delta);
                break;
            case InventoryKind.UniqueItem:
                if (amount > 0) AddUniqueItem(inventoryCode);
                else if (InstancesOf(inventoryCode).FirstOrDefault(id => EquippedCount(EquipRef.Unique(id)) == 0) is { } spare)
                    RemoveUniqueItem(spare);
                break;
            case InventoryKind.Character:
                if (amount > 0) AddCharacter(inventoryCode);
                else RemoveCharacter(inventoryCode);
                break;
        }
    }

    /// <summary>
    /// 인벤토리 코드로 가진 개수(JumpInventory* 칸이 견주는 값): 커먼 아이템은 보유 개수, 번치 아이템은 부품이 모두 갖춰진 세트 수
    /// (부품별 보유 개수 ÷ 부품 어마운트의 최솟값), 유니크 아이템은 인스턴스 수, 캐릭터는 보유하면 1 아니면 0. 마스터에 없는 코드는 0.
    /// </summary>
    public long CountInventory(long inventoryCode)
    {
        if (Pack.FindInventory(inventoryCode) is not { } entry) return 0;
        return entry.Kind switch
        {
            InventoryKind.CommonItem => InventoryItems.GetValueOrDefault(inventoryCode).Amount,
            InventoryKind.BunchItem => Pack.FindBunch(inventoryCode) is { Parts.Count: > 0 } bunch
                ? bunch.Parts.Min(p => p.Amount <= 0 ? 0 : InventoryItems.GetValueOrDefault(p.ItemCode).Amount / p.Amount)
                : 0,
            InventoryKind.UniqueItem => InstancesOf(inventoryCode).Count,
            InventoryKind.Character => Characters.ContainsKey(inventoryCode) ? 1 : 0,
            _ => 0,
        };
    }

    /// <summary>보유한 캐릭터가 지금 그 태그를 가지고 있는가(JumpCharacterHaveTag 칸). 보유하지 않은 캐릭터는 거짓.</summary>
    public bool CharacterHasTag(long characterCode, long tagCode) =>
        Characters.ContainsKey(characterCode) && CharacterTags(characterCode).Contains(tagCode);

    /// <summary>보유한 유니크 아이템 인스턴스 중 하나라도 지금 그 태그를 가지고 있는가(JumpUniqueItemHaveTag 칸). 보유하지 않으면 거짓.</summary>
    public bool UniqueItemHasTag(long itemCode, long tagCode) =>
        InstancesOf(itemCode).Any(id => TagsOf(EquipRef.Unique(id)).Contains(tagCode));

    /// <summary>커먼 아이템 보유량을 바꾼다: 더할 때는 소지 한도에서, 뺄 때는 쓸 수 있는(장비에 끼지 않은) 보유량에서 멈춘다.</summary>
    void ChangeCommonItem(long item, long delta)
    {
        var have = InventoryItems.GetValueOrDefault(item).Amount;
        long after;
        if (delta > 0)
        {
            var limit = Pack.FindItem(item)?.CarryLimit ?? long.MaxValue;
            if (have >= limit) return;
            after = delta > limit - have ? limit : have + delta;
        }
        else
        {
            var removable = have - EquippedCount(EquipRef.Common(item));
            if (removable <= 0) return;
            after = delta < -removable ? have - removable : have + delta;
        }
        if (after == 0) InventoryItems.Remove(item);
        else InventoryItems[item] = new InventoryItem(after);
    }
}
