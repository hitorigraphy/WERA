using System.Globalization;

namespace Wera.Core;

// 장비 보유와 장착. 장비 슬롯에는 유니크 아이템, 커먼 아이템, 캐릭터가 들어갈 수 있고, 보유해야 낄 수 있다.
public sealed partial class GameSession
{
    /// <summary>
    /// 보유 유니크 아이템: 유니크 아이템 코드 → 보유 개수. 유니크 아이템 마스터의 것이어야 한다. 코드 공간은 아이템 인벤토리와 따로다.
    /// 같은 것을 여러 개 가질 수 있고, 한 개는 한 곳에만 낄 수 있다. 커먼 아이템의 보유는 인벤토리 수량이고
    /// 캐릭터의 보유는 보유 캐릭터 목록이다.
    /// </summary>
    public Dictionary<long, long> OwnedEquipment { get; }

    /// <summary>장착 상태: 캐릭터 코드 → (장비 슬롯 코드 → 낀 것). 낀 것이 없는 캐릭터는 키가 없다.</summary>
    public Dictionary<long, Dictionary<long, EquipRef>> EquippedItems { get; }

    /// <summary>캐릭터의 슬롯에 끼고 있는 것. 비어 있으면 null.</summary>
    public EquipRef? EquippedItemOf(long characterCode, long equipmentSlotCode) =>
        EquippedItems.TryGetValue(characterCode, out var slots) && slots.TryGetValue(equipmentSlotCode, out var item) ? item : null;

    /// <summary>이것을 모든 캐릭터가 합쳐서 몇 개 끼고 있는가.</summary>
    public long EquippedCount(EquipRef target) =>
        EquippedItems.Values.Sum(slots => (long)slots.Values.Count(i => i == target));

    public long EquippedCount(long uniqueItemCode) => EquippedCount(EquipRef.Unique(uniqueItemCode));

    /// <summary>보유 개수: 유니크 아이템은 OwnedEquipment, 커먼 아이템은 인벤토리 수량, 캐릭터는 보유하면 1.</summary>
    public long OwnedCount(EquipRef target) => target.Kind switch
    {
        EquipKind.UniqueItem => OwnedEquipment.GetValueOrDefault(target.Code),
        EquipKind.CommonItem => InventoryItems.GetValueOrDefault(target.Code).Amount,
        EquipKind.Character => Characters.ContainsKey(target.Code) ? 1 : 0,
        _ => 0,
    };

    /// <summary>보유 개수에서 끼고 있는 개수를 뺀, 새로 낄 수 있는 개수.</summary>
    public long FreeCopies(EquipRef target) => OwnedCount(target) - EquippedCount(target);

    public long FreeCopies(long uniqueItemCode) => FreeCopies(EquipRef.Unique(uniqueItemCode));

    /// <summary>
    /// 보유한 것을 캐릭터의 장비 슬롯에 낀다. 슬롯 태그 마스터에 부합해야 하고, 남는 보유 개수가 있어야 한다
    /// (그 슬롯에 이미 같은 것을 끼고 있으면 아무것도 바꾸지 않고 true). 슬롯에 낀 다른 것이 있으면 빠지고 보유로 돌아간다.
    /// 보유하지 않은 캐릭터이거나 낄 수 없으면 false.
    /// </summary>
    public bool Equip(long characterCode, long equipmentSlotCode, EquipRef target)
    {
        if (!Characters.ContainsKey(characterCode) || !Pack.CanEquip(characterCode, equipmentSlotCode, target)) return false;
        if (EquippedItemOf(characterCode, equipmentSlotCode) == target) return true;
        if (FreeCopies(target) <= 0) return false;

        if (!EquippedItems.TryGetValue(characterCode, out var slots)) EquippedItems[characterCode] = slots = [];
        slots[equipmentSlotCode] = target;
        return true;
    }

    public bool Equip(long characterCode, long equipmentSlotCode, long uniqueItemCode) =>
        Equip(characterCode, equipmentSlotCode, EquipRef.Unique(uniqueItemCode));

