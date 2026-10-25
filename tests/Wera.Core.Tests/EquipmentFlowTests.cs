using Wera.Core;

namespace Wera.Core.Tests;

public class EquipmentFlowTests
{
    static GamePack Pack() => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
        ("master/character_inventory_master_v1.tsv",
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
            "7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n")));

    /// <summary>용사는 슬롯 1(인덱스 0), 마법사는 슬롯 5(인덱스 4)에 배치.</summary>
    static GameSession Session()
    {
        var s = GameSession.NewGame(Pack());
        s.Characters[7001] = new CharacterState(5, 36, 40, 3, 30);
        s.Characters[7002] = new CharacterState(3, 10, 25, 12, 30);
        s.Party[0] = 7001;
        s.Party[4] = 7002;
        s.Activate("202");
        return s;
    }

    // ---- 파티 설정 화면 아래의 텍스트 버튼 ----

    [Fact]
    public void Party_setup_has_placement_and_equipment_mode_buttons_below_the_slots()
    {
        var s = Session();
        var modes = s.Entries.Where(e => e.Code is "10" or "11").ToList();
        Assert.Equal(
            [new ScreenEntry("10", "캐릭터 배치", Screen.PartySetup), new ScreenEntry("11", "장비 착용", Screen.EquipmentSetup)],
            modes);
        // 슬롯 항목(1~9) 다음, 돌아가기 앞에 온다.
        Assert.Equal(["10", "11", "999"], s.Entries.Where(e => e.Arg is null).Select(e => e.Code));
    }

    [Fact]
    public void The_two_buttons_switch_between_placement_and_equipment_screens()
    {
        var s = Session();
        Assert.NotNull(s.Activate("11"));
        Assert.Equal(Screen.EquipmentSetup, s.Screen);

        Assert.NotNull(s.Activate("10"));
        Assert.Equal(Screen.PartySetup, s.Screen);

        Assert.NotNull(s.Activate("11"));
        Assert.NotNull(s.Activate("11"));                       // 지금 모드의 버튼을 눌러도 그대로
        Assert.Equal(Screen.EquipmentSetup, s.Screen);
    }

    [Fact]
    public void Equipment_setup_has_the_same_buttons_and_slot_codes_for_occupied_slots_only()
    {
        var s = Session();
        s.Activate("11");

        Assert.Equal(["1", "5", "10", "11", "999"], s.Entries.Select(e => e.Code));   // 캐릭터가 있는 슬롯 1, 5만
        Assert.Null(s.Activate("2"));                           // 빈 슬롯은 고를 수 없다
        Assert.Null(s.Activate("9"));
        Assert.Equal(Screen.EquipmentSetup, s.Screen);
    }

    // ---- 돌아가기 ----

    [Fact]
    public void Back_from_equipment_setup_goes_home_not_to_placement()
    {
        var s = Session();
        s.Activate("11");
        Assert.NotNull(s.Activate("999"));
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Back_from_equipment_setup_goes_home_even_when_reached_from_the_home_button_directly()
    {
        var s = GameSession.NewGame(Pack());
        s.Activate("202");
        s.Activate("11");
        s.Activate("10");
        s.Activate("11");
        s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Back_from_placement_still_goes_home()
    {
        var s = Session();
        s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
    }

    // ---- 슬롯을 고르면 그 캐릭터의 장비 목록 ----

    [Fact]
    public void Picking_a_slot_in_equipment_setup_opens_that_characters_equipment_list()
    {
        var s = Session();
        s.Activate("11");

        Assert.NotNull(s.Activate("5"));

        Assert.Equal(Screen.EquipmentList, s.Screen);
        Assert.Equal(4, s.CurrentSlot);
        Assert.Equal("마법사", s.CurrentSlotCharacterName);
    }

    [Fact]
    public void Back_from_the_equipment_list_returns_to_equipment_setup_and_clears_the_slot()
    {
        var s = Session();
        s.Activate("11");
        s.Activate("1");
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));    // 장비 데이터가 정해질 때까지는 돌아가기뿐

        Assert.NotNull(s.Activate("999"));

        Assert.Equal(Screen.EquipmentSetup, s.Screen);
        Assert.Null(s.CurrentSlot);
        Assert.Null(s.CurrentSlotCharacterName);
        Assert.Equal(Screen.Home, (s.Activate("999"), s.Screen).Screen);   // 거기서 돌아가면 홈
    }

    [Fact]
    public void Slot_numbers_in_the_equipment_list_screen_are_not_slot_codes()
    {
        var s = Session();
        s.Activate("11");
        s.Activate("1");
        Assert.Null(s.Activate("5"));                           // 다른 슬롯으로 바로 갈 수 없다
        Assert.Equal(Screen.EquipmentList, s.Screen);
        Assert.Equal(0, s.CurrentSlot);
    }

    // ---- 배치 화면은 그대로 ----

    [Fact]
    public void Placement_slot_picking_still_opens_the_character_list_including_empty_slots()
    {
        var s = Session();
        Assert.NotNull(s.Activate("2"));                        // 빈 슬롯도 고를 수 있다
        Assert.Equal(Screen.CharacterSelect, s.Screen);
        Assert.Equal(1, s.CurrentSlot);
        Assert.Equal(Screen.PartySetup, (s.Activate("999"), s.Screen).Screen);
    }

    [Fact]
    public void Leaving_the_equipment_flow_by_save_and_load_ends_up_on_home()
    {
        var s = Session();
        s.Activate("11");
        s.Activate("1");
        Assert.Contains("\"EquipmentList\"", s.ToSaveJson());

        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal(Screen.Home, loaded.Screen);
        Assert.Null(loaded.CurrentSlot);
    }

    [Fact]
    public void Equipment_screens_show_the_same_slot_views_as_the_placement_screen()
    {
        var s = Session();
        var placement = s.PartySlotViews;
        s.Activate("11");
        Assert.Equal(placement.Select(v => v.Name), s.PartySlotViews.Select(v => v.Name));
        Assert.Equal(placement.Select(v => v.RowName), s.PartySlotViews.Select(v => v.RowName));
    }
}
