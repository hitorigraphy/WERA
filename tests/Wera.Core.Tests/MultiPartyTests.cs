using Wera.Core;

namespace Wera.Core.Tests;

// 파티 열여섯 개(파티-A ~ 파티-P): 버튼 [11]~[26], 파티끼리는 캐릭터도 장비도 공유할 수 없다.
public class MultiPartyTests
{
    static GameSession Session(params (long item, long count)[] owned)
    {
        var s = GameSession.NewGame(EquipmentTests.Pack());
        s.Characters[7001] = CharacterInstance.Stats(5, 36, 40, 3, 30);
        s.Characters[7002] = CharacterInstance.Stats(3, 10, 25, 12, 30);
        foreach (var (item, count) in owned)
            for (var i = 0; i < count; i++) s.AddUniqueItem(item);
        return s;
    }

    [Fact]
    public void There_are_sixteen_parties_named_A_to_P_and_A_is_selected_first()
    {
        var s = Session();
        Assert.Equal(16, s.Parties.Length);
        Assert.Equal("파티-A", s.Parties[0].Name);
        Assert.Equal("파티-P", s.Parties[15].Name);
        Assert.Equal(0, s.CurrentPartyIndex);

        s.Activate("105");
        var buttons = s.Entries.Where(e => e.Action == EntryAction.SelectParty).ToList();
        Assert.Equal(Enumerable.Range(11, 16).Select(c => c.ToString()), buttons.Select(e => e.Code));
        Assert.Equal("파티-A", buttons[0].Label);
        Assert.Equal("파티-P", buttons[15].Label);
        Assert.Single(buttons, e => !e.Enabled);
        Assert.Equal("11", buttons.Single(e => !e.Enabled).Code);
    }

    [Fact]
    public void Choosing_a_party_switches_the_one_the_slots_and_placement_follow()
    {
        var s = Session();
        s.Activate("105");
        s.Activate("1"); s.Activate("7001");                     // 파티-A의 슬롯 1에 용사
        Assert.Equal(7001, s.Party[0]);

        s.Activate("11");                                        // 지금 보는 파티의 버튼은 눌러도 반응이 없다
        Assert.Equal(0, s.CurrentPartyIndex);
        Assert.NotNull(s.Activate("12"));                        // 파티-B
        Assert.Equal(Screen.PartySetup, s.Screen);
        Assert.Equal(1, s.CurrentPartyIndex);
        Assert.Null(s.Party[0]);                                 // 파티-B는 비어 있다
        Assert.Equal("12", s.Entries.Single(e => e.Action == EntryAction.SelectParty && !e.Enabled).Code);

        s.Activate("1"); s.Activate("7002");
        Assert.Equal(7002, s.Party[0]);
        s.Activate("11");
        Assert.Equal(7001, s.Party[0]);                          // 파티-A는 그대로
    }

    [Fact]
    public void A_character_in_another_party_is_gray_and_cannot_be_placed()
    {
        var s = Session();
        s.Activate("105");
        s.Activate("1"); s.Activate("7001");
        s.Activate("12");                                        // 파티-B
        s.Activate("1");
        var dragon = s.CharacterList.Single(c => c.Code == 7001);
        Assert.Equal(CharacterListColor.Gray, dragon.Color);
        Assert.False(dragon.Selectable);
        Assert.DoesNotContain(s.Entries, e => e.Code == "7001");
        Assert.Null(s.Activate("7001"));
        Assert.Equal(CharacterListColor.White, s.CharacterList.Single(c => c.Code == 7002).Color);
    }

    [Fact]
    public void Parties_cannot_share_equipment_and_each_party_equips_its_own_units()
    {
        var s = Session((9001, 1));
        s.Party[0] = 7001;
        s.PartyInstance.Units[0] = new PartyUnit(7001);          // 파티-A의 용사
        s.Activate("105"); s.Activate("12");
        s.Party[0] = 7002;                                       // 파티-B의 마법사
        Assert.True(s.Equip(7001, 1, 9001));
        Assert.False(s.Equip(7002, 1, 9001));                    // 철검 한 개는 두 파티에 동시에 낄 수 없다
        Assert.Equal(0, s.FreeCopies(9001));
        Assert.Equal(9001, s.EquippedUniqueCode(7001, 1));       // 다른 파티를 보고 있어도 용사의 장비다
    }

    [Fact]
    public void Removing_a_character_clears_it_from_every_party()
    {
        var s = Session();
        s.AddCharacter(7003);
        s.Activate("105"); s.Activate("13");
        s.Party[2] = 7003;
        Assert.True(s.RemoveCharacter(7003));
        Assert.Null(s.Party[2]);
    }