    /// <summary>슬롯의 것을 뺀다(보유로 돌아간다). 낀 것이 없었으면 false.</summary>
    public bool Unequip(long characterCode, long equipmentSlotCode)
    {
        if (!EquippedItems.TryGetValue(characterCode, out var slots) || !slots.Remove(equipmentSlotCode)) return false;
        if (slots.Count == 0) EquippedItems.Remove(characterCode);
        return true;
    }

    /// <summary>
    /// 선택한 캐릭터의 선택한 장비 슬롯에 지금 낄 수 있는 것들: 슬롯 태그에 부합하고 보유 중이며
    /// (남는 개수가 있거나 이미 이 슬롯에 끼고 있는 것). 유니크 아이템, 커먼 아이템, 캐릭터 순.
    /// </summary>
    public IReadOnlyList<EquipCandidate> EquippableItems
    {
        get
        {
            if (CurrentCharacterCode is not { } character || CurrentEquipmentSlotCode is not { } slot) return [];
            var current = EquippedItemOf(character, slot);
            return Pack.EquippableTargets(character, slot)
                .Where(i => i.Ref == current || FreeCopies(i.Ref) > 0)
                .ToList();
        }
    }

    /// <summary>장비 슬롯 목록의 항목 이름: "무기 : 철검", 비어 있으면 "무기 : (없음)".</summary>
    string EquipmentSlotLabel(EquipmentSlotEntry slot)
    {
        var item = CurrentCharacterCode is { } c ? EquippedItemOf(c, slot.Code) : null;
        return $"{slot.Name} : {(item is { } target ? Pack.NameOf(target) : "(없음)")}";
    }

    static string RefText(EquipRef target) => $"{target.Kind}:{target.Code.ToString(CultureInfo.InvariantCulture)}";

