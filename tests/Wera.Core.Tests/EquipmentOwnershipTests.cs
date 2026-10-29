using Wera.Core;

namespace Wera.Core.Tests;

// 장비는 보유해야 낄 수 있다. (슬롯/태그 구성은 EquipmentTests와 같다:
//  용사 7001: 무기 = (검 AND 중량 아님) OR 마법, 방어구 = 갑옷 / 마법사 7002: 무기 = 마법 / 도적 7003: 장비 슬롯 없음.
//  장비 9001 철검, 9002 대검(중량), 9003 지팡이(마법), 9004 판금갑옷, 9005 천 옷.)
public class EquipmentOwnershipTests
{
    static GameSession Session(params (long item, long count)[] owned)
    {
        var s = GameSession.NewGame(EquipmentTests.Pack());
        s.Characters[7001] = CharacterInstance.Stats(5, 36, 40, 3, 30);
        s.Characters[7002] = CharacterInstance.Stats(3, 10, 25, 12, 30);
        s.Party[0] = 7001;
        s.Party[4] = 7002;
        foreach (var (item, count) in owned)
            for (var i = 0; i < count; i++) s.AddUniqueItem(item);
        return s;
    }

    /// <summary>파티 설정 → 장비 착용 → 슬롯 번호 → 장비 슬롯 코드까지 들어간 세션.</summary>
    static GameSession InSelect(string partySlot, string equipmentSlot, params (long item, long count)[] owned)
    {
        var s = Session(owned);
        s.Activate("105"); s.Activate("11"); s.Activate(partySlot); s.Activate(equipmentSlot);
        return s;
    }

    // ---- 코어: 보유와 장착 ----

    [Fact]
    public void Equipment_must_be_owned_to_be_equipped()
    {
        var s = Session();
        Assert.False(s.Equip(7001, 1, 9001));                    // 보유하지 않음
        Assert.Null(s.EquippedItemOf(7001, 1));

        s.AddUniqueItem(9001);
        Assert.True(s.Equip(7001, 1, 9001));
        Assert.Equal(9001, s.EquippedUniqueCode(7001, 1));
    }

    [Fact]
    public void Owned_but_unfit_equipment_cannot_be_equipped()
    {
        var s = Session((9002, 1), (9004, 1));
        Assert.False(s.Equip(7001, 1, 9002));                    // 대검: 중량이라 무기 슬롯 태그에 부합하지 않음
        Assert.False(s.Equip(7001, 1, 9004));                    // 갑옷은 무기가 아니다
        Assert.True(s.Equip(7001, 2, 9004));                     // 방어구에는 가능
        Assert.False(s.Equip(7002, 2, 9004));                    // 마법사는 방어구 슬롯이 없다
    }

    [Fact]
    public void Unowned_character_or_unknown_slot_or_item_is_refused()
    {
        var s = Session((9001, 1));
        Assert.False(s.Equip(7003, 1, 9001));                    // 보유하지 않은 캐릭터(마스터에는 있음)
        Assert.False(s.Equip(9999, 1, 9001));
        Assert.False(s.Equip(7001, 99, 9001));
        Assert.False(s.Equip(7001, 1, 99999));
        Assert.All(s.PartyInstance.Units.OfType<PartyUnit>(), u => Assert.Empty(u.Equipment));
    }

    [Fact]
    public void A_single_owned_copy_cannot_be_on_two_characters_at_once()
    {
        // 지팡이는 용사(무기, 두 번째 그룹)와 마법사(무기) 둘 다 낄 수 있다.
        var s = Session((9003, 1));
        Assert.True(s.Equip(7001, 1, 9003));
        Assert.False(s.Equip(7002, 1, 9003));                    // 남는 개수가 없다
        Assert.Equal(9003, s.EquippedUniqueCode(7001, 1));
        Assert.Null(s.EquippedItemOf(7002, 1));

        s.AddUniqueItem(9003);                                   // 한 개 더 보유하면 가능
        Assert.True(s.Equip(7002, 1, 9003));
        Assert.Equal((2L, 0L), (s.EquippedCount(9003), s.FreeCopies(9003)));
    }

