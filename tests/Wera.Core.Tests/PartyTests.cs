using Wera.Core;

namespace Wera.Core.Tests;

public class PartyTests
{
    // 속성: 1 불, 2 물, 3 풀, 4 빛, 5 어둠
    const string PackText = "id\tdemo\ntitle\t데모\n";
    const string Attributes ="attribute_code\tattribute_name\n1\t불\n2\t물\n3\t풀\n4\t빛\n5\t어둠\n";

    // 7001 용사(유리 빛, 불리 어둠), 7002 마법사(불/물), 7003 전사(풀/불), 7004 도적(보유하지 않음)
    const string Characters =
        "character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
        "7001\t용사\t1\t4\t5\n7002\t마법사\t1\t1\t2\n7003\t전사\t1\t3\t1\n7004\t도적\t1\t5\t4\n";

    static GamePack Pack(string? characters = Characters)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        };
        if (characters is not null)
        {
            files.Add(("master/character_inventory_master_v1.tsv", characters));
            files.Add(("master/attribute_master_v1.tsv", Attributes));
        }
        return GamePack.Load(CoreTests.Zip(files.ToArray()));
    }

    /// <summary>용사 Lv5 36/40 3/30, 마법사 Lv3 10/25 12/30, 전사(사망) Lv8 0/50 40/80을 보유. 도적은 보유하지 않음.</summary>
    static GameSession Session()
    {
        var s = GameSession.NewGame(Pack());
        s.Characters[7001] = new CharacterState(5, 36, 40, 3, 30);
        s.Characters[7002] = new CharacterState(3, 10, 25, 12, 30);
        s.Characters[7003] = new CharacterState(8, 0, 50, 40, 80);
        return s;
    }

    // ---- 게이지 ----

    [Fact]
    public void Gauge_matches_the_examples_in_the_spec()
    {
        Assert.Equal("****36/40***-", Gauge.Render(36, 40, gray: false).Text);
        Assert.Equal("*----3/30----", Gauge.Render(3, 30, gray: false).Text);
    }

    [Theory]
    [InlineData(40, 40, "****40/40****")]   // 가득 참: 전부 '*', 숫자가 가운데를 덮는다
    [InlineData(0, 40, "-----0/40----")]    // 비어 있음: 전부 '-'
    public void Gauge_full_and_empty(int cur, int max, string expected)
        => Assert.Equal(expected, Gauge.Render(cur, max, gray: false).Text);

    [Fact]
    public void Gauge_is_always_the_requested_width_and_centers_the_label()
    {
        foreach (var width in new[] { 9, 12, 13, 20 })
        {
            var text = Gauge.Render(7, 10, gray: false, width).Text;
            Assert.Equal(width, text.Length);
            // "7/10"은 4글자. 남는 칸이 홀수면 오른쪽으로 치우친다.
            Assert.Equal((width - 4 + 1) / 2, text.IndexOf("7/10", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Empty_slot_gauge_is_gray_zero_over_zero()
    {
        var g = Gauge.Render(0, 0, gray: true);
        Assert.Equal("-----0/0-----", g.Text);
        Assert.Equal(GaugeColor.Gray, g.Color);
    }

    [Theory]
    [InlineData(40, 40, GaugeColor.White)]
    [InlineData(20, 40, GaugeColor.White)]    // 정확히 50%는 흰색
    [InlineData(19, 40, GaugeColor.Yellow)]
    [InlineData(10, 40, GaugeColor.Yellow)]   // 정확히 25%는 노랑
    [InlineData(9, 40, GaugeColor.Red)]
    [InlineData(1, 40, GaugeColor.Red)]
    [InlineData(0, 40, GaugeColor.Red)]       // 0퍼센트 이상은 빨강 (사망으로 회색이 되지 않는 한)
    public void Gauge_color_by_percentage(int cur, int max, GaugeColor expected)
        => Assert.Equal(expected, Gauge.Render(cur, max, gray: false).Color);

    [Fact]
    public void Gray_overrides_the_percentage_color()
        => Assert.Equal(GaugeColor.Gray, Gauge.Render(40, 40, gray: true).Color);

    [Fact]
    public void Gauge_clamps_nonsense_values()
    {
        Assert.Equal(13, Gauge.Render(50, 40, gray: false).Text.Length);
        Assert.Equal(13, Gauge.Render(-5, 40, gray: false).Text.Length);
        Assert.Equal(13, Gauge.Render(5, 0, gray: false).Text.Length);
    }

    // ---- 홈 / 파티 설정 화면 ----

    [Fact]
    public void Home_always_shows_party_setup_103_even_without_any_master()
    {
        var s = GameSession.NewGame(Pack(null));
        Assert.Contains(new ScreenEntry("103", "파티 설정", Screen.PartySetup), s.Entries);
    }

    [Fact]
    public void Party_setup_has_nine_slot_entries_the_two_mode_buttons_and_back()
    {
        var s = Session();
        s.Activate("103");
        Assert.Equal(Screen.PartySetup, s.Screen);
        Assert.Equal(["1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "999"], s.Entries.Select(e => e.Code));
        Assert.Null(s.Activate("103"));
        Assert.NotNull(s.Activate("999"));
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Empty_slots_show_the_empty_slot_name_and_gray_zero_gauges()
    {
        var s = Session();
        var views = s.PartySlotViews;
        Assert.Equal(9, views.Count);
        Assert.All(views, v =>
        {
            Assert.True(v.IsEmpty);
            Assert.Equal("빈 슬롯", v.Name);
            Assert.Equal("", v.LevelText);
            Assert.Equal(new GaugeView("-----0/0-----", GaugeColor.Gray), v.Hp);
            Assert.Equal(new GaugeView("-----0/0-----", GaugeColor.Gray), v.Xp);
        });
        Assert.Equal(Enumerable.Range(0, 9), views.Select(v => v.Slot));
    }

    [Fact]
    public void Rows_are_front_middle_and_back_from_top_to_bottom()
    {
        Assert.Equal(["전위", "중위", "후위"], PartySlotView.RowNames);

        var views = Session().PartySlotViews;
        Assert.Equal(["전위", "전위", "전위", "중위", "중위", "중위", "후위", "후위", "후위"], views.Select(v => v.RowName));
        Assert.Equal(["전위", "중위", "후위"], views.Where(v => v.Slot % 3 == 0).Select(v => v.RowName));   // 줄의 첫 슬롯마다 한 번
    }

    [Fact]
    public void Row_name_does_not_depend_on_whether_the_slot_is_filled()
    {
        var s = Session();
        s.Party[4] = 7002;
        Assert.Equal("중위", s.PartySlotViews[4].RowName);      // 채워진 슬롯
        Assert.Equal("중위", s.PartySlotViews[3].RowName);      // 빈 슬롯
    }

    [Fact]
    public void Filled_slot_shows_level_name_hp_and_xp_gauges()
    {
        var s = Session();
        s.Party[0] = 7001;
        var v = s.PartySlotViews[0];

        Assert.False(v.IsEmpty);
        Assert.Equal("Lv.5", v.LevelText);
        Assert.Equal("용사", v.Name);
        Assert.Equal(new GaugeView("****36/40***-", GaugeColor.White), v.Hp);   // 90%
        Assert.Equal(new GaugeView("*----3/30----", GaugeColor.Red), v.Xp);     // 10%
        Assert.True(s.PartySlotViews[1].IsEmpty);
    }

    [Fact]
    public void Slot_icon_line_shows_advantage_then_disadvantage_attribute()
    {
        var s = Session();
        s.Party[0] = 7001;
        var badges = s.PartySlotViews[0].Badges;

        Assert.Equal(
            [new AttributeBadge(4, "빛", Advantage: true), new AttributeBadge(5, "어둠", Advantage: false)],
            badges);
        Assert.Equal(["⭕", "❌"], badges.Select(b => b.Marker));
    }

    [Fact]
    public void Each_character_shows_its_own_attributes_and_dead_ones_too()
    {
        var s = Session();
        s.Party[4] = 7002;                                   // 마법사: 유리 불, 불리 물
        s.Party[8] = 7003;                                   // 전사(사망): 유리 풀, 불리 불
        Assert.Equal(["불", "물"], s.PartySlotViews[4].Badges.Select(b => b.Name));
        Assert.Equal(["풀", "불"], s.PartySlotViews[8].Badges.Select(b => b.Name));
    }

    [Fact]
    public void Empty_slot_has_no_icon_line_content()
    {
        var s = Session();
        Assert.All(s.PartySlotViews, v => Assert.Empty(v.Badges));
    }

    [Fact]
    public void Dead_character_gauges_are_gray()
    {
        var s = Session();
        s.Party[8] = 7003;
        var v = s.PartySlotViews[8];
        Assert.Equal(GaugeColor.Gray, v.Hp.Color);
        Assert.Equal(GaugeColor.Gray, v.Xp.Color);
        Assert.Equal("전사", v.Name);
    }

    [Fact]
    public void Party_slot_with_unknown_state_is_shown_as_empty()
    {
        var s = Session();
        s.Party[2] = 7004;                                   // 보유하지 않은 캐릭터 코드
        Assert.True(s.PartySlotViews[2].IsEmpty);
    }

    // ---- 캐릭터 선택 ----

    [Fact]
    public void Picking_a_slot_by_number_or_click_opens_the_character_list_for_it()
    {
        var s = Session();
        s.Activate("103");
        Assert.NotNull(s.Activate("5"));
        Assert.Equal(Screen.CharacterSelect, s.Screen);
        Assert.Equal(4, s.CurrentSlot);
        Assert.Equal(["7001", "7002", "7003", "999"], s.Entries.Select(e => e.Code));   // 7004는 회색이라 고를 수 없다
    }

    [Fact]
    public void Character_list_colors()
    {
        var s = Session();
        s.Party[4] = 7001;                                   // 이 슬롯(5번)에 용사
        s.Party[0] = 7002;                                   // 다른 슬롯에 마법사
        s.Activate("103");
        s.Activate("5");

        var colors = s.CharacterList.ToDictionary(c => c.Code, c => c.Color);
        Assert.Equal(CharacterListColor.SkyBlue, colors[7001]);   // 이미 이 슬롯에 있는 자신
        Assert.Equal(CharacterListColor.Green, colors[7002]);     // 다른 슬롯의 파티원
        Assert.Equal(CharacterListColor.White, colors[7003]);     // 배치 가능
        Assert.Equal(CharacterListColor.Gray, colors[7004]);      // 보유하지 않음 → 배치 불가
        Assert.Equal([7001L, 7002L, 7003L, 7004L], s.CharacterList.Select(c => c.Code));
        Assert.False(s.CharacterList.Single(c => c.Code == 7004).Selectable);
    }

    [Fact]
    public void Gray_character_cannot_be_chosen_even_by_typing_its_code()
    {
        var s = Session();
        s.Activate("103");
        s.Activate("1");
        Assert.Null(s.Activate("7004"));
        Assert.Equal(Screen.CharacterSelect, s.Screen);
        Assert.All(s.Party, p => Assert.Null(p));
    }

    [Fact]
    public void Choosing_a_free_character_places_it_and_returns_to_party_setup()
    {
        var s = Session();
        s.Activate("103");
        s.Activate("1");
        s.Activate("7001");

        Assert.Equal(Screen.PartySetup, s.Screen);
        Assert.Null(s.CurrentSlot);
        Assert.Equal(7001L, s.Party[0]);
        Assert.Equal("용사", s.PartySlotViews[0].Name);
    }

    [Fact]
    public void Choosing_a_free_character_replaces_the_slot_occupant_who_leaves_the_party()
    {
        var s = Session();
        s.Party[0] = 7001;
        s.Activate("103");
        s.Activate("1");
        s.Activate("7002");

        Assert.Equal(7002L, s.Party[0]);
        Assert.DoesNotContain((long?)7001, s.Party);
    }

    [Fact]
    public void Choosing_the_character_already_in_this_slot_changes_nothing()
    {
        var s = Session();
        s.Party[3] = 7001;
        s.Activate("103");
        s.Activate("4");
        Assert.NotNull(s.Activate("7001"));
        Assert.Equal(Screen.PartySetup, s.Screen);
        Assert.Equal(7001L, s.Party[3]);
        Assert.Single(s.Party.OfType<long>());
    }

    [Fact]
    public void Choosing_a_placed_party_member_swaps_positions()
    {
        var s = Session();
        s.Party[0] = 7001;
        s.Party[5] = 7002;
        s.Activate("103");
        s.Activate("1");                                       // 슬롯 1(전위 첫 칸)에 마법사를 고른다
        s.Activate("7002");

        Assert.Equal(7002L, s.Party[0]);
        Assert.Equal(7001L, s.Party[5]);                      // 용사는 마법사가 있던 칸으로
    }

    [Fact]
    public void Choosing_a_placed_party_member_for_an_empty_slot_moves_it()
    {
        var s = Session();
        s.Party[5] = 7002;
        s.Activate("103");
        s.Activate("1");
        s.Activate("7002");

        Assert.Equal(7002L, s.Party[0]);
        Assert.Null(s.Party[5]);                               // 원래 칸은 비게 된다
        Assert.Single(s.Party.OfType<long>());
    }

    // ---- [0] 비우기 ----

    [Fact]
    public void Clear_entry_is_on_top_of_the_list_only_when_the_slot_has_a_character()
    {
        var s = Session();
        s.Party[0] = 7001;
        s.Activate("103");

        s.Activate("1");                                       // 용사가 있는 슬롯
        Assert.Equal(["0", "7001", "7002", "7003", "999"], s.Entries.Select(e => e.Code));
        Assert.Equal(new ScreenEntry("0", "비우기", Screen.PartySetup, EntryAction.ClearSlot), s.Entries[0]);
        s.Activate("999");

        s.Activate("2");                                       // 빈 슬롯
        Assert.Equal(["7001", "7002", "7003", "999"], s.Entries.Select(e => e.Code));
        Assert.Null(s.Activate("0"));                          // 빈 슬롯에서는 비울 것이 없다
        Assert.Equal(Screen.CharacterSelect, s.Screen);
    }

    [Fact]
    public void Clearing_a_slot_removes_the_character_from_the_party_and_returns_to_party_setup()
    {
        var s = Session();
        s.Party[4] = 7002;
        s.Party[0] = 7001;
        s.Activate("103");
        s.Activate("5");

        var entry = s.Activate("0");

        Assert.Equal(EntryAction.ClearSlot, entry!.Action);
        Assert.Equal(Screen.PartySetup, s.Screen);
        Assert.Null(s.CurrentSlot);
        Assert.Null(s.Party[4]);                               // 그 슬롯만 비었고
        Assert.Equal(7001, s.Party[0]);                        // 다른 슬롯은 그대로
        Assert.True(s.PartySlotViews[4].IsEmpty);
        Assert.Equal("빈 슬롯", s.PartySlotViews[4].Name);
        Assert.Contains(s.Characters.Keys, k => k == 7002);    // 캐릭터를 잃은 것이 아니다(여전히 보유)
    }

    [Fact]
    public void A_cleared_character_can_be_placed_again_and_is_white_in_the_list()
    {
        var s = Session();
        s.Party[4] = 7002;
        s.Activate("103");
        s.Activate("5");
        s.Activate("0");

        s.Activate("1");                                       // 다른 슬롯에서 목록을 열면
        Assert.Equal(CharacterListColor.White, s.CharacterList.Single(c => c.Code == 7002).Color);
        s.Activate("7002");
        Assert.Equal(7002, s.Party[0]);
    }

    [Fact]
    public void Clear_slot_works_for_the_first_and_last_slot_too()
    {
        var s = Session();
        s.Party[0] = 7001;
        s.Party[8] = 7003;
        s.Activate("103");
        s.Activate("1"); s.Activate("0");
        s.Activate("9"); s.Activate("0");
        Assert.All(s.Party, p => Assert.Null(p));
    }

    [Fact]
    public void Clearing_by_typing_zero_with_spaces_or_leading_zeros_also_works()
    {
        var s = Session();
        s.Party[2] = 7001;
        s.Activate("103");
        s.Activate("3");
        Assert.NotNull(s.Activate(" 000 "));
        Assert.Null(s.Party[2]);
    }

    [Fact]
    public void Clear_entry_does_not_exist_outside_the_character_select_screen()
    {
        var s = Session();
        s.Party[0] = 7001;
        Assert.Null(s.Activate("0"));                          // 홈
        s.Activate("103");
        Assert.Null(s.Activate("0"));                          // 파티 설정 (슬롯 번호는 1~9)
        Assert.Equal(7001, s.Party[0]);
    }

    [Fact]
    public void Back_from_the_character_list_returns_to_party_setup_without_changes()
    {
        var s = Session();
        s.Activate("103");
        s.Activate("1");
        Assert.NotNull(s.Activate("999"));
        Assert.Equal(Screen.PartySetup, s.Screen);
        Assert.Null(s.CurrentSlot);
        Assert.All(s.Party, p => Assert.Null(p));
    }

    [Fact]
    public void Character_list_is_empty_without_a_character_inventory_master()
    {
        var s = GameSession.NewGame(Pack(null));
        s.Activate("103");
        s.Activate("1");
        Assert.Empty(s.CharacterList);
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));
    }

    // ---- 세이브 ----

    [Fact]
    public void Save_roundtrip_keeps_characters_and_party_and_loads_into_home()
    {
        var pack = Pack();
        var s = Session();
        s.Party[0] = 7001;
        s.Party[4] = 7002;
        s.Activate("103");

        var json = s.ToSaveJson();
        Assert.Contains("\"Characters\"", json);
        Assert.Contains("\"Party\"", json);

        var loaded = GameSession.FromSaveJson(pack, json);
        Assert.Equal(Screen.Home, loaded.Screen);
        Assert.Equal(new CharacterState(5, 36, 40, 3, 30), loaded.Characters[7001]);
        Assert.Equal(new CharacterState(8, 0, 50, 40, 80), loaded.Characters[7003]);
        long?[] expectedParty = [7001, null, null, null, 7002, null, null, null, null];
        Assert.True(expectedParty.SequenceEqual(loaded.Party));
    }

    [Fact]
    public void Save_without_characters_or_party_loads_as_empty()
    {
        var s = GameSession.FromSaveJson(Pack(), "{\"Version\":1,\"PackId\":\"demo\"}");
        Assert.Empty(s.Characters);
        Assert.Equal(GameSession.PartySlotCount, s.Party.Length);
        Assert.All(s.Party, p => Assert.Null(p));
    }

    [Fact]
    public void Short_party_array_is_padded_and_null_means_an_empty_slot()
    {
        var s = GameSession.FromSaveJson(Pack(),
            "{\"Version\":1,\"PackId\":\"demo\"," +
            "\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}," +
            "\"Party\":[null,7001]}");
        Assert.Equal(9, s.Party.Length);
        Assert.Null(s.Party[0]);
        Assert.Equal(7001L, s.Party[1]);
    }

    const string Header = "{\"Version\":1,\"PackId\":\"demo\",";
    const string Owned7001 = "\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}";

    [Theory]
    [InlineData("\"Characters\":{\"9999\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}")]   // 마스터에 없는 캐릭터
    [InlineData("\"Characters\":{\"7001\":{\"Level\":0,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}")]   // 레벨 0
    [InlineData("\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":6,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}")]   // HP > MaxHp
    [InlineData("\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":-1,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}")]  // HP 음수
    [InlineData("\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":11,\"XpMax\":10}}")]  // XP > XpMax
    [InlineData("\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":-1,\"XpMax\":10}}")]  // XP 음수
    [InlineData(Owned7001 + ",\"Party\":[7002]")]                                                    // 보유하지 않은 캐릭터를 배치
    [InlineData(Owned7001 + ",\"Party\":[7001,7001]")]                                           // 같은 캐릭터 중복 배치
    [InlineData(Owned7001 + ",\"Party\":[7001,null,null,null,null,null,null,null,null,null]")]     // 10칸
    public void Invalid_character_or_party_data_in_a_save_is_rejected(string body)
        => Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(), Header + body + "}"));

    [Fact]
    public void Characters_in_a_save_need_a_character_inventory_master()
        => Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(null), Header + Owned7001 + "}"));

    [Fact]
    public void Home_has_empty_partner_and_assist_slots_for_later_extension()
    {
        var s = GameSession.NewGame(GamePack.Load(CoreTests.Zip(("pack.tsv", PackText))));
        foreach (var slot in new[] { s.PartnerSlot, s.AssistSlot })
        {
            Assert.True(slot.IsEmpty);
            Assert.Equal(PartySlotView.EmptyName, slot.Name);
            Assert.Equal(-1, slot.Slot);                              // 파티 슬롯 번호(0~8)가 아니다
            Assert.Equal("-----0/0-----", slot.Hp.Text);
        }
        Assert.Equal(["103", "200", "300"], s.Entries.Select(e => e.Code));   // 홈 항목에는 영향이 없다
    }
}
