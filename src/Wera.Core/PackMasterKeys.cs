namespace Wera.Core;

/// <summary>모드(중첩 팩)를 합칠 때 한 마스터를 어떻게 다루는가.</summary>
public enum MasterMergeKind
{
    /// <summary>키 열로 행을 알아본다: 나중 팩의 행이 같은 키의 앞선 팩의 행을 통째로 덮어쓰고, 새 키의 행은 뒤에 붙는다.</summary>
    Keyed,
    /// <summary>키를 두지 않는다: 행을 그대로 이어 붙인다(같은 키가 겹치면 그 마스터의 로더가 중복 오류를 낸다). 더 이상 쓰지 않는 옛 마스터가 여기에 든다.</summary>
    AppendOnly,
    /// <summary>합치지 않는다(pack_master처럼 바깥 팩에만 있는 파일).</summary>
    NotMerged,
}

/// <summary>
/// 마스터 하나의 병합 규칙.
/// <paramref name="Key"/>는 행을 알아보는 열 이름들(복합키 가능), <paramref name="Alternates"/>는 그 마스터가 따로 유일해야 한다고 검사하는 또 다른 열 묶음들이다
/// (나중 팩의 행이 앞선 팩의 행과 이 묶음 가운데 어느 것으로든 같으면 그 행을 덮어쓴다. 예: 이미지 마스터의 (kind, target_code, variation_number)).
/// <paramref name="ColumnOrder"/>가 있으면 합친 마스터의 열을 그 순서로 놓는다(로더가 선택 열의 위치를 정해 둔 마스터용; 목록에 없는 열은 그 뒤에 처음 나온 순서로).
/// </summary>
/// <paramref name="OptionalKey"/>는 <paramref name="Key"/>(와 <paramref name="Alternates"/>)의 열 가운데 레이어의 헤더에 없어도 되는 열이다(없는 열은 빈 칸으로 보고 키에 넣는다. 예: 아지트 마스터들의 hideout_code).
public sealed record MasterKeySpec(MasterMergeKind Kind, string[] Key, string[][] Alternates, string[]? ColumnOrder = null, string[]? OptionalKey = null);

/// <summary>
/// 모든 마스터 파일의 병합 규칙(<see cref="MasterKeySpec"/>) 목록. 모드 팩(중첩 팩)을 합칠 때 같은 키의 행이 나중 팩에 있으면 덮어쓰는 데 쓴다.
/// <b>새 마스터를 만들면 여기에 키를 꼭 등록한다</b>(테스트가 <see cref="GamePack"/>의 모든 마스터 경로 상수가 등록되었는지 확인한다).
/// 키 열 이름은 그 마스터 로더가 읽는 헤더 이름과 같아야 하고, 로더가 중복을 거부하는 키와 같아야 한다.
/// </summary>
public static class PackMasterKeys
{
    static MasterKeySpec Keyed(params string[] key) => new(MasterMergeKind.Keyed, key, []);

    /// <summary>선택 열 hideout_code가 키의 앞에 붙는 마스터(아지트별 행).</summary>
    static MasterKeySpec KeyedHideout(params string[] key) => new(MasterMergeKind.Keyed, ["hideout_code", .. key], [], null, ["hideout_code"]);

    static MasterKeySpec KeyedAlt(string[] key, params string[][] alternates) => new(MasterMergeKind.Keyed, key, alternates);

    static MasterKeySpec AppendOnly() => new(MasterMergeKind.AppendOnly, [], []);

    static MasterKeySpec NotMerged() => new(MasterMergeKind.NotMerged, [], []);

