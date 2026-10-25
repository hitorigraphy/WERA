using MasterMemory;
using MessagePack;

// 생성되는 테이블/데이터베이스 코드를 행 타입과 같은 네임스페이스(Wera.Core.Masters) 아래에 둔다.
[assembly: MasterMemoryGeneratorOptions(Namespace = "Wera.Core.Masters")]

namespace Wera.Core.Masters;

// MasterMemory 테이블 정의. 게임팩의 TSV 마스터 한 개가 테이블 한 개이고, 행 한 개가 TSV의 한 줄이다.
// Order는 게임팩 파일에서의 행 순서다(MasterMemory는 키 순서로 정렬해 두므로, 파일 순서가 필요한 목록은 Order로 다시 정렬한다).
// 키가 따로 정해지지 않은 조건표는 Order를 기본키로 쓴다.

[MemoryTable("attribute"), MessagePackObject(true)]
public record AttributeRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("status_effect"), MessagePackObject(true)]
public record StatusEffectRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("status_effect_effect"), MessagePackObject(true)]
public record StatusEffectEffectRow
{
    [PrimaryKey] public required int Order { get; init; }
    [SecondaryKey(0), NonUnique] public required long StatusEffectCode { get; init; }
    public required TriggerType Trigger { get; init; }
    public required long Priority { get; init; }
    public required long EffectCode { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

[MemoryTable("dungeon"), MessagePackObject(true)]
public record DungeonRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("dungeon_flag"), MessagePackObject(true)]
public record DungeonFlagRow
{
    [PrimaryKey] public required int Order { get; init; }
    [SecondaryKey(0), NonUnique] public required long DungeonCode { get; init; }
    public required int GroupNo { get; init; }
    public required long FlagCode { get; init; }
    public required ConditionType Type { get; init; }
    public required long Value { get; init; }
}

[MemoryTable("shop"), MessagePackObject(true)]
public record ShopRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("shop_condition"), MessagePackObject(true)]
public record ShopConditionRow
{
    [PrimaryKey] public required int Order { get; init; }
    [SecondaryKey(0), NonUnique] public required long ShopCode { get; init; }
    public required int GroupNo { get; init; }
    public required long FlagCode { get; init; }
    public required ConditionType Type { get; init; }
    public required long Value { get; init; }
}

[MemoryTable("common_item_inventory"), MessagePackObject(true)]
public record ItemInventoryRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required long CarryLimit { get; init; }
    public required int Order { get; init; }
}

/// <summary>번치 인벤토리: 한 행이 번치의 부품 하나다. 같은 (번치, 부품)은 한 번만 쓸 수 있다.</summary>
[MemoryTable("bunch_item_inventory"), MessagePackObject(true)]
public record BunchInventoryRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long ItemCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    public required long PartItemCode { get; init; }

    public required string ItemName { get; init; }
    public required long PartAmount { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("character_inventory"), MessagePackObject(true)]
public record CharacterInventoryRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required long CarryLimit { get; init; }
    public required long AdvantageAttributeCode { get; init; }
    public required long DisadvantageAttributeCode { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("shop_trade"), MessagePackObject(true)]
public record ShopTradeRow
{
    [PrimaryKey] public required long Code { get; init; }
    [SecondaryKey(0), NonUnique] public required long ShopCode { get; init; }
    public required long CostItemCode { get; init; }
    public required long CostAmount { get; init; }
    public required long ReceiveItemCode { get; init; }
    public required long ReceiveAmount { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("shop_trade_condition"), MessagePackObject(true)]
public record ShopTradeConditionRow
{
    [PrimaryKey] public required int Order { get; init; }
    [SecondaryKey(0), NonUnique] public required long TradeCode { get; init; }
    public required int GroupNo { get; init; }
    public required long FlagCode { get; init; }
    public required ConditionType Type { get; init; }
    public required long Value { get; init; }
}

[MemoryTable("equipment_slot"), MessagePackObject(true)]
public record EquipmentSlotRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("tag"), MessagePackObject(true)]
public record TagRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("unique_item_inventory"), MessagePackObject(true)]
public record UniqueItemRow
{
    [PrimaryKey] public required long ItemCode { get; init; }
    public required string ItemName { get; init; }
    public required int Order { get; init; }
}

/// <summary>유니크 아이템 태그 마스터: 같은 (아이템, 태그)는 한 번만 쓸 수 있다. 보조키로 아이템의 태그 전부를 찾는다.</summary>
[MemoryTable("unique_item_tag"), MessagePackObject(true)]
public record UniqueItemTagRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long ItemCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    public required long TagCode { get; init; }

    public required int Order { get; init; }
}

/// <summary>커먼 아이템(아이템 인벤토리) 태그 마스터: 같은 (아이템, 태그)는 한 번만 쓸 수 있다.</summary>
[MemoryTable("common_item_tag"), MessagePackObject(true)]
public record CommonItemTagRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long ItemCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    public required long TagCode { get; init; }

    public required int Order { get; init; }
}

/// <summary>캐릭터 태그 마스터: 같은 (캐릭터, 태그)는 한 번만 쓸 수 있다.</summary>
[MemoryTable("character_tag"), MessagePackObject(true)]
public record CharacterTagRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long CharacterCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    public required long TagCode { get; init; }

    public required int Order { get; init; }
}

/// <summary>
/// 장비 슬롯 태그 마스터: 복합키는 앞의 네 열(캐릭터, 슬롯, 태그, 그룹)이고 IsPositive는 값이다.
/// 보조키 (캐릭터, 슬롯)로 그 슬롯에 걸린 규칙 전부를 찾는다.
/// </summary>
[MemoryTable("equipment_slot_tag"), MessagePackObject(true)]
public record EquipmentSlotTagRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0, keyOrder: 0), NonUnique]
    public required long CharacterCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    [SecondaryKey(0, keyOrder: 1), NonUnique]
    public required long EquipmentSlotCode { get; init; }

    [PrimaryKey(keyOrder: 2)]
    public required long TagCode { get; init; }

    [PrimaryKey(keyOrder: 3)]
    public required int GroupNumber { get; init; }

    public required bool IsPositive { get; init; }
    public required int Order { get; init; }
}

[MemoryTable("character_status"), MessagePackObject(true)]
public record CharacterStatusRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required int Order { get; init; }
}

/// <summary>캐릭터 초기 스테이터스 마스터: 복합키 (캐릭터, 스테이터스), 보조키 캐릭터로 그 캐릭터의 초깃값 전부를 찾는다.</summary>
[MemoryTable("character_initial_status"), MessagePackObject(true)]
public record CharacterInitialStatusRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long CharacterCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    public required long StatusCode { get; init; }

    public required long InitialValue { get; init; }
    public required int Order { get; init; }
}
