using System.Globalization;
using Wera.Core.Masters;

namespace Wera.Core;

// 게임팩의 모든 TSV 마스터를 MasterMemory 데이터베이스에 올리고, 키로 찾는 조회는 여기서 한다.
// TSV 파싱과 외래키 검증(한국어 오류 메시지)은 기존 로더가 맡고, 그 결과를 테이블로 올린 뒤 Validate()로 한 번 더 확인한다.
// 순서가 필요한 목록(Dungeons, Shops, CharacterInventory 등)은 로더가 만든 목록을 그대로 쓴다(파일 순서 유지).
public sealed partial class GamePack
{
    /// <summary>
    /// 모든 마스터를 올린 읽기 전용 인메모리 데이터베이스. 마스터 파일이 없으면 그 테이블은 비어 있다
    /// (파일이 있었는지는 <see cref="Dungeons"/> 같은 목록이 null인지로 안다).
    /// </summary>
    public required MemoryDatabase Masters { get; init; }

    /// <summary>
    /// 로더가 만든 마스터들을 테이블로 올려 데이터베이스를 만든다. 기본키 중복 같은 위반은 Build/로드 단계에서는 잡히지 않으므로
    /// 반드시 Validate()를 호출한다. 로더가 이미 검증하므로 여기서 실패하면 로더나 테이블 정의의 버그다.
    /// </summary>
    static MemoryDatabase BuildMasters(
        List<Dungeon>? dungeons, List<FlatCondition> dungeonConditions,
        List<Shop>? shops, List<FlatCondition> shopConditions,
        List<ItemEntry>? items, List<BunchEntry>? bunches, List<CharacterEntry>? characters, List<AttributeEntry>? attributes,
        List<ShopTrade>? trades, List<FlatCondition> tradeConditions,
        List<StatusEffectEntry>? statusEffects, List<StatusEffectEffect>? statusEffectEffects,
        List<EquipmentSlotEntry>? equipmentSlots, List<TagEntry>? tags, List<UniqueItem>? uniqueItems,
        List<UniqueItemTag>? uniqueItemTags, List<CommonItemTag>? commonItemTags, List<CharacterTag>? characterTags,
        List<EquipmentSlotTag>? equipmentSlotTags,
        List<CharacterStatusEntry>? characterStatuses, List<CharacterInitialStatus>? characterInitialStatuses)
    {
        var builder = new DatabaseBuilder();

        builder.Append((attributes ?? []).Select((a, i) => new AttributeRow { Code = a.Code, Name = a.Name, Order = i }).ToList());
        builder.Append((statusEffects ?? []).Select((s, i) => new StatusEffectRow { Code = s.Code, Name = s.Name, Order = i }).ToList());
        builder.Append((statusEffectEffects ?? []).Select((e, i) => new StatusEffectEffectRow
        {
            Order = i, StatusEffectCode = e.StatusEffectCode, Trigger = e.Trigger, Priority = e.Priority,
            EffectCode = e.EffectCode, Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3,
        }).ToList());

        builder.Append((dungeons ?? []).Select((d, i) => new DungeonRow { Code = d.Code, Name = d.Name, Order = i }).ToList());
        builder.Append(dungeonConditions.Select((c, i) => new DungeonFlagRow
        {
            Order = i, DungeonCode = c.Code, GroupNo = c.GroupNo,
            FlagCode = c.Condition.FlagCode, Type = c.Condition.Type, Value = c.Condition.Value,
        }).ToList());

        builder.Append((shops ?? []).Select((s, i) => new ShopRow { Code = s.Code, Name = s.Name, Order = i }).ToList());
        builder.Append(shopConditions.Select((c, i) => new ShopConditionRow
        {
            Order = i, ShopCode = c.Code, GroupNo = c.GroupNo,
            FlagCode = c.Condition.FlagCode, Type = c.Condition.Type, Value = c.Condition.Value,
        }).ToList());

        builder.Append((items ?? []).Select((x, i) => new ItemInventoryRow
            { Code = x.Code, Name = x.Name, CarryLimit = x.CarryLimit, Order = i }).ToList());

        var order = 0;
        builder.Append((bunches ?? []).SelectMany(b => b.Parts.Select(p => new BunchInventoryRow
            { ItemCode = b.Code, PartItemCode = p.ItemCode, ItemName = b.Name, PartAmount = p.Amount, Order = order++ })).ToList());

        builder.Append((characters ?? []).Select((c, i) => new CharacterInventoryRow
        {
            Code = c.Code, Name = c.Name, CarryLimit = c.CarryLimit,
            AdvantageAttributeCode = c.AdvantageAttributeCode, DisadvantageAttributeCode = c.DisadvantageAttributeCode, Order = i,
        }).ToList());

        builder.Append((trades ?? []).Select((t, i) => new ShopTradeRow
        {
            Code = t.Code, ShopCode = t.ShopCode, CostItemCode = t.CostItemCode, CostAmount = t.CostAmount,
            ReceiveItemCode = t.ReceiveItemCode, ReceiveAmount = t.ReceiveAmount, Order = i,
        }).ToList());
        builder.Append(tradeConditions.Select((c, i) => new ShopTradeConditionRow
        {
            Order = i, TradeCode = c.Code, GroupNo = c.GroupNo,
            FlagCode = c.Condition.FlagCode, Type = c.Condition.Type, Value = c.Condition.Value,
        }).ToList());

        builder.Append((equipmentSlots ?? []).Select((s, i) => new EquipmentSlotRow { Code = s.Code, Name = s.Name, Order = i }).ToList());
        builder.Append((tags ?? []).Select((t, i) => new TagRow { Code = t.Code, Name = t.Name, Order = i }).ToList());
        builder.Append((uniqueItems ?? []).Select((e, i) => new UniqueItemRow
            { ItemCode = e.ItemCode, ItemName = e.ItemName, Order = i }).ToList());
        builder.Append((uniqueItemTags ?? []).Select((t, i) => new UniqueItemTagRow
            { ItemCode = t.ItemCode, TagCode = t.TagCode, Order = i }).ToList());
        builder.Append((commonItemTags ?? []).Select((t, i) => new CommonItemTagRow
            { ItemCode = t.ItemCode, TagCode = t.TagCode, Order = i }).ToList());
        builder.Append((characterTags ?? []).Select((t, i) => new CharacterTagRow
            { CharacterCode = t.CharacterCode, TagCode = t.TagCode, Order = i }).ToList());
        builder.Append((equipmentSlotTags ?? []).Select((t, i) => new EquipmentSlotTagRow
        {
            CharacterCode = t.CharacterCode, EquipmentSlotCode = t.EquipmentSlotCode, TagCode = t.TagCode,
            GroupNumber = t.GroupNumber, IsPositive = t.IsPositive, Order = i,
        }).ToList());

        builder.Append((characterStatuses ?? []).Select((s, i) => new CharacterStatusRow { Code = s.Code, Name = s.Name, Order = i }).ToList());
        builder.Append((characterInitialStatuses ?? []).Select((s, i) => new CharacterInitialStatusRow
            { CharacterCode = s.CharacterCode, StatusCode = s.StatusCode, InitialValue = s.InitialValue, Order = i }).ToList());

        var db = new MemoryDatabase(builder.Build());
        var result = db.Validate();
        if (result.IsValidationFailed)
            throw new PackFormatException($"마스터 검증에 실패했습니다: {result.FormatFailedResults()}");
        return db;
    }

