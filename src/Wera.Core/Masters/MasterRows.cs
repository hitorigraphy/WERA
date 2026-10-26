using MasterMemory;
using MessagePack;

// 생성되는 테이블/데이터베이스 코드를 행 타입과 같은 네임스페이스(Wera.Core.Masters) 아래에 둔다.
[assembly: MasterMemoryGeneratorOptions(Namespace = "Wera.Core.Masters")]

namespace Wera.Core.Masters;

// MasterMemory 테이블 정의. 게임팩의 TSV 마스터 한 개가 테이블 한 개이고, 행 한 개가 TSV의 한 줄이다.
// 행 순서를 따로 기억하지 않는다(int 순번은 long 코드보다 범위가 좁다). MasterMemory가 기본키 순서로 정렬해 두고,
// 순서가 필요한 곳은 조회하는 쪽에서 키 순서대로 OrderBy/ThenBy 한다. 화면에 나오는 목록은 로더가 만든 목록(게임팩 파일 순서)을 쓴다.
// 복합키가 있는 마스터는 키가 같은 행을 두 번 쓸 수 없다.

[MemoryTable("attribute"), MessagePackObject(true)]
public record AttributeRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

[MemoryTable("status_effect"), MessagePackObject(true)]
public record StatusEffectRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>상태이상의 이펙트: 복합키는 (상태이상, 트리거, 이펙트 코드)이고, 보조키 상태이상 코드로 그 상태이상의 이펙트 전부를 찾는다.</summary>
[MemoryTable("status_effect_effect"), MessagePackObject(true)]
public record StatusEffectEffectRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long StatusEffectCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required TriggerType Trigger { get; init; }
    [PrimaryKey(keyOrder: 2)] public required long EffectCode { get; init; }
    public required long Priority { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

[MemoryTable("dungeon"), MessagePackObject(true)]
public record DungeonRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>던전 입장 조건: 복합키는 (던전, 그룹넘버, 플래그)이고, 보조키 던전 코드로 그 던전의 조건 전부를 찾는다.</summary>
[MemoryTable("dungeon_flag"), MessagePackObject(true)]
public record DungeonFlagRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long DungeonCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required int GroupNumber { get; init; }
    [PrimaryKey(keyOrder: 2)] public required long FlagCode { get; init; }
    public required ConditionType Type { get; init; }
    public required long Value { get; init; }
}

[MemoryTable("shop"), MessagePackObject(true)]
public record ShopRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>상점 입장 조건: 복합키는 (상점, 그룹넘버, 플래그)이고, 보조키 상점 코드로 그 상점의 조건 전부를 찾는다.</summary>
[MemoryTable("shop_condition"), MessagePackObject(true)]
public record ShopConditionRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long ShopCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required int GroupNumber { get; init; }
    [PrimaryKey(keyOrder: 2)] public required long FlagCode { get; init; }
    public required ConditionType Type { get; init; }
    public required long Value { get; init; }
}

[MemoryTable("common_item_inventory"), MessagePackObject(true)]
public record ItemInventoryRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required long CarryLimit { get; init; }
}

/// <summary>번치 아이템 인벤토리: 한 행이 번치의 부품 하나다. 같은 (번치, 부품)은 한 번만 쓸 수 있다.</summary>
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
}

[MemoryTable("character_inventory"), MessagePackObject(true)]
public record CharacterInventoryRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required long CarryLimit { get; init; }
    public required long AdvantageAttributeCode { get; init; }
    public required long DisadvantageAttributeCode { get; init; }
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
    public required TradeKind CostKind { get; init; }
    public required TradeKind ReceiveKind { get; init; }
}

/// <summary>교환 조건: 복합키는 (교환, 그룹넘버, 플래그)이고, 보조키 교환 코드로 그 교환의 조건 전부를 찾는다.</summary>
[MemoryTable("shop_trade_condition"), MessagePackObject(true)]
public record ShopTradeConditionRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long TradeCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required int GroupNumber { get; init; }
    [PrimaryKey(keyOrder: 2)] public required long FlagCode { get; init; }
    public required ConditionType Type { get; init; }
    public required long Value { get; init; }
}

[MemoryTable("equipment_slot"), MessagePackObject(true)]
public record EquipmentSlotRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

[MemoryTable("tag"), MessagePackObject(true)]
public record TagRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

[MemoryTable("unique_item_inventory"), MessagePackObject(true)]
public record UniqueItemRow
{
    [PrimaryKey] public required long ItemCode { get; init; }
    public required string ItemName { get; init; }
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
}

