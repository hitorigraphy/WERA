using MasterMemory;
using Wera.Core;
using Wera.Core.Masters;

namespace Wera.Core.Tests;

public class MasterDatabaseTests
{
    // 일부러 코드가 파일 순서와 다르게 정렬되도록(내림차순 등) 적어서, 파일 순서 유지와 키 정렬을 함께 확인한다.
    static GamePack FullPack() => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n3\t풀\n1\t불\n2\t물\n"),
        ("master/character_inventory_master_v1.tsv",
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
            "1\t당신\t1\t1\t2\n7003\t도적\t1\t3\t1\n7001\t용사\t1\t1\t2\n7002\t마법사\t2\t2\t3\n"),
        ("master/dungeon_master_v1.tsv", "code\tname\n1002\t동굴\n1001\t초원\n"),
        ("master/dungeon_flag_master_v1.tsv",
            "dungeon_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n" +
            "1002\t2\t1003\tEqual\t1\n1002\t1\t1001\tGreaterOrEqual\t1\n1001\t1\t1001\tGreaterOrEqual\t1\n"),
        ("master/shop_master_v1.tsv", "code\tname\n2002\t무기점\n2001\t잡화점\n"),
        ("master/shop_condition_master_v1.tsv",
            "shop_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n2002\t1\t1002\tGreaterOrEqual\t3\n"),
        ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n3\t철광석\t99\n11\t금화\t99999\n2\t약초\t99\n"),
        ("master/bunch_item_inventory_master_v1.tsv",
            "inventory_code\titem_name\tpart_item_code\tpart_amount\n9501\t초보자 세트\t3\t1\n9501\t초보자 세트\t2\t3\n"),
        ("master/shop_trade_master_v1.tsv",
            "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\n" +
            "5003\t2002\t3\t3\t9501\t1\n5001\t2001\t11\t100\t2\t5\n5002\t2001\t2\t10\t11\t30\n"),
        ("master/shop_trade_condition_master_v1.tsv",
            "trade_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n5002\t1\t1001\tGreaterOrEqual\t1\n"),
        ("master/status_effect_master_v1.tsv", "status_effect_code\tstatus_effect_name\n3\t재생\n1\t독\n"),
        ("master/status_effect_effect_master_v1.tsv",
            "status_effect_code\ttrigger_type\tpriority\teffect_code\teffect_value_1\teffect_value_2\teffect_value_3\n" +
            "1\tAfterTurnEnd\t2\t1\t-3\t3\t0\n3\tAfterTurnStart\t1\t1\t5\t5\t0\n1\tBeforeAttack\t1\t1\t-1\t2\t9\n"),
        ("master/equipment_slot_master_v1.tsv", "equipment_slot_code\tequipment_slot_name\n2\t방어구\n1\t무기\n"),
        ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n6\t마법\n9\t검\n5\t중량\n4\t갑옷\n3\t메인 통화\n"),
        ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n"),
        ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n9003\t지팡이\n9001\t철검\n9002\t대검\n"),
        ("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n9002\t5\n9001\t9\n9002\t9\n9003\t6\n"),
        ("master/equipment_slot_tag_master_v1.tsv",
            "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n" +
            "7002\t1\t6\t1\ttrue\n7001\t1\t5\t1\tfalse\n7001\t1\t9\t1\ttrue\n7001\t2\t4\t1\ttrue\n")));

    static GamePack Empty() => GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));

    // ---- 모든 마스터가 테이블로 올라간다 ----

    [Fact]
    public void Every_master_is_loaded_into_a_table_with_the_same_number_of_rows()
    {
        var pack = FullPack();
        var db = pack.Masters;

        Assert.Equal(pack.Attributes!.Count, db.AttributeRowTable.Count);
        Assert.Equal(pack.StatusEffects!.Count, db.StatusEffectRowTable.Count);
        Assert.Equal(pack.StatusEffectEffects!.Count, db.StatusEffectEffectRowTable.Count);
        Assert.Equal(pack.Dungeons!.Count, db.DungeonRowTable.Count);
        Assert.Equal(pack.Shops!.Count, db.ShopRowTable.Count);
        Assert.Equal(pack.ItemInventory!.Count, db.ItemInventoryRowTable.Count);
        Assert.Equal(pack.BunchInventory!.Sum(b => b.Parts.Count), db.BunchInventoryRowTable.Count);
        Assert.Equal(pack.CharacterInventory!.Count, db.CharacterInventoryRowTable.Count);
        Assert.Equal(pack.Trades!.Count, db.ShopTradeRowTable.Count);
        Assert.Equal(pack.EquipmentSlots!.Count, db.EquipmentSlotRowTable.Count);
        Assert.Equal(pack.Tags!.Count, db.TagRowTable.Count);
        Assert.Equal(pack.UniqueItems!.Count, db.UniqueItemRowTable.Count);
        Assert.Equal(pack.UniqueItemTags!.Count, db.UniqueItemTagRowTable.Count);
        Assert.Equal(pack.EquipmentSlotTags!.Count, db.EquipmentSlotTagRowTable.Count);
    }

    [Fact]
    public void Condition_tables_store_every_row_sorted_by_their_composite_key()
    {
        var db = FullPack().Masters;

        // 파일에는 (1002,그룹2), (1002,그룹1), (1001,그룹1) 순으로 적혀 있지만 테이블은 키 순서다.
        Assert.Equal(3, db.DungeonFlagRowTable.Count);
        var dungeonRows = db.DungeonFlagRowTable.All.ToList();
        Assert.Equal([(1001L, 1, 1001L), (1002L, 1, 1001L), (1002L, 2, 1003L)],
            dungeonRows.Select(r => (r.DungeonCode, r.GroupNumber, r.FlagCode)));
        Assert.Equal(ConditionType.Equal, dungeonRows[2].Type);

        Assert.Single(db.ShopConditionRowTable.All);
        Assert.Equal(3, db.ShopConditionRowTable.All.Single().Value);
        Assert.Equal(5002, db.ShopTradeConditionRowTable.All.Single().TradeCode);
    }

    [Fact]
    public void A_pack_without_masters_has_a_usable_database_with_empty_tables()
    {
        var db = Empty().Masters;
        Assert.Equal(0, db.EquipmentSlotTagRowTable.Count);
        Assert.Equal(1, db.CharacterInventoryRowTable.Count);                      // 필수 마스터의 기본값(플레이어)만
        Assert.False(db.CharacterInventoryRowTable.TryFindByCode(7001, out _));
        Assert.False(db.Validate().IsValidationFailed);
    }

    // ---- 파일 순서는 그대로, 키 조회는 키 순서와 무관 ----

    [Fact]
    public void Lists_keep_the_file_order_while_tables_are_sorted_by_key()
    {
        var pack = FullPack();
        Assert.Equal([1L, 7003L, 7001L, 7002L], pack.CharacterInventory!.Select(c => c.Code));       // 파일 순서
        Assert.Equal([3L, 11L, 2L], pack.ItemInventory!.Select(i => i.Code));
        Assert.Equal([1002L, 1001L], pack.Dungeons!.Select(d => d.Code));

        // 테이블은 기본키(코드) 순서다. 파일 순서는 따로 기억하지 않는다.
        Assert.Equal([1L, 7001L, 7002L, 7003L], pack.Masters.CharacterInventoryRowTable.All.Select(r => r.Code));
    }

    // ---- 키로 찾기 ----

    [Fact]
    public void Find_by_primary_key_returns_the_master_entries_or_null()
    {
        var pack = FullPack();
        Assert.Equal(new CharacterEntry(7001, "용사", 1, 1, 2), pack.FindCharacter(7001));
        Assert.Equal(new AttributeEntry(2, "물"), pack.FindAttribute(2));
        Assert.Equal(new StatusEffectEntry(1, "독"), pack.FindStatusEffect(1));
        Assert.Equal(new ItemEntry(2, "약초", 99), pack.FindItem(2));
        Assert.Equal(new EquipmentSlotEntry(1, "무기"), pack.FindEquipmentSlot(1));
        Assert.Equal(new UniqueItem(9002, "대검"), pack.FindUniqueItem(9002));

        Assert.Null(pack.FindCharacter(9999));
        Assert.Null(pack.FindAttribute(9999));
        Assert.Null(pack.FindStatusEffect(9999));
        Assert.Null(pack.FindItem(9999));
        Assert.Null(pack.FindEquipmentSlot(9999));
        Assert.Null(pack.FindUniqueItem(9999));
        Assert.Null(Empty().FindCharacter(7001));
    }

    [Fact]
    public void Bunch_is_found_by_its_item_code_with_parts_in_part_code_order()
    {
        var bunch = FullPack().FindBunch(9501)!;
        Assert.Equal("초보자 세트", bunch.Name);
        Assert.Equal([new BunchPart(2, 3), new BunchPart(3, 1)], bunch.Parts);          // 부품 코드 순(파일에는 3 다음 2)
        Assert.Null(FullPack().FindBunch(1));                                             // 일반 아이템은 번치가 아니다
    }

    [Fact]
    public void Item_names_and_bunch_expansion_come_from_the_tables()
    {
        var pack = FullPack();
        Assert.Equal("금화", pack.ItemName(11));
        Assert.Equal("초보자 세트", pack.ItemName(9501));
        Assert.Equal("77", pack.ItemName(77));
        Assert.Equal(new Dictionary<long, long> { [3] = 2, [2] = 6 }, pack.Expand(9501, 2));
        Assert.Equal(new Dictionary<long, long> { [11] = 5 }, pack.Expand(11, 5));
    }

    [Fact]
    public void Status_effect_effects_of_one_status_are_found_by_secondary_key_in_key_order()
    {
        var pack = FullPack();
        Assert.Equal(
            [new StatusEffectEffect(1, TriggerType.BeforeAttack, 1, 1, -1, 2, 9), new StatusEffectEffect(1, TriggerType.AfterTurnEnd, 2, 1, -3, 3, 0)],
            pack.StatusEffectEffectsOf(1));
        Assert.Single(pack.StatusEffectEffectsOf(3));
        Assert.Empty(pack.StatusEffectEffectsOf(99));
    }

    // ---- 복합키 ----

    [Fact]
    public void Equipment_slot_tag_has_a_four_column_composite_primary_key()
    {
        var table = FullPack().Masters.EquipmentSlotTagRowTable;

        var row = table.FindByCharacterCodeAndEquipmentSlotCodeAndTagCodeAndGroupNumber((7001, 1, 5, 1));
        Assert.False(row.IsPositive);
        Assert.True(table.FindByCharacterCodeAndEquipmentSlotCodeAndTagCodeAndGroupNumber((7001, 1, 9, 1)).IsPositive);
        Assert.False(table.TryFindByCharacterCodeAndEquipmentSlotCodeAndTagCodeAndGroupNumber((7001, 1, 9, 2), out _));   // 그룹만 다름
        Assert.False(table.TryFindByCharacterCodeAndEquipmentSlotCodeAndTagCodeAndGroupNumber((7001, 2, 9, 1), out _));   // 슬롯만 다름
    }

    [Fact]
    public void Equipment_slot_tag_secondary_key_finds_all_rules_of_a_character_slot()
    {
        var table = FullPack().Masters.EquipmentSlotTagRowTable;
        var rules = table.FindByCharacterCodeAndEquipmentSlotCode((7001, 1));
        Assert.Equal([5L, 9L], rules.Select(r => r.TagCode).Order());
        Assert.Empty(table.FindByCharacterCodeAndEquipmentSlotCode((7001, 99)));
        Assert.Equal(4, table.FindRangeByCharacterCodeAndEquipmentSlotCode((7001, long.MinValue), (7002, long.MaxValue)).Count);
    }

    [Fact]
    public void Bunch_and_equipment_tag_tables_have_two_column_composite_keys()
    {
        var db = FullPack().Masters;
        Assert.Equal(3, db.BunchInventoryRowTable.FindByItemCodeAndPartItemCode((9501, 2)).PartAmount);
        Assert.False(db.BunchInventoryRowTable.TryFindByItemCodeAndPartItemCode((9501, 1), out _));
        Assert.Equal(2, db.UniqueItemTagRowTable.FindByItemCode(9002).Count);
        Assert.True(db.UniqueItemTagRowTable.TryFindByItemCodeAndTagCode((9002, 5), out _));
        Assert.False(db.UniqueItemTagRowTable.TryFindByItemCodeAndTagCode((9001, 5), out _));
    }

    [Fact]
    public void Equipment_queries_give_the_same_answers_as_the_rules()
    {
        var pack = FullPack();
        Assert.Equal(["무기", "방어구"], pack.EquipmentSlotsOf(7001).Select(s => s.Name));
        Assert.Equal(["무기"], pack.EquipmentSlotsOf(7002).Select(s => s.Name));
        Assert.Empty(pack.EquipmentSlotsOf(7003));
        Assert.True(pack.CanEquip(7001, 1, 9001));
        Assert.False(pack.CanEquip(7001, 1, 9002));                    // 대검은 중량
        Assert.True(pack.CanEquip(7002, 1, 9003));                     // 마법
        Assert.False(pack.CanEquip(7001, 1, 99999));
        Assert.Equal(["지팡이"], pack.EquippableItems(7002, 1).Select(i => i.ItemName));
        Assert.Equal(["철검"], pack.EquippableItems(7001, 1).Select(i => i.ItemName));        // 장비 마스터 순서
    }

    // ---- 검증기와 직렬화 ----

    [Fact]
    public void Validate_passes_for_a_loaded_pack_and_a_rebuilt_database()
    {
        var pack = FullPack();
        Assert.False(pack.Masters.Validate().IsValidationFailed);

        var again = new MemoryDatabase(pack.Masters.ToDatabaseBuilder().Build());
        Assert.False(again.Validate().IsValidationFailed);
        Assert.Equal(pack.Masters.EquipmentSlotTagRowTable.Count, again.EquipmentSlotTagRowTable.Count);
        Assert.Equal(pack.FindCharacter(7002)!.Name, again.CharacterInventoryRowTable.FindByCode(7002).Name);
    }

    [Fact]
    public void The_library_only_reports_duplicate_primary_keys_in_Validate_not_in_Build()
    {
        // MasterMemory의 동작 문서화: Build/로드는 중복 기본키를 거부하지 않고 Validate()가 잡는다.
        // 그래서 GamePack은 항상 Validate()를 호출하고, 로더도 TSV 단계에서 중복을 먼저 거부한다.
        var row = new EquipmentSlotTagRow { CharacterCode = 1, EquipmentSlotCode = 1, TagCode = 1, GroupNumber = 1, IsPositive = true };
        var duplicated = new MemoryDatabase(new DatabaseBuilder().Append([row, row with { IsPositive = false }]).Build());
        Assert.True(duplicated.Validate().IsValidationFailed);
    }

    [Fact]
    public void Duplicate_keys_in_the_tsv_are_still_rejected_with_a_message_before_the_database_is_built()
    {
        var error = Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
            ("master/equipment_slot_master_v1.tsv", "equipment_slot_code\tequipment_slot_name\n1\t무기\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n"),
            ("master/equipment_slot_tag_master_v1.tsv",
                "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n7001\t1\t1\t1\ttrue\n7001\t1\t1\t1\tfalse\n"))));
        Assert.Contains("같은 키", error.Message);
    }

    [Theory]
    [InlineData("master/dungeon_flag_master_v1.tsv", "dungeon_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n1001\t1\t1001\tGreaterOrEqual\t1\n1001\t1\t1001\tLessThan\t5\n", "던전")]
    [InlineData("master/shop_condition_master_v1.tsv", "shop_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n2001\t1\t1001\tGreaterOrEqual\t1\n2001\t1\t1001\tGreaterOrEqual\t1\n", "상점")]
    public void Conditions_with_the_same_code_group_and_flag_are_rejected(string file, string rows, string noun)
    {
        // 복합키는 (코드, 그룹넘버, 플래그)라 타입과 밸류가 달라도 같은 키는 두 번 쓸 수 없다.
        var error = Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/dungeon_master_v1.tsv", "code\tname\n1001\t초원\n"),
            ("master/shop_master_v1.tsv", "code\tname\n2001\t잡화점\n"),
            (file, rows))));
        Assert.Contains(noun, error.Message);
        Assert.Contains("두 번", error.Message);
    }

    [Fact]
    public void The_same_flag_in_different_groups_is_fine()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/dungeon_master_v1.tsv", "code\tname\n1001\t초원\n"),
            ("master/dungeon_flag_master_v1.tsv",
                "dungeon_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n1001\t1\t1001\tGreaterOrEqual\t1\n1001\t2\t1001\tLessThan\t5\n")));
        Assert.Equal(2, pack.Masters.DungeonFlagRowTable.Count);
    }

    [Fact]
    public void Status_effect_effects_with_the_same_status_trigger_and_effect_code_are_rejected()
    {
        // 복합키는 (상태이상, 트리거, 이펙트 코드). 우선순위나 밸류가 달라도 같은 키는 두 번 쓸 수 없다.
        const string header = "status_effect_code\ttrigger_type\tpriority\teffect_code\teffect_value_1\teffect_value_2\teffect_value_3\n";
        GamePack Load(string rows) => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/status_effect_master_v1.tsv", "status_effect_code\tstatus_effect_name\n1\t독\n"),
            ("master/status_effect_effect_master_v1.tsv", header + rows)));

        var error = Assert.Throws<PackFormatException>(() =>
            Load("1\tAfterTurnEnd\t1\t1\t-3\t3\t0\n1\tAfterTurnEnd\t2\t1\t-5\t3\t0\n"));
        Assert.Contains("두 번", error.Message);

        Assert.Equal(2, Load("1\tAfterTurnEnd\t1\t1\t-3\t3\t0\n1\tBeforeAttack\t1\t1\t-3\t3\t0\n").StatusEffectEffects!.Count);   // 트리거가 다르면 괜찮다
    }
}
