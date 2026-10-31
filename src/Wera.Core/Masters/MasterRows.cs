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
    public required long? SkillCode { get; init; }
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
    public required long? SkillCode { get; init; }
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

[MemoryTable("communication_group"), MessagePackObject(true)]
public record CommunicationGroupRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

[MemoryTable("communication"), MessagePackObject(true)]
public record CommunicationRow
{
    [PrimaryKey] public required long Code { get; init; }
    [SecondaryKey(0), NonUnique] public required long GroupCode { get; init; }
    public required string Name { get; init; }
    public required bool IsAssist { get; init; }
}

/// <summary>교류 솔로 흐름: 복합키는 (교류 코드, 캐릭터 코드, 흐름 번호)다.</summary>
[MemoryTable("communication_solo_flow"), MessagePackObject(true)]
public record CommunicationSoloFlowRow
{
    [PrimaryKey(keyOrder: 0)] public required long CommunicationCode { get; init; }
    [PrimaryKey(keyOrder: 1)] public required long CharacterCode { get; init; }
    [PrimaryKey(keyOrder: 2)] public required long FlowNumber { get; init; }
    public required CommunicationFlowType FlowType { get; init; }
    public required long Param1 { get; init; }
    public required long Param2 { get; init; }
    public required long Param3 { get; init; }
    public required string Text { get; init; }
}

/// <summary>교류 듀오 흐름: 복합키는 (교류 코드, 파트너 코드, 어시스트 코드, 흐름 번호)다.</summary>
[MemoryTable("communication_duo_flow"), MessagePackObject(true)]
public record CommunicationDuoFlowRow
{
    [PrimaryKey(keyOrder: 0)] public required long CommunicationCode { get; init; }
    [PrimaryKey(keyOrder: 1)] public required long PartnerCode { get; init; }
    [PrimaryKey(keyOrder: 2)] public required long AssistCode { get; init; }
    [PrimaryKey(keyOrder: 3)] public required long FlowNumber { get; init; }
    public required CommunicationFlowType FlowType { get; init; }
    public required long Param1 { get; init; }
    public required long Param2 { get; init; }
    public required long Param3 { get; init; }
    public required string Text { get; init; }
}

/// <summary>교류 기본 흐름: 복합키는 (교류 코드, 흐름 번호)다.</summary>
[MemoryTable("communication_default_flow"), MessagePackObject(true)]
public record CommunicationDefaultFlowRow
{
    [PrimaryKey(keyOrder: 0)] public required long CommunicationCode { get; init; }
    [PrimaryKey(keyOrder: 1)] public required long FlowNumber { get; init; }
    public required CommunicationFlowType FlowType { get; init; }
    public required long Param1 { get; init; }
    public required long Param2 { get; init; }
    public required long Param3 { get; init; }
    public required string Text { get; init; }
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

[MemoryTable("battle"), MessagePackObject(true)]
public record BattleRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required bool CanRun { get; init; }
}

/// <summary>타일 효과: 복합키는 (타일, 효과 종류)이고, 보조키 타일 코드로 그 타일의 효과 전부를 찾는다.</summary>
[MemoryTable("tile_effect"), MessagePackObject(true)]
public record TileEffectRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long TileCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required TileEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>던전 타일: 복합키는 (던전, 맵 글자)이고 값은 타일 코드다.</summary>
[MemoryTable("dungeon_tile"), MessagePackObject(true)]
public record DungeonTileRow
{
    [PrimaryKey(keyOrder: 0)] public required long DungeonCode { get; init; }
    [PrimaryKey(keyOrder: 1)] public required string LocalTileText { get; init; }
    public required long TileCode { get; init; }
}

/// <summary>던전 맵의 한 줄: 복합키는 (던전, 맵 번호, pos_y)이고, 보조키 던전 코드로 그 던전의 줄 전부를 찾는다. Cells는 pos_x_0부터의 글자(빈 칸은 "").</summary>
[MemoryTable("dungeon_map"), MessagePackObject(true)]
public record DungeonMapRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long DungeonCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long LayerNumber { get; init; }
    [PrimaryKey(keyOrder: 2)] public required long PosY { get; init; }
    public required string[] Cells { get; init; }
}

[MemoryTable("preset_unit"), MessagePackObject(true)]
public record PresetUnitRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required long CharacterCode { get; init; }
}

