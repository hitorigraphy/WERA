namespace Wera.Core;

// 장비 보유와 장착. 장비는 보유해야 낄 수 있다.
public sealed partial class GameSession
{
    /// <summary>
    /// 보유 장비: 장비 코드 → 보유 개수. 장비 마스터의 장비여야 한다. 장비의 코드 공간은 아이템 인벤토리와 따로다.
    /// 같은 장비를 여러 개 가질 수 있고, 한 개는 한 곳에만 낄 수 있다.
    /// </summary>
    public Dictionary<long, long> OwnedEquipment { get; }

    /// <summary>장착 상태: 캐릭터 코드 → (장비 슬롯 코드 → 장비 코드). 낀 것이 없는 캐릭터는 키가 없다.</summary>
    public Dictionary<long, Dictionary<long, long>> EquippedItems { get; }

    /// <summary>캐릭터의 슬롯에 끼고 있는 장비 코드. 비어 있으면 null.</summary>
    public long? EquippedItemOf(long characterCode, long equipmentSlotCode) =>
        EquippedItems.TryGetValue(characterCode, out var slots) && slots.TryGetValue(equipmentSlotCode, out var item) ? item : null;

    /// <summary>이 장비를 모든 캐릭터가 합쳐서 몇 개 끼고 있는가.</summary>
    public long EquippedCount(long itemCode) =>
        EquippedItems.Values.Sum(slots => (long)slots.Values.Count(i => i == itemCode));

    /// <summary>보유 개수에서 끼고 있는 개수를 뺀, 새로 낄 수 있는 개수.</summary>
    public long FreeCopies(long itemCode) => OwnedEquipment.GetValueOrDefault(itemCode) - EquippedCount(itemCode);

    /// <summary>
    /// 보유 장비를 캐릭터의 장비 슬롯에 낀다. 슬롯 태그 마스터에 부합하는 장비여야 하고, 남는 보유 개수가 있어야 한다
    /// (그 슬롯에 이미 같은 장비를 끼고 있으면 아무것도 바꾸지 않고 true). 슬롯에 낀 다른 장비가 있으면 빠지고 보유로 돌아간다.
    /// 보유하지 않은 캐릭터이거나 낄 수 없으면 false.
    /// </summary>
    public bool Equip(long characterCode, long equipmentSlotCode, long itemCode)
    {
        if (!Characters.ContainsKey(characterCode) || !Pack.CanEquip(characterCode, equipmentSlotCode, itemCode)) return false;
        if (EquippedItemOf(characterCode, equipmentSlotCode) == itemCode) return true;
        if (FreeCopies(itemCode) <= 0) return false;

        if (!EquippedItems.TryGetValue(characterCode, out var slots)) EquippedItems[characterCode] = slots = [];
        slots[equipmentSlotCode] = itemCode;
        return true;
    }

    /// <summary>슬롯의 장비를 뺀다(보유로 돌아간다). 낀 것이 없었으면 false.</summary>
    public bool Unequip(long characterCode, long equipmentSlotCode)
    {
        if (!EquippedItems.TryGetValue(characterCode, out var slots) || !slots.Remove(equipmentSlotCode)) return false;
        if (slots.Count == 0) EquippedItems.Remove(characterCode);
        return true;
    }

    /// <summary>
    /// 선택한 캐릭터의 선택한 장비 슬롯에 지금 낄 수 있는 장비들: 슬롯 태그에 부합하고 보유 중이며
    /// (남는 개수가 있거나 이미 이 슬롯에 끼고 있는 것). 장비 마스터 순서.
    /// </summary>
    public IReadOnlyList<EquipmentItem> EquippableItems
    {
        get
        {
            if (CurrentCharacterCode is not { } character || CurrentEquipmentSlotCode is not { } slot) return [];
            var current = EquippedItemOf(character, slot);
            return Pack.EquippableItems(character, slot)
                .Where(i => i.ItemCode == current || FreeCopies(i.ItemCode) > 0)
                .ToList();
        }
    }

    string EquipmentItemName(long itemCode) =>
        Pack.FindEquipmentItem(itemCode)?.ItemName ?? itemCode.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>장비 슬롯 목록의 항목 이름: "무기 : 철검", 비어 있으면 "무기 : (없음)".</summary>
    string EquipmentSlotLabel(EquipmentSlotEntry slot)
    {
        var item = CurrentCharacterCode is { } c ? EquippedItemOf(c, slot.Code) : null;
        return $"{slot.Name} : {(item is { } code ? EquipmentItemName(code) : "(없음)")}";
    }

