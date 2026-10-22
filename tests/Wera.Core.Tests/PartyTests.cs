using Wera.Core;

namespace Wera.Core.Tests;

public class PartyTests
{
    // 7001 용사, 7002 마법사, 7003 전사, 7004 도적(보유하지 않음)
    const string Characters =
        "character_code\tcharacter_name\tcarry_limit\n7001\t용사\t1\n7002\t마법사\t1\n7003\t전사\t1\n7004\t도적\t1\n";

    static GamePack Pack(string? characters = Characters)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
            ("scenes.tsv", "id\ttext\na\tx\n"),
            ("choices.tsv", "scene\tlabel\tnext\n"),
        };
        if (characters is not null) files.Add(("master/character_inventory_master_v1.tsv", characters));
        return GamePack.Load(CoreTests.Zip(files.ToArray()));
    }

    /// <summary>용사 Lv5 36/40 3/30, 마법사 Lv3 10/25 12/30, 전사(사망) Lv8 0/50 40/80을 보유. 도적은 보유하지 않음.</summary>
    static GameSession Session()
    {
        var s = GameSession.NewGame(Pack());
        s.Characters["7001"] = new CharacterState(5, 36, 40, 3, 30);
        s.Characters["7002"] = new CharacterState(3, 10, 25, 12, 30);
        s.Characters["7003"] = new CharacterState(8, 0, 50, 40, 80);
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
    public void Party_setup_has_nine_slot_entries_plus_back()
    {
        var s = Session();
        s.Activate("103");
        Assert.Equal(Screen.PartySetup, s.Screen);
        Assert.Equal(["1", "2", "3", "4", "5", "6", "7", "8", "9", "999"], s.Entries.Select(e => e.Code));
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
    public void Filled_slot_shows_level_name_hp_and_xp_gauges()
    {
        var s = Session();
        s.Party[0] = "7001";
        var v = s.PartySlotViews[0];

        Assert.False(v.IsEmpty);
        Assert.Equal("Lv.5", v.LevelText);
        Assert.Equal("용사", v.Name);
        Assert.Equal(new GaugeView("****36/40***-", GaugeColor.White), v.Hp);   // 90%
        Assert.Equal(new GaugeView("*----3/30----", GaugeColor.Red), v.Xp);     // 10%
        Assert.True(s.PartySlotViews[1].IsEmpty);
    }

    [Fact]
    public void Dead_character_gauges_are_gray()
    {
        var s = Session();
        s.Party[8] = "7003";
        var v = s.PartySlotViews[8];
        Assert.Equal(GaugeColor.Gray, v.Hp.Color);
        Assert.Equal(GaugeColor.Gray, v.Xp.Color);
        Assert.Equal("전사", v.Name);
    }

    [Fact]
    public void Party_slot_with_unknown_state_is_shown_as_empty()
    {
        var s = Session();
        s.Party[2] = "7004";                                   // 보유하지 않은 캐릭터 코드
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
        s.Party[4] = "7001";                                   // 이 슬롯(5번)에 용사
        s.Party[0] = "7002";                                   // 다른 슬롯에 마법사
        s.Activate("103");
        s.Activate("5");

        var colors = s.CharacterList.ToDictionary(c => c.Code, c => c.Color);
        Assert.Equal(CharacterListColor.SkyBlue, colors["7001"]);   // 이미 이 슬롯에 있는 자신
        Assert.Equal(CharacterListColor.Green, colors["7002"]);     // 다른 슬롯의 파티원
        Assert.Equal(CharacterListColor.White, colors["7003"]);     // 배치 가능
        Assert.Equal(CharacterListColor.Gray, colors["7004"]);      // 보유하지 않음 → 배치 불가
        Assert.Equal(["7001", "7002", "7003", "7004"], s.CharacterList.Select(c => c.Code));
        Assert.False(s.CharacterList.Single(c => c.Code == "7004").Selectable);
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
        Assert.Equal("7001", s.Party[0]);
        Assert.Equal("용사", s.PartySlotViews[0].Name);
    }

    [Fact]
    public void Choosing_a_free_character_replaces_the_slot_occupant_who_leaves_the_party()
    {
        var s = Session();
        s.Party[0] = "7001";
        s.Activate("103");
        s.Activate("1");
        s.Activate("7002");

        Assert.Equal("7002", s.Party[0]);
        Assert.DoesNotContain("7001", s.Party);
    }

    [Fact]
    public void Choosing_the_character_already_in_this_slot_changes_nothing()
    {
        var s = Session();
        s.Party[3] = "7001";
        s.Activate("103");
        s.Activate("4");
        Assert.NotNull(s.Activate("7001"));
        Assert.Equal(Screen.PartySetup, s.Screen);
        Assert.Equal("7001", s.Party[3]);
        Assert.Single(s.Party.OfType<string>());
    }

    [Fact]
    public void Choosing_a_placed_party_member_swaps_positions()
    {
        var s = Session();
        s.Party[0] = "7001";
        s.Party[5] = "7002";
        s.Activate("103");
        s.Activate("1");                                       // 슬롯 1(전위 첫 칸)에 마법사를 고른다
        s.Activate("7002");

        Assert.Equal("7002", s.Party[0]);
        Assert.Equal("7001", s.Party[5]);                      // 용사는 마법사가 있던 칸으로
    }

    [Fact]
    public void Choosing_a_placed_party_member_for_an_empty_slot_moves_it()
    {
        var s = Session();
        s.Party[5] = "7002";
        s.Activate("103");
        s.Activate("1");
        s.Activate("7002");

        Assert.Equal("7002", s.Party[0]);
        Assert.Null(s.Party[5]);                               // 원래 칸은 비게 된다
        Assert.Single(s.Party.OfType<string>());
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
        s.Party[0] = "7001";
        s.Party[4] = "7002";
        s.Activate("103");

        var json = s.ToSaveJson();
        Assert.Contains("\"Characters\"", json);
        Assert.Contains("\"Party\"", json);

        var loaded = GameSession.FromSaveJson(pack, json);
        Assert.Equal(Screen.Home, loaded.Screen);
        Assert.Equal(new CharacterState(5, 36, 40, 3, 30), loaded.Characters["7001"]);
        Assert.Equal(new CharacterState(8, 0, 50, 40, 80), loaded.Characters["7003"]);
        string?[] expectedParty = ["7001", null, null, null, "7002", null, null, null, null];
        Assert.True(expectedParty.SequenceEqual(loaded.Party));
    }

    [Fact]
    public void Save_without_characters_or_party_loads_as_empty()
    {
        var s = GameSession.FromSaveJson(Pack(), "{\"Version\":1,\"PackId\":\"demo\",\"SceneId\":\"a\"}");
        Assert.Empty(s.Characters);
        Assert.Equal(GameSession.PartySlotCount, s.Party.Length);
        Assert.All(s.Party, p => Assert.Null(p));
    }

    [Fact]
    public void Short_party_array_is_padded_and_empty_strings_mean_empty_slots()
    {
        var s = GameSession.FromSaveJson(Pack(),
            "{\"Version\":1,\"PackId\":\"demo\",\"SceneId\":\"a\"," +
            "\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}," +
            "\"Party\":[\"\",\"7001\"]}");
        Assert.Equal(9, s.Party.Length);
        Assert.Null(s.Party[0]);
        Assert.Equal("7001", s.Party[1]);
    }

    const string Header = "{\"Version\":1,\"PackId\":\"demo\",\"SceneId\":\"a\",";
    const string Owned7001 = "\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}";

    [Theory]
    [InlineData("\"Characters\":{\"9999\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}")]   // 마스터에 없는 캐릭터
    [InlineData("\"Characters\":{\"7001\":{\"Level\":0,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}")]   // 레벨 0
    [InlineData("\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":6,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}")]   // HP > MaxHp
    [InlineData("\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":-1,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}")]  // HP 음수
    [InlineData("\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":11,\"XpMax\":10}}")]  // XP > XpMax
    [InlineData("\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":-1,\"XpMax\":10}}")]  // XP 음수
    [InlineData(Owned7001 + ",\"Party\":[\"7002\"]")]                                                    // 보유하지 않은 캐릭터를 배치
    [InlineData(Owned7001 + ",\"Party\":[\"7001\",\"7001\"]")]                                           // 같은 캐릭터 중복 배치
    [InlineData(Owned7001 + ",\"Party\":[\"7001\",null,null,null,null,null,null,null,null,null]")]     // 10칸
    public void Invalid_character_or_party_data_in_a_save_is_rejected(string body)
        => Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(), Header + body + "}"));

    [Fact]
    public void Characters_in_a_save_need_a_character_inventory_master()
        => Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(null), Header + Owned7001 + "}"));
}
