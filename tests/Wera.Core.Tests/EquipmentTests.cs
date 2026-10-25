using Wera.Core;

namespace Wera.Core.Tests;

public class EquipmentTests
{
    const string SlotHeader = "equipment_slot_code\tequipment_slot_name\n";
    const string TagHeader = "tag_code\ttag_name\n";
    const string ItemHeader = "item_code\titem_name\n";
    const string ItemTagHeader = "item_code\ttag_code\n";
    const string SlotTagHeader = "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n";

    // 슬롯: 1 무기, 2 방어구 / 태그: 1 검, 2 갑옷, 3 중량, 4 마법
    const string Slots = SlotHeader + "1\t무기\n2\t방어구\n";
    const string Tags = TagHeader + "1\t검\n2\t갑옷\n3\t중량\n4\t마법\n";

    // 장비: 9001 철검(검), 9002 대검(검, 중량), 9003 지팡이(마법), 9004 판금갑옷(갑옷, 중량), 9005 천 옷(갑옷)
    const string Items = ItemHeader + "9001\t철검\n9002\t대검\n9003\t지팡이\n9004\t판금갑옷\n9005\t천 옷\n";
    const string ItemTags = ItemTagHeader +
        "9001\t1\n9002\t1\n9002\t3\n9003\t4\n9004\t2\n9004\t3\n9005\t2\n";

    // 용사(7001): 무기 = (검 AND 중량 없음) OR 마법, 방어구 = 갑옷 / 마법사(7002): 무기 = 마법
    const string SlotTags = SlotTagHeader +
        "7001\t1\t1\t1\ttrue\n" +
        "7001\t1\t3\t1\tfalse\n" +
        "7001\t1\t4\t2\ttrue\n" +
        "7001\t2\t2\t1\ttrue\n" +
        "7002\t1\t4\t1\ttrue\n";

