using Wera.Core;

namespace Wera.Core.Tests;

// 같은 캐릭터를 여러 명 가질 수 있다: 캐릭터 인벤토리 마스터의 carry_limit이 한 코드의 최대 수이고, 보유한 캐릭터는 인스턴스 ID로 가리킨다.
//  용사 7001은 한도 2, 마법사 7002와 플레이어 1은 한도 1. 용사는 리더(2)와 정비(4) 태그를 마스터에서 가진다.
public class DuplicateCharacterTests
{
    static GamePack Pack() => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
        ("master/character_inventory_master_v1.tsv",
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
            "1\t당신\t1\t1\t2\n7001\t용사\t2\t1\t2\n7002\t마법사\t1\t2\t1\n"),
        ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n11\t금화\t99999\n4\t철광석\t99\n"),
        ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"),
        ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t정비\n9\t검\n"),
        ("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n8001\t9\n"),
        ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7001\t2\n7001\t4\n7002\t2\n"),
        ("master/equipment_slot_master_v1.tsv", "equipment_slot_code\tequipment_slot_name\n1\t무기\n"),
        ("master/equipment_slot_tag_master_v1.tsv",
            "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n7001\t1\t9\t1\ttrue\n"),
        ("master/status_effect_master_v1.tsv", "status_effect_code\tstatus_effect_name\n2\t화상\n"),
        ("master/status_effect_effect_master_v1.tsv",
            "status_effect_code\ttrigger_type\tpriority\teffect_code\teffect_value_1\teffect_value_2\teffect_value_3\n2\tAfterTurnEnd\t1\t1\t-5\t2\t0\n"),
        ("master/facility_master_v1.tsv", "code\tname\n1\t정문\n2\t대장간\n"),
        ("master/operator_slot_master_v1.tsv", "code\tname\n1\t정비\n"),
        ("master/facility_operator_slot_master_v1.tsv", "facility_code\toperator_slot_code\n2\t1\n"),
        ("master/operator_slot_tag_master_v1.tsv", "operator_slot_code\ttag_code\n1\t4\n"),
        ("master/shop_master_v1.tsv", "code\tname\n2001\t상점\n"),
        ("master/shop_trade_master_v1.tsv",
            "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\tcost_kind\treceive_kind\n" +
            "5003\t2001\t11\t500\t7001\t1\t\tcharacter\n" +
            "5004\t2001\t7001\t1\t11\t300\tcharacter\tcommon_item\n")));

    // 용사 둘(앞, 뒤)을 가진 세션.
    static (GameSession S, string A, string B) Session()
    {
        var s = GameSession.NewGame(Pack());
        var a = s.AddCharacterInstance(7001)!;
        var b = s.AddCharacterInstance(7001)!;
        return (s, a, b);
    }

    // ---- 소지 한도 ----

    [Fact]
    public void AddCharacter_makes_a_new_instance_until_the_carry_limit()
    {
        var s = GameSession.NewGame(Pack());
        Assert.True(s.AddCharacter(7001));
        Assert.True(s.AddCharacter(7001));
        Assert.False(s.AddCharacter(7001));                                // 한도 2
        Assert.Equal(2, s.OwnedCountOf(7001));
        Assert.Equal(2, s.CharacterInstancesOf(7001).Select(c => c.InstanceId).Distinct().Count());
        Assert.False(s.AddCharacter(7002 + 100));                          // 마스터에 없는 코드
        Assert.True(s.AddCharacter(7002));
        Assert.False(s.AddCharacter(7002));                                // 한도 1
        Assert.False(s.AddCharacter(GamePack.PlayerCharacterCode));        // 플레이어는 이미 있고 한도 1
        Assert.Equal(2, s.CharacterLimitOf(7001));
    }

    [Fact]
    public void An_inventory_cell_and_a_trade_follow_the_same_limit()
    {
        var s = GameSession.NewGame(Pack());
        s.AddInventory(7001, 1);
        s.AddInventory(7001, 1);
        s.AddInventory(7001, 1);                                           // 한도라 아무 일 없다
        Assert.Equal(2, s.CountInventory(7001));

        s.Characters.Remove(s.InstanceIdOf(7001)!);
        s.Characters.Remove(s.InstanceIdOf(7001)!);
        s.InventoryItems[11] = new InventoryItem(1500);
        s.Pick("104"); s.Pick("2001"); s.Pick("5003");
        s.Pick("5003");
        Assert.Equal(2, s.OwnedCountOf(7001));
        Assert.Equal(500, s.InventoryItems[11].Amount);
        s.Pick("5003");                                                    // 한도라 교환되지 않는다: 금화도 그대로
        Assert.Equal(2, s.OwnedCountOf(7001));
        Assert.Equal(500, s.InventoryItems[11].Amount);
    }

    [Fact]
    public void Paying_a_character_in_a_trade_gives_up_a_spare_copy_and_keeps_the_placed_one()
    {
        var (s, a, b) = Session();
        s.Party[0] = 7001;                                                 // 파티에 세운 인스턴스(앞)
        var placed = ((PartyUnit)s.Parties[0].Units[0]!).InstanceId;
        s.Pick("104"); s.Pick("2001"); s.Pick("5004");
        Assert.Equal(1, s.OwnedCountOf(7001));
        Assert.Equal(placed, s.InstanceIdOf(7001));                        // 서 있는 쪽이 남았다
        Assert.Equal(300, s.InventoryItems[11].Amount);
        Assert.Contains(placed, new[] { a, b });                           // 남은 것은 처음 가진 둘 중 하나
    }

    // ---- 인스턴스마다 따로 ----

    [Fact]
    public void Two_copies_in_different_slots_keep_separate_statuses_equipment_and_tags()
    {
        var (s, a, b) = Session();
        s.Party[0] = 7001;
        s.Party[1] = 7001;
        var first = ((PartyUnit)s.Parties[0].Units[0]!).InstanceId;
        var second = ((PartyUnit)s.Parties[0].Units[1]!).InstanceId;
        Assert.NotEqual(first, second);                                    // 두 번째는 아직 서지 않은 다른 인스턴스
        Assert.Equal([a, b], new[] { first, second }.Order());

        Assert.True(s.ApplyStatusEffect(first, 2));
        Assert.Single(s.StatusEffectsOf(first));
        Assert.Empty(s.StatusEffectsOf(second));

        s.AddUniqueItem(8001);
        var sword = EquipRef.Unique(s.InstancesOf(8001)[0]);
        Assert.True(s.Equip(first, 1, sword));
        Assert.Equal(sword, s.EquippedItemOf(first, 1));
        Assert.Null(s.EquippedItemOf(second, 1));

        Assert.True(s.AddCharacterTag(first, 9));
        Assert.Contains(9L, s.CharacterTags(first));
        Assert.DoesNotContain(9L, s.CharacterTags(second));
    }

    [Fact]
    public void A_copy_is_in_only_one_place_a_party_slot_or_an_operator_slot_or_a_defense_party()
    {
        var (s, a, b) = Session();
        Assert.True(s.BuildFacility(0, 2));
        s.HideoutParties[0].Units[0] = new PartyUnit(7001, [], IsLeader: true, InstanceId: a);

        Assert.Equal(0, s.HideoutFloorOf(a));
        Assert.Equal(-1, s.HideoutFloorOf(b));
        Assert.True(s.CanOperate(b, 1));
        Assert.True(s.SetHideoutOperatorInstance(0, true, 1, b));          // 다른 복사본은 서 있어도 오퍼레이터가 될 수 있다
        Assert.Equal(b, s.HideoutOperatorInstanceOf(0, true, 1));
        Assert.Equal(7001, s.HideoutOperatorOf(0, true, 1));
        Assert.Equal(0, s.HideoutFloorOf(a));                              // 방어 파티의 복사본은 그대로

        Assert.True(s.SetHideoutOperatorInstance(0, true, 1, a));          // 방어 파티에 선 복사본을 오퍼레이터로 하면 파티에서 빠진다
        Assert.Equal(-1, s.HideoutFloorOf(a));
        Assert.Equal(a, s.HideoutOperatorInstanceOf(0, true, 1));
    }

    // ---- 목록 ----

    [Fact]
    public void Character_lists_show_each_copy_as_its_own_row()
    {
        var (s, a, b) = Session();
        s.Pick("105"); s.Pick("1");
        Assert.Equal(Screen.CharacterSelect, s.Screen);
        var rows = s.CharacterList.Where(c => c.Code == 7001).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal([a, b], rows.Select(r => r.InstanceId));

        var row = s.CharacterList.ToList().FindIndex(c => c.InstanceId == b) + 1;     // 줄 번호로 둘째 복사본을 고른다
        s.Pick(row.ToString());
        Assert.Equal(b, ((PartyUnit)s.Parties[0].Units[0]!).InstanceId);
    }

    [Fact]
    public void The_companion_list_has_a_row_per_copy()
    {
        var (s, _, _) = Session();
        Assert.Equal(2, s.Companions.Count(r => r.Name == "용사"));
    }

    // ---- 내보내기 ----

    [Fact]
    public void Removing_one_copy_leaves_the_other()
    {
        var (s, a, b) = Session();
        s.Party[0] = 7001;
        s.ApplyStatusEffect(b, 2);
        Assert.True(s.RemoveCharacter(b));
        Assert.Equal(1, s.OwnedCountOf(7001));
        Assert.Equal(a, s.InstanceIdOf(7001));
        Assert.False(s.CharacterStatusEffects.ContainsKey(b));
        Assert.False(s.CharacterTagInstances.ContainsKey(b));
        Assert.False(s.RemoveCharacter(b));                                // 이미 없다
        Assert.True(s.AddCharacter(7001));                                 // 한도에 여유가 생겼다
    }

    [Fact]
    public void Removing_by_code_prefers_a_copy_that_is_not_placed()
    {
        var (s, _, _) = Session();
        s.Party[0] = 7001;
        var placed = ((PartyUnit)s.Parties[0].Units[0]!).InstanceId;
        Assert.True(s.RemoveCharacter(7001));
        Assert.Equal(placed, s.InstanceIdOf(7001));
    }

    // ---- 코드로 찾는 것은 "어느 인스턴스든" ----

    [Fact]
    public void Code_based_lookups_mean_any_instance_and_first_for_a_single_pick()
    {
        var (s, a, b) = Session();
        Assert.Equal(a, s.InstanceIdOf(7001));                             // 첫 인스턴스: OrderNumber순
        Assert.Equal(2, s.CountInventory(7001));
        Assert.False(s.CharacterHasTag(7001, 9));
        s.AddCharacterTag(b, 9);                                           // 둘째에만 태그가 있어도 코드로는 가진 것이다
        Assert.True(s.CharacterHasTag(7001, 9));
        Assert.True(s.CanOperate(7001, 1));
        Assert.True(s.ApplyStatusEffect(7001, 2));                         // 코드로 걸면 첫 인스턴스에 걸린다
        Assert.Single(s.StatusEffectsOf(a));
        Assert.Empty(s.StatusEffectsOf(b));
        s.Party[0] = 7001;                                                 // 코드로 세우면 서지 않은 첫 인스턴스가 선다
        Assert.Equal(a, ((PartyUnit)s.Parties[0].Units[0]!).InstanceId);
    }

    [Fact]
    public void A_party_unit_with_an_empty_id_resolves_by_code_to_the_first_instance()
    {
        var (s, a, _) = Session();
        s.Parties[0].Units[0] = new PartyUnit(7001, [], IsLeader: true);
        Assert.Equal(a, s.InstanceIdOf(7001));
        Assert.Equal(0, s.PartyIndexOf(7001));                             // 코드로도, 첫 인스턴스의 ID로도 같은 칸
        Assert.Equal(0, s.PartyIndexOf(a));
    }

    // ---- 세이브 ----

    [Fact]
    public void Duplicates_round_trip_through_the_save()
    {
        var (s, a, b) = Session();
        s.Party[0] = 7001;                                                 // 앞 복사본(a)은 파티, 뒤 복사본(b)은 오퍼레이터
        s.ApplyStatusEffect(b, 2);
        s.AddCharacterTag(a, 9);
        Assert.True(s.BuildFacility(0, 2));
        s.SetHideoutOperatorInstance(0, true, 1, b);
        s.SetSupportInstance(SupportRole.Partner, a);

        var loaded = GameSession.FromSaveJson(Pack(), s.ToSaveJson());
        Assert.Equal(s.Characters.Keys.Order(), loaded.Characters.Keys.Order());
        Assert.Equal(2, loaded.OwnedCountOf(7001));
        Assert.Single(loaded.StatusEffectsOf(b));
        Assert.Empty(loaded.StatusEffectsOf(a));
        Assert.Contains(9L, loaded.CharacterTags(a));
        Assert.DoesNotContain(9L, loaded.CharacterTags(b));
        Assert.Equal(b, loaded.HideoutOperatorInstanceOf(0, true, 1));
        Assert.Equal(a, loaded.PartnerId);
        Assert.Equal(((PartyUnit)s.Parties[0].Units[0]!).InstanceId, ((PartyUnit)loaded.Parties[0].Units[0]!).InstanceId);
    }

    [Fact]
    public void A_save_with_a_copy_in_two_places_or_over_the_limit_is_rejected()
    {
        var (s, a, _) = Session();
        s.Parties[0].Units[0] = new PartyUnit(7001, [], IsLeader: true, InstanceId: a);
        s.Parties[1].Units[0] = new PartyUnit(7001, [], IsLeader: true, InstanceId: a);       // 같은 복사본이 두 파티에
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(), s.ToSaveJson()));

        var (t, _, _) = Session();
        var extra = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        t.Characters[extra] = t.FirstInstanceOf(7001)! with { InstanceId = extra, OrderNumber = 9 };       // 셋째: 한도 2 초과
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(), t.ToSaveJson()));

        var (u, ua, _) = Session();
        var root = System.Text.Json.Nodes.JsonNode.Parse(u.ToSaveJson())!;
        root["Characters"]![ua]!["InstanceId"] = "01ARZ3NDEKTSV4RRFFQ69G5FAW";                // 키와 안의 ID가 다르다
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(), root.ToJsonString()));
    }
}
