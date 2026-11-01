using Wera.Core;

namespace Wera.Core.Tests;

// 아지트: 층마다 계층/시설/보스 슬롯(3×3, 한 번에 3개 층). 확장하면 새 층이 1층이 되고 기존 층은 밀려난다. 보스 슬롯의 캐릭터는 파티와 아지트 중 한 곳에만 있다.
public class HideoutTests
{
    const string Header = "expand_count\tgroup_code\titem_code\tamount\n";

    static GameSession FacilitySession(string build = "2\t1\t2\t50\n2\t2\t4\t3\n3\t1\t2\t200\n", string floors = "1\t1\n")
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n4\t철광석\t99\n"),
            ("master/facility_master_v1.tsv", "code\tname\n1\t정문\n2\t대장간\n3\t마법탑\n"),
            ("master/facility_condition_master_v1.tsv", "facility_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n3\t1\t1\tGreaterOrEqual\t1\n"),
            ("master/facility_build_master_v1.tsv", "facility_code\tgroup_code\titem_code\tamount\n" + build),
            ("master/hideout_floor_master_v1.tsv", "floor_order\tfacility_code\n" + floors)));
        return GameSession.NewGame(pack);
    }

    // 대장간(2)을 지을 수 있고 오퍼레이터 슬롯 1(정비, 태그 4 필요)이 있다. 용사 7001은 태그 4가 있고 마법사 7002는 없다.
    static GameSession OperatorSession(string flows = "2\t1\tJumpOperatorNotExists\t1\t0\t4\n2\t2\tAddInventory\t4\t3\t0\n2\t3\tFinish\t0\t0\t0\n2\t4\tAddInventory\t4\t1\t0\n",
        string operatorSlots = "2\t1\n", string slotTags = "1\t4\n")
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
                "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t정비\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7001\t2\n7001\t4\n7002\t2\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n4\t철광석\t99\n"),
            ("master/facility_master_v1.tsv", "code\tname\n1\t정문\n2\t대장간\n"),
            ("master/operator_slot_master_v1.tsv", "code\tname\n1\t정비\n2\t경비\n"),
            ("master/facility_operator_slot_master_v1.tsv", "facility_code\toperator_slot_code\n" + operatorSlots),
            ("master/operator_slot_tag_master_v1.tsv", "operator_slot_code\ttag_code\n" + slotTags),
            ("master/facility_flow_master_v1.tsv", "facility_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + flows)));
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);
        s.AddCharacter(7002);
        return s;
    }

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
        Assert.Equal(["1", "4", "6", "1000", "1001", "999"], s.Entries.Select(e => e.Code));     // [1] 확장 가능, [4] 1층, [6] 1층 보스, ▲ ▼ 돌아가기
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
        Assert.Equal(["1", "3", "4", "6", "7", "9", "1000", "1001", "999"], s.Entries.Select(e => e.Code));
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

    [Fact]
    public void The_base_facility_belongs_to_the_floor_by_creation_order_and_shows_its_name_only_in_the_floor_detail()
    {
        var s = FacilitySession("", "1\t1\n");
        Assert.Equal("정문", s.BaseFacilityOfFloor(0)!.Name);
        s.Activate(GameSession.HideoutEntryCode);
        Assert.Equal("1층", s.HideoutRows[1].FloorName);                    // 이름은 n층
        s.Activate("4");
        Assert.Equal(Screen.HideoutFloorDetail, s.Screen);
        Assert.Equal("기본 시설: 정문", s.HideoutFloorDetailText);
        s.Activate("999");
        Assert.Equal(Screen.Hideout, s.Screen);
    }

    [Fact]
    public void Floors_keep_their_creation_order_when_pushed_down_by_an_expansion()
    {
        var s = Session();
        s.InventoryItems[2] = new InventoryItem(1000);
        Assert.Equal(1, s.HideoutFloorOrderOf(0));
        s.ExpandHideout(1);
        Assert.Equal(2, s.HideoutFloorOrderOf(0));                         // 새 층은 2번째, 처음 층은 아래로 밀려도 1번째
        Assert.Equal(1, s.HideoutFloorOrderOf(1));
        var withBase = FacilitySession("", "1\t1\n");
        withBase.InventoryItems[2] = new InventoryItem(1000);
        Assert.Equal("정문", withBase.BaseFacilityOfFloor(0)!.Name);
    }

    [Fact]
    public void A_facility_is_built_in_the_middle_slot_with_a_material_group_and_can_be_demolished_without_refund()
    {
        var s = FacilitySession();
        s.InventoryItems[2] = new InventoryItem(100);
        s.Activate(GameSession.HideoutEntryCode);
        s.Activate("5");
        Assert.Equal(Screen.HideoutFacilitySelect, s.Screen);
        Assert.Equal(["1", "2", "999"], s.Entries.Select(e => e.Code));       // 마법탑(3)은 플래그 조건 때문에 없다
        s.Activate("2");
        Assert.Equal(Screen.HideoutBuild, s.Screen);
        Assert.Equal(["1", "2", "999"], s.Entries.Select(e => e.Code));
        Assert.Equal([true, false, true], s.Entries.Select(e => e.Enabled));
        s.Activate("1");
        Assert.Equal(Screen.Hideout, s.Screen);
        Assert.Equal(50, s.InventoryItems[2].Amount);
        Assert.Equal("대장간", s.HideoutRows[1].FacilityName);
        Assert.Equal(2, s.HideoutFacilities[0]);

        s.Activate("5");                                                    // 지은 시설 상세
        Assert.Equal(Screen.HideoutFacilityDetail, s.Screen);
        Assert.Equal("시설: 대장간", s.HideoutFacilityDetailText);
        s.Activate(GameSession.DemolishEntryCode);
        Assert.Equal(Screen.Hideout, s.Screen);
        Assert.Null(s.HideoutFacilities[0]);
        Assert.Equal(50, s.InventoryItems[2].Amount);                       // 재료는 돌려받지 못한다
        Assert.Equal("공터", s.HideoutRows[1].FacilityName);
    }

    [Fact]
    public void Facility_conditions_check_flags_and_facilities_without_build_rows_are_free()
    {
        var s = FacilitySession();
        Assert.Equal([1L, 2L], s.AvailableFacilities.Select(f => f.Code));
        s.PlayerFlags[1] = 1;
        Assert.Equal([1L, 2L, 3L], s.AvailableFacilities.Select(f => f.Code));
        Assert.True(s.CanAffordFacility(1));                                // 정문은 재료 행이 없다
        Assert.False(s.CanAffordFacility(2));
        Assert.True(s.BuildFacility(0, 1));
        Assert.False(s.BuildFacility(0, 2));                                // 이미 지어져 있다
        Assert.Equal(1, s.HideoutFacilities[0]);
    }

    [Fact]
    public void Selecting_a_free_facility_builds_it_at_once_and_the_facilities_survive_the_save()
    {
        var s = FacilitySession();
        s.Activate(GameSession.HideoutEntryCode);
        s.Activate("5"); s.Activate("1");
        Assert.Equal(Screen.Hideout, s.Screen);
        Assert.Equal(1, s.HideoutFacilities[0]);
        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal([1L], loaded.HideoutFacilities);
    }

    [Fact]
    public void The_facility_masters_are_validated()
    {
        PackFormatException Fail(string build, string floors) => Assert.Throws<PackFormatException>(() => FacilitySession(build, floors));
        Assert.Contains("시설 '9'", Fail("9\t1\t2\t1\n", "1\t1\n").Message);
        Assert.Contains("아이템 '9'", Fail("2\t1\t9\t1\n", "1\t1\n").Message);
        Assert.Contains("1 이상", Fail("2\t1\t2\t0\n", "1\t1\n").Message);
        Assert.Contains("두 번", Fail("2\t1\t2\t1\n2\t1\t2\t1\n", "1\t1\n").Message);
        Assert.Contains("기본 시설 '9'", Fail("", "1\t9\n").Message);
        Assert.Contains("두 번", Fail("", "1\t1\n1\t2\n").Message);
        Assert.Contains("1 이상", Fail("", "0\t1\n").Message);
    }

    [Fact]
    public void An_operator_slot_takes_only_characters_with_the_required_tags_and_moves_them_out_of_parties()
    {
        var s = OperatorSession();
        Assert.True(s.BuildFacility(0, 2));
        s.Party[0] = 7001;
        Assert.True(s.CanOperate(7001, 1));
        Assert.False(s.CanOperate(7002, 1));                                   // 정비 태그가 없다
        Assert.False(s.SetHideoutOperator(0, true, 1, 7002));
        Assert.False(s.SetHideoutOperator(0, true, 2, 7001));                  // 이 시설의 슬롯이 아니다
        Assert.False(s.SetHideoutOperator(0, false, 1, 7001));                 // 기본 시설이 없다
        Assert.True(s.SetHideoutOperator(0, true, 1, 7001));
        Assert.Equal(7001, s.HideoutOperatorOf(0, true, 1));
        Assert.Equal(-1, s.PartyIndexOf(7001));                                // 파티에서 빠졌다
        Assert.True(s.SetHideoutOperator(0, true, 1, null));
        Assert.Null(s.HideoutOperatorOf(0, true, 1));
    }

    [Fact]
    public void The_facility_detail_lists_operator_slots_and_the_operator_select_screen_lists_eligible_characters()
    {
        var s = OperatorSession();
        s.BuildFacility(0, 2);
        s.Activate(GameSession.HideoutEntryCode);
        s.Activate("5");                                                       // 지은 시설 상세
        Assert.Equal(["1", "2", "999"], s.Entries.Select(e => e.Code));         // [1] 철거, [2] 정비 슬롯
        Assert.Equal("정비: 미지정", s.Entries[1].Label);
        s.Activate("2");
        Assert.Equal(Screen.HideoutOperatorSelect, s.Screen);
        Assert.Equal("1층 대장간 - 정비", s.CurrentHideoutOperatorTitle);
        Assert.Equal(1, s.Entries.Count(e => e.Action == EntryAction.SetHideoutOperator));   // 용사만
        s.Pick("7001");
        Assert.Equal(Screen.HideoutFacilityDetail, s.Screen);
        Assert.Equal("정비: 용사", s.Entries[1].Label);
        Assert.Equal(7001, s.HideoutOperatorOf(0, true, 1));
    }

    [Fact]
    public void The_facility_circuit_runs_after_the_turn_ends_and_the_operator_changes_the_output()
    {
        var s = OperatorSession();
        s.BuildFacility(0, 2);
        Assert.Equal("시설 생산: 철광석 +1", s.RunFacilityCircuits());           // 오퍼레이터가 없다: 1개
        Assert.Equal(1, s.InventoryItems[4].Amount);
        s.SetHideoutOperator(0, true, 1, 7001);
        Assert.Equal("시설 생산: 철광석 +3", s.RunFacilityCircuits());           // 오퍼레이터가 있다: 3개
        Assert.Equal(4, s.InventoryItems[4].Amount);
    }

    [Fact]
    public void An_invasion_skips_the_production_once_and_the_end_turn_button_runs_the_circuit()
    {
        var s = OperatorSession();
        s.BuildFacility(0, 2);
        s.HideoutInvaded = true;
        Assert.Equal("적이 침입해 시설이 생산하지 못했습니다", s.RunFacilityCircuits());
        Assert.False(s.HideoutInvaded);
        Assert.False(s.InventoryItems.ContainsKey(4));
        Assert.Equal("시설 생산: 철광석 +1", s.RunFacilityCircuits());
        Assert.Null(new GameSession[] { OperatorSession() }[0].RunFacilityCircuits());     // 시설이 없으면 생산도 없다
    }

    [Fact]
    public void Operators_survive_the_save_and_a_character_in_two_places_is_rejected()
    {
        var s = OperatorSession();
        s.BuildFacility(0, 2);
        s.SetHideoutOperator(0, true, 1, 7001);
        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal(7001, loaded.HideoutOperatorOf(0, true, 1));
        s.Party[0] = 7001;
        var bad = s.ToSaveJson();
        Assert.Contains("파티와 아지트", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(s.Pack, bad)).Message);
    }

    [Fact]
    public void The_operator_masters_and_flows_are_validated()
    {
        PackFormatException Fail(string flows = "", string slots = "2\t1\n", string tags = "1\t4\n") =>
            Assert.Throws<PackFormatException>(() => OperatorSession(flows, slots, tags));
        Assert.Contains("시설 '9'", Fail(slots: "9\t1\n").Message);
        Assert.Contains("오퍼레이터 슬롯 '9'", Fail(slots: "2\t9\n").Message);
        Assert.Contains("두 번", Fail(slots: "2\t1\n2\t1\n").Message);
        Assert.Contains("태그 '9'", Fail(tags: "1\t9\n").Message);
        Assert.Contains("두 번", Fail(tags: "1\t4\n1\t4\n").Message);
        Assert.Contains("알 수 없습니다", Fail("2\t1\tNope\t0\t0\t0\n").Message);
        Assert.Contains("보다 커야", Fail("2\t1\tJump\t1\t0\t0\n").Message);
        Assert.Contains("오퍼레이터 슬롯 '2'", Fail("2\t1\tJumpOperatorExists\t2\t0\t2\n2\t2\tFinish\t0\t0\t0\n").Message);   // 대장간은 슬롯 1만 가진다
        Assert.Contains("태그 코드 '9'", Fail("2\t1\tJumpOperatorHaveTag\t1\t9\t2\n2\t2\tFinish\t0\t0\t0\n").Message);
        Assert.Contains("인벤토리 코드 '9'", Fail("2\t1\tAddInventory\t9\t1\t0\n").Message);
        Assert.Contains("모수", Fail("2\t1\tJumpRandom\t1\t0\t2\n2\t2\tFinish\t0\t0\t0\n").Message);
    }
}
