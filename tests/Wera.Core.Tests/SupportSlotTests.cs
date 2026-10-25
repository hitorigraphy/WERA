using Wera.Core;

namespace Wera.Core.Tests;

// 홈 화면 위쪽의 슬롯: 당신(변경 불가), 파트너 [1], 어시스트 [2]. 파티 설정과 독립이다.
public class SupportSlotTests
{
    static GamePack Pack() => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
        ("master/character_inventory_master_v1.tsv",
            "character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
            "7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n7003\t전사\t1\t1\t2\n7004\t도적\t1\t2\t1\n")));

    // 용사, 마법사, 전사를 보유(도적은 보유하지 않음). 파티에는 용사가 첫 칸에 있다.
    static GameSession Session()
    {
        var s = GameSession.NewGame(Pack());
        s.Characters[7001] = new CharacterState(5, 36, 40, 3, 30);
        s.Characters[7002] = new CharacterState(3, 10, 25, 12, 30);
        s.Characters[7003] = new CharacterState(8, 50, 50, 40, 80);
        s.Party[0] = 7001;
        return s;
    }

    // ---- 슬롯 표시 ----

    [Fact]
    public void The_player_slot_is_labelled_and_never_changes()
    {
        var s = Session();
        Assert.Equal("당신", s.PlayerSlot.Name);
        Assert.True(s.PlayerSlot.IsEmpty);
        Assert.Null(s.Activate("0"));                                 // 당신 슬롯에는 코드가 없다
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Partner_and_assist_slots_start_empty_and_show_the_chosen_character()
    {
        var s = Session();
        Assert.True(s.PartnerSlot.IsEmpty);
        Assert.True(s.AssistSlot.IsEmpty);

        s.SetSupport(SupportRole.Partner, 7002);
        Assert.Equal("마법사", s.PartnerSlot.Name);
        Assert.Equal("Lv.3", s.PartnerSlot.LevelText);
        Assert.Equal(-1, s.PartnerSlot.Slot);                         // 파티 슬롯 번호가 아니다
        Assert.Equal(2, s.PartnerSlot.Badges.Count);                  // 속성 아이콘도 파티 슬롯과 같다
        Assert.True(s.AssistSlot.IsEmpty);
    }

    [Fact]
    public void The_home_entry_list_is_unchanged_and_the_slot_codes_are_hidden_typed_codes()
    {
        var s = Session();
        Assert.Equal(["103", "200", "300"], s.Entries.Select(e => e.Code));
        Assert.NotNull(s.Activate("1"));
        Assert.Equal(Screen.SupportSelect, s.Screen);
    }

    // ---- 선택 화면 ----

    [Fact]
    public void Choosing_the_partner_slot_lists_every_character_with_unowned_ones_grayed()
    {
        var s = Session();
        s.Activate("1");
        Assert.Equal(SupportRole.Partner, s.CurrentSupportRole);
        Assert.Equal("파트너", s.CurrentSupportRoleName);

        Assert.Equal([7001L, 7002L, 7003L, 7004L, 1L], s.SupportList.Select(c => c.Code));
        Assert.Equal(CharacterListColor.Gray, s.SupportList.Single(c => c.Code == 7004).Color);
        Assert.Equal(["7001", "7002", "7003", "999"], s.Entries.Select(e => e.Code));    // 비우기는 비어 있으면 없다, 회색은 못 고른다
    }

    [Fact]
    public void Selecting_a_character_sets_it_and_returns_home()
    {
        var s = Session();
        s.Activate("1");
        var entry = s.Activate("7002");

        Assert.Equal(EntryAction.SetSupport, entry!.Action);
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Equal(7002, s.PartnerCode);
        Assert.Null(s.AssistCode);
        Assert.Null(s.CurrentSupportRole);
    }

    [Fact]
    public void An_unowned_character_cannot_be_chosen()
    {
        var s = Session();
        s.Activate("1");
        Assert.Null(s.Activate("7004"));
        Assert.Equal(Screen.SupportSelect, s.Screen);
        Assert.False(s.SetSupport(SupportRole.Partner, 7004));
        Assert.Null(s.PartnerCode);
    }

    [Fact]
    public void The_clear_entry_appears_only_when_the_slot_has_a_character_and_empties_it()
    {
        var s = Session();
        s.SetSupport(SupportRole.Assist, 7003);

        s.Activate("1");                                              // 파트너는 비어 있다
        Assert.DoesNotContain(s.Entries, e => e.Code == "0");
        s.Activate("999");

        s.Activate("2");
        Assert.Equal("0", s.Entries[0].Code);
        Assert.Equal(CharacterListColor.SkyBlue, s.SupportList.Single(c => c.Code == 7003).Color);   // 이 슬롯의 캐릭터
        s.Activate("0");
        Assert.Null(s.AssistCode);
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Back_returns_home_without_changing_anything()
    {
        var s = Session();
        s.SetSupport(SupportRole.Partner, 7001);
        s.Activate("1");
        s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Equal(7001, s.PartnerCode);
        Assert.Null(s.CurrentSupportRole);
    }

    [Fact]
    public void Slot_codes_work_only_on_the_home_screen()
    {
        var s = Session();
        s.Activate("103");
        s.Activate("1");                                              // 파티 설정의 1은 슬롯 1번(캐릭터 목록)이지 파트너가 아니다
        Assert.Equal(Screen.CharacterSelect, s.Screen);
        Assert.Null(s.CurrentSupportRole);
        s.Activate("999"); s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
    }

    // ---- 독립성과 교환 ----

    [Fact]
    public void Partner_and_assist_are_independent_of_the_party()
    {
        var s = Session();
        s.SetSupport(SupportRole.Partner, 7001);                      // 파티에 있는 용사도 지정할 수 있다
        Assert.Equal(7001, s.Party[0]);
        Assert.Equal(7001, s.PartnerCode);

        s.Party[0] = null;                                            // 파티에서 빼도 파트너는 그대로
        Assert.Equal(7001, s.PartnerCode);
        s.Activate("103"); s.Activate("1"); s.Activate("7002");       // 파티 설정에서 마법사를 배치해도 파트너/어시스트는 그대로
        Assert.Equal(7002, s.Party[0]);
        Assert.Equal(7001, s.PartnerCode);
        Assert.Null(s.AssistCode);
    }

    [Fact]
    public void Choosing_the_character_of_the_other_slot_swaps_them()
    {
        var s = Session();
        s.SetSupport(SupportRole.Partner, 7001);
        s.SetSupport(SupportRole.Assist, 7002);

        s.Activate("1");
        Assert.Equal(CharacterListColor.Green, s.SupportList.Single(c => c.Code == 7002).Color);   // 다른 쪽 슬롯의 캐릭터
        s.Activate("7002");
        Assert.Equal((7002L, 7001L), (s.PartnerCode, s.AssistCode));
    }

    [Fact]
    public void Choosing_the_other_slots_character_moves_it_when_this_slot_was_empty()
    {
        var s = Session();
        s.SetSupport(SupportRole.Assist, 7002);
        s.SetSupport(SupportRole.Partner, 7002);
        Assert.Equal(7002, s.PartnerCode);
        Assert.Null(s.AssistCode);
    }

    // ---- 세이브 ----

    [Fact]
    public void Partner_and_assist_round_trip_through_the_save()
    {
        var s = Session();
        s.SetSupport(SupportRole.Partner, 7003);
        s.SetSupport(SupportRole.Assist, 7001);

        var json = s.ToSaveJson();
        Assert.Contains("\"Partner\": 7003", json);
        var loaded = GameSession.FromSaveJson(Pack(), json);
        Assert.Equal((7003L, 7001L), (loaded.PartnerCode, loaded.AssistCode));
        Assert.Equal(Screen.Home, loaded.Screen);
    }

    [Fact]
    public void A_save_without_partner_and_assist_loads_with_empty_slots()
    {
        var s = GameSession.FromSaveJson(Pack(), "{\"Version\":1,\"PackId\":\"demo\"}");
        Assert.Null(s.PartnerCode);
        Assert.Null(s.AssistCode);
    }

    const string Head = "{\"Version\":1,\"PackId\":\"demo\",";
    const string Chars =
        "\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10},\"7002\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}},";

    [Theory]
    [InlineData("\"Partner\":7004")]                                 // 보유하지 않은 캐릭터
    [InlineData("\"Assist\":7003")]
    [InlineData("\"Partner\":7001,\"Assist\":7001")]                  // 같은 캐릭터
    public void Invalid_partner_or_assist_in_a_save_is_rejected(string fragment) =>
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(), Head + Chars + fragment + "}"));
}
