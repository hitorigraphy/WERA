using Wera.Core;

namespace Wera.Core.Tests;

// 상점 교환의 코스트/리시브로 유니크 아이템과 캐릭터를 쓸 수 있다 (cost_kind, receive_kind 열).
//  상점 2001. 금화 = 커먼 아이템 1, 철검 = 유니크 아이템 8001(검 태그), 캐릭터 7001 용사, 7002 마법사(소환수 태그).
//  용사의 슬롯 1(무기) = 검, 슬롯 3(소환) = 소환수.
public class TradeKindTests
{
    const string Header = "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\tcost_kind\treceive_kind\n";

    const string Trades =
        Header +
        "5001\t2001\t11\t100\t8001\t1\tcommon_item\tunique_item\n" +     // 금화 100 → 철검 ×1
        "5002\t2001\t8001\t1\t11\t50\tunique_item\t\n" +                // 철검 ×1 → 금화 50 (받는 쪽 종류 생략 = 커먼)
        "5003\t2001\t11\t500\t7002\t1\t\tcharacter\n" +                  // 금화 500 → 마법사
        "5004\t2001\t7002\t1\t11\t300\tcharacter\tcommon_item\n";         // 마법사 → 금화 300

    static GamePack Pack(string trades = Trades, string? tradeFileHeaderOverride = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n11\t금화\t99999\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n"),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t검\n8\t소환수\n"),
            ("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n8001\t3\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7002\t8\n"),
            ("master/equipment_slot_master_v1.tsv", "equipment_slot_code\tequipment_slot_name\n1\t무기\n3\t소환\n"),
            ("master/equipment_slot_tag_master_v1.tsv",
                "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n7001\t1\t3\t1\ttrue\n7001\t3\t8\t1\ttrue\n"),
            ("master/shop_master_v1.tsv", "code\tname\n2001\t상점\n"),
            ("master/shop_trade_master_v1.tsv", trades),
        };
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    static GameSession Session(long coins = 1000)
    {
        var s = GameSession.NewGame(Pack());
        s.AddCharacter(7001);
        s.InventoryItems[11] = new InventoryItem(coins);
        return s;
    }

    static string Trade(GameSession s, string tradeCode)
    {
        s.Activate("102"); s.Activate("2001"); s.Activate(tradeCode);
        return s.Message!;
    }

    // ---- 마스터 ----

    [Fact]
    public void Kinds_are_loaded_and_an_empty_cell_means_common_item()
    {
        var trades = Pack().Trades!;
        Assert.Equal((TradeKind.CommonItem, TradeKind.UniqueItem), (trades[0].CostKind, trades[0].ReceiveKind));
        Assert.Equal((TradeKind.UniqueItem, TradeKind.CommonItem), (trades[1].CostKind, trades[1].ReceiveKind));
        Assert.Equal((TradeKind.CommonItem, TradeKind.Character), (trades[2].CostKind, trades[2].ReceiveKind));
        Assert.Equal((TradeKind.Character, TradeKind.CommonItem), (trades[3].CostKind, trades[3].ReceiveKind));
        Assert.Equal(TradeKind.Character, Pack().Masters.ShopTradeRowTable.FindByCode(5004).CostKind);
    }

    [Fact]
    public void The_two_kind_columns_are_optional_and_default_to_common_items()
    {
        var pack = Pack("code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\n5001\t2001\t11\t100\t11\t5\n");
        Assert.Equal((TradeKind.CommonItem, TradeKind.CommonItem), (pack.Trades![0].CostKind, pack.Trades[0].ReceiveKind));
    }

    [Theory]
    [InlineData("code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\tcost_kind\n5001\t2001\t11\t1\t11\t1\tcommon_item\n", "cost_kind")]   // 한 열만
    [InlineData("code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\treceive_kind\tcost_kind\n5001\t2001\t11\t1\t11\t1\t\t\n", "cost_kind")]  // 순서가 다름
    [InlineData(Header + "5001\t2001\t11\t1\t11\t1\tpet\tcommon_item\n", "pet")]                                // 알 수 없는 종류
    [InlineData(Header + "5001\t2001\t7002\t2\t11\t1\tcharacter\tcommon_item\n", "캐릭터이면 어마운트는 1")]       // 캐릭터 코스트 2개
    [InlineData(Header + "5001\t2001\t11\t1\t7002\t2\tcommon_item\tcharacter\n", "캐릭터이면 어마운트는 1")]       // 캐릭터 리시브 2개
    [InlineData(Header + "5001\t2001\t8001\t2\t11\t1\tunique_item\tcommon_item\n", "유니크 아이템이면 어마운트는 1")]   // 유니크 코스트 2개
    [InlineData(Header + "5001\t2001\t11\t1\t8001\t2\tcommon_item\tunique_item\n", "유니크 아이템이면 어마운트는 1")]   // 유니크 리시브 2개
    [InlineData(Header + "5001\t2001\t99\t1\t11\t1\tcharacter\tcommon_item\n", "코스트 '99'")]                 // 캐릭터 인벤토리에 없다
    [InlineData(Header + "5001\t2001\t11\t1\t99\t1\tcommon_item\tunique_item\n", "리시브 '99'")]               // 유니크 아이템 인벤토리에 없다
    [InlineData(Header + "5001\t2001\t8001\t1\t11\t1\tcommon_item\tcommon_item\n", "코드 '8001'")]           // 8001은 커먼 아이템이 아니다
    public void Bad_kinds_amounts_and_codes_are_rejected(string trades, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Pack(trades)).Message);

    [Fact]
    public void The_trade_list_shows_names_of_every_kind()
    {
        var s = Session();
        s.Activate("102"); s.Activate("2001");
        Assert.Equal(
            ["금화 ×100 → 철검 ×1", "철검 ×1 → 금화 ×50", "금화 ×500 → 마법사 ×1", "마법사 ×1 → 금화 ×300"],
            s.CurrentShopTrades.Select(t => t.Text));
    }

    // ---- 유니크 아이템 받기 ----

    [Fact]
    public void Receiving_a_unique_item_makes_one_instance_with_master_tags()
    {
        var s = Session();
        Assert.Equal("교환 완료: 금화 ×100 → 철검 ×1", Trade(s, "5001"));
        Assert.Equal(900, s.InventoryItems[11].Amount);
        var id = Assert.Single(s.InstancesOf(8001));
        Assert.True(Ulid.TryParse(id, out _));                          // 인스턴스 ID는 ULID
        Assert.Equal([3L], s.TagsOf(EquipRef.Unique(id)));

        Trade(s, "5001");                                               // 또 받으면 다른 인스턴스
        Assert.Equal(2, s.InstancesOf(8001).Count);
    }

    [Fact]
    public void Failing_to_pay_for_a_unique_item_changes_nothing()
    {
        var s = Session(coins: 50);
        Assert.StartsWith("교환 실패 - 부족: 금화", Trade(s, "5001"));
        Assert.Equal(50, s.InventoryItems[11].Amount);
        Assert.Empty(s.UniqueItemInstances);
    }

    // ---- 유니크 아이템 내기 ----

    [Fact]
    public void Paying_with_a_unique_item_uses_up_one_unequipped_instance()
    {
        var s = Session(coins: 0);
        var first = s.AddUniqueItem(8001)!; var second = s.AddUniqueItem(8001)!;
        Assert.Equal("교환 완료: 철검 ×1 → 금화 ×50", Trade(s, "5002"));
        Assert.Equal([second], s.InstancesOf(8001));                  // 작은 ID(먼저 얻은 것)부터 쓴다
        Assert.DoesNotContain(first, s.InstancesOf(8001));
        Assert.Equal(50, s.InventoryItems[11].Amount);
    }

    [Fact]
    public void An_equipped_unique_item_cannot_be_paid()
    {
        var s = Session(coins: 0);
        var equipped = s.AddUniqueItem(8001)!;
        s.Equip(7001, 1, EquipRef.Unique(equipped));
        Assert.StartsWith("교환 실패 - 부족: 철검 (보유 0 / 필요 1)", Trade(s, "5002"));
        Assert.Single(s.UniqueItemInstances);
        Assert.Equal(0, s.InventoryItems[11].Amount);

        s.AddUniqueItem(8001);                                        // 끼지 않은 것이 생기면 그것을 낸다
        Assert.StartsWith("교환 완료", Trade(s, "5002"));
        Assert.Equal([equipped], s.InstancesOf(8001));
        Assert.Equal(EquipRef.Unique(equipped), s.EquippedItemOf(7001, 1));
    }

    // ---- 캐릭터 받기 ----

    [Fact]
    public void Receiving_a_character_adds_it_with_its_initial_state_and_tags()
    {
        var s = Session();
        Assert.Equal("교환 완료: 금화 ×500 → 마법사 ×1", Trade(s, "5003"));
        Assert.True(s.Characters.ContainsKey(7002));
        Assert.Equal(new CharacterState(1, 1, 1, 0, 0), s.Characters[7002]);     // 스테이터스 마스터가 없는 게임팩의 기본
        Assert.Equal([8L], s.CharacterTags(7002));
        Assert.Equal(500, s.InventoryItems[11].Amount);
    }

    [Fact]
    public void An_owned_character_cannot_be_received_again_and_nothing_is_spent()
    {
        var s = Session();
        s.AddCharacter(7002);
        Assert.StartsWith("교환 실패 - 이미 보유한 캐릭터: 마법사", Trade(s, "5003"));
        Assert.Equal(1000, s.InventoryItems[11].Amount);
    }

    // ---- 캐릭터 내기 ----

    [Fact]
    public void Paying_with_a_character_removes_it_from_everything()
    {
        var s = Session(coins: 0);
        s.AddCharacter(7002);
        s.Party[4] = 7002;
        s.AddUniqueItem(8001);
        s.Equip(7002, 1, 8001);                                       // 마법사에게는 슬롯이 없어 끼지 못한다(규칙 없음)
        s.AddCharacterTag(7002, 3);

        Assert.Equal("교환 완료: 마법사 ×1 → 금화 ×300", Trade(s, "5004"));
        Assert.False(s.Characters.ContainsKey(7002));
        Assert.Null(s.Party[4]);
        Assert.False(s.CharacterTagInstances.ContainsKey(7002));
        Assert.Equal(300, s.InventoryItems[11].Amount);
    }

    [Fact]
    public void A_departing_character_returns_its_equipment()
    {
        // 용사는 마법사를 소환 슬롯에 낀다 → 마법사는 낼 수 없다. 용사가 가진 철검을 보유로 돌려주는 쪽은 RemoveCharacter.
        var s = Session(coins: 0);
        s.AddUniqueItem(8001);
        s.Equip(7001, 1, 8001);
        Assert.True(s.RemoveCharacter(7001));
        Assert.Empty(s.EquippedItems);
        Assert.Equal(1, s.FreeCopies(8001));
        Assert.False(s.RemoveCharacter(7001));                        // 이미 없다
    }

    [Fact]
    public void A_character_equipped_on_another_cannot_be_paid()
    {
        var s = Session(coins: 0);
        s.AddCharacter(7002);
        Assert.True(s.Equip(7001, 3, EquipRef.Char(7002)));           // 용사의 소환 슬롯에 마법사
        Assert.StartsWith("교환 실패 - 장비 슬롯에 끼워 둔 캐릭터: 마법사", Trade(s, "5004"));
        Assert.True(s.Characters.ContainsKey(7002));
        Assert.False(s.RemoveCharacter(7002));
    }

    [Fact]
    public void A_character_not_owned_cannot_be_paid()
    {
        var s = Session(coins: 0);
        Assert.StartsWith("교환 실패 - 부족: 마법사 (보유 0 / 필요 1)", Trade(s, "5004"));
    }

    [Fact]
    public void Paying_and_receiving_the_same_character_is_allowed()
    {
        var pack = Pack(Header + "5001\t2001\t7002\t1\t7002\t1\tcharacter\tcharacter\n");
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7002);
        s.Characters[7002] = new CharacterState(9, 5, 50, 10, 99);
        s.Activate("102"); s.Activate("2001"); s.Activate("5001");
        Assert.StartsWith("교환 완료", s.Message);
        Assert.Equal(new CharacterState(1, 1, 1, 0, 0), s.Characters[7002]);     // 새 캐릭터로 다시 시작
    }
}
