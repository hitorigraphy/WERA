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
    /// 인벤토리 코드로 찾는 공통 사전: 커먼 아이템, 유니크 아이템, 번치 아이템, 캐릭터 인벤토리 마스터의 모든 항목이 들어 있고
    /// 코드는 겹치지 않는다. 읽지 않은(없는) 마스터의 항목은 없다.
    /// </summary>
    public required IReadOnlyDictionary<long, InventoryEntry> Inventory { get; init; }

    /// <summary>인벤토리 코드의 항목. 어느 인벤토리 마스터에도 없으면 null.</summary>
    public InventoryEntry? FindInventory(long inventoryCode) => Inventory.GetValueOrDefault(inventoryCode);

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
        List<StatusEntry>? statuses, List<CharacterStatus>? characterStatuses,
        List<TimeSlotEntry>? timeSlots, List<TurnEntry>? turns,
        List<GameEvent>? events, List<FlatCondition> eventConditions, List<EventFlow>? eventFlows,
        List<AdvEntry>? advs, List<BattleEntry>? battles, List<AdvFlow>? advFlows,
        List<CommunicationGroupEntry>? communicationGroups, List<CommunicationEntry>? communications,
        List<CommunicationSoloFlow>? communicationSoloFlows, List<CommunicationDuoFlow>? communicationDuoFlows,
        List<CommunicationDefaultFlow>? communicationDefaultFlows,
        List<PresetUnit>? presetUnits, List<PresetUnitEquip>? presetUnitEquips, List<PresetUnitDrop>? presetUnitDrops,
        List<PresetParty>? presetParties, List<BattleEnemy>? battleEnemies,
        List<TileEffect>? tileEffects, List<DungeonTile>? dungeonTiles, List<DungeonMapLine>? dungeonMaps,
        List<DungeonEncounter>? dungeonEncounters,
        List<SkillSlotEntry>? skillSlots, List<CharacterSkillSlotTag>? characterSkillSlotTags,
        List<SkillEntry>? skills, List<SkillTag>? skillTags, List<SkillExecuteEffect>? skillExecuteEffects,
        List<SkillOrderEffect>? skillOrderEffects, List<SkillAutoTargetEffect>? skillAutoTargetEffects,
        List<OwnerStatus>? skillStatuses, List<OwnerStatus>? uniqueItemStatuses,
        List<LoadCharacterFlow>? afterLoadCharacterFlows, List<LoadUniqueItemFlow>? afterLoadUniqueItemFlows,
        List<AfterHitEffect>? afterHitEffects, List<AfterAttackEffect>? afterAttackEffects,
        List<AfterBattleWinEffect>? afterBattleWinEffects,
        List<AfterBattleLoseEffect>? afterBattleLoseEffects,
        List<AfterBattleRunEffect>? afterBattleRunEffects,
        List<CharacterAfterCreateEffect>? characterAfterCreateEffects, List<UniqueItemAfterCreateEffect>? uniqueItemAfterCreateEffects)
    {
        var builder = new DatabaseBuilder();

        builder.Append((attributes ?? []).Select(a => new AttributeRow { Code = a.Code, Name = a.Name }).ToList());
        builder.Append((statusEffects ?? []).Select(s => new StatusEffectRow { Code = s.Code, Name = s.Name }).ToList());
        builder.Append((statusEffectEffects ?? []).Select(e => new StatusEffectEffectRow
        { StatusEffectCode = e.StatusEffectCode, Trigger = e.Trigger, Priority = e.Priority,
            EffectCode = e.EffectCode, Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3,
        }).ToList());

        builder.Append((dungeons ?? []).Select(d => new DungeonRow
            { Code = d.Code, Name = d.Name, EntryLayerNumber = d.Entry?.LayerNumber, EntryPosX = d.Entry?.PosX, EntryPosY = d.Entry?.PosY, EncounterRate = d.EncounterRate }).ToList());
        builder.Append(dungeonConditions.Select(c => new DungeonFlagRow
        { DungeonCode = c.Code, GroupNumber = c.GroupNumber,
            FlagCode = c.Condition.FlagCode, Type = c.Condition.Type, Value = c.Condition.Value,
        }).ToList());

        builder.Append((shops ?? []).Select(s => new ShopRow { Code = s.Code, Name = s.Name }).ToList());
        builder.Append(shopConditions.Select(c => new ShopConditionRow
        { ShopCode = c.Code, GroupNumber = c.GroupNumber,
            FlagCode = c.Condition.FlagCode, Type = c.Condition.Type, Value = c.Condition.Value,
        }).ToList());

        builder.Append((items ?? []).Select(x => new ItemInventoryRow
            { Code = x.Code, Name = x.Name, CarryLimit = x.CarryLimit, SkillCode = x.SkillCode }).ToList());

        builder.Append((bunches ?? []).SelectMany(b => b.Parts.Select(p => new BunchInventoryRow
            { ItemCode = b.Code, PartItemCode = p.ItemCode, ItemName = b.Name, PartAmount = p.Amount })).ToList());

        builder.Append((characters ?? []).Select(c => new CharacterInventoryRow
        {
            Code = c.Code, Name = c.Name, CarryLimit = c.CarryLimit,
            AdvantageAttributeCode = c.AdvantageAttributeCode, DisadvantageAttributeCode = c.DisadvantageAttributeCode, Description = c.Description, XpTypeCode = c.XpTypeCode,
        }).ToList());

        builder.Append((trades ?? []).Select(t => new ShopTradeRow
        {
            Code = t.Code, ShopCode = t.ShopCode, CostItemCode = t.CostItemCode, CostAmount = t.CostAmount,
            ReceiveItemCode = t.ReceiveItemCode, ReceiveAmount = t.ReceiveAmount,
            CostKind = t.CostKind, ReceiveKind = t.ReceiveKind,
        }).ToList());
        builder.Append(tradeConditions.Select(c => new ShopTradeConditionRow
        { TradeCode = c.Code, GroupNumber = c.GroupNumber,
            FlagCode = c.Condition.FlagCode, Type = c.Condition.Type, Value = c.Condition.Value,
        }).ToList());

        builder.Append((equipmentSlots ?? []).Select(s => new EquipmentSlotRow { Code = s.Code, Name = s.Name }).ToList());
        builder.Append((tags ?? []).Select(t => new TagRow { Code = t.Code, Name = t.Name }).ToList());
        builder.Append((uniqueItems ?? []).Select(e => new UniqueItemRow
            { ItemCode = e.ItemCode, ItemName = e.ItemName, SkillCode = e.SkillCode }).ToList());
        builder.Append((uniqueItemTags ?? []).Select(t => new UniqueItemTagRow
            { ItemCode = t.ItemCode, TagCode = t.TagCode }).ToList());
        builder.Append((commonItemTags ?? []).Select(t => new CommonItemTagRow
            { ItemCode = t.ItemCode, TagCode = t.TagCode }).ToList());
        builder.Append((characterTags ?? []).Select(t => new CharacterTagRow
            { CharacterCode = t.CharacterCode, TagCode = t.TagCode }).ToList());
        builder.Append((equipmentSlotTags ?? []).Select(t => new EquipmentSlotTagRow
        {
            CharacterCode = t.CharacterCode, EquipmentSlotCode = t.EquipmentSlotCode, TagCode = t.TagCode,
            GroupNumber = t.GroupNumber, IsPositive = t.IsPositive,
        }).ToList());

        builder.Append((statuses ?? []).Select(s => new StatusRow { Code = s.Code, Name = s.Name, SpecDisplayOrder = s.SpecDisplayOrder }).ToList());
        builder.Append((characterStatuses ?? []).Select(s => new CharacterStatusRow
        {
            CharacterCode = s.CharacterCode, StatusCode = s.StatusCode, Level = s.Level,
            InitialValue = s.InitialValue, MinValue = s.MinValue, MaxValue = s.MaxValue,
        }).ToList());

        builder.Append((communicationGroups ?? []).Select(g => new CommunicationGroupRow { Code = g.Code, Name = g.Name }).ToList());
        builder.Append((communications ?? []).Select(c => new CommunicationRow { Code = c.Code, GroupCode = c.GroupCode, Name = c.Name, IsAssist = c.IsAssist }).ToList());
        builder.Append((communicationSoloFlows ?? []).Select(f => new CommunicationSoloFlowRow
        {
            CommunicationCode = f.CommunicationCode, CharacterCode = f.CharacterCode, FlowNumber = f.FlowNumber, FlowType = f.FlowType,
            Param1 = f.Param1, Param2 = f.Param2, Param3 = f.Param3, Text = f.Text,
        }).ToList());
        builder.Append((communicationDuoFlows ?? []).Select(f => new CommunicationDuoFlowRow
        {
            CommunicationCode = f.CommunicationCode, PartnerCode = f.PartnerCode, AssistCode = f.AssistCode,
            FlowNumber = f.FlowNumber, FlowType = f.FlowType,
            Param1 = f.Param1, Param2 = f.Param2, Param3 = f.Param3, Text = f.Text,
        }).ToList());
        builder.Append((communicationDefaultFlows ?? []).Select(f => new CommunicationDefaultFlowRow
        {
            CommunicationCode = f.CommunicationCode, FlowNumber = f.FlowNumber, FlowType = f.FlowType,
            Param1 = f.Param1, Param2 = f.Param2, Param3 = f.Param3, Text = f.Text,
        }).ToList());
        builder.Append((timeSlots ?? []).Select(s => new TimeSlotRow { Code = s.Code, Name = s.Name }).ToList());
        builder.Append((turns ?? []).Select(t => new TurnRow
            { TurnCount = t.TurnCount, Year = t.Year, Month = t.Month, Day = t.Day, TimeSlotCode = t.TimeSlotCode }).ToList());

        builder.Append((events ?? []).Select(e => new EventRow { Code = e.Code, Name = e.Name }).ToList());
        builder.Append(eventConditions.Select(c => new EventConditionRow
        {
            EventCode = c.Code, GroupNumber = c.GroupNumber,
            FlagCode = c.Condition.FlagCode, Type = c.Condition.Type, Value = c.Condition.Value,
        }).ToList());
        builder.Append((eventFlows ?? []).Select(f => new EventFlowRow
        {
            EventCode = f.EventCode, FlowNumber = f.FlowNumber, FlowType = f.FlowType,
            Param1 = f.Param1, Param2 = f.Param2, Param3 = f.Param3,
        }).ToList());
        builder.Append((advs ?? []).Select(a => new AdvRow { Code = a.Code, Name = a.Name }).ToList());
        builder.Append((advFlows ?? []).Select(f => new AdvFlowRow
        {
            AdvCode = f.AdvCode, FlowNumber = f.FlowNumber, FlowType = f.FlowType,
            Param1 = f.Param1, Param2 = f.Param2, Param3 = f.Param3, Text = f.Text, ForceName = f.ForceName,
        }).ToList());
        builder.Append((battles ?? []).Select(b => new BattleRow { Code = b.Code, Name = b.Name, CanRun = b.CanRun }).ToList());

        builder.Append((tileEffects ?? []).Select(t => new TileEffectRow
            { TileCode = t.TileCode, EffectType = t.EffectType, Value1 = t.Value1, Value2 = t.Value2, Value3 = t.Value3 }).ToList());
        builder.Append((dungeonTiles ?? []).Select(t => new DungeonTileRow
            { DungeonCode = t.DungeonCode, LocalTileText = t.LocalTileText, TileCode = t.TileCode }).ToList());
        builder.Append((dungeonMaps ?? []).Select(m => new DungeonMapRow
            { DungeonCode = m.DungeonCode, LayerNumber = m.LayerNumber, PosY = m.PosY, Cells = [.. m.Cells] }).ToList());
        builder.Append((dungeonEncounters ?? []).Select(e => new DungeonEncounterRow
            { DungeonCode = e.DungeonCode, LayerNumber = e.LayerNumber, BattleCode = e.BattleCode, Weight = e.Weight }).ToList());
        builder.Append((presetUnits ?? []).Select(u => new PresetUnitRow { Code = u.Code, CharacterCode = u.CharacterCode }).ToList());
        builder.Append((presetUnitEquips ?? []).Select(e => new PresetUnitEquipRow
            { PresetUnitCode = e.PresetUnitCode, EquipmentSlotCode = e.EquipmentSlotCode, ItemCode = e.ItemCode }).ToList());
        builder.Append((presetUnitDrops ?? []).Select(d => new PresetUnitDropRow
            { PresetUnitCode = d.PresetUnitCode, ItemCode = d.ItemCode, Amount = d.Amount }).ToList());
        builder.Append((presetParties ?? []).Select(p => new PresetPartyRow
        {
            Code = p.Code, Name = p.Name,
            FrontLeftUnitCode = p.UnitCodes[0], FrontCenterUnitCode = p.UnitCodes[1], FrontRightUnitCode = p.UnitCodes[2],
            MiddleLeftUnitCode = p.UnitCodes[3], MiddleCenterUnitCode = p.UnitCodes[4], MiddleRightUnitCode = p.UnitCodes[5],
            BackLeftUnitCode = p.UnitCodes[6], BackCenterUnitCode = p.UnitCodes[7], BackRightUnitCode = p.UnitCodes[8],
        }).ToList());
        builder.Append((battleEnemies ?? []).Select(b => new BattleEnemyRow
            { BattleCode = b.BattleCode, PresetPartyCode = b.PresetPartyCode, Weight = b.Weight }).ToList());

        builder.Append((skillSlots ?? []).Select(s => new SkillSlotRow { Code = s.Code, Name = s.Name }).ToList());
        builder.Append((characterSkillSlotTags ?? []).Select(t => new CharacterSkillSlotTagRow
            { CharacterCode = t.CharacterCode, SkillSlotCode = t.SkillSlotCode, GroupNumber = t.GroupNumber, TagCode = t.TagCode, IsPositive = t.IsPositive }).ToList());
        builder.Append((afterLoadCharacterFlows ?? []).Select(f => new AfterLoadCharacterFlowRow
        { ExecuteNumber = f.ExecuteNumber, FlowType = f.FlowType, Value1 = f.Value1, Value2 = f.Value2, Value3 = f.Value3 }).ToList());
        builder.Append((afterLoadUniqueItemFlows ?? []).Select(f => new AfterLoadUniqueItemFlowRow
        { ExecuteNumber = f.ExecuteNumber, FlowType = f.FlowType, Value1 = f.Value1, Value2 = f.Value2, Value3 = f.Value3 }).ToList());
        builder.Append((afterHitEffects ?? []).Select(e => new AfterHitEffectRow
        { ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType, Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3 }).ToList());
        builder.Append((afterAttackEffects ?? []).Select(e => new AfterAttackEffectRow
        { ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType, Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3 }).ToList());
        builder.Append((afterBattleWinEffects ?? []).Select(e => new AfterBattleWinEffectRow
        { ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType, Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3 }).ToList());
        builder.Append((afterBattleLoseEffects ?? []).Select(e => new AfterBattleLoseEffectRow
        { ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType, Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3 }).ToList());
        builder.Append((afterBattleRunEffects ?? []).Select(e => new AfterBattleRunEffectRow
        { ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType, Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3 }).ToList());
        builder.Append((characterAfterCreateEffects ?? []).Select(e => new CharacterAfterCreateEffectRow
        { CharacterCode = e.CharacterCode, ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType, Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3 }).ToList());
        builder.Append((uniqueItemAfterCreateEffects ?? []).Select(e => new UniqueItemAfterCreateEffectRow
        { ItemCode = e.ItemCode, ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType, Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3 }).ToList());
        builder.Append((skillStatuses ?? []).Select(s => new SkillStatusRow
        { SkillCode = s.OwnerCode, StatusCode = s.StatusCode, InitialValue = s.InitialValue, MinValue = s.MinValue, MaxValue = s.MaxValue }).ToList());
        builder.Append((uniqueItemStatuses ?? []).Select(s => new UniqueItemStatusRow
        { ItemCode = s.OwnerCode, StatusCode = s.StatusCode, InitialValue = s.InitialValue, MinValue = s.MinValue, MaxValue = s.MaxValue }).ToList());
        builder.Append((skills ?? []).Select(s => new SkillRow { Code = s.Code, Name = s.Name, MainTargetType = (int)s.MainTargetType, SubTargetType = (int)s.SubTargetType }).ToList());
        builder.Append((skillTags ?? []).Select(t => new SkillTagRow { SkillCode = t.SkillCode, TagCode = t.TagCode }).ToList());
        builder.Append((skillExecuteEffects ?? []).Select(e => new SkillExecuteEffectRow
        { SkillCode = e.SkillCode, ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType,
            Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3,
        }).ToList());
        builder.Append((skillOrderEffects ?? []).Select(e => new SkillOrderEffectRow
        { SkillCode = e.SkillCode, ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType,
            Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3,
        }).ToList());
        builder.Append((skillAutoTargetEffects ?? []).Select(e => new SkillAutoTargetEffectRow
        { SkillCode = e.SkillCode, ExecuteNumber = e.ExecuteNumber, EffectType = e.EffectType,
            Value1 = e.Value1, Value2 = e.Value2, Value3 = e.Value3,
        }).ToList());

        var db = new MemoryDatabase(builder.Build());
        var result = db.Validate();
        if (result.IsValidationFailed)
            throw new PackFormatException($"마스터 검증에 실패했습니다: {result.FormatFailedResults()}");
        return db;
    }

    // ---- 키로 찾기 (기본키 조회) ----

    public CharacterEntry? FindCharacter(long code) =>
        Masters.CharacterInventoryRowTable.TryFindByCode(code, out var r)
            ? new CharacterEntry(r.Code, r.Name, r.CarryLimit, r.AdvantageAttributeCode, r.DisadvantageAttributeCode, r.Description, r.XpTypeCode)
            : null;

    public AttributeEntry? FindAttribute(long code) =>
        Masters.AttributeRowTable.TryFindByCode(code, out var r) ? new AttributeEntry(r.Code, r.Name) : null;

    public StatusEffectEntry? FindStatusEffect(long code) =>
        Masters.StatusEffectRowTable.TryFindByCode(code, out var r) ? new StatusEffectEntry(r.Code, r.Name) : null;

    /// <summary>메인 통화 아이템(커먼 아이템 2): 필수 마스터라 로드된 게임팩에는 늘 있다. 이름은 게임팩 마음대로다.</summary>
    public ItemEntry MainCurrency => FindItem(MainCurrencyItemCode)!;

    public ItemEntry? FindItem(long code) =>
        Masters.ItemInventoryRowTable.TryFindByCode(code, out var r) ? new ItemEntry(r.Code, r.Name, r.CarryLimit, r.SkillCode) : null;

    /// <summary>번치(부품은 부품 아이템 코드 순). 없으면 null.</summary>
    public BunchEntry? FindBunch(long code)
    {
        var rows = Masters.BunchInventoryRowTable.FindByItemCode(code).OrderBy(r => r.PartItemCode).ToList();
        return rows.Count == 0
            ? null
            : new BunchEntry(code, rows[0].ItemName, rows.Select(r => new BunchPart(r.PartItemCode, r.PartAmount)).ToList());
    }

    public EquipmentSlotEntry? FindEquipmentSlot(long code) =>
        Masters.EquipmentSlotRowTable.TryFindByCode(code, out var r) ? new EquipmentSlotEntry(r.Code, r.Name) : null;

    public UniqueItem? FindUniqueItem(long itemCode) =>
        Masters.UniqueItemRowTable.TryFindByItemCode(itemCode, out var r) ? new UniqueItem(r.ItemCode, r.ItemName, r.SkillCode) : null;

    /// <summary>상태이상이 가진 이펙트 행들(키 순서: 트리거, 이펙트 코드).</summary>
    public IReadOnlyList<StatusEffectEffect> StatusEffectEffectsOf(long statusEffectCode) =>
        Masters.StatusEffectEffectRowTable.FindByStatusEffectCode(statusEffectCode)
            .OrderBy(r => r.Trigger).ThenBy(r => r.EffectCode)
            .Select(r => new StatusEffectEffect(
                r.StatusEffectCode, r.Trigger, r.Priority, r.EffectCode, r.Value1, r.Value2, r.Value3))
            .ToList();

    /// <summary>한 캐릭터의 한 장비 슬롯에 걸린 태그 규칙(키 순서: 태그, 그룹). 보조키 (캐릭터, 슬롯)로 찾는다.</summary>
    IReadOnlyList<EquipmentSlotTag> SlotTagRulesOf(long characterCode, long equipmentSlotCode) =>
        Masters.EquipmentSlotTagRowTable.FindByCharacterCodeAndEquipmentSlotCode((characterCode, equipmentSlotCode))
            .OrderBy(r => r.TagCode).ThenBy(r => r.GroupNumber)
            .Select(r => new EquipmentSlotTag(r.CharacterCode, r.EquipmentSlotCode, r.TagCode, r.GroupNumber, r.IsPositive))
            .ToList();

    /// <summary>유니크 아이템 마스터에 적힌 태그 코드들(인스턴스가 만들어질 때 복사되는 태그).</summary>
    public IReadOnlyList<long> MasterTagsOfUniqueItem(long itemCode) =>
        Masters.UniqueItemTagRowTable.FindByItemCode(itemCode).OrderBy(r => r.TagCode).Select(r => r.TagCode).ToList();

    /// <summary>커먼 아이템의 태그 코드들. 커먼 아이템은 인스턴스가 없어 늘 마스터의 태그다.</summary>
    public IReadOnlyList<long> MasterTagsOfCommonItem(long itemCode) =>
        Masters.CommonItemTagRowTable.FindByItemCode(itemCode).OrderBy(r => r.TagCode).Select(r => r.TagCode).ToList();

    /// <summary>캐릭터 태그 마스터에 적힌 태그 코드들(캐릭터를 얻을 때 복사되는 태그).</summary>
    public IReadOnlyList<long> MasterTagsOfCharacter(long characterCode) =>
        Masters.CharacterTagRowTable.FindByCharacterCode(characterCode).OrderBy(r => r.TagCode).Select(r => r.TagCode).ToList();

    /// <summary>교환의 코스트/리시브 이름: 커먼·번치 아이템, 유니크 아이템, 캐릭터. 마스터에 없으면 코드 그대로.</summary>
    public string TradeTargetName(TradeKind kind, long code) => kind switch
    {
        TradeKind.UniqueItem => FindUniqueItem(code)?.ItemName ?? CodeText(code),
        TradeKind.Character => FindCharacter(code)?.Name ?? CodeText(code),
        _ => ItemName(code),
    };

    string CodeText(long code) => code.ToString(CultureInfo.InvariantCulture);
}