    /// <summary>
    /// 장비 가능 목록 화면의 항목: 맨 위 "[0] 해제"(이 슬롯에 장비를 끼고 있을 때만), 낄 수 있는 장비들(코드를 입력하거나
    /// 클릭하면 끼운다, 지금 끼고 있는 것은 "(장착중)"), 돌아가기.
    /// </summary>
    IReadOnlyList<ScreenEntry> EquipmentSelectEntries()
    {
        var character = CurrentCharacterCode!.Value;
        var slot = CurrentEquipmentSlotCode!.Value;
        var current = EquippedItemOf(character, slot);

        var entries = new List<ScreenEntry>();
        if (current is not null)
            entries.Add(new ScreenEntry(ClearEntryCode, "해제", Screen.EquipmentList, EntryAction.Unequip));
        entries.AddRange(EquippableItems.Select(i => new ScreenEntry(
            Text(i.ItemCode),
            i.ItemCode == current ? $"{i.ItemName} (장착중)" : i.ItemName,
            Screen.EquipmentList,
            EntryAction.EquipItem)));
        // 같은 캐릭터의 장비 슬롯 목록으로 돌아간다(Arg로 캐릭터가 있는 파티 슬롯을 넘겨 유지한다).
        entries.Add(new ScreenEntry(BackEntryCode, "돌아가기", Screen.EquipmentList,
            Arg: CurrentSlot!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return entries;
    }

    string EquipFromScreen(long itemCode)
    {
        var character = CurrentCharacterCode!.Value;
        var slot = CurrentEquipmentSlotCode!.Value;
        var who = $"{CurrentSlotCharacterName}의 {CurrentEquipmentSlot?.Name}";
        var item = EquipmentItemName(itemCode);
        var previous = EquippedItemOf(character, slot);

        if (!Equip(character, slot, itemCode)) return $"장착 실패: {who}에 {item}을(를) 낄 수 없습니다.";
        return previous == itemCode ? $"이미 장착 중: {who} = {item}"
            : previous is { } old ? $"장착: {who} {EquipmentItemName(old)} → {item}"
            : $"장착: {who} ← {item}";
    }

    string UnequipFromScreen()
    {
        var character = CurrentCharacterCode!.Value;
        var slot = CurrentEquipmentSlotCode!.Value;
        var who = $"{CurrentSlotCharacterName}의 {CurrentEquipmentSlot?.Name}";
        var old = EquippedItemOf(character, slot);
        return Unequip(character, slot) ? $"해제: {who} {EquipmentItemName(old!.Value)}" : $"해제할 장비가 없습니다: {who}";
    }

    // ---- 세이브 ----

    Dictionary<long, long> CopyOwnedEquipment() =>
        OwnedEquipment.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

    Dictionary<long, Dictionary<long, long>> CopyEquippedItems() =>
        EquippedItems.Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => new Dictionary<long, long>(kv.Value));

    /// <summary>
    /// 세이브의 보유 장비와 장착 상태를 검증하고 복사한다. 장비는 장비 마스터에 있어야 하고 개수는 0 이상,
    /// 장착한 캐릭터는 보유 캐릭터, 슬롯은 그 캐릭터의 장비 슬롯, 장비는 그 슬롯의 태그에 부합해야 하며,
    /// 장비마다 끼고 있는 개수가 보유 개수를 넘으면 안 된다.
    /// </summary>
    static (Dictionary<long, long> Owned, Dictionary<long, Dictionary<long, long>> Equipped) ValidateEquipment(
        GamePack pack, Dictionary<long, CharacterState> characters,
        Dictionary<long, long>? savedOwned, Dictionary<long, Dictionary<long, long>>? savedEquipped)
    {
        bool IsKnownItem(long item) => pack.FindEquipmentItem(item) is not null;

        var owned = new Dictionary<long, long>();
        foreach (var (item, count) in savedOwned ?? [])
        {
            if (!IsKnownItem(item))
                throw new SaveFormatException($"세이브의 보유 장비 '{item}'가 장비 마스터에 없습니다.");
            if (count < 0)
                throw new SaveFormatException($"세이브의 보유 장비 '{item}'의 개수가 음수입니다: {count}");
            if (count > 0) owned[item] = count;
        }

        var equipped = new Dictionary<long, Dictionary<long, long>>();
        foreach (var (character, slots) in savedEquipped ?? [])
        {
            if (!characters.ContainsKey(character))
                throw new SaveFormatException($"세이브의 장비가 보유하지 않은 캐릭터 '{character}'에 장착되어 있습니다.");
            var copy = new Dictionary<long, long>();
            foreach (var (slot, item) in slots ?? [])
            {
                if (pack.EquipmentSlotsOf(character).All(s => s.Code != slot))
                    throw new SaveFormatException($"세이브의 캐릭터 '{character}'에게는 장비 슬롯 '{slot}'가 없습니다.");
                if (!IsKnownItem(item))
                    throw new SaveFormatException($"세이브의 장착 장비 '{item}'가 장비 마스터에 없습니다.");
                if (!pack.CanEquip(character, slot, item))
                    throw new SaveFormatException($"세이브의 캐릭터 '{character}' 슬롯 '{slot}'에 장비 '{item}'를 끼울 수 없습니다(슬롯 태그에 부합하지 않음).");
                copy[slot] = item;
            }
            if (copy.Count > 0) equipped[character] = copy;
        }

        foreach (var group in equipped.Values.SelectMany(slots => slots.Values).GroupBy(i => i))
            if (group.LongCount() > owned.GetValueOrDefault(group.Key))
                throw new SaveFormatException(
                    $"세이브에서 장비 '{group.Key}'를 {group.LongCount()}개 끼고 있는데 보유는 {owned.GetValueOrDefault(group.Key)}개입니다.");

        return (owned, equipped);
    }
}
