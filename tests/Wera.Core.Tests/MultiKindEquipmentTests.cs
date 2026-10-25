using Wera.Core;

namespace Wera.Core.Tests;

// 장비 슬롯에는 유니크 아이템뿐 아니라 커먼 아이템과 캐릭터도 들어갈 수 있다(강령술사가 강시를 장비하는 식).
//  캐릭터 7001 강령술사: 슬롯 1 무기(검), 슬롯 3 소환(소환수 태그). 캐릭터 7002 강시: 소환수 태그. 캐릭터 7003 용사: 태그 없음.
//  유니크 아이템 8001 철검(검), 커먼 아이템 3 뼈 조각(소환수), 1 금화(태그 없음).
public class MultiKindEquipmentTests
{
    static GamePack Pack() => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
        ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n11\t금화\t99999\n3\t뼈 조각\t99\n"),
        ("master/character_inventory_master_v1.tsv",
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
            "1\t당신\t1\t1\t2\n7001\t강령술사\t1\t1\t2\n7002\t강시\t1\t1\t2\n7003\t용사\t1\t1\t2\n"),
        ("master/equipment_slot_master_v1.tsv", "equipment_slot_code\tequipment_slot_name\n1\t무기\n3\t소환\n"),
        ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n9\t검\n8\t소환수\n3\t메인 통화\n"),
        ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"),
        ("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n8001\t9\n"),
        ("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n2\t3\n3\t8\n"),
        ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7002\t8\n"),
        ("master/equipment_slot_tag_master_v1.tsv",
            "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n" +
            "7001\t1\t9\t1\ttrue\n7001\t3\t8\t1\ttrue\n")));

    static GameSession Session()
    {
        var s = GameSession.NewGame(Pack());
        s.Characters[7001] = new CharacterState(5, 36, 40, 3, 30);
        s.Characters[7002] = new CharacterState(1, 10, 10, 0, 30);
        s.Characters[7003] = new CharacterState(1, 10, 10, 0, 30);
        s.Party[0] = 7001;
        s.InventoryItems[3] = new InventoryItem(2);
        s.InventoryItems[11] = new InventoryItem(500);
        s.AddUniqueItem(8001);
        return s;
    }

    // ---- 마스터 ----

    [Fact]
    public void The_new_tag_masters_are_loaded_and_looked_up_per_kind()
    {
        var pack = Pack();
        Assert.Equal([new UniqueItemTag(8001, 9)], pack.UniqueItemTags);
        Assert.Equal([new CommonItemTag(2, 3), new CommonItemTag(3, 8)], pack.CommonItemTags);
        Assert.Equal([new CharacterTag(1, 1), new CharacterTag(7002, 8)], pack.CharacterTags);
        Assert.Equal("철검", pack.FindUniqueItem(8001)!.ItemName);
        Assert.Single(pack.Masters.CharacterTagRowTable.FindByCharacterCode(7002));
        Assert.Single(pack.Masters.CommonItemTagRowTable.FindByItemCode(3));
    }

    [Theory]
    [InlineData("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n99\t8\n", "커먼 아이템 '99'")]
    [InlineData("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n3\t99\n", "태그 '99'")]
    [InlineData("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n3\t8\n3\t8\n", "두 번")]
    [InlineData("master/character_tag_master_v1.tsv", "character_code\ttag_code\n9999\t8\n", "캐릭터 '9999'")]
    [InlineData("master/character_tag_master_v1.tsv", "character_code\ttag_code\n7002\t8\n7002\t8\n", "두 번")]
    [InlineData("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n99999\t9\n", "유니크 아이템 '99999'")]
    public void Tag_masters_reject_unknown_targets_unknown_tags_and_duplicates(string file, string content, string message)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n3\t뼈 조각\t99\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7002\t강시\t1\t1\t2\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n9\t검\n8\t소환수\n3\t메인 통화\n"),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"),
            (file, content),
        };
        var error = Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(files.ToArray())));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void A_tag_master_needs_the_master_of_its_target_and_the_tag_master()
    {
        Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n8\t소환수\n3\t메인 통화\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n7002\t8\n"))));
        Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n3\t뼈 조각\t99\n"),
            ("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n3\t8\n"))));
    }

    // ---- 낄 수 있는 것 ----

    [Fact]
    public void The_slot_rules_decide_which_kinds_fit()
    {
        var s = Session();                                                   // 유니크 인스턴스 1 = 철검
        var sword = EquipRef.Unique(s.InstancesOf(8001)[0]);
        Assert.True(s.Equip(7001, 1, sword));
        Assert.True(s.Equip(7001, 3, EquipRef.Common(3)));
        Assert.True(s.Equip(7001, 3, EquipRef.Char(7002)));
        Assert.False(s.Equip(7001, 3, EquipRef.Common(11)));                  // 태그 없는 커먼 아이템
        Assert.False(s.Equip(7001, 3, EquipRef.Char(7003)));                 // 태그 없는 캐릭터
        Assert.False(s.Equip(7001, 3, sword));                               // 소환 슬롯에 검은 안 맞는다
        Assert.False(s.Equip(7001, 3, EquipRef.Common(7002)));               // 코드가 같아도 종류가 다르면 다른 것(아이템 7002는 없다)
    }

    // ---- 장착 ----

    [Fact]
    public void A_character_can_equip_a_pet_character_and_the_pet_is_used_up_once()
    {
        var s = Session();
        Assert.True(s.Equip(7001, 3, EquipRef.Char(7002)));
        Assert.Equal(EquipRef.Char(7002), s.EquippedItemOf(7001, 3));
        Assert.Equal(0, s.FreeCopies(EquipRef.Char(7002)));
        Assert.False(s.Equip(7003, 3, EquipRef.Char(7002)));              // 7003에게는 소환 슬롯이 없고, 이미 낀 캐릭터다
    }

    [Fact]
    public void A_character_cannot_equip_itself_or_an_unowned_character()
    {
        var s = Session();
        Assert.False(s.Equip(7002, 3, EquipRef.Char(7002)));
        s.Characters.Remove(7002);
        Assert.False(s.Equip(7001, 3, EquipRef.Char(7002)));              // 보유하지 않은 캐릭터
    }

    [Fact]
    public void Common_items_are_limited_by_the_inventory_amount()
    {
        var s = Session();
        Assert.True(s.Equip(7001, 3, EquipRef.Common(3)));
        Assert.Equal(1, s.FreeCopies(EquipRef.Common(3)));                // 뼈 조각 2개 중 1개 낌
        s.InventoryItems.Remove(3);
        Assert.False(s.Equip(7001, 3, EquipRef.Common(11)));
        s.Unequip(7001, 3);
        Assert.False(s.Equip(7001, 3, EquipRef.Common(3)));               // 인벤토리에 없으면 보유하지 않은 것
    }

    [Fact]
    public void Equipping_another_kind_in_the_same_slot_replaces_the_old_one()
    {
        var s = Session();
        s.Equip(7001, 3, EquipRef.Common(3));
        Assert.True(s.Equip(7001, 3, EquipRef.Char(7002)));
        Assert.Equal(EquipRef.Char(7002), s.EquippedItemOf(7001, 3));
        Assert.Equal(2, s.FreeCopies(EquipRef.Common(3)));
    }

    [Fact]
    public void An_equipped_common_item_cannot_be_spent_in_a_shop_trade()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n11\t금화\t99999\n3\t뼈 조각\t99\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t강령술사\t1\t1\t2\n"),
            ("master/shop_master_v1.tsv", "code\tname\n2001\t상점\n"),
            ("master/shop_trade_master_v1.tsv",
                "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\n5001\t2001\t3\t2\t11\t10\n"),
            ("master/equipment_slot_master_v1.tsv", "equipment_slot_code\tequipment_slot_name\n3\t소환\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n8\t소환수\n3\t메인 통화\n"),
            ("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n2\t3\n3\t8\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n"),
            ("master/equipment_slot_tag_master_v1.tsv",
                "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n7001\t3\t8\t1\ttrue\n")));
        var s = GameSession.NewGame(pack);
        s.Characters[7001] = new CharacterState(5, 36, 40, 3, 30);
        s.InventoryItems[3] = new InventoryItem(2);
        s.Equip(7001, 3, EquipRef.Common(3));

        s.Activate("102"); s.Activate("2001");
        s.Activate("5001");
        Assert.Contains("교환 실패", s.Message);
        Assert.Equal(2, s.InventoryItems[3].Amount);                       // 낀 뼈 조각 때문에 2개를 다 쓸 수 없다
    }

    // ---- 화면 ----

    [Fact]
    public void The_select_screen_numbers_the_candidates_from_one_across_kinds()
    {
        var s = Session();
        s.Activate("103"); s.Activate("11"); s.Activate("1"); s.Activate("3");     // 강령술사의 소환 슬롯
        Assert.Equal(["1", "2", "999"], s.Entries.Select(e => e.Code));
        Assert.Equal(["뼈 조각", "강시", "돌아가기"], s.Entries.Select(e => e.Label));

        s.Activate("2");                                                          // 강시
        Assert.Equal("장착: 강령술사의 소환 ← 강시", s.Message);
        Assert.Equal(EquipRef.Char(7002), s.EquippedItemOf(7001, 3));
        Assert.Equal(Screen.EquipmentList, s.Screen);
        Assert.Contains(s.Entries, e => e.Label == "소환 : 강시");
    }

    // ---- 세이브 ----

    [Fact]
    public void Equipped_kinds_round_trip_through_the_save()
    {
        var s = Session();
        s.Equip(7001, 1, EquipRef.Unique(s.InstancesOf(8001)[0]));
        s.Equip(7001, 3, EquipRef.Char(7002));

        var json = s.ToSaveJson();
        Assert.Contains("\"Kind\": \"Character\"", json);
        var loaded = GameSession.FromSaveJson(Pack(), json);
        Assert.Equal(8001, loaded.EquippedUniqueCode(7001, 1));
        Assert.Equal(EquipRef.Char(7002), loaded.EquippedItemOf(7001, 3));
    }

    const string Head = "{\"Version\":1,\"PackId\":\"demo\",";
    const string Chars =
        "\"Characters\":{\"7001\":{\"Level\":5,\"Hp\":36,\"MaxHp\":40,\"Xp\":3,\"XpMax\":30},\"7002\":{\"Level\":1,\"Hp\":10,\"MaxHp\":10,\"Xp\":0,\"XpMax\":30}},";

    [Theory]
    [InlineData("\"EquippedItems\":{\"7001\":{\"3\":{\"Kind\":\"Character\",\"Code\":7003}}}")]               // 보유하지 않은 캐릭터를 낌
    [InlineData("\"EquippedItems\":{\"7001\":{\"3\":{\"Kind\":\"Character\",\"Code\":7001}}}")]               // 자기 자신
    [InlineData("\"EquippedItems\":{\"7001\":{\"3\":{\"Kind\":\"CommonItem\",\"Code\":3}}}")]                // 인벤토리에 없는 커먼 아이템
    [InlineData("\"InventoryItems\":{\"1\":{\"Amount\":5}},\"EquippedItems\":{\"7001\":{\"3\":{\"Kind\":\"CommonItem\",\"Code\":1}}}")]  // 태그에 부합하지 않음
    [InlineData("\"EquippedItems\":{\"7001\":{\"3\":{\"Kind\":\"CommonItem\",\"Code\":77}}}")]                // 마스터에 없음
    [InlineData("\"EquippedItems\":{\"7001\":{\"3\":{\"Kind\":\"Pet\",\"Code\":7002}}}")]                     // 알 수 없는 종류
    public void Invalid_multi_kind_equipment_in_a_save_is_rejected(string fragment)
    {
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(), Head + Chars + fragment + "}"));
    }

    [Fact]
    public void A_valid_multi_kind_save_loads()
    {
        var s = GameSession.FromSaveJson(Pack(), Head + Chars +
            "\"InventoryItems\":{\"3\":{\"Amount\":1}}," +
            "\"EquippedItems\":{\"7001\":{\"3\":{\"Kind\":\"CommonItem\",\"Code\":3}}}}");
        Assert.Equal(EquipRef.Common(3), s.EquippedItemOf(7001, 3));
    }

    [Fact]
    public void The_equipping_characters_own_tags_are_combined_with_the_item_tags()
    {
        // 슬롯 규칙: 검 AND 소환수. 철검은 검 태그만 있고, 소환수 태그는 장비하는 캐릭터(7001)가 가진다.
        GamePack Make(string characterTags) => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t강령술사\t1\t1\t2\n"),
            ("master/equipment_slot_master_v1.tsv", "equipment_slot_code\tequipment_slot_name\n1\t무기\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n9\t검\n8\t소환수\n3\t메인 통화\n"),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"),
            ("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n8001\t9\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n" + characterTags),
            ("master/equipment_slot_tag_master_v1.tsv",
                "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n7001\t1\t9\t1\ttrue\n7001\t1\t8\t1\ttrue\n")));

        Assert.True(Make("7001\t8\n").CanEquip(7001, 1, 8001));
        Assert.False(Make("").CanEquip(7001, 1, 8001));
    }
}