    /// <summary>마스터 경로 → 병합 규칙.</summary>
    public static IReadOnlyDictionary<string, MasterKeySpec> All { get; } = new Dictionary<string, MasterKeySpec>
    {
        // 바깥 팩에만 있다.
        [GamePack.PackMasterPath] = NotMerged(),

        // 이름 목록(키는 코드 하나)
        [GamePack.AttributeMasterPath] = Keyed("attribute_code"),
        [GamePack.TagMasterPath] = Keyed("tag_code"),
        [GamePack.StatusMasterPath] = Keyed("status_code"),
        [GamePack.StatusEffectMasterPath] = Keyed("status_effect_code"),
        [GamePack.StatusEffectEffectMasterPath] = Keyed("status_effect_code", "trigger_type", "effect_code"),
        [GamePack.TimeSlotMasterPath] = Keyed("code"),
        [GamePack.TurnMasterPath] = Keyed("turn_count"),
        [GamePack.XpTypeMasterPath] = Keyed("xp_type_code"),
        [GamePack.GameSettingMasterPath] = Keyed("setting_type"),

        // 캐릭터
        [GamePack.CharacterInventoryMasterPath] = Keyed("inventory_code"),
        [GamePack.CharacterStatusMasterPath] = Keyed("character_code", "status_code", "level"),
        [GamePack.CharacterTagMasterPath] = Keyed("character_code", "tag_code"),
        [GamePack.EquipmentSlotMasterPath] = Keyed("equipment_slot_code"),
        [GamePack.EquipmentSlotTagMasterPath] = Keyed("character_code", "equipment_slot_code", "tag_code", "group_number"),
        [GamePack.SkillSlotMasterPath] = Keyed("code"),
        [GamePack.CharacterSkillSlotTagMasterPath] = Keyed("character_code", "skill_slot_code", "group_number", "tag_code"),
        [GamePack.CharacterAfterCreateEffectMasterPath] = Keyed("character_code", "execute_number"),

        // 아이템
        [GamePack.CommonItemInventoryMasterPath] = Keyed("inventory_code"),
        [GamePack.CommonItemTagMasterPath] = Keyed("item_code", "tag_code"),
        [GamePack.BunchItemInventoryMasterPath] = Keyed("inventory_code", "part_item_code"),
        [GamePack.UniqueItemInventoryMasterPath] = Keyed("inventory_code"),
        [GamePack.UniqueItemTagMasterPath] = Keyed("item_code", "tag_code"),
        [GamePack.UniqueItemStatusMasterPath] = Keyed("item_code", "status_code"),
        [GamePack.UniqueItemEnhanceStatusMasterPath] = Keyed("item_code", "status_code", "enhance_level"),
        [GamePack.UniqueItemAfterCreateEffectMasterPath] = Keyed("item_code", "execute_number"),
        [GamePack.PartSlotMasterPath] = Keyed("code"),
        [GamePack.PartSlotTagMasterPath] = Keyed("item_code", "part_slot_code", "group_number", "tag_code"),
        [GamePack.ModSlotMasterPath] = Keyed("code"),
        [GamePack.UniqueItemModStatusMasterPath] = Keyed("unique_item_code", "mod_slot_code", "item_code", "status_code"),
        // (장비, 개조 슬롯)이 키이고 (장비, priority)도 따로 유일해야 한다.
        [GamePack.UniqueItemModSkillMasterPath] = KeyedAlt(["unique_item_code", "mod_slot_code"], ["unique_item_code", "priority"]),

        // 스킬
        [GamePack.SkillMasterPath] = Keyed("code"),
        [GamePack.SkillTagMasterPath] = Keyed("skill_code", "tag_code"),
        [GamePack.SkillStatusMasterPath] = Keyed("skill_code", "status_code"),
        [GamePack.SkillExecuteEffectMasterPath] = Keyed("skill_code", "execute_number"),
        [GamePack.SkillOrderEffectMasterPath] = Keyed("skill_code", "execute_number"),
        [GamePack.SkillAutoTargetEffectMasterPath] = Keyed("skill_code", "execute_number"),

        // 범용 이펙트/흐름 서킷(키는 실행 번호 하나, XP는 (XP 종류, 실행 번호))
        [GamePack.AfterHitEffectMasterPath] = Keyed("execute_number"),
        [GamePack.AfterAttackEffectMasterPath] = Keyed("execute_number"),
        [GamePack.AfterBattleWinEffectMasterPath] = Keyed("execute_number"),
        [GamePack.AfterBattleLoseEffectMasterPath] = Keyed("execute_number"),
        [GamePack.AfterBattleRunEffectMasterPath] = Keyed("execute_number"),
        [GamePack.AfterLoadCharacterFlowMasterPath] = Keyed("execute_number"),
        [GamePack.AfterLoadUniqueItemFlowMasterPath] = Keyed("execute_number"),
        [GamePack.SynthesisFlowMasterPath] = Keyed("execute_number"),
        [GamePack.SmeltFlowMasterPath] = Keyed("execute_number"),
        [GamePack.XpCalculateEffectMasterPath] = Keyed("xp_type_code", "execute_number"),

        // 서브모듈(이펙트 서킷 종류별)
        [GamePack.AfterAttackSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.AfterBattleLoseSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.AfterBattleRunSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.AfterBattleWinSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.AfterHitSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.AfterLoadCharacterSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.AfterLoadUniqueItemSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.CharacterAfterCreateSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.SkillExecuteSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.SkillAutoTargetSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.SkillOrderSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.SmeltSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.SynthesisSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.UniqueItemAfterCreateSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),
        [GamePack.XpCalculateSubmoduleMasterPath] = Keyed("submodule_code", "execute_number"),

        // 서브모듈(흐름 서킷 종류별)
        [GamePack.EventSubmoduleMasterPath] = Keyed("submodule_code", "flow_number"),
        [GamePack.DungeonEventSubmoduleMasterPath] = Keyed("submodule_code", "flow_number"),
        [GamePack.AdvSubmoduleMasterPath] = Keyed("submodule_code", "flow_number"),
        [GamePack.CommunicationSubmoduleMasterPath] = Keyed("submodule_code", "flow_number"),
        [GamePack.FacilitySubmoduleMasterPath] = Keyed("submodule_code", "flow_number"),
        [GamePack.DefenseSubmoduleMasterPath] = Keyed("submodule_code", "flow_number"),

        // 상점
        [GamePack.ShopMasterPath] = Keyed("code"),
        [GamePack.ShopConditionMasterPath] = Keyed("shop_code", "group_number", "flag_code"),
        [GamePack.ShopTradeMasterPath] = Keyed("code"),
        [GamePack.ShopTradeConditionMasterPath] = Keyed("trade_code", "group_number", "flag_code"),

        // 전투와 적
        [GamePack.BattleMasterPath] = Keyed("code"),
        [GamePack.PresetUnitMasterPath] = Keyed("code"),
        [GamePack.PresetUnitEquipMasterPath] = Keyed("preset_unit_code", "equipment_slot_code"),
        [GamePack.PresetUnitDropMasterPath] = Keyed("preset_unit_code", "item_code"),
        [GamePack.PresetPartyMasterPath] = Keyed("code"),
        [GamePack.BattleEnemyMasterPath] = Keyed("battle_code", "preset_party_code"),

        // 던전
        // 도입 지점 열 셋이 encounter_rate보다 앞에 있어야 로더가 읽는다.
        [GamePack.DungeonMasterPath] = new MasterKeySpec(MasterMergeKind.Keyed, ["code"], [],
            ["code", "name", "entry_layer_number", "entry_pos_x", "entry_pos_y", "encounter_rate"]),
        [GamePack.DungeonFlagMasterPath] = Keyed("dungeon_code", "group_number", "flag_code"),
        [GamePack.TileMasterPath] = Keyed("code", "tile_effect"),
        [GamePack.DungeonTileMasterPath] = Keyed("dungeon_code", "local_tile_text"),
        [GamePack.DungeonMapMasterPath] = Keyed("dungeon_code", "layer_number", "pos_y"),
        [GamePack.DungeonEncounterMasterPath] = Keyed("dungeon_code", "layer_number", "battle_code"),
        [GamePack.DungeonLayerMasterPath] = Keyed("dungeon_code", "layer_number"),
        [GamePack.DungeonEventMasterPath] = Keyed("code"),
        [GamePack.DungeonEventFlowMasterPath] = Keyed("dungeon_event_code", "flow_number"),

        // 이벤트, ADV, 교류
        [GamePack.EventMasterPath] = Keyed("code"),
        [GamePack.EventConditionMasterPath] = Keyed("event_code", "group_number", "flag_code"),
        [GamePack.EventFlowMasterPath] = Keyed("event_code", "flow_number"),
        [GamePack.AdvMasterPath] = Keyed("code"),
        [GamePack.AdvFlowMasterPath] = Keyed("adv_code", "flow_number"),
        [GamePack.CommunicationGroupMasterPath] = Keyed("code"),
        [GamePack.CommunicationMasterPath] = Keyed("code"),
        [GamePack.CommunicationSoloFlowMasterPath] = Keyed("communication_code", "character_code", "flow_number"),
        [GamePack.CommunicationDuoFlowMasterPath] = Keyed("communication_code", "partner_code", "assist_code", "flow_number"),
        [GamePack.CommunicationDefaultFlowMasterPath] = Keyed("communication_code", "flow_number"),

        // 아지트
        [GamePack.FacilityMasterPath] = Keyed("code"),
        [GamePack.FacilityConditionMasterPath] = Keyed("facility_code", "group_number", "flag_code"),
        [GamePack.FacilityBuildMasterPath] = Keyed("facility_code", "group_code", "item_code"),
        [GamePack.HideoutMasterPath] = Keyed("hideout_code"),
        [GamePack.HideoutFloorMasterPath] = KeyedHideout("floor_order"),
        [GamePack.HideoutExpandMasterPath] = KeyedHideout("expand_count", "group_code", "item_code"),
        [GamePack.OperatorSlotMasterPath] = Keyed("code"),
        [GamePack.FacilityOperatorSlotMasterPath] = Keyed("facility_code", "operator_slot_code"),
        [GamePack.OperatorSlotTagMasterPath] = Keyed("operator_slot_code", "tag_code"),
        [GamePack.FacilityFlowMasterPath] = Keyed("facility_code", "flow_number"),
        [GamePack.DefenseFlowMasterPath] = Keyed("operator_slot_code", "flow_number"),
        [GamePack.FacilityDefenseMasterPath] = Keyed("facility_code"),
        // 코드가 키이고 priority도 따로 유일해야 한다.
        // (아지트, code)가 키이고 (아지트, priority)도 따로 유일해야 한다(공용 행과 그 아지트 전용 행 사이에서도).
        [GamePack.DefenseEnemyMasterPath] = new MasterKeySpec(MasterMergeKind.Keyed, ["hideout_code", "code"], [["hideout_code", "priority"]], null, ["hideout_code"]),

        // 이미지: image_code가 키이고 (kind, target_code, variation_number)도 따로 유일해야 한다.
        [GamePack.ImageMasterPath] = KeyedAlt(["image_code"], ["kind", "target_code", "variation_number"]),

        // 더 이상 쓰이지 않아 있으면 로드 오류인 옛 마스터(합치지 않고 이어 붙여 그 오류가 그대로 나게 한다).
        [GamePack.AdvFlowConditionMasterPath] = AppendOnly(),
        [GamePack.EventFlowConditionMasterPath] = AppendOnly(),
        [GamePack.OldCommunicationFlowMasterPath] = AppendOnly(),
    };

    /// <summary>이 경로의 병합 규칙. 등록되지 않은 마스터는 <see cref="MasterMergeKind.AppendOnly"/>로 다룬다.</summary>
    public static MasterKeySpec For(string path) => All.TryGetValue(path, out var spec) ? spec : AppendOnly();
}