    // ---- 키로 찾기 (기본키 조회) ----

    public CharacterEntry? FindCharacter(long code) =>
        Masters.CharacterInventoryRowTable.TryFindByCode(code, out var r)
            ? new CharacterEntry(r.Code, r.Name, r.CarryLimit, r.AdvantageAttributeCode, r.DisadvantageAttributeCode)
            : null;

    public AttributeEntry? FindAttribute(long code) =>
        Masters.AttributeRowTable.TryFindByCode(code, out var r) ? new AttributeEntry(r.Code, r.Name) : null;

    public StatusEffectEntry? FindStatusEffect(long code) =>
        Masters.StatusEffectRowTable.TryFindByCode(code, out var r) ? new StatusEffectEntry(r.Code, r.Name) : null;

    public ItemEntry? FindItem(long code) =>
        Masters.ItemInventoryRowTable.TryFindByCode(code, out var r) ? new ItemEntry(r.Code, r.Name, r.CarryLimit) : null;

    /// <summary>번치(부품은 게임팩 파일 순서). 없으면 null.</summary>
    public BunchEntry? FindBunch(long code)
    {
        var rows = Masters.BunchInventoryRowTable.FindByItemCode(code).OrderBy(r => r.Order).ToList();
        return rows.Count == 0
            ? null
            : new BunchEntry(code, rows[0].ItemName, rows.Select(r => new BunchPart(r.PartItemCode, r.PartAmount)).ToList());
    }

