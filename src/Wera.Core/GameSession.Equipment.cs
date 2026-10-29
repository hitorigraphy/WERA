using System.Globalization;

namespace Wera.Core;

// 장비 보유와 장착. 장비 슬롯에는 유니크 아이템, 커먼 아이템, 캐릭터가 들어갈 수 있고, 보유해야 낄 수 있다.
// 유니크 아이템은 한 개가 인스턴스 하나이고, 캐릭터와 유니크 아이템 인스턴스는 자기 태그를 가진다(마스터에서 복사해 와서
// 게임 중에 추가/소실). 커먼 아이템은 인스턴스가 없어 늘 마스터의 태그다.
public sealed partial class GameSession
{
    /// <summary>보유 유니크 아이템 인스턴스: 인스턴스 ID(ULID 문자열) → 인스턴스. 유니크 아이템 코드는 유니크 아이템 마스터에 있어야 한다.</summary>
    public Dictionary<string, UniqueItemInstance> UniqueItemInstances { get; }

    /// <summary>
    /// 캐릭터 태그 인스턴스: 캐릭터 코드 → 현재 태그. 아직 없는 캐릭터는 캐릭터 태그 마스터의 태그를 그대로 가진 것으로 본다
    /// (저장할 때 보유 캐릭터 전원이 여기로 복사된다).
    /// </summary>
    public Dictionary<long, List<long>> CharacterTagInstances { get; }

    /// <summary>모든 파티의 유닛들(빈 칸 제외). 장비는 유닛이 가진다.</summary>
    IEnumerable<PartyUnit> PartyUnits => Parties.SelectMany(p => p.Units).OfType<PartyUnit>();

    // ---- 유니크 아이템 인스턴스 ----

    /// <summary>유니크 아이템을 한 개 얻는다: 새 인스턴스를 만들고 유니크 아이템 태그 마스터의 태그를 복사한다. 마스터에 없으면 null.</summary>
    public string? AddUniqueItem(long itemCode)
    {
        if (Pack.FindUniqueItem(itemCode) is null) return null;
        var id = NewInstanceId();
        UniqueItemInstances[id] = new UniqueItemInstance(id, itemCode, UniqueItemInstances.Count, Pack.MasterTagsOfUniqueItem(itemCode).ToList());
        return id;
    }

    /// <summary>
    /// 지금 가진 유니크 아이템 인스턴스 전부, OrderNumber순이고 같으면 InstanceId순(장비 확인 화면의 목록).
    /// 줄은 "[OrderNumber] 이름"이다.
    /// </summary>
    public IReadOnlyList<UniqueItemInstance> OwnedUniqueItems =>
        UniqueItemInstances.Values.OrderBy(i => i.OrderNumber).ThenBy(i => i.InstanceId, StringComparer.Ordinal).ToList();

    /// <summary>장비 확인 화면의 한 줄: 코드 칸("[순서 번호]")과 장비 이름.</summary>
    public sealed record EquipmentRow(string Order, string Name);

    /// <summary>장비 확인 화면의 줄들: <see cref="OwnedUniqueItems"/> 순서대로 "[OrderNumber] 장비 이름". 칸 너비는 동료 확인과 같다.</summary>
    public IReadOnlyList<EquipmentRow> Equipments =>
        OwnedUniqueItems.Select(i => new EquipmentRow(
            $"[{i.OrderNumber.ToString(CultureInfo.InvariantCulture)}]",
            Pack.FindUniqueItem(i.ItemCode)?.ItemName ?? Text(i.ItemCode))).ToList();

    /// <summary>
    /// 새 인스턴스 ID(ULID의 ToString). ULID는 시간순으로 정렬되므로 이미 있는 것보다 시각이 반드시 늦도록 해서
    /// 만든 순서가 ID의 순서와 같게 한다(같은 밀리초에 여러 개를 만들어도).
    /// </summary>
    string NewInstanceId() => NextInstanceId(UniqueItemInstances.Keys.Concat(Characters.Values.Select(c => c.InstanceId)));