    static EquipRef ParseRef(string text)
    {
        var parts = text.Split(':');
        return new EquipRef(Enum.Parse<EquipKind>(parts[0]), long.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 장비 가능 목록 화면의 항목: 맨 위 "[0] 해제"(이 슬롯에 낀 것이 있을 때만), 낄 수 있는 것들(번호를 입력하거나
    /// 클릭하면 끼운다, 지금 끼고 있는 것은 "(장착중)"), 돌아가기. 낄 수 있는 것들의 코드 공간이 종류마다 따로라서
    /// 번호는 1부터 차례로 붙인다.
    /// </summary>
    IReadOnlyList<ScreenEntry> EquipmentSelectEntries()
    {
        var character = CurrentCharacterCode!.Value;
        var slot = CurrentEquipmentSlotCode!.Value;
        var current = EquippedItemOf(character, slot);

        var entries = new List<ScreenEntry>();
        if (current is not null)
            entries.Add(new ScreenEntry(ClearEntryCode, "해제", Screen.EquipmentList, EntryAction.Unequip));
        entries.AddRange(EquippableItems.Select((i, index) => new ScreenEntry(
            Text(index + 1),
            i.Ref == current ? $"{i.Name} (장착중)" : i.Name,
            Screen.EquipmentList,
            EntryAction.EquipItem,
            RefText(i.Ref))));
        // 같은 캐릭터의 장비 슬롯 목록으로 돌아간다(Arg로 캐릭터가 있는 파티 슬롯을 넘겨 유지한다).
        entries.Add(new ScreenEntry(BackEntryCode, "돌아가기", Screen.EquipmentList,
            Arg: CurrentSlot!.Value.ToString(CultureInfo.InvariantCulture)));
        return entries;
    }

    string EquipFromScreen(EquipRef target)
    {
        var character = CurrentCharacterCode!.Value;
        var slot = CurrentEquipmentSlotCode!.Value;
        var who = $"{CurrentSlotCharacterName}의 {CurrentEquipmentSlot?.Name}";
        var item = Pack.NameOf(target);
        var previous = EquippedItemOf(character, slot);

        if (!Equip(character, slot, target)) return $"장착 실패: {who}에 {item}을(를) 낄 수 없습니다.";
        return previous == target ? $"이미 장착 중: {who} = {item}"
            : previous is { } old ? $"장착: {who} {Pack.NameOf(old)} → {item}"
            : $"장착: {who} ← {item}";
    }

    string UnequipFromScreen()
    {
        var character = CurrentCharacterCode!.Value;
        var slot = CurrentEquipmentSlotCode!.Value;
        var who = $"{CurrentSlotCharacterName}의 {CurrentEquipmentSlot?.Name}";
        var old = EquippedItemOf(character, slot);
        return Unequip(character, slot) ? $"해제: {who} {Pack.NameOf(old!.Value)}" : $"해제할 장비가 없습니다: {who}";
    }

    // ---- 세이브 ----

    Dictionary<long, long> CopyOwnedEquipment() =>
        OwnedEquipment.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

    Dictionary<long, Dictionary<long, EquipRef>> CopyEquippedItems() =>
        EquippedItems.Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => new Dictionary<long, EquipRef>(kv.Value));

    /// <summary>
    /// 세이브의 보유 유니크 아이템과 장착 상태를 검증하고 복사한다. 유니크 아이템은 마스터에 있어야 하고 개수는 0 이상,
    /// 장착한 캐릭터는 보유 캐릭터, 슬롯은 그 캐릭터의 장비 슬롯, 끼운 것은 마스터에 있고 그 슬롯의 태그에 부합해야 하며,
    /// 끼운 개수가 보유 개수(유니크 아이템은 OwnedEquipment, 커먼 아이템은 인벤토리 수량, 캐릭터는 보유 캐릭터 1명)를 넘으면 안 된다.
    /// 캐릭터는 자기 자신을 낄 수 없다.
    /// </summary>
    static (Dictionary<long, long> Owned, Dictionary<long, Dictionary<long, EquipRef>> Equipped) ValidateEquipment(
        GamePack pack, Dictionary<long, CharacterState> characters, Dictionary<long, InventoryItem> inventory,
        Dictionary<long, long>? savedOwned, Dictionary<long, Dictionary<long, EquipRef>>? savedEquipped)
    {
        var owned = new Dictionary<long, long>();
        foreach (var (item, count) in savedOwned ?? [])
        {
            if (pack.FindUniqueItem(item) is null)
                throw new SaveFormatException($"세이브의 보유 유니크 아이템 '{item}'가 유니크 아이템 마스터에 없습니다.");
            if (count < 0)
                throw new SaveFormatException($"세이브의 보유 유니크 아이템 '{item}'의 개수가 음수입니다: {count}");
            if (count > 0) owned[item] = count;
        }

        var equipped = new Dictionary<long, Dictionary<long, EquipRef>>();
        foreach (var (character, slots) in savedEquipped ?? [])
        {
            if (!characters.ContainsKey(character))
                throw new SaveFormatException($"세이브의 장비가 보유하지 않은 캐릭터 '{character}'에 장착되어 있습니다.");
            var copy = new Dictionary<long, EquipRef>();
            foreach (var (slot, target) in slots ?? [])
            {
                if (pack.EquipmentSlotsOf(character).All(s => s.Code != slot))
                    throw new SaveFormatException($"세이브의 캐릭터 '{character}'에게는 장비 슬롯 '{slot}'가 없습니다.");
                if (!pack.Exists(target))
                    throw new SaveFormatException($"세이브의 장착한 {target.Kind} '{target.Code}'가 마스터에 없습니다.");
                if (!pack.CanEquip(character, slot, target))
                    throw new SaveFormatException($"세이브의 캐릭터 '{character}' 슬롯 '{slot}'에 {target.Kind} '{target.Code}'를 끼울 수 없습니다(슬롯 태그에 부합하지 않음).");
                copy[slot] = target;
            }
            if (copy.Count > 0) equipped[character] = copy;
        }

        foreach (var group in equipped.Values.SelectMany(slots => slots.Values).GroupBy(i => i))
        {
            var have = group.Key.Kind switch
            {
                EquipKind.UniqueItem => owned.GetValueOrDefault(group.Key.Code),
                EquipKind.CommonItem => inventory.GetValueOrDefault(group.Key.Code).Amount,
                _ => characters.ContainsKey(group.Key.Code) ? 1 : 0,
            };
            if (group.LongCount() > have)
                throw new SaveFormatException(
                    $"세이브에서 {group.Key.Kind} '{group.Key.Code}'를 {group.LongCount()}개 끼고 있는데 보유는 {have}개입니다.");
        }

        return (owned, equipped);
    }
}
