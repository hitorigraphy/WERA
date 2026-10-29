using Wera.Core;

namespace Wera.Core.Tests;

// 홈 메뉴는 구분선으로 나뉜 세 분류다:
//  [RPG] 101 던전, 102 상점, 103 이벤트, 104 교류, 105 파티 설정
//  [정보] 201 동료 확인, 202 장비 확인, 203 아이템 확인
//  [시스템] 200 세이브, 300 로드
public class HomeMenuTests
{
    static GameSession Session() =>
        GameSession.NewGame(GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"))));

    [Fact]
    public void The_menu_has_three_groups_in_order()
    {
        var menu = Session().HomeMenu;
        Assert.Equal(["RPG", "정보", "시스템"], menu.Select(g => g.Name));
        Assert.Equal(
            [("101", "던전"), ("102", "상점"), ("103", "이벤트"), ("104", "교류"), ("105", "파티 설정"), ("110", "전투(임시)")],
            menu[0].Entries.Select(e => (e.Code, e.Label)));                      // 던전/상점은 항상 있고, 열려 있지 않으면 비활성
        Assert.Equal(
            [("201", "동료 확인"), ("202", "장비 확인"), ("203", "아이템 확인")],
            menu[1].Entries.Select(e => (e.Code, e.Label)));
        Assert.Equal([("200", "세이브"), ("300", "로드")], menu[2].Entries.Select(e => (e.Code, e.Label)));
    }

    [Fact]
    public void Dungeon_and_shop_lead_the_rpg_group_when_they_are_open()
    {
        var s = GameSession.NewGame(SampleLoader.Pack());
        var rpg = s.HomeMenu[0].Entries;
        Assert.Equal(["101", "102", "103", "104", "105", "110"], rpg.Select(e => e.Code));
        Assert.Equal(["던전", "상점", "이벤트", "교류", "파티 설정", "전투(임시)"], rpg.Select(e => e.Label));
    }

    [Fact]
    public void The_flat_entries_are_the_groups_in_order()
    {
        var s = Session();
        Assert.Equal(s.HomeMenu.SelectMany(g => g.Entries).Select(e => e.Code), s.Entries.Select(e => e.Code));
        Assert.Equal(["101", "102", "103", "104", "105", "110", "201", "202", "203", "200", "300"], s.Entries.Select(e => e.Code));
    }

    [Fact]
    public void The_party_setup_is_105_and_103_is_the_event()
    {
        var s = Session();
        Assert.Equal("105", GameSession.PartyEntryCode);
        Assert.Equal(Screen.Event, s.Activate("103")!.Target);
        s.Activate("999");
        Assert.Equal(Screen.PartySetup, s.Activate("105")!.Target);
        Assert.Equal(Screen.PartySetup, s.Screen);
    }

    [Theory]
    [InlineData("103", Screen.Event)]
    [InlineData("104", Screen.Interaction)]
    [InlineData("201", Screen.CompanionCheck)]
    [InlineData("202", Screen.EquipmentCheck)]
    [InlineData("203", Screen.ItemCheck)]
    public void The_menu_entries_open_their_screens_and_back_returns_home(string code, Screen screen)
    {
        var s = Session();
        s.Activate(code);
        Assert.Equal(screen, s.Screen);
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));
        s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void The_system_group_keeps_save_and_load()
    {
        var s = Session();
        Assert.Equal(EntryAction.Save, s.Activate("200")!.Action);
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Equal(Screen.Load, s.Activate("300")!.Target);
    }

    [Fact]
    public void The_old_codes_no_longer_mean_what_they_did()
    {
        var s = Session();
        s.Activate("202");                                              // 예전 파티 설정 → 지금은 장비 확인
        Assert.Equal(Screen.EquipmentCheck, s.Screen);
        s.Activate("999");
        s.Activate("205");                                              // 예전 아이템 확인 코드는 이제 없다
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void The_shop_entry_is_labelled_shop_and_still_needs_an_open_shop()
    {
        var s = Session();
        var shop = s.Entries.Single(e => e.Code == "102");
        Assert.False(shop.Enabled);
        Assert.Equal(shop, s.Activate("102"));
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Null(s.Message);

        var sample = GameSession.NewGame(SampleLoader.Pack());
        Assert.Equal("상점", sample.Entries.Single(e => e.Code == "102").Label);
    }

    [Fact]
    public void The_old_party_code_does_not_open_the_party_setup_any_more()
    {
        var s = Session();
        s.Activate("103");
        Assert.NotEqual(Screen.PartySetup, s.Screen);
    }

    [Fact]
    public void The_new_screens_survive_the_save_as_home()
    {
        var s = Session();
        s.Activate("203");
        var json = s.ToSaveJson();
        Assert.Contains("\"ItemCheck\"", json);
        Assert.Equal(Screen.Home, GameSession.FromSaveJson(s.Pack, json).Screen);       // 불러오기는 늘 홈으로 간다
    }

    // ---- 슬롯 대상 목록은 보유한 캐릭터만 ----

    [Fact]
    public void Slot_candidate_lists_hide_characters_that_are_not_owned()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
                "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n7003\t전사\t1\t1\t2\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n1\t2\n7001\t2\n")));
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);                                            // 마법사(7002)와 전사(7003)는 보유하지 않았다

        s.Activate("105"); s.Activate("1");
        Assert.Equal([1L, 7001L], s.CharacterList.Select(c => c.Code).Order());      // 파티 슬롯 목록
        Assert.Equal(["1", "7001", "999"], s.Entries.Select(e => e.Code));              // 마스터 순서(플레이어가 먼저), 비우기는 빈 슬롯이라 없다
        Assert.Null(s.Activate("7002"));                                               // 목록에 없으니 코드를 쳐도 안 된다
        s.Activate("999"); s.Activate("999");

        s.Activate("1");                                                               // 파트너 슬롯 목록
        Assert.Equal([7001L], s.SupportList.Select(c => c.Code));                       // 플레이어도 목록에 없다
        Assert.DoesNotContain(s.SupportList, c => c.Code is 7002 or 7003);
        Assert.Null(s.Activate("7003"));
    }

    [Fact]
    public void A_character_appears_in_the_lists_as_soon_as_it_is_owned()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
                "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n")));
        var s = GameSession.NewGame(pack);
        s.Activate("1");
        Assert.DoesNotContain(s.SupportList, c => c.Code == 7001);
        s.Activate("999");

        s.AddCharacter(7001);
        s.Activate("1");
        Assert.Contains(s.SupportList, c => c.Code == 7001);
    }
}

file static class SampleLoader
{
    public static GamePack Pack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(zip);
    }
}
