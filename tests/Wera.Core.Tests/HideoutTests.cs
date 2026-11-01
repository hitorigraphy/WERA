using Wera.Core;

namespace Wera.Core.Tests;

// 아지트: 층마다 계층/시설/보스 슬롯(3×3, 한 번에 3개 층). 확장하면 새 층이 1층이 되고 기존 층은 밀려난다. 보스 슬롯의 캐릭터는 파티와 아지트 중 한 곳에만 있다.
public class HideoutTests
{
    const string Header = "expand_count\tgroup_code\titem_code\tamount\n";

    static GameSession Session(string expand = "1\t1\t2\t100\n1\t2\t4\t5\n1\t2\t5\t2\n2\t1\t2\t300\n3\t1\t2\t1\n4\t1\t2\t1\n")
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
                "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n7003\t전사\t1\t1\t2\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7001\t2\n7003\t2\n"),
            ("master/common_item_inventory_master_v1.tsv",
                "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n4\t철광석\t99\n5\t제련석\t99\n"),
            ("master/hideout_expand_master_v1.tsv", Header + expand)));
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);
        s.AddCharacter(7002);
        s.AddCharacter(7003);
        return s;
    }

    [Fact]
    public void It_starts_with_one_floor_and_the_expand_row_above_it()
    {
        var s = Session();
        s.Activate(GameSession.HideoutEntryCode);
        Assert.Equal(Screen.Hideout, s.Screen);
        Assert.Equal(1, s.HideoutFloorCount);
        Assert.Equal(-1, s.HideoutViewTop);
        Assert.Equal(["확장 가능", "1층", "----"], s.HideoutRows.Select(r => r.FloorName));
        Assert.Equal(["1", "6", "1000", "1001", "999"], s.Entries.Select(e => e.Code));          // [1] 확장 가능, [6] 1층 보스, ▲ ▼ 돌아가기
        Assert.False(s.Entries.Single(e => e.Code == "1000").Enabled);
        Assert.False(s.Entries.Single(e => e.Code == "1001").Enabled);
        Assert.True(s.HideoutRows[1].Boss!.IsEmpty);
    }

    [Fact]
    public void Expanding_costs_one_group_and_the_new_floor_is_the_first_floor_while_old_floors_move_down_with_their_boss()
    {
        var s = Session();
        s.InventoryItems[2] = new InventoryItem(150);
        s.Activate(GameSession.HideoutEntryCode);
        s.Pick("6"); s.Pick("7001");                         // 1층 보스에 용사
        Assert.Equal(Screen.Hideout, s.Screen);
        Assert.Equal(7001, s.HideoutBosses[0]);

        s.Activate("1");
        Assert.Equal(Screen.HideoutExpand, s.Screen);
        Assert.Equal(["1", "2", "999"], s.Entries.Select(e => e.Code));
        Assert.Equal([true, false, true], s.Entries.Select(e => e.Enabled));                  // 그룹 2(철광석, 제련석)는 못 갖춤
        Assert.Equal("금화 ×100", s.Entries[0].Label);
        Assert.Equal("철광석 ×5, 제련석 ×2", s.Entries[1].Label);
        s.Activate("2");                                            // 갖추지 못한 그룹은 반응 없음
        Assert.Equal(Screen.HideoutExpand, s.Screen);

        s.Activate("1");
        Assert.Equal(Screen.Hideout, s.Screen);
        Assert.Equal(50, s.InventoryItems[2].Amount);
        Assert.Equal(2, s.HideoutFloorCount);
        Assert.Equal([null, 7001L], s.HideoutBosses);               // 용사는 2층으로 밀려났다
        Assert.Equal(["확장 가능", "1층", "2층"], s.HideoutRows.Select(r => r.FloorName));
        Assert.Equal(7001, s.HideoutRows[2].Boss!.CharacterCode);
    }

    [Fact]
    public void A_group_needs_all_of_its_items_and_groups_are_alternatives()
    {
        var s = Session();
        s.InventoryItems[4] = new InventoryItem(5);
        s.InventoryItems[5] = new InventoryItem(1);
        Assert.False(s.CanAffordHideoutExpand(s.HideoutExpandGroups[1]));
        s.InventoryItems[5] = new InventoryItem(2);
        Assert.True(s.CanAffordHideoutExpand(s.HideoutExpandGroups[1]));
        Assert.True(s.ExpandHideout(2));
        Assert.Equal(2, s.HideoutFloorCount);
        Assert.False(s.InventoryItems.ContainsKey(4));              // 다 쓴 아이템은 없어진다
        Assert.False(s.ExpandHideout(2));                           // 2회째에는 그룹 2가 없다
        Assert.False(s.ExpandHideout(1));                           // 금화가 없다
    }

    [Fact]
    public void The_expand_row_disappears_when_there_is_no_next_expansion()
    {
        var s = Session("1\t1\t2\t1\n");
        s.InventoryItems[2] = new InventoryItem(5);
        Assert.True(s.ExpandHideout(1));
        Assert.Empty(s.HideoutExpandGroups);
        s.Activate(GameSession.HideoutEntryCode);
        Assert.Equal(["----", "1층", "2층"], s.HideoutRows.Select(r => r.FloorName));
        Assert.DoesNotContain(s.Entries, e => e.Code == "1");
    }

    [Fact]
    public void The_view_scrolls_one_floor_at_a_time()
    {
        var s = Session();
        s.InventoryItems[2] = new InventoryItem(1000);
        for (var i = 1; i <= 3; i++) Assert.True(s.ExpandHideout(1));                      // 4층
        s.Activate(GameSession.HideoutEntryCode);
        Assert.Equal(["확장 가능", "1층", "2층"], s.HideoutRows.Select(r => r.FloorName));
        Assert.True(s.Entries.Single(e => e.Code == "1001").Enabled);
        s.Activate("1001");
        Assert.Equal(["1층", "2층", "3층"], s.HideoutRows.Select(r => r.FloorName));
        s.Activate("1001");
        Assert.Equal(["2층", "3층", "4층"], s.HideoutRows.Select(r => r.FloorName));
        Assert.False(s.Entries.Single(e => e.Code == "1001").Enabled);                    // 가장 오래된 층이 맨 아래
        Assert.Equal(["3", "6", "9", "1000", "1001", "999"], s.Entries.Select(e => e.Code));
        s.Activate("1000");
        Assert.Equal(["1층", "2층", "3층"], s.HideoutRows.Select(r => r.FloorName));
    }

    [Fact]
    public void A_character_is_in_either_a_party_or_the_hideout_never_both()
    {
        var s = Session();
        s.Party[0] = 7001;
        Assert.True(s.SetHideoutBoss(0, 7001));                     // 파티의 용사를 아지트로: 파티에서 빠진다
        Assert.Equal(-1, s.PartyIndexOf(7001));
        Assert.Equal(7001, s.HideoutBosses[0]);

        s.Pick("105"); s.Pick("1"); s.Pick("7001");          // 파티에 넣으면 아지트에서 빠진다
        Assert.Equal(0, s.PartyIndexOf(7001));
        Assert.Null(s.HideoutBosses[0]);
    }

    [Fact]
    public void A_boss_on_another_floor_swaps_and_a_boss_slot_can_be_cleared()
    {
        var s = Session();
        s.InventoryItems[2] = new InventoryItem(1000);
        s.ExpandHideout(1);
        Assert.True(s.SetHideoutBoss(0, 7001));
        Assert.True(s.SetHideoutBoss(1, 7002));
        Assert.True(s.SetHideoutBoss(1, 7001));                     // 다른 층의 용사: 자리를 바꾼다
        Assert.Equal([7002L, 7001L], s.HideoutBosses);
        Assert.True(s.SetHideoutBoss(1, null));
        Assert.Equal([7002L, null], s.HideoutBosses);
        Assert.False(s.SetHideoutBoss(5, 7001));                    // 없는 층
        Assert.False(s.SetHideoutBoss(0, 9999));                    // 보유하지 않은 캐릭터
        Assert.True(s.RemoveCharacter(7002));                       // 내보내면 아지트에서도 빠진다
        Assert.Equal([null, (long?)null], s.HideoutBosses);
    }

    [Fact]
    public void The_boss_select_screen_lists_owned_characters_and_the_clear_row_only_when_a_boss_stands_there()
    {
        var s = Session();
        s.Activate(GameSession.HideoutEntryCode);
        s.Activate("6");
        Assert.Equal(Screen.HideoutBossSelect, s.Screen);
        Assert.Equal("1층", s.CurrentHideoutFloorName);
        Assert.DoesNotContain(s.Entries, e => e.Code == GameSession.ClearEntryCode);
        Assert.Equal(4, s.Entries.Count(e => e.Action == EntryAction.SetHideoutBoss));      // 당신, 용사, 마법사, 전사
        s.Pick("7002");
        s.Activate("6");
        Assert.Contains(s.Entries, e => e.Code == GameSession.ClearEntryCode);
        s.Activate(GameSession.ClearEntryCode);
        Assert.Equal(Screen.Hideout, s.Screen);
        Assert.Null(s.HideoutBosses[0]);
    }

    [Fact]
    public void The_hideout_survives_the_save_and_a_character_in_both_places_is_rejected()
    {
        var s = Session();
        s.InventoryItems[2] = new InventoryItem(1000);
        s.ExpandHideout(1);
        s.SetHideoutBoss(1, 7003);
        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal([null, 7003L], loaded.HideoutBosses);

        s.Party[0] = 7001;
        var bad = s.ToSaveJson().Replace("\"Hideout\": [", "\"Hideout\": [7001, ");
        Assert.Contains("파티와 아지트", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(s.Pack, bad)).Message);
    }

    [Fact]
    public void The_expand_master_is_validated()
    {
        PackFormatException Fail(string rows) => Assert.Throws<PackFormatException>(() => Session(rows));
        Assert.Contains("1 이상", Fail("0\t1\t2\t1\n").Message);
        Assert.Contains("1 이상", Fail("1\t1\t2\t0\n").Message);
        Assert.Contains("아이템 '9'", Fail("1\t1\t9\t1\n").Message);
        Assert.Contains("두 번", Fail("1\t1\t2\t1\n1\t1\t2\t2\n").Message);
        Assert.Contains("확장 횟수", Fail("x\t1\t2\t1\n").Message);
    }
}