/// <summary>프리셋 유닛 장비: 복합키는 (프리셋 유닛, 장비 슬롯)이고, 보조키 프리셋 유닛 코드로 그 유닛의 장비 전부를 찾는다.</summary>
[MemoryTable("preset_unit_equip"), MessagePackObject(true)]
public record PresetUnitEquipRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long PresetUnitCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long EquipmentSlotCode { get; init; }
    public required long ItemCode { get; init; }
}

/// <summary>프리셋 유닛 드롭: 복합키는 (프리셋 유닛, 아이템)이고, 보조키 프리셋 유닛 코드로 그 유닛의 드롭 전부를 찾는다.</summary>
[MemoryTable("preset_unit_drop"), MessagePackObject(true)]
public record PresetUnitDropRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long PresetUnitCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long ItemCode { get; init; }
    public required long Amount { get; init; }
}

/// <summary>프리셋 파티: 이름과 진형 아홉 칸의 프리셋 유닛 코드(빈 칸은 null).</summary>
[MemoryTable("preset_party"), MessagePackObject(true)]
public record PresetPartyRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required long? FrontLeftUnitCode { get; init; }
    public required long? FrontCenterUnitCode { get; init; }
    public required long? FrontRightUnitCode { get; init; }
    public required long? MiddleLeftUnitCode { get; init; }
    public required long? MiddleCenterUnitCode { get; init; }
    public required long? MiddleRightUnitCode { get; init; }
    public required long? BackLeftUnitCode { get; init; }
    public required long? BackCenterUnitCode { get; init; }
    public required long? BackRightUnitCode { get; init; }
}

/// <summary>전투 적: 복합키는 (전투, 프리셋 파티)이고, 보조키 전투 코드로 그 전투에 나올 파티 전부를 찾는다.</summary>
[MemoryTable("battle_enemy"), MessagePackObject(true)]
public record BattleEnemyRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long BattleCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long PresetPartyCode { get; init; }
    public required long Weight { get; init; }
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

/// <summary>스킬 스테이터스 마스터: 복합키 (스킬, 스테이터스), 보조키 스킬로 그 스킬의 행 전부를 찾는다.</summary>
[MemoryTable("skill_status"), MessagePackObject(true)]
public record SkillStatusRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long SkillCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    public required long StatusCode { get; init; }

    public required long InitialValue { get; init; }
    public required long MinValue { get; init; }
    public required long MaxValue { get; init; }
}

/// <summary>유니크 아이템 스테이터스 마스터: 복합키 (아이템, 스테이터스), 보조키 아이템으로 그 아이템의 행 전부를 찾는다.</summary>
[MemoryTable("unique_item_status"), MessagePackObject(true)]
public record UniqueItemStatusRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long ItemCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    public required long StatusCode { get; init; }

    public required long InitialValue { get; init; }
    public required long MinValue { get; init; }
    public required long MaxValue { get; init; }
}

[MemoryTable("skill_slot"), MessagePackObject(true)]
public record SkillSlotRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// 캐릭터 스킬 슬롯 태그: 복합키는 (캐릭터, 스킬 슬롯, 그룹넘버, 태그)이고 IsPositive는 값이다.
/// 보조키 캐릭터 코드로 그 캐릭터의 규칙 전부를, 보조키 (캐릭터, 스킬 슬롯, 그룹넘버)로 한 그룹의 규칙 전부를 찾는다.
/// </summary>
[MemoryTable("character_skill_slot_tag"), MessagePackObject(true)]
public record CharacterSkillSlotTagRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    [SecondaryKey(1, keyOrder: 0), NonUnique]
    public required long CharacterCode { get; init; }

    [PrimaryKey(keyOrder: 1)]
    [SecondaryKey(1, keyOrder: 1), NonUnique]
    public required long SkillSlotCode { get; init; }

    [PrimaryKey(keyOrder: 2)]
    [SecondaryKey(1, keyOrder: 2), NonUnique]
    public required int GroupNumber { get; init; }

    [PrimaryKey(keyOrder: 3)]
    public required long TagCode { get; init; }

    public required bool IsPositive { get; init; }
}

[MemoryTable("skill"), MessagePackObject(true)]
public record SkillRow
{
    [PrimaryKey] public required long Code { get; init; }
    public required string Name { get; init; }
    public required int MainTargetType { get; init; }
    public required int SubTargetType { get; init; }
}

/// <summary>스킬 태그: 복합키는 (스킬, 태그)이고, 보조키 스킬 코드로 그 스킬의 태그 전부를 찾는다.</summary>
[MemoryTable("skill_tag"), MessagePackObject(true)]
public record SkillTagRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long SkillCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long TagCode { get; init; }
}