    [Fact]
    public void All_parties_and_the_selection_survive_the_save()
    {
        var s = Session((9001, 1));
        s.Party[0] = 7001;
        s.Equip(7001, 1, 9001);
        s.Activate("105"); s.Activate("14");                     // 파티-D
        s.Party[4] = 7002;
        s.PartyName = "파견대";

        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal(3, loaded.CurrentPartyIndex);
        Assert.Equal("파견대", loaded.Parties[3].Name);
        Assert.Equal(7001, loaded.Parties[0].Units[0]!.CharacterCode);
        Assert.Equal(9001, loaded.EquippedUniqueCode(7001, 1));
        Assert.Equal(7002, loaded.Parties[3].Units[4]!.CharacterCode);
        Assert.Equal("파티-P", loaded.Parties[15].Name);
    }

    [Fact]
    public void A_save_with_the_same_character_in_two_parties_is_rejected()
    {
        var s = Session();
        s.Party[0] = 7001;
        var json = s.ToSaveJson();
        var tampered = json.Replace("\"CurrentParty\"", "\"X\"");                   // 형식은 그대로 두고
        Assert.NotNull(GameSession.FromSaveJson(s.Pack, tampered));

        s.PartyInstance.Units[0] = new PartyUnit(7001);
        s.Parties[1].Units[0] = new PartyUnit(7001);                                 // 같은 캐릭터가 두 파티에
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(s.Pack, s.ToSaveJson()));
    }

    [Fact]
    public void An_old_single_party_save_becomes_party_A()
    {
        var s = Session();
        var json = "{\"Version\":1,\"PackId\":\"demo\",\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}},"
            + "\"PartyInstance\":{\"Name\":\"옛 파티\",\"Units\":[{\"CharacterCode\":7001,\"Equipment\":{}}]}}";
        var loaded = GameSession.FromSaveJson(s.Pack, json);
        Assert.Equal("옛 파티", loaded.Parties[0].Name);
        Assert.Equal(7001, loaded.Party[0]);
        Assert.Equal(0, loaded.CurrentPartyIndex);
        Assert.Equal("파티-B", loaded.Parties[1].Name);
    }

    // ---- 파티 이름 변경: 제목의 [0] 파티명 ----

    [Fact]
    public void Zero_opens_the_rename_screen_and_the_typed_text_becomes_the_party_name()
    {
        var s = Session();
        s.Activate("105");
        var title = s.Entries.Single(e => e.Code == "0");
        Assert.Equal(("파티-A", EntryAction.RenameParty), (title.Label, title.Action));

        s.Activate("0");
        Assert.Equal(Screen.PartyRename, s.Screen);
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));
        s.Activate("정예 부대");
        Assert.Equal(Screen.PartySetup, s.Screen);                 // 들어온 화면으로 돌아간다
        Assert.Equal("정예 부대", s.PartyName);
        Assert.Equal("정예 부대", s.Entries.Single(e => e.Code == "0").Label);
        Assert.Equal("정예 부대", s.Entries.Single(e => e.Action == EntryAction.SelectParty && !e.Enabled).Label);
        Assert.Equal("파티-B", s.Parties[1].Name);                 // 다른 파티는 그대로
    }

    [Fact]
    public void The_rename_returns_to_where_it_started_and_back_keeps_the_name()
    {
        var s = Session();
        s.Party[0] = 7001;
        s.Activate("105"); s.Activate("102");                      // 장비 착용 화면
        s.Activate("0");
        Assert.Equal(Screen.PartyRename, s.Screen);
        s.Activate("999");                                         // 돌아가기: 이름은 그대로
        Assert.Equal(Screen.EquipmentSetup, s.Screen);
        Assert.Equal("파티-A", s.PartyName);

        s.Activate("0");
        s.Activate("007");                                         // 숫자도 이름이 된다(앞자리 0을 없애지 않는다)
        Assert.Equal(Screen.EquipmentSetup, s.Screen);
        Assert.Equal("007", s.PartyName);
    }

    [Fact]
    public void An_empty_or_too_long_name_changes_nothing()
    {
        var s = Session();
        s.Activate("105"); s.Activate("0");
        Assert.Null(s.Activate("   "));
        Assert.Equal(Screen.PartyRename, s.Screen);
        Assert.Null(s.Activate(new string('가', GameSession.MaxPartyNameLength + 1)));
        Assert.Equal(Screen.PartyRename, s.Screen);
        Assert.Contains("20자", s.Message);
        Assert.Equal("파티-A", s.PartyName);

        s.Activate(new string('가', GameSession.MaxPartyNameLength));
        Assert.Equal(new string('가', GameSession.MaxPartyNameLength), s.PartyName);
    }

    [Fact]
    public void Renaming_follows_the_party_being_viewed()
    {
        var s = Session();
        s.Activate("105"); s.Activate("13");                       // 파티-C
        s.Activate("0"); s.Activate("순찰대");
        Assert.Equal("순찰대", s.Parties[2].Name);
        Assert.Equal("파티-A", s.Parties[0].Name);
        Assert.Equal("순찰대", GameSession.FromSaveJson(s.Pack, s.ToSaveJson()).Parties[2].Name);
    }
}