    [Fact]
    public void Equipping_over_another_item_replaces_it_and_returns_it_to_the_free_copies()
    {
        var s = Session((9001, 1), (9003, 1));
        s.Equip(7001, 1, 9001);
        Assert.Equal(0, s.FreeCopies(9001));

        Assert.True(s.Equip(7001, 1, 9003));

        Assert.Equal(9003, s.EquippedUniqueCode(7001, 1));
        Assert.Equal(1, s.FreeCopies(9001));                     // 철검은 빠져 보유로 돌아갔다
        Assert.Equal(0, s.FreeCopies(9003));
    }

    [Fact]
    public void Equipping_the_same_item_again_changes_nothing_and_succeeds()
    {
        var s = Session((9001, 1));
        s.Equip(7001, 1, 9001);
        Assert.True(s.Equip(7001, 1, 9001));
        Assert.Equal(1, s.EquippedCount(9001));
    }

    [Fact]
    public void Unequip_frees_the_copy_and_cleans_up_empty_characters()
    {
        var s = Session((9001, 1), (9004, 1));
        s.Equip(7001, 1, 9001);
        s.Equip(7001, 2, 9004);

        Assert.True(s.Unequip(7001, 1));
        Assert.Null(s.EquippedItemOf(7001, 1));
        Assert.Equal(9004, s.EquippedUniqueCode(7001, 2));
        Assert.Equal(1, s.FreeCopies(9001));

        Assert.False(s.Unequip(7001, 1));                        // 이미 비어 있음
        Assert.True(s.Unequip(7001, 2));
        Assert.Empty(s.PartyInstance.UnitOf(7001)!.Equipment);         // 비면 빈 사전
        Assert.False(s.Unequip(7002, 1));
    }

    [Fact]
    public void Equipment_returns_to_the_inventory_when_its_unit_leaves_the_party()
    {
        var s = Session((9001, 1));
        s.Equip(7001, 1, 9001);
        Assert.Equal(0, s.FreeCopies(9001));

        s.Party[0] = null;                                       // 유닛이 빠지면 장비도 보유로 돌아간다
        Assert.Null(s.EquippedItemOf(7001, 1));
        Assert.Equal(1, s.FreeCopies(9001));

        s.Party[0] = 7001;                                       // 다시 두면 장비 없이 시작한다
        Assert.Null(s.EquippedItemOf(7001, 1));
        Assert.False(s.Equip(7003, 1, 9001));                    // 파티에 없는 캐릭터는 장비를 끼지 못한다
    }

    [Fact]
    public void Replacing_a_unit_drops_its_equipment_but_choosing_the_same_character_keeps_it()
    {
        var s = Session((9001, 1));
        s.Equip(7001, 1, 9001);
        s.Party[0] = 7001;                                       // 같은 캐릭터: 아무것도 바꾸지 않는다
        Assert.Equal(9001, s.EquippedUniqueCode(7001, 1));
        s.Party[0] = 7002;                                       // 다른 캐릭터로 교체: 원래 유닛의 장비는 보유로
        Assert.Equal(1, s.FreeCopies(9001));
        Assert.Null(s.EquippedItemOf(7002, 1));
    }

    [Fact]
    public void Swapping_party_slots_moves_the_equipment_with_the_unit()
    {
        var s = Session((9001, 1));
        s.Equip(7001, 1, 9001);
        s.Activate("105"); s.Activate("5");                      // 슬롯 5(칸 4, 마법사)를 고르고
        s.Activate("7001");                                      // 용사를 고르면 서로 바뀐다
        Assert.Equal(7001, s.Party[4]);
        Assert.Equal(7002, s.Party[0]);
        Assert.Equal(9001, s.EquippedUniqueCode(7001, 1));       // 장비는 유닛(용사)과 함께 이동했다
        Assert.Equal(0, s.FreeCopies(9001));
    }

    [Fact]
    public void The_party_has_a_name_that_survives_the_save()
    {
        var s = Session();
        Assert.Equal(GameSession.DefaultPartyName, s.PartyName);
        s.PartyName = "정예";
        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal("정예", loaded.PartyName);
        Assert.Equal(GameSession.DefaultPartyName, GameSession.FromSaveJson(s.Pack, "{\"Version\":1,\"PackId\":\"demo\"}").PartyName);
    }