    /// <summary>주어진 ID들보다 시각이 반드시 늦은 새 ULID 문자열.</summary>
    static string NextInstanceId(IEnumerable<string> existing)
    {
        var latest = existing
            .Select(k => Ulid.TryParse(k, out var u) ? u.Time : DateTimeOffset.MinValue)
            .DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        // ULID의 시각은 밀리초 단위이므로 밀리초로 비교한다(같은 밀리초면 뒤의 무작위 부분이 순서를 정해 버린다).
        var latestMs = latest.ToUnixTimeMilliseconds();
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return Ulid.NewUlid(DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(nowMs, latestMs + 1))).ToString();
    }

    /// <summary>인스턴스를 없앤다(끼고 있었다면 빠진다). 없으면 false.</summary>
    public bool RemoveUniqueItem(string instanceId)
    {
        if (!UniqueItemInstances.Remove(instanceId)) return false;
        RevalidateEquipment();
        return true;
    }

    /// <summary>이 유니크 아이템 코드의 인스턴스 ID들(ID순 = 만든 순서).</summary>
    public IReadOnlyList<string> InstancesOf(long itemCode) =>
        UniqueItemInstances.Where(kv => kv.Value.ItemCode == itemCode).Select(kv => kv.Key).Order(StringComparer.Ordinal).ToList();

    // ---- 태그 ----

    /// <summary>끼울 것의 현재 태그: 유니크 아이템은 인스턴스의 태그, 커먼 아이템은 마스터, 캐릭터는 캐릭터 인스턴스의 태그.</summary>
    public IReadOnlyList<long> TagsOf(EquipRef target) => TagsOfTarget(Pack, UniqueItemInstances, CharacterTagInstances, target).Order().ToList();

    /// <summary>캐릭터의 현재 태그.</summary>
    public IReadOnlyList<long> CharacterTags(long characterCode) => TagsOf(EquipRef.Char(characterCode));

    /// <summary>보유 캐릭터에게 태그를 더한다. 태그 마스터에 없거나 이미 있거나 보유하지 않은 캐릭터면 false. 끼운 것이 더는 맞지 않으면 빠진다.</summary>
    public bool AddCharacterTag(long characterCode, long tagCode) =>
        Characters.ContainsKey(characterCode) && CanHoldTag(characterCode, tagCode, isCharacter: true)
        && ChangeTags(OwnCharacterTags(characterCode), tagCode, add: true);

    /// <summary>보유 캐릭터에게서 태그를 없앤다. 없었으면 false. 끼운 것이 더는 맞지 않으면 빠진다.</summary>
    public bool RemoveCharacterTag(long characterCode, long tagCode) =>
        Characters.ContainsKey(characterCode) && !IsPlayerTagOfPlayer(characterCode, tagCode)
        && ChangeTags(OwnCharacterTags(characterCode), tagCode, add: false);

    /// <summary>
    /// 이 태그를 더할 수 있는가: 플레이어 태그는 캐릭터 1만, 메인 통화 태그는 커먼 아이템 2만 가질 수 있다
    /// (캐릭터와 유니크 아이템 인스턴스에는 메인 통화 태그를 더할 수 없다).
    /// </summary>
    static bool CanHoldTag(long code, long tagCode, bool isCharacter) =>
        GamePack.MayHoldTag(tagCode, isCharacter ? InventoryKind.Character : InventoryKind.UniqueItem, code);

    /// <summary>캐릭터 1에게서 플레이어 태그를 없애려는 시도인가(항상 가져야 하므로 막는다).</summary>
    static bool IsPlayerTagOfPlayer(long characterCode, long tagCode) =>
        characterCode == GamePack.PlayerCharacterCode && tagCode == GamePack.PlayerTagCode;

    /// <summary>유니크 아이템 인스턴스에 태그를 더한다.</summary>
    public bool AddUniqueItemTag(string instanceId, long tagCode) =>
        UniqueItemInstances.TryGetValue(instanceId, out var item) && CanHoldTag(0, tagCode, isCharacter: false)
        && ChangeTags(OwnItemTags(item), tagCode, add: true);

    /// <summary>유니크 아이템 인스턴스에서 태그를 없앤다.</summary>
    public bool RemoveUniqueItemTag(string instanceId, long tagCode) =>
        UniqueItemInstances.TryGetValue(instanceId, out var item) && ChangeTags(OwnItemTags(item), tagCode, add: false);

    // 인스턴스가 자기 태그 목록을 갖게 한다(캐릭터는 처음 바꿀 때 마스터에서 복사).
    List<long> OwnCharacterTags(long characterCode)
    {
        if (!CharacterTagInstances.TryGetValue(characterCode, out var tags))
            CharacterTagInstances[characterCode] = tags = Pack.MasterTagsOfCharacter(characterCode).ToList();
        return tags;
    }

    List<long> OwnItemTags(UniqueItemInstance item)
    {
        if (item.Tags is { } tags) return tags;
        var copy = Pack.MasterTagsOfUniqueItem(item.ItemCode).ToList();
        UniqueItemInstances[UniqueItemInstances.First(kv => ReferenceEquals(kv.Value, item)).Key] = item with { Tags = copy };
        return copy;
    }

    bool ChangeTags(List<long> tags, long tagCode, bool add)
    {
        if (add && (Pack.Tags is null || Pack.Tags.All(t => t.Code != tagCode))) return false;
        var changed = add ? !tags.Contains(tagCode) && Append(tags, tagCode) : tags.Remove(tagCode);
        if (changed) RevalidateEquipment();
        return changed;

        static bool Append(List<long> list, long tag) { list.Add(tag); return true; }
    }

    /// <summary>태그가 바뀌어 더는 슬롯 규칙에 맞지 않거나 없어진 것을 장비에서 뺀다.</summary>
    void RevalidateEquipment()
    {
        EnsureLeaders();
        foreach (var unit in PartyUnits)
            foreach (var (slot, target) in unit.Equipment.ToList())
                if (!Characters.ContainsKey(unit.CharacterCode)
                    || !Fits(Pack, UniqueItemInstances, CharacterTagInstances, Characters, unit.CharacterCode, slot, target))
                    unit.Equipment.Remove(slot);
    }

    // ---- 낀 것 조회 ----

    /// <summary>파티에 있는 캐릭터의 슬롯에 끼고 있는 것. 비어 있거나 파티에 없는 캐릭터면 null.</summary>
    public EquipRef? EquippedItemOf(long characterCode, long equipmentSlotCode) =>
        UnitOf(characterCode) is { } unit && unit.Equipment.TryGetValue(equipmentSlotCode, out var item) ? item : null;

    /// <summary>슬롯에 끼고 있는 것이 유니크 아이템이면 그 유니크 아이템 코드, 아니면 null.</summary>
    public long? EquippedUniqueCode(long characterCode, long equipmentSlotCode) =>
        EquippedItemOf(characterCode, equipmentSlotCode) is { Kind: EquipKind.UniqueItem } r && UniqueItemInstances.TryGetValue(r.Instance ?? "", out var i)
            ? i.ItemCode : null;

    /// <summary>이것을 파티의 모든 유닛이 합쳐서 몇 개 끼고 있는가.</summary>
    public long EquippedCount(EquipRef target) =>
        PartyUnits.Sum(unit => (long)unit.Equipment.Values.Count(i => i == target));

    /// <summary>이 유니크 아이템 코드의 인스턴스를 몇 개 끼고 있는가.</summary>
    public long EquippedCount(long uniqueItemCode) => InstancesOf(uniqueItemCode).Sum(id => EquippedCount(EquipRef.Unique(id)));

    /// <summary>보유 개수: 유니크 아이템 인스턴스와 캐릭터는 있으면 1, 커먼 아이템은 인벤토리 수량.</summary>
    public long OwnedCount(EquipRef target) => target.Kind switch
    {
        EquipKind.UniqueItem => UniqueItemInstances.ContainsKey(target.Instance ?? "") ? 1 : 0,
        EquipKind.CommonItem => InventoryItems.GetValueOrDefault(target.Code).Amount,
        EquipKind.Character => Characters.ContainsKey(target.Code) ? 1 : 0,
        _ => 0,
    };

    /// <summary>보유 개수에서 끼고 있는 개수를 뺀, 새로 낄 수 있는 개수.</summary>
    public long FreeCopies(EquipRef target) => OwnedCount(target) - EquippedCount(target);

    /// <summary>이 유니크 아이템 코드의 인스턴스 중 끼지 않은 것의 수.</summary>
    public long FreeCopies(long uniqueItemCode) => InstancesOf(uniqueItemCode).Count - EquippedCount(uniqueItemCode);

    // ---- 장착 ----

    /// <summary>
    /// 보유한 것을 캐릭터의 장비 슬롯에 낀다. (캐릭터의 현재 태그 + 끼울 것의 현재 태그)가 슬롯 태그 규칙에 부합해야 하고,
    /// 남는 보유 개수가 있어야 한다(그 슬롯에 이미 같은 것을 끼고 있으면 아무것도 바꾸지 않고 true).
    /// 슬롯에 낀 다른 것이 있으면 빠지고 보유로 돌아간다. 보유하지 않은 캐릭터이거나 파티에 없거나(장비는 유닛이 가진다) 낄 수 없으면 false.
    /// </summary>
    public bool Equip(long characterCode, long equipmentSlotCode, EquipRef target)
    {
        if (!Characters.ContainsKey(characterCode) || UnitOf(characterCode) is not { } unit
            || !Fits(Pack, UniqueItemInstances, CharacterTagInstances, Characters, characterCode, equipmentSlotCode, target)) return false;
        if (EquippedItemOf(characterCode, equipmentSlotCode) == target) return true;
        if (FreeCopies(target) <= 0) return false;

        unit.Equipment[equipmentSlotCode] = target;
        return true;
    }

    /// <summary>이 유니크 아이템 코드의 인스턴스 중 낄 수 있는 것 하나를 낀다(작은 ID부터).</summary>
    public bool Equip(long characterCode, long equipmentSlotCode, long uniqueItemCode) =>
        InstancesOf(uniqueItemCode).Any(id => Equip(characterCode, equipmentSlotCode, EquipRef.Unique(id)));

    /// <summary>슬롯의 것을 뺀다(보유로 돌아간다). 낀 것이 없었으면 false.</summary>
    public bool Unequip(long characterCode, long equipmentSlotCode)
    {
        return UnitOf(characterCode) is { } unit && unit.Equipment.Remove(equipmentSlotCode);
    }

    /// <summary>끼울 것의 이름. 마스터에 없으면 코드 그대로.</summary>
    public string NameOf(EquipRef target) => (target.Kind switch
    {
        EquipKind.UniqueItem => UniqueItemInstances.TryGetValue(target.Instance ?? "", out var i) ? Pack.FindUniqueItem(i.ItemCode)?.ItemName : null,
        EquipKind.CommonItem => Pack.FindItem(target.Code)?.Name,
        EquipKind.Character => Pack.FindCharacter(target.Code)?.Name,
        _ => null,
    }) ?? target.Key;

    /// <summary>
    /// 선택한 캐릭터의 선택한 장비 슬롯에 지금 낄 수 있는 것들: 슬롯 태그에 부합하고 보유 중이며
    /// (남는 개수가 있거나 이미 이 슬롯에 끼고 있는 것). 유니크 아이템 인스턴스(마스터 순서, 같은 아이템은 ID 순),
    /// 커먼 아이템, 캐릭터 순.
    /// </summary>
    public IReadOnlyList<EquipCandidate> EquippableItems
    {
        get
        {
            if (CurrentCharacterCode is not { } character || CurrentEquipmentSlotCode is not { } slot) return [];
            var current = EquippedItemOf(character, slot);

            var unique = OwnedUniqueItems.Select(i => EquipRef.Unique(i.InstanceId));
            var common = (Pack.ItemInventory ?? []).Select(i => EquipRef.Common(i.Code));
            var characters = (Pack.CharacterInventory ?? []).Select(c => EquipRef.Char(c.Code));

            return unique.Concat(common).Concat(characters)
                .Where(r => Fits(Pack, UniqueItemInstances, CharacterTagInstances, Characters, character, slot, r))
                .Where(r => r == current || FreeCopies(r) > 0)
                .Select(r => new EquipCandidate(r, NameOf(r)))
                .ToList();
        }
    }

    // ---- 판정 (세이브 검증과 같은 규칙을 쓰려고 상태를 인자로 받는다) ----

    static IReadOnlySet<long> TagsOfTarget(
        GamePack pack, Dictionary<string, UniqueItemInstance> instances, Dictionary<long, List<long>> characterTags, EquipRef target) =>
        target.Kind switch
        {
            EquipKind.UniqueItem => instances.TryGetValue(target.Instance ?? "", out var i)
                ? (i.Tags ?? pack.MasterTagsOfUniqueItem(i.ItemCode)).ToHashSet() : new HashSet<long>(),
            EquipKind.CommonItem => pack.MasterTagsOfCommonItem(target.Code).ToHashSet(),
            EquipKind.Character => (characterTags.TryGetValue(target.Code, out var t) ? t : pack.MasterTagsOfCharacter(target.Code)).ToHashSet(),
            _ => new HashSet<long>(),
        };

    /// <summary>
    /// 이 캐릭터의 이 슬롯에 이것을 낄 수 있는가(보유 개수와 무관): 마스터/인스턴스에 있고, 캐릭터는 자기 자신이 아니며,
    /// (캐릭터의 태그 + 끼울 것의 태그)가 슬롯 규칙에 부합한다.
    /// </summary>
    static bool Fits(
        GamePack pack, Dictionary<string, UniqueItemInstance> instances, Dictionary<long, List<long>> characterTags,
        Dictionary<long, CharacterInstance> owned, long characterCode, long equipmentSlotCode, EquipRef target)
    {
        var exists = target.Kind switch
        {
            EquipKind.UniqueItem => instances.ContainsKey(target.Instance ?? ""),
            EquipKind.CommonItem => pack.FindItem(target.Code) is not null,
            EquipKind.Character => owned.ContainsKey(target.Code) && target.Code != characterCode,
            _ => false,
        };
        if (!exists) return false;

        var tags = TagsOfTarget(pack, instances, characterTags, target).ToHashSet();
        tags.UnionWith(TagsOfTarget(pack, instances, characterTags, EquipRef.Char(characterCode)));
        return pack.Fits(characterCode, equipmentSlotCode, tags);
    }

    // ---- 화면 ----

    /// <summary>장비 슬롯 목록의 항목 이름: "무기 : 철검", 비어 있으면 "무기 : (없음)".</summary>
    string EquipmentSlotLabel(EquipmentSlotEntry slot)
    {
        var item = CurrentCharacterCode is { } c ? EquippedItemOf(c, slot.Code) : null;
        return $"{slot.Name} : {(item is { } target ? NameOf(target) : "(없음)")}";
    }

    static string RefText(EquipRef target) => $"{target.Kind}:{target.Key}";

    static EquipRef ParseRef(string text)
    {
        var parts = text.Split(':');
        var kind = Enum.Parse<EquipKind>(parts[0]);
        return kind == EquipKind.UniqueItem
            ? EquipRef.Unique(parts[1])
            : new EquipRef(kind, long.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 장비 가능 목록 화면의 항목: 맨 위 "[0] 해제"(이 슬롯에 낀 것이 있을 때만), 낄 수 있는 것들(번호를 입력하거나
    /// 클릭하면 끼운다, 지금 끼고 있는 것은 "(장착중)"), 돌아가기. 열 때마다 OrderNumber순(같으면 InstanceId순)으로 새로 정렬하고,
    /// 유니크 아이템의 번호가 OrderNumber다(해제가 있으면 0이 쓰였으므로 1부터, 겹치면 다음 빈 번호).
    /// </summary>
    IReadOnlyList<ScreenEntry> EquipmentSelectEntries()
    {
        var character = CurrentCharacterCode!.Value;
        var slot = CurrentEquipmentSlotCode!.Value;
        var current = EquippedItemOf(character, slot);

        var entries = new List<ScreenEntry>();
        if (current is not null)
            entries.Add(new ScreenEntry(ClearEntryCode, "해제", Screen.EquipmentList, EntryAction.Unequip));
        // 유니크 아이템은 OrderNumber가 코드(같은 번호가 이미 쓰였으면 다음 빈 번호), 번호가 없는 커먼 아이템/캐릭터는 그 뒤로 이어서 붙인다.
        var used = new HashSet<long>();
        if (current is not null) used.Add(0);                 // 해제가 0번이면 번호는 1부터
        long nextFree(long from) { while (!used.Add(from)) from++; return from; }
        var next = current is not null ? 1L : 0L;
        foreach (var i in EquippableItems)
        {
            var order = i.Ref.Kind == EquipKind.UniqueItem && UniqueItemInstances.TryGetValue(i.Ref.Instance ?? "", out var u) ? u.OrderNumber : next;
            var code = nextFree(Math.Max(order, 0));
            next = Math.Max(next, code + 1);
            entries.Add(new ScreenEntry(Text(code),
                i.Ref == current ? $"{i.Name} (장착중)" : i.Name,
                Screen.EquipmentList,
                EntryAction.EquipItem,
                RefText(i.Ref)));
        }
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
        var item = NameOf(target);
        var previous = EquippedItemOf(character, slot);

        if (!Equip(character, slot, target)) return $"장착 실패: {who}에 {item}을(를) 낄 수 없습니다.";
        return previous == target ? $"이미 장착 중: {who} = {item}"
            : previous is { } old ? $"장착: {who} {NameOf(old)} → {item}"
            : $"장착: {who} ← {item}";
    }

    string UnequipFromScreen()
    {
        var character = CurrentCharacterCode!.Value;
        var slot = CurrentEquipmentSlotCode!.Value;
        var who = $"{CurrentSlotCharacterName}의 {CurrentEquipmentSlot?.Name}";
        var old = EquippedItemOf(character, slot);
        return Unequip(character, slot) ? $"해제: {who} {NameOf(old!.Value)}" : $"해제할 장비가 없습니다: {who}";
    }

    // ---- 세이브 ----

    Dictionary<string, UniqueItemInstance> CopyUniqueItems() =>
        UniqueItemInstances.ToDictionary(kv => kv.Key,
            kv => new UniqueItemInstance(kv.Key, kv.Value.ItemCode, kv.Value.OrderNumber, (kv.Value.Tags ?? Pack.MasterTagsOfUniqueItem(kv.Value.ItemCode)).ToList()));

    /// <summary>보유 캐릭터 전원의 현재 태그(아직 마스터 그대로인 캐릭터는 마스터에서 복사해 적는다).</summary>
    Dictionary<long, List<long>> CopyCharacterTags() =>
        Characters.Keys.ToDictionary(code => code,
            code => (CharacterTagInstances.TryGetValue(code, out var tags) ? tags : Pack.MasterTagsOfCharacter(code)).ToList());

    static List<long> CheckTags(GamePack pack, string who, IEnumerable<long> tags, InventoryKind kind, long code)
    {
        var list = new List<long>();
        foreach (var tag in tags)
        {
            if (!GamePack.MayHoldTag(tag, kind, code))
                throw new SaveFormatException($"세이브의 {who}에 태그 {tag}가 있습니다. 플레이어 태그({GamePack.PlayerTagCode})는 캐릭터 {GamePack.PlayerCharacterCode}만, 메인 통화 태그({GamePack.MainCurrencyTagCode})는 커먼 아이템 {GamePack.MainCurrencyItemCode}만 가질 수 있습니다.");
            if (pack.Tags is null || pack.Tags.All(t => t.Code != tag))
                throw new SaveFormatException($"세이브의 {who}의 태그 '{tag}'가 태그 마스터에 없습니다.");
            if (list.Contains(tag))
                throw new SaveFormatException($"세이브의 {who}에 태그 '{tag}'가 두 번 있습니다.");
            list.Add(tag);
        }
        return list;
    }

    /// <summary>
    /// 세이브의 유니크 아이템 인스턴스, 캐릭터 태그, 장착 상태를 검증하고 복사한다.
    /// 인스턴스의 유니크 아이템 코드는 마스터에 있어야 하고 ID는 1 이상, 태그가 비어(null) 있으면 마스터 태그를 복사한다.
    /// 캐릭터 태그는 보유 캐릭터의 것이어야 하고 없는 캐릭터는 마스터 태그를 복사한다. 태그는 태그 마스터에 있어야 하고 겹치면 안 된다.
    /// 장착한 캐릭터는 보유 캐릭터, 슬롯은 그 캐릭터의 장비 슬롯, 끼운 것은 있어야 하고 (캐릭터 태그 + 끼운 것의 태그)가
    /// 슬롯 규칙에 부합해야 하며, 같은 것을 두 곳에 끼거나 커먼 아이템을 인벤토리 수량보다 많이 끼면 안 된다.
    /// </summary>
    static (Dictionary<string, UniqueItemInstance> Instances, Dictionary<long, List<long>> CharacterTags,
        Dictionary<long, Dictionary<long, EquipRef>> Equipped) ValidateEquipment(
        GamePack pack, Dictionary<long, CharacterInstance> characters, Dictionary<long, InventoryItem> inventory,
        Dictionary<string, UniqueItemInstance>? savedInstances, Dictionary<long, List<long>>? savedCharacterTags,
        Dictionary<long, Dictionary<long, EquipRef>>? savedEquipped)
    {
        var instances = new Dictionary<string, UniqueItemInstance>();
        foreach (var (savedId, item) in savedInstances ?? [])
        {
            if (!Ulid.TryParse(savedId, out var ulid))
                throw new SaveFormatException($"세이브의 유니크 아이템 인스턴스 ID '{savedId}'가 ULID가 아닙니다.");
            var id = ulid.ToString();          // 소문자로 적혀 있어도 ToString 값으로 맞춘다
            if (instances.ContainsKey(id))
                throw new SaveFormatException($"세이브의 유니크 아이템 인스턴스 ID '{id}'가 두 번 있습니다.");
            if (item is null || pack.FindUniqueItem(item.ItemCode) is null)
                throw new SaveFormatException($"세이브의 유니크 아이템 인스턴스 '{id}'의 아이템이 유니크 아이템 마스터에 없습니다.");
            var tags = item.Tags is null
                ? pack.MasterTagsOfUniqueItem(item.ItemCode).ToList()
                : CheckTags(pack, $"유니크 아이템 인스턴스 '{id}'", item.Tags, InventoryKind.UniqueItem, item.ItemCode);
            if (!string.IsNullOrEmpty(item.InstanceId) && !(Ulid.TryParse(item.InstanceId, out var inner) && inner.ToString() == id))
                throw new SaveFormatException($"세이브의 유니크 아이템 인스턴스 '{id}'에 적힌 인스턴스 ID '{item.InstanceId}'가 키와 다릅니다.");
            instances[id] = new UniqueItemInstance(id, item.ItemCode, item.OrderNumber, tags);
        }

        var characterTags = new Dictionary<long, List<long>>();
        foreach (var (code, tags) in savedCharacterTags ?? [])
        {
            if (!characters.ContainsKey(code))
                throw new SaveFormatException($"세이브의 캐릭터 태그가 보유하지 않은 캐릭터 '{code}'의 것입니다.");
            characterTags[code] = CheckTags(pack, $"캐릭터 '{code}'", tags ?? [], InventoryKind.Character, code);
        }
        foreach (var code in characters.Keys.Where(c => !characterTags.ContainsKey(c)))
            characterTags[code] = pack.MasterTagsOfCharacter(code).ToList();
        // 보유한 캐릭터 1은 항상 플레이어 태그를 가진다.
        if (pack.Tags is not null && characterTags.TryGetValue(GamePack.PlayerCharacterCode, out var playerTags)
            && !playerTags.Contains(GamePack.PlayerTagCode))
            throw new SaveFormatException($"세이브의 캐릭터 {GamePack.PlayerCharacterCode}에 플레이어 태그({GamePack.PlayerTagCode})가 없습니다.");

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
                if (!Fits(pack, instances, characterTags, characters, character, slot, target))
                    throw new SaveFormatException(
                        $"세이브의 캐릭터 '{character}' 슬롯 '{slot}'에 {target.Kind} '{target.Key}'를 끼울 수 없습니다(없거나, 자기 자신이거나, 슬롯 태그에 부합하지 않음).");
                copy[slot] = target;
            }
            if (copy.Count > 0) equipped[character] = copy;
        }

        foreach (var group in equipped.Values.SelectMany(slots => slots.Values).GroupBy(i => i))
        {
            var have = group.Key.Kind == EquipKind.CommonItem ? inventory.GetValueOrDefault(group.Key.Code).Amount : 1;
            if (group.LongCount() > have)
                throw new SaveFormatException(
                    $"세이브에서 {group.Key.Kind} '{group.Key.Key}'를 {group.LongCount()}개 끼고 있는데 보유는 {have}개입니다.");
        }

        return (instances, characterTags, equipped);
    }
}