    static GamePack Pack(
        string? slots = Slots, string? tags = Tags, string? items = Items, string? itemTags = ItemTags, string? slotTags = SlotTags)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
            ("master/character_inventory_master_v1.tsv",
                "character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
                "7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n7003\t도적\t1\t1\t2\n"),
        };
        if (slots is not null) files.Add(("master/equipment_slot_master_v1.tsv", slots));
        if (tags is not null) files.Add(("master/tag_master_v1.tsv", tags));
        if (items is not null) files.Add(("master/equipment_master_v1.tsv", items));
        if (itemTags is not null) files.Add(("master/equipment_tag_master_v1.tsv", itemTags));
        if (slotTags is not null) files.Add(("master/equipment_slot_tag_master_v1.tsv", slotTags));
        return GamePack.Load(CoreTests.Zip(files.ToArray()));
    }

    // ---- 마스터 로드 ----

    [Fact]
    public void All_equipment_masters_are_optional()
    {
        var pack = Pack(null, null, null, null, null);
        Assert.Null(pack.EquipmentSlots);
        Assert.Null(pack.Tags);
        Assert.Null(pack.EquipmentItems);
        Assert.Null(pack.EquipmentTags);
        Assert.Null(pack.EquipmentSlotTags);
        Assert.Empty(pack.EquipmentSlotsOf(7001));
        Assert.Empty(pack.EquippableItems(7001, 1));
        Assert.False(pack.CanEquip(7001, 1, 9001));
    }

    [Fact]
    public void Slot_tag_equipment_and_equipment_tag_masters_load_in_file_order()
    {
        var pack = Pack();
        Assert.Equal([new EquipmentSlotEntry(1, "무기"), new EquipmentSlotEntry(2, "방어구")], pack.EquipmentSlots);
        Assert.Equal(4, pack.Tags!.Count);
        Assert.Equal(new TagEntry(3, "중량"), pack.Tags[2]);
        Assert.Equal(new EquipmentItem(9004, "판금갑옷"), pack.EquipmentItems![3]);
        Assert.Equal(7, pack.EquipmentTags!.Count);
        Assert.Contains(new EquipmentTag(9002, 3), pack.EquipmentTags);
    }

    [Fact]
    public void Slot_tag_master_loads_all_five_columns()
    {
        var rows = Pack().EquipmentSlotTags!;
        Assert.Equal(5, rows.Count);
        Assert.Equal(new EquipmentSlotTag(7001, 1, 1, 1, true), rows[0]);
        Assert.Equal(new EquipmentSlotTag(7001, 1, 3, 1, false), rows[1]);
        Assert.Equal(new EquipmentSlotTag(7001, 1, 4, 2, true), rows[2]);
    }

    [Theory]
    [InlineData("1\ta\n1\tb\n")]       // 중복 코드
    [InlineData("\ta\n")]               // 빈 코드
    [InlineData("abc\ta\n")]            // 정수 아님
    public void Bad_slot_tag_and_equipment_rows_are_rejected(string rows)
    {
        Assert.Throws<PackFormatException>(() => Pack(slots: SlotHeader + rows, slotTags: null));
        Assert.Throws<PackFormatException>(() => Pack(tags: TagHeader + rows, itemTags: null, slotTags: null));
        Assert.Throws<PackFormatException>(() => Pack(items: ItemHeader + rows, itemTags: null));
    }

    [Theory]
    [InlineData("code\tname\n1\ta\n")]
    [InlineData("equipment_slot_code\n1\n")]
    public void Equipment_slot_master_header_is_checked(string tsv)
        => Assert.Throws<PackFormatException>(() => Pack(slots: tsv, slotTags: null));

    [Theory]
    [InlineData("code\tname\n1\ta\n")]
    [InlineData("tag_code\n1\n")]
    public void Tag_master_header_is_checked(string tsv)
        => Assert.Throws<PackFormatException>(() => Pack(tags: tsv, itemTags: null, slotTags: null));

    [Theory]
    [InlineData("code\tname\n1\ta\n")]
    [InlineData("item_code\n1\n")]
    public void Equipment_master_header_is_checked(string tsv)
        => Assert.Throws<PackFormatException>(() => Pack(items: tsv, itemTags: null));

    // ---- 장비 태그 마스터 ----

    [Theory]
    [InlineData("9999\t1\n")]            // 장비 마스터에 없는 장비
    [InlineData("9001\t99\n")]           // 태그 마스터에 없는 태그
    [InlineData("9001\t1\n9001\t1\n")]   // 같은 (장비, 태그) 두 번
    [InlineData("abc\t1\n")]
    [InlineData("9001\tabc\n")]
    [InlineData("9001\t\n")]
    public void Bad_equipment_tag_rows_are_rejected(string rows)
        => Assert.Throws<PackFormatException>(() => Pack(itemTags: ItemTagHeader + rows));

    [Fact]
    public void Equipment_tag_master_needs_the_equipment_and_tag_masters_and_the_right_header()
    {
        Assert.Throws<PackFormatException>(() => Pack(items: null, slotTags: null));
        Assert.Throws<PackFormatException>(() => Pack(tags: null, slotTags: null));
        Assert.Throws<PackFormatException>(() => Pack(itemTags: "item_code\titem_tag\n9001\t1\n"));
    }

    // ---- 장비 슬롯 태그 마스터: 복합키는 네 열 ----

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("TRUE")]
    [InlineData("False")]
    [InlineData("1")]
    [InlineData("0")]
    public void Is_positive_accepts_true_false_and_one_zero(string value)
        => Assert.Single(Pack(slotTags: SlotTagHeader + $"7001\t1\t1\t1\t{value}\n").EquipmentSlotTags!);

    [Theory]
    [InlineData("yes")]
    [InlineData("2")]
    [InlineData("")]
    public void Is_positive_rejects_anything_else(string value)
        => Assert.Throws<PackFormatException>(() => Pack(slotTags: SlotTagHeader + $"7001\t1\t1\t1\t{value}\n"));

    [Theory]
    [InlineData("9999\t1\t1\t1\ttrue\n")]     // 캐릭터 마스터에 없는 캐릭터
    [InlineData("7001\t99\t1\t1\ttrue\n")]    // 장비 슬롯 마스터에 없는 슬롯
    [InlineData("7001\t1\t99\t1\ttrue\n")]    // 태그 마스터에 없는 태그
    [InlineData("abc\t1\t1\t1\ttrue\n")]
    [InlineData("7001\tx\t1\t1\ttrue\n")]
    [InlineData("7001\t1\tx\t1\ttrue\n")]
    [InlineData("7001\t1\t1\tx\ttrue\n")]     // 그룹넘버가 정수 아님
    [InlineData("7001\t1\t1\t1.5\ttrue\n")]   // 그룹넘버는 int: 소수 불가
    [InlineData("7001\t1\t1\t\ttrue\n")]
    public void Bad_slot_tag_rows_are_rejected(string row)
        => Assert.Throws<PackFormatException>(() => Pack(slotTags: SlotTagHeader + row));

    [Fact]
    public void The_same_four_column_key_cannot_appear_twice_even_when_is_positive_differs()
    {
        // 앞의 네 열(캐릭터, 슬롯, 태그, 그룹)이 복합키이고 is_positive는 값이다.
        Assert.Throws<PackFormatException>(() =>
            Pack(slotTags: SlotTagHeader + "7001\t1\t1\t1\ttrue\n7001\t1\t1\t1\ttrue\n"));    // 완전히 같은 행
        Assert.Throws<PackFormatException>(() =>
            Pack(slotTags: SlotTagHeader + "7001\t1\t1\t1\ttrue\n7001\t1\t1\t1\tfalse\n"));   // is_positive만 다른 행
    }

    [Fact]
    public void Rows_differing_in_any_one_key_column_are_different_keys()
    {
        var pack = Pack(slotTags: SlotTagHeader +
            "7001\t1\t1\t1\ttrue\n" +
            "7002\t1\t1\t1\ttrue\n" +      // 캐릭터만 다름
            "7001\t2\t1\t1\ttrue\n" +      // 슬롯만 다름
            "7001\t1\t2\t1\ttrue\n" +      // 태그만 다름
            "7001\t1\t1\t2\ttrue\n");      // 그룹만 다름
        Assert.Equal(5, pack.EquipmentSlotTags!.Count);
    }

    [Fact]
    public void Slot_tag_master_needs_the_character_slot_and_tag_masters_and_the_right_header()
    {
        Assert.Throws<PackFormatException>(() => Pack(slots: null));
        Assert.Throws<PackFormatException>(() => Pack(tags: null, itemTags: null));
        Assert.Throws<PackFormatException>(() => Pack(slotTags: "character_code\tequipment_slot_code\ttag_code\tgroup_number\n7001\t1\t1\t1\n"));
        Assert.Throws<PackFormatException>(() => Pack(slotTags: "code\tslot\ttag\tgroup\tpositive\n7001\t1\t1\t1\ttrue\n"));

        // 캐릭터 마스터가 없으면 거부
        var withoutCharacters = CoreTests.Zip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/equipment_slot_master_v1.tsv", Slots),
            ("master/tag_master_v1.tsv", Tags),
            ("master/equipment_slot_tag_master_v1.tsv", SlotTags));
        Assert.Throws<PackFormatException>(() => GamePack.Load(withoutCharacters));
    }

    // ---- 부합 규칙: 그룹 안 AND, 그룹끼리 OR, 양성은 있어야 / 음성은 없어야 ----

    static EquipmentSlotTag Rule(long tag, int group, bool positive) => new(1, 1, tag, group, positive);

    static IReadOnlySet<long> Has(params long[] tags) => tags.ToHashSet();

    [Fact]
    public void Positive_tag_must_be_present_and_negative_tag_must_be_absent()
    {
        Assert.True(EquipmentRules.Matches([Rule(1, 1, true)], Has(1)));
        Assert.False(EquipmentRules.Matches([Rule(1, 1, true)], Has(2)));
        Assert.True(EquipmentRules.Matches([Rule(3, 1, false)], Has(1)));
        Assert.False(EquipmentRules.Matches([Rule(3, 1, false)], Has(3, 1)));
    }

    [Fact]
    public void Rules_in_the_same_group_are_anded()
    {
        var rules = new[] { Rule(1, 1, true), Rule(3, 1, false) };       // 검이면서 중량이 아님
        Assert.True(EquipmentRules.Matches(rules, Has(1)));
        Assert.False(EquipmentRules.Matches(rules, Has(1, 3)));
        Assert.False(EquipmentRules.Matches(rules, Has(3)));
        Assert.False(EquipmentRules.Matches(rules, Has()));
    }

    [Fact]
    public void Different_groups_are_ored()
    {
        var rules = new[] { Rule(1, 1, true), Rule(3, 1, false), Rule(4, 2, true) };   // (검 AND 비중량) OR 마법
        Assert.True(EquipmentRules.Matches(rules, Has(4)));
        Assert.True(EquipmentRules.Matches(rules, Has(1)));
        Assert.True(EquipmentRules.Matches(rules, Has(1, 4, 3)));        // 두 번째 그룹이 만족
        Assert.False(EquipmentRules.Matches(rules, Has(1, 3)));
    }

    [Fact]
    public void No_rules_match_nothing()
        => Assert.False(EquipmentRules.Matches([], Has(1, 2, 3)));

    [Fact]
    public void A_tagless_equipment_matches_only_negative_only_groups()
    {
        Assert.False(EquipmentRules.Matches([Rule(1, 1, true)], Has()));
        Assert.True(EquipmentRules.Matches([Rule(3, 1, false)], Has()));
    }

    // ---- 게임팩 전체에서의 조회 ----

    [Fact]
    public void A_character_has_the_equipment_slots_that_appear_for_it_in_the_slot_tag_master()
    {
        var pack = Pack();
        Assert.Equal(["무기", "방어구"], pack.EquipmentSlotsOf(7001).Select(s => s.Name));
        Assert.Equal(["무기"], pack.EquipmentSlotsOf(7002).Select(s => s.Name));
        Assert.Empty(pack.EquipmentSlotsOf(7003));                      // 행이 없는 캐릭터
        Assert.Empty(pack.EquipmentSlotsOf(9999));
    }

    [Fact]
    public void Only_equipment_matching_the_slot_tags_can_be_equipped()
    {
        var pack = Pack();
        Assert.True(pack.CanEquip(7001, 1, 9001));       // 철검: 검, 중량 없음
        Assert.False(pack.CanEquip(7001, 1, 9002));      // 대검: 중량이라 불가
        Assert.True(pack.CanEquip(7001, 1, 9003));       // 지팡이: 두 번째 그룹(마법)
        Assert.False(pack.CanEquip(7001, 1, 9004));      // 판금갑옷은 무기가 아니다
        Assert.True(pack.CanEquip(7001, 2, 9004));       // 방어구: 갑옷
        Assert.True(pack.CanEquip(7001, 2, 9005));
        Assert.False(pack.CanEquip(7001, 2, 9001));
        Assert.True(pack.CanEquip(7002, 1, 9003));       // 마법사는 마법 무기만
        Assert.False(pack.CanEquip(7002, 1, 9001));
    }

    [Fact]
    public void Equipping_is_refused_for_unknown_character_slot_or_item_and_slots_the_character_lacks()
    {
        var pack = Pack();
        Assert.False(pack.CanEquip(9999, 1, 9001));
        Assert.False(pack.CanEquip(7001, 99, 9001));
        Assert.False(pack.CanEquip(7001, 1, 99999));
        Assert.False(pack.CanEquip(7002, 2, 9005));      // 마법사는 방어구 슬롯이 없다
        Assert.False(pack.CanEquip(7003, 1, 9001));      // 도적은 슬롯이 하나도 없다
    }

    [Fact]
    public void Equippable_items_come_in_equipment_master_order()
    {
        var pack = Pack();
        Assert.Equal(["철검", "지팡이"], pack.EquippableItems(7001, 1).Select(i => i.ItemName));
        Assert.Equal(["판금갑옷", "천 옷"], pack.EquippableItems(7001, 2).Select(i => i.ItemName));
        Assert.Equal(["지팡이"], pack.EquippableItems(7002, 1).Select(i => i.ItemName));
        Assert.Empty(pack.EquippableItems(7003, 1));
    }

    // ---- 화면 흐름 ----

    static GameSession Session()
    {
        var s = GameSession.NewGame(Pack());
        s.Characters[7001] = new CharacterState(5, 36, 40, 3, 30);
        s.Characters[7002] = new CharacterState(3, 10, 25, 12, 30);
        s.Characters[7003] = new CharacterState(2, 5, 20, 1, 10);
        s.Party[0] = 7001;
        s.Party[4] = 7002;
        s.Party[8] = 7003;
        s.Activate("103");
        s.Activate("11");
        return s;
    }

    [Fact]
    public void Equipment_list_shows_the_characters_equipment_slots_as_selectable_entries()
    {
        var s = Session();
        s.Activate("1");                                          // 용사

        Assert.Equal(Screen.EquipmentList, s.Screen);
        Assert.Equal(7001, s.CurrentCharacterCode);
        Assert.Equal(
            [new ScreenEntry("1", "무기", Screen.EquipmentSelect), new ScreenEntry("2", "방어구", Screen.EquipmentSelect),
             new ScreenEntry("999", "돌아가기", Screen.EquipmentSetup)],
            s.Entries);
    }

    [Fact]
    public void A_character_without_equipment_slots_gets_only_the_back_entry()
    {
        var s = Session();
        s.Activate("9");                                          // 도적
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));
    }

    [Fact]
    public void Picking_an_equipment_slot_lists_the_equipment_that_fits_it()
    {
        var s = Session();
        s.Activate("1");
        Assert.NotNull(s.Activate("1"));                          // 무기

        Assert.Equal(Screen.EquipmentSelect, s.Screen);
        Assert.Equal(1, s.CurrentEquipmentSlotCode);
        Assert.Equal("무기", s.CurrentEquipmentSlot!.Name);
        Assert.Equal(["철검", "지팡이"], s.EquippableItems.Select(i => i.ItemName));
        Assert.Equal("용사", s.CurrentSlotCharacterName);
    }

    [Fact]
    public void Different_slots_and_characters_list_different_equipment()
    {
        var s = Session();
        s.Activate("1"); s.Activate("2");
        Assert.Equal(["판금갑옷", "천 옷"], s.EquippableItems.Select(i => i.ItemName));
        s.Activate("999"); s.Activate("999");                     // 장비 슬롯 목록 → 장비 착용 화면

        s.Activate("5"); s.Activate("1");                         // 마법사의 무기
        Assert.Equal(["지팡이"], s.EquippableItems.Select(i => i.ItemName));
    }

    [Fact]
    public void Back_walks_equipment_select_to_equipment_list_to_equipment_setup_to_home()
    {
        var s = Session();
        s.Activate("1"); s.Activate("1");
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));

        s.Activate("999");
        Assert.Equal(Screen.EquipmentList, s.Screen);
        Assert.Equal(7001, s.CurrentCharacterCode);               // 같은 캐릭터를 계속 본다
        Assert.Null(s.CurrentEquipmentSlotCode);
        Assert.Equal(2, s.Entries.Count(e => e.Target == Screen.EquipmentSelect));

        s.Activate("999");
        Assert.Equal(Screen.EquipmentSetup, s.Screen);
        Assert.Null(s.CurrentSlot);

        s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Slot_codes_of_other_screens_do_not_work_inside_equipment_select()
    {
        var s = Session();
        s.Activate("1"); s.Activate("1");
        Assert.Null(s.Activate("2"));                             // 다른 장비 슬롯으로 바로 갈 수 없다
        Assert.Null(s.Activate("5"));
        Assert.Equal(Screen.EquipmentSelect, s.Screen);
        Assert.Equal(1, s.CurrentEquipmentSlotCode);
    }

    [Fact]
    public void Save_taken_while_choosing_equipment_loads_into_home_with_the_selection_cleared()
    {
        var s = Session();
        s.Activate("1"); s.Activate("1");
        Assert.Contains("\"EquipmentSelect\"", s.ToSaveJson());

        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal(Screen.Home, loaded.Screen);
        Assert.Null(loaded.CurrentSlot);
        Assert.Null(loaded.CurrentEquipmentSlotCode);
        Assert.Empty(loaded.EquippableItems);
    }
}