    public EquipmentSlotEntry? FindEquipmentSlot(long code) =>
        Masters.EquipmentSlotRowTable.TryFindByCode(code, out var r) ? new EquipmentSlotEntry(r.Code, r.Name) : null;

    public UniqueItem? FindUniqueItem(long itemCode) =>
        Masters.UniqueItemRowTable.TryFindByItemCode(itemCode, out var r) ? new UniqueItem(r.ItemCode, r.ItemName) : null;

    /// <summary>상태이상이 가진 이펙트 행들(게임팩 파일 순서).</summary>
    public IReadOnlyList<StatusEffectEffect> StatusEffectEffectsOf(long statusEffectCode) =>
        Masters.StatusEffectEffectRowTable.FindByStatusEffectCode(statusEffectCode)
            .OrderBy(r => r.Order)
            .Select(r => new StatusEffectEffect(
                r.StatusEffectCode, r.Trigger, r.Priority, r.EffectCode, r.Value1, r.Value2, r.Value3))
            .ToList();

    /// <summary>한 캐릭터의 한 장비 슬롯에 걸린 태그 규칙(게임팩 파일 순서). 보조키 (캐릭터, 슬롯)로 찾는다.</summary>
    IReadOnlyList<EquipmentSlotTag> SlotTagRulesOf(long characterCode, long equipmentSlotCode) =>
        Masters.EquipmentSlotTagRowTable.FindByCharacterCodeAndEquipmentSlotCode((characterCode, equipmentSlotCode))
            .OrderBy(r => r.Order)
            .Select(r => new EquipmentSlotTag(r.CharacterCode, r.EquipmentSlotCode, r.TagCode, r.GroupNumber, r.IsPositive))
            .ToList();

    /// <summary>유니크 아이템 마스터에 적힌 태그 코드들(인스턴스가 만들어질 때 복사되는 태그).</summary>
    public IReadOnlyList<long> MasterTagsOfUniqueItem(long itemCode) =>
        Masters.UniqueItemTagRowTable.FindByItemCode(itemCode).OrderBy(r => r.Order).Select(r => r.TagCode).ToList();

    /// <summary>커먼 아이템의 태그 코드들. 커먼 아이템은 인스턴스가 없어 늘 마스터의 태그다.</summary>
    public IReadOnlyList<long> MasterTagsOfCommonItem(long itemCode) =>
        Masters.CommonItemTagRowTable.FindByItemCode(itemCode).OrderBy(r => r.Order).Select(r => r.TagCode).ToList();

    /// <summary>캐릭터 태그 마스터에 적힌 태그 코드들(캐릭터를 얻을 때 복사되는 태그).</summary>
    public IReadOnlyList<long> MasterTagsOfCharacter(long characterCode) =>
        Masters.CharacterTagRowTable.FindByCharacterCode(characterCode).OrderBy(r => r.Order).Select(r => r.TagCode).ToList();

    string CodeText(long code) => code.ToString(CultureInfo.InvariantCulture);
}