[MemoryTable("time_slot"), MessagePackObject(true)]
public record TimeSlotRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>턴 마스터: 기본키는 턴 카운트다(키 순서가 곧 턴 순서).</summary>
[MemoryTable("turn"), MessagePackObject(true)]
public record TurnRow
{
    [PrimaryKey] public required long TurnCount { get; init; }
    public required long Year { get; init; }
    public required long Month { get; init; }
    public required long Day { get; init; }
    public required long TimeSlotCode { get; init; }
}

[MemoryTable("event"), MessagePackObject(true)]
public record EventRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>이벤트 조건: 복합키는 (이벤트, 그룹넘버, 플래그)이고, 보조키 이벤트 코드로 그 이벤트의 조건 전부를 찾는다.</summary>
[MemoryTable("event_condition"), MessagePackObject(true)]
public record EventConditionRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long EventCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required int GroupNumber { get; init; }
    [PrimaryKey(keyOrder: 2)] public required long FlagCode { get; init; }
    public required ConditionType Type { get; init; }
    public required long Value { get; init; }
}

/// <summary>이벤트 흐름: 복합키는 (이벤트, 흐름 번호)이고, 보조키 이벤트 코드로 그 이벤트의 흐름 전부를 찾는다.</summary>
[MemoryTable("event_flow"), MessagePackObject(true)]
public record EventFlowRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long EventCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long FlowNumber { get; init; }
    public required EventFlowType FlowType { get; init; }
    public required long Param1 { get; init; }
    public required long Param2 { get; init; }
    public required long Param3 { get; init; }
}

/// <summary>
/// 이벤트 흐름 조건: 복합키는 (이벤트, 흐름 번호, 그룹넘버, 플래그)이고, 보조키 (이벤트, 흐름 번호)로 그 흐름의 조건 전부를 찾는다.
/// </summary>
[MemoryTable("event_flow_condition"), MessagePackObject(true)]
public record EventFlowConditionRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0, keyOrder: 0), NonUnique]
    public required long EventCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    [SecondaryKey(0, keyOrder: 1), NonUnique]
    public required long FlowNumber { get; init; }

    [PrimaryKey(keyOrder: 2)] public required int GroupNumber { get; init; }
    [PrimaryKey(keyOrder: 3)] public required long FlagCode { get; init; }
    public required ConditionType Type { get; init; }
    public required long Value { get; init; }
}

[MemoryTable("adv"), MessagePackObject(true)]
public record AdvRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>ADV 흐름: 복합키는 (ADV, 흐름 번호)이고, 보조키 ADV 코드로 그 ADV의 흐름 전부를 찾는다.</summary>
[MemoryTable("adv_flow"), MessagePackObject(true)]
public record AdvFlowRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long AdvCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long FlowNumber { get; init; }
    public required AdvFlowType FlowType { get; init; }
    public required long Param1 { get; init; }
    public required long Param2 { get; init; }
    public required long Param3 { get; init; }
    public required string Text { get; init; }
    public required string ForceName { get; init; }
}

/// <summary>ADV 흐름 조건: 복합키는 (ADV, 흐름 번호, 그룹넘버, 플래그)이고, 보조키 (ADV, 흐름 번호)로 그 흐름의 조건 전부를 찾는다.</summary>
[MemoryTable("adv_flow_condition"), MessagePackObject(true)]
public record AdvFlowConditionRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0, keyOrder: 0), NonUnique]
    public required long AdvCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    [SecondaryKey(0, keyOrder: 1), NonUnique]
    public required long FlowNumber { get; init; }

    [PrimaryKey(keyOrder: 2)] public required int GroupNumber { get; init; }
    [PrimaryKey(keyOrder: 3)] public required long FlagCode { get; init; }
    public required ConditionType Type { get; init; }
    public required long Value { get; init; }
}

[MemoryTable("battle"), MessagePackObject(true)]
public record BattleRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

[MemoryTable("status"), MessagePackObject(true)]
public record StatusRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>캐릭터 스테이터스 마스터: 복합키 (캐릭터, 스테이터스, 레벨), 보조키 캐릭터로 그 캐릭터의 행 전부를 찾는다.</summary>
[MemoryTable("character_status"), MessagePackObject(true)]
public record CharacterStatusRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long CharacterCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    public required long StatusCode { get; init; }

    [PrimaryKey(keyOrder: 2)]
    public required long Level { get; init; }

    public required long InitialValue { get; init; }
    public required long MinValue { get; init; }
    public required long MaxValue { get; init; }
}