/// <summary>스킬 이펙트: 복합키는 (스킬, 실행 번호)이고, 보조키 스킬 코드로 그 스킬의 이펙트 전부를 찾는다.</summary>
[MemoryTable("skill_execute_effect"), MessagePackObject(true)]
public record SkillExecuteEffectRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long SkillCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long ExecuteNumber { get; init; }
    public required EffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>로드 후 캐릭터 흐름: 키는 execute_number 하나.</summary>
[MemoryTable("after_load_character_flow"), MessagePackObject(true)]
public record AfterLoadCharacterFlowRow
{
    [PrimaryKey] public required long ExecuteNumber { get; init; }
    public required LoadCharacterFlowType FlowType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>로드 후 유니크 아이템 흐름: 키는 execute_number 하나.</summary>
[MemoryTable("after_load_unique_item_flow"), MessagePackObject(true)]
public record AfterLoadUniqueItemFlowRow
{
    [PrimaryKey] public required long ExecuteNumber { get; init; }
    public required LoadUniqueItemFlowType FlowType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>피격 후 이펙트: 키는 execute_number 하나.</summary>
[MemoryTable("after_hit_effect"), MessagePackObject(true)]
public record AfterHitEffectRow
{
    [PrimaryKey] public required long ExecuteNumber { get; init; }
    public required AfterHitEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>공격 후 이펙트: 키는 execute_number 하나.</summary>
[MemoryTable("after_attack_effect"), MessagePackObject(true)]
public record AfterAttackEffectRow
{
    [PrimaryKey] public required long ExecuteNumber { get; init; }
    public required AfterAttackEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>전투 승리 후 이펙트: 키는 execute_number 하나.</summary>
[MemoryTable("after_battle_win_effect"), MessagePackObject(true)]
public record AfterBattleWinEffectRow
{
    [PrimaryKey] public required long ExecuteNumber { get; init; }
    public required AfterBattleWinEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>전투 패배 후 이펙트: 키는 execute_number 하나.</summary>
[MemoryTable("after_battle_lose_effect"), MessagePackObject(true)]
public record AfterBattleLoseEffectRow
{
    [PrimaryKey] public required long ExecuteNumber { get; init; }
    public required AfterBattleLoseEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>전투 도망 후 이펙트: 키는 execute_number 하나.</summary>
[MemoryTable("after_battle_run_effect"), MessagePackObject(true)]
public record AfterBattleRunEffectRow
{
    [PrimaryKey] public required long ExecuteNumber { get; init; }
    public required AfterBattleRunEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>스킬 순서 이펙트: 복합키는 (스킬, 실행 번호)이고, 보조키 스킬 코드로 그 스킬의 순서 이펙트 전부를 찾는다.</summary>
[MemoryTable("skill_order_effect"), MessagePackObject(true)]
public record SkillOrderEffectRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long SkillCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long ExecuteNumber { get; init; }
    public required OrderEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>스킬 자동 타겟 이펙트: 복합키는 (스킬, 실행 번호)이고, 보조키 스킬 코드로 그 스킬의 자동 타겟 이펙트 전부를 찾는다.</summary>
[MemoryTable("skill_auto_target_effect"), MessagePackObject(true)]
public record SkillAutoTargetEffectRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long SkillCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long ExecuteNumber { get; init; }
    public required AutoTargetEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>캐릭터 생성 후 이펙트: 복합키는 (캐릭터, 실행 번호)이고, 보조키 캐릭터 코드로 그 캐릭터의 이펙트 전부를 찾는다.</summary>
[MemoryTable("character_after_create_effect"), MessagePackObject(true)]
public record CharacterAfterCreateEffectRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long CharacterCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long ExecuteNumber { get; init; }
    public required CharacterAfterCreateEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}

/// <summary>유니크 아이템 생성 후 이펙트: 복합키는 (유니크 아이템, 실행 번호)이고, 보조키 유니크 아이템 코드로 그 유니크 아이템의 이펙트 전부를 찾는다.</summary>
[MemoryTable("unique_item_after_create_effect"), MessagePackObject(true)]
public record UniqueItemAfterCreateEffectRow
{
    [PrimaryKey(keyOrder: 0)]
    [SecondaryKey(0), NonUnique]
    public required long ItemCode { get; init; }

    [PrimaryKey(keyOrder: 1)] public required long ExecuteNumber { get; init; }
    public required UniqueItemAfterCreateEffectType EffectType { get; init; }
    public required long Value1 { get; init; }
    public required long Value2 { get; init; }
    public required long Value3 { get; init; }
}