    [Fact]
    public void An_old_save_moves_party_and_equipment_into_units_and_returns_equipment_of_characters_outside_the_party()
    {
        // 예전 형식(Party와 EquippedItems): 용사는 파티에 있고 마법사는 없다 → 마법사의 장비는 보유로 돌아간다
        var s = GameSession.FromSaveJson(EquipmentTests.Pack(),
            Head + Chars + @"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9001},""01H00000000000000000000002"":{""ItemCode"":9003}}," +
            @"""Party"":[7001]," +
            @"""EquippedItems"":{""7001"":{""1"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000001""}},""7002"":{""1"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000002""}}}}");
        Assert.Equal(9001, s.EquippedUniqueCode(7001, 1));
        Assert.Null(s.EquippedItemOf(7002, 1));
        Assert.Equal(1, s.FreeCopies(9003));
        Assert.Equal(GameSession.DefaultPartyName, s.PartyName);
        Assert.Equal(7001, s.PartyInstance.Units[0]!.CharacterCode);
    }

    // ---- 화면 ----

    [Fact]
    public void Equipment_list_labels_show_what_is_equipped()
    {
        var s = Session((9001, 1));
        s.Equip(7001, 1, 9001);
        s.Activate("105"); s.Activate("11"); s.Activate("1");

        Assert.Equal(["무기 : 철검", "방어구 : (없음)", "돌아가기"], s.Entries.Select(e => e.Label));
    }

    [Fact]
    public void The_select_screen_lists_only_owned_fitting_equipment_as_entries()
    {
        var s = InSelect("1", "1", (9001, 1), (9003, 1), (9002, 1), (9004, 1));   // 대검(불가), 판금갑옷(불가)도 보유

        Assert.Equal(["0", "1", "999"], s.Entries.Select(e => e.Code));   // 번호는 OrderNumber(얻은 순서)
        Assert.Equal(["철검", "지팡이", "돌아가기"], s.Entries.Select(e => e.Label));
        Assert.All(s.Entries.Take(2), e => Assert.Equal(EntryAction.EquipItem, e.Action));
        Assert.Equal(["철검", "지팡이"], s.EquippableItems.Select(i => i.Name));
    }

    [Fact]
    public void The_select_screen_is_sorted_by_order_number_each_time_and_uses_it_as_the_code()
    {
        var s = InSelect("1", "1", (9001, 1), (9003, 1));
        Assert.Equal(["철검", "지팡이"], s.Entries.Where(e => e.Action == EntryAction.EquipItem).Select(e => e.Label));

        var ids = s.UniqueItemInstances.Keys.ToList();                       // 철검, 지팡이 순으로 얻음
        s.UniqueItemInstances[ids[0]] = s.UniqueItemInstances[ids[0]] with { OrderNumber = 5 };
        var items = s.Entries.Where(e => e.Action == EntryAction.EquipItem).ToList();
        Assert.Equal(["지팡이", "철검"], items.Select(e => e.Label));       // 순서를 바꾸면 다시 열 때 그 순서
        Assert.Equal(["1", "5"], items.Select(e => e.Code));

        s.UniqueItemInstances[ids[0]] = s.UniqueItemInstances[ids[0]] with { OrderNumber = 1 };   // 같은 번호는 다음 빈 번호
        Assert.Equal(["1", "2"], s.Entries.Where(e => e.Action == EntryAction.EquipItem).Select(e => e.Code));
    }

    [Fact]
    public void Nothing_is_listed_when_no_fitting_equipment_is_owned()
    {
        var s = InSelect("1", "1", (9004, 1));
        Assert.Empty(s.EquippableItems);
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));
        Assert.Null(s.Activate("1"));
    }

    [Fact]
    public void Choosing_an_item_equips_it_and_returns_to_the_same_characters_slot_list_with_a_message()
    {
        var s = InSelect("1", "1", (9001, 1));

        var entry = s.Activate("0");

        Assert.Equal(EntryAction.EquipItem, entry!.Action);
        Assert.Equal(Screen.EquipmentList, s.Screen);
        Assert.Equal(7001, s.CurrentCharacterCode);
        Assert.Null(s.CurrentEquipmentSlotCode);
        Assert.Equal(9001, s.EquippedUniqueCode(7001, 1));
        Assert.Equal("장착: 용사의 무기 ← 철검", s.Message);
        Assert.Equal("무기 : 철검", s.Entries[0].Label);
    }

    [Fact]
    public void Choosing_another_item_replaces_the_equipped_one_and_says_so()
    {
        var s = InSelect("1", "1", (9001, 1), (9003, 1));
        s.Activate("0");
        s.Activate("1");                                          // 다시 무기 슬롯

        Assert.Equal(["0", "1", "2", "999"], s.Entries.Select(e => e.Code));   // 0번 해제 + 두 장비(OrderNumber 0, 1이 1, 2로 밀림)
        Assert.Equal(["해제", "철검 (장착중)", "지팡이", "돌아가기"], s.Entries.Select(e => e.Label));

        s.Activate("2");
        Assert.Equal("장착: 용사의 무기 철검 → 지팡이", s.Message);
        Assert.Equal(9003, s.EquippedUniqueCode(7001, 1));
        Assert.Equal(1, s.FreeCopies(9001));
    }

    [Fact]
    public void Choosing_the_item_already_in_the_slot_changes_nothing()
    {
        var s = InSelect("1", "1", (9001, 1));
        s.Activate("0");
        s.Activate("1");
        s.Activate("1");                                          // 해제가 0번이라 철검은 1번

        Assert.Equal("이미 장착 중: 용사의 무기 = 철검", s.Message);
        Assert.Equal(1, s.EquippedCount(9001));
    }

    [Fact]
    public void The_clear_entry_appears_only_when_something_is_equipped_and_unequips()
    {
        var s = InSelect("1", "1", (9001, 1));
        Assert.DoesNotContain(s.Entries, e => e.Action == EntryAction.Unequip);   // 아직 낀 것이 없다: 장비 번호는 0부터
        Assert.Equal("0", s.Entries[0].Code);

        s.Activate("0");
        s.Activate("1");
        Assert.Equal(["0", "1", "999"], s.Entries.Select(e => e.Code));          // 해제가 생기면 0번이 해제, 장비는 1부터
        var entry = s.Activate("0");

        Assert.Equal(EntryAction.Unequip, entry!.Action);
        Assert.Equal(Screen.EquipmentList, s.Screen);
        Assert.Equal("해제: 용사의 무기 철검", s.Message);
        Assert.Null(s.EquippedItemOf(7001, 1));
        Assert.Equal(1, s.FreeCopies(9001));
    }

    [Fact]
    public void An_item_with_no_free_copy_is_hidden_unless_it_is_the_one_equipped_here()
    {
        // 지팡이 한 개를 마법사가 끼면, 용사의 목록에서는 사라진다.
        var s = Session((9003, 1), (9001, 1));
        s.Equip(7002, 1, 9003);
        s.Activate("105"); s.Activate("11"); s.Activate("1"); s.Activate("1");
        Assert.Equal(["철검"], s.EquippableItems.Select(i => i.Name));
        Assert.Null(s.Activate("2"));

        // 마법사 본인의 목록에는 낀 채로 남는다.
        s.Activate("999"); s.Activate("999"); s.Activate("5"); s.Activate("1");
        Assert.Equal(["지팡이"], s.EquippableItems.Select(i => i.Name));
        Assert.Contains(s.Entries, e => e.Label == "지팡이 (장착중)");
    }

    [Fact]
    public void Back_from_the_select_screen_keeps_the_character_and_shows_no_message()
    {
        var s = InSelect("1", "1", (9001, 1));
        s.Activate("999");
        Assert.Equal(Screen.EquipmentList, s.Screen);
        Assert.Equal(7001, s.CurrentCharacterCode);
        Assert.Null(s.Message);
    }

    // ---- 세이브 ----

    [Fact]
    public void Save_roundtrip_keeps_owned_and_equipped_equipment()
    {
        var s = Session((9001, 2), (9003, 1), (9004, 1));
        s.Equip(7001, 1, 9001);
        s.Equip(7001, 2, 9004);
        s.Equip(7002, 1, 9003);

        var json = s.ToSaveJson();
        Assert.Contains("\"UniqueItems\"", json);
        Assert.Contains("\"Equipment\"", json);

        var loaded = GameSession.FromSaveJson(s.Pack, json);
        Assert.Equal([9001L, 9001L, 9003L, 9004L], loaded.UniqueItemInstances.Values.Select(i => i.ItemCode).Order());
        Assert.Equal(9001, loaded.EquippedUniqueCode(7001, 1));
        Assert.Equal(9004, loaded.EquippedUniqueCode(7001, 2));
        Assert.Equal(9003, loaded.EquippedUniqueCode(7002, 1));
        Assert.Equal(1, loaded.FreeCopies(9001));
    }

    [Fact]
    public void Save_without_equipment_fields_loads_as_empty()
    {
        var s = GameSession.FromSaveJson(EquipmentTests.Pack(), "{\"Version\":1,\"PackId\":\"demo\"}");
        Assert.Empty(s.UniqueItemInstances);
        Assert.All(s.PartyInstance.Units.OfType<PartyUnit>(), u => Assert.Empty(u.Equipment));
    }

    const string Head = "{\"Version\":1,\"PackId\":\"demo\",";
    const string Chars =
        "\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}," +
        "\"7002\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}},";

    [Theory]
    [InlineData(@"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":99999}}")]                                      // 유니크 아이템 마스터에 없는 아이템
    [InlineData(@"""UniqueItems"":{""0"":{""ItemCode"":9001}}")]                                        // 인스턴스 ID가 1 미만
    [InlineData(@"""UniqueItems"":{""철검"":{""ItemCode"":9001}}")]                                      // 숫자가 아닌 키
    [InlineData(@"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9001,""Tags"":[99]}}")]                          // 태그 마스터에 없는 태그
    [InlineData(@"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9001,""Tags"":[1,1]}}")]                         // 같은 태그가 두 번
    [InlineData(@"""CharacterTags"":{""7003"":[1]}")]                                                  // 보유하지 않은 캐릭터의 태그
    [InlineData(@"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9001}},""EquippedItems"":{""7003"":{""1"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000001""}}}")]  // 보유하지 않은 캐릭터
    [InlineData(@"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9001}},""EquippedItems"":{""7001"":{""99"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000001""}}}")]  // 그 캐릭터에게 없는 슬롯
    [InlineData(@"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9001}},""EquippedItems"":{""7002"":{""2"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000001""}}}")]   // 마법사는 방어구 슬롯이 없다
    [InlineData(@"""EquippedItems"":{""7001"":{""1"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000001""}}}")]          // 없는 인스턴스를 장착
    [InlineData(@"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9002}},""EquippedItems"":{""7001"":{""1"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000001""}}}")]  // 슬롯 태그에 부합하지 않는 장비(대검)
    [InlineData(@"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9003}},""EquippedItems"":{""7001"":{""1"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000001""}},""7002"":{""1"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000001""}}}")]  // 한 개를 두 곳에
    public void Invalid_equipment_in_a_save_is_rejected(string body)
        => Assert.Throws<SaveFormatException>(() =>
            GameSession.FromSaveJson(EquipmentTests.Pack(), Head + Chars + body + "}"));

    [Fact]
    public void Two_copies_may_be_equipped_in_two_places_when_owned_twice()
    {
        var s = GameSession.FromSaveJson(EquipmentTests.Pack(),
            Head + Chars + @"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9003},""01H00000000000000000000002"":{""ItemCode"":9003}}," +
            @"""Party"":[7001,null,null,null,7002]," +
            @"""EquippedItems"":{""7001"":{""1"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000001""}},""7002"":{""1"":{""Kind"":""UniqueItem"",""Instance"":""01H00000000000000000000002""}}}}");
        Assert.Equal(2, s.EquippedCount(9003));
        Assert.Equal(0, s.FreeCopies(9003));
    }

    [Fact]
    public void Equipment_in_a_save_needs_the_equipment_masters_of_the_pack()
    {
        var packWithoutEquipment = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Throws<SaveFormatException>(() =>
            GameSession.FromSaveJson(packWithoutEquipment, Head + @"""UniqueItems"":{""01H00000000000000000000001"":{""ItemCode"":9001}}}"));
    }
}
