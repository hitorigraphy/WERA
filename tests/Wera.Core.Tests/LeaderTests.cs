using Wera.Core;

namespace Wera.Core.Tests;

// 리더: 리더 태그(2)를 가진 캐릭터만 리더가 될 수 있고, 파티에는 리더가 한 명 이상 있어야 한다.
// 아무도 없는 파티와 리더의 슬롯에는 리더 태그를 가진 캐릭터만 둘 수 있다.
public class LeaderTests
{
    // 용사 7001, 전사 7003은 리더 태그가 있고 마법사 7002는 없다.
    static GameSession Session()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
                "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n7003\t전사\t1\t1\t2\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7001\t2\n7003\t2\n")));
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);
        s.AddCharacter(7002);
        s.AddCharacter(7003);
        return s;
    }

    static IEnumerable<long> Listed(GameSession s) => s.CharacterList.Select(c => c.Code).Order();

    [Fact]
    public void An_empty_party_lists_only_leader_tagged_characters_and_the_first_one_becomes_the_leader()
    {
        var s = Session();
        s.Activate("105"); s.Activate("5");                       // 아무도 없는 파티의 슬롯 5
        Assert.Equal([7001L, 7003L], Listed(s));                  // 플레이어(리더 태그 없음)와 마법사는 걸러진다
        Assert.DoesNotContain(s.Entries, e => e.Code == "7002");

        s.Activate("7001");
        Assert.True(s.PartyInstance.Units[4]!.IsLeader);
        Assert.True(s.PartySlotViews[4].IsLeader);
    }

    [Fact]
    public void Other_slots_of_a_party_with_someone_list_everyone_and_place_non_leaders()
    {
        var s = Session();
        s.Activate("105"); s.Activate("1"); s.Activate("7001");
        s.Activate("2");                                          // 리더가 아닌 슬롯
        Assert.Equal([1L, 7002L, 7003L], Listed(s).Where(c => c != 7001));
        Assert.Equal(CharacterListColor.Green, s.CharacterList.Single(c => c.Code == 7001).Color);
        s.Activate("7002");
        Assert.False(s.PartyInstance.Units[1]!.IsLeader);
        Assert.True(s.PartyInstance.Units[0]!.IsLeader);
    }

    [Fact]
    public void Changing_the_character_of_a_leader_slot_lists_only_leader_tagged_ones_and_keeps_the_leadership()
    {
        var s = Session();
        s.Activate("105"); s.Activate("1"); s.Activate("7001");
        s.Activate("1");                                          // 리더의 슬롯을 바꾼다
        Assert.Equal([7003L], Listed(s));                         // 지금 그 슬롯의 용사는 목록에 없다
        s.Activate("7003");
        Assert.Equal(7003, s.Party[0]);
        Assert.True(s.PartyInstance.Units[0]!.IsLeader);          // 새 유닛이 리더 자리를 잇는다
    }

    [Fact]
    public void The_last_leader_cannot_be_cleared_while_others_remain_but_can_when_alone()
    {
        var s = Session();
        s.Activate("105"); s.Activate("1"); s.Activate("7001");
        s.Activate("2"); s.Activate("7002");
        s.Activate("1");
        Assert.DoesNotContain(s.Entries, e => e.Code == GameSession.ClearEntryCode);   // 리더가 한 명뿐이고 동료가 있다
        s.Activate("999");

        s.Activate("2");                                          // 동료는 비울 수 있다
        Assert.Contains(s.Entries, e => e.Code == GameSession.ClearEntryCode);
        s.Activate("0");
        s.Activate("1");                                          // 이제 혼자라 비울 수 있다
        Assert.Contains(s.Entries, e => e.Code == GameSession.ClearEntryCode);
        s.Activate("0");
        Assert.All(s.Party, p => Assert.Null(p));
    }

    [Fact]
    public void Leader_mode_toggles_leaders_only_for_leader_tagged_characters_and_keeps_at_least_one()
    {
        var s = Session();
        s.Activate("105"); s.Activate("1"); s.Activate("7001");
        s.Activate("2"); s.Activate("7002");
        s.Activate("3"); s.Activate("7003");

        s.Activate("103");
        Assert.Equal(Screen.LeaderSetup, s.Screen);
        Assert.Equal(["1", "3"], s.Entries.Where(e => e.Action == EntryAction.ToggleLeader && e.Enabled).Select(e => e.Code));   // 마법사(슬롯 2)는 회색
        Assert.False(s.Entries.Single(e => e.Code == "2").Enabled);
        Assert.False(s.Entries.Single(e => e.Code == "103").Enabled);                                              // 지금 모드

        s.Activate("2");                                          // 리더 태그가 없는 캐릭터: 반응 없음
        Assert.False(s.PartyInstance.Units[1]!.IsLeader);

        s.Activate("3");                                          // 전사를 리더로
        Assert.Equal([true, false, true], s.PartyInstance.Units.Take(3).Select(u => u!.IsLeader));
        s.Activate("1");                                          // 용사 해제(전사가 남아 있다)
        Assert.False(s.PartyInstance.Units[0]!.IsLeader);

        s.Activate("3");                                          // 마지막 리더는 해제할 수 없다
        Assert.True(s.PartyInstance.Units[2]!.IsLeader);
        Assert.Equal("파티에는 리더가 한 명 이상 있어야 합니다.", s.Message);
        Assert.Equal(Screen.LeaderSetup, s.Screen);
    }

    [Fact]
    public void A_leader_losing_the_leader_tag_passes_leadership_to_another_tagged_unit()
    {
        var s = Session();
        s.Activate("105"); s.Activate("1"); s.Activate("7001");
        s.Activate("2"); s.Activate("7003");
        Assert.True(s.RemoveCharacterTag(7001, GamePack.LeaderTagCode));
        Assert.False(s.PartyInstance.Units[0]!.IsLeader);
        Assert.True(s.PartyInstance.Units[1]!.IsLeader);           // 리더 태그를 가진 다음 유닛이 리더가 된다
    }

    [Fact]
    public void Removing_the_leader_character_promotes_another_leader_tagged_unit()
    {
        var s = Session();
        s.Activate("105"); s.Activate("1"); s.Activate("7001");
        s.Activate("2"); s.Activate("7003");
        Assert.True(s.RemoveCharacter(7001));
        Assert.True(s.PartyInstance.Units[1]!.IsLeader);
    }

    [Fact]
    public void Leaders_survive_the_save_and_old_saves_get_the_first_leader_tagged_unit_as_leader()
    {
        var s = Session();
        s.Activate("105"); s.Activate("1"); s.Activate("7001");
        s.Activate("2"); s.Activate("7003");
        s.Activate("103"); s.Activate("2");                       // 전사도 리더
        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal([true, true], loaded.PartyInstance.Units.Take(2).Select(u => u!.IsLeader));

        // 리더 정보가 없던 예전 세이브: 마법사(리더 태그 없음)가 앞에 있어도 리더 태그를 가진 첫 유닛이 리더가 된다
        var old = GameSession.FromSaveJson(s.Pack,
            "{\"Version\":1,\"PackId\":\"demo\",\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}," +
            "\"7002\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10},\"7003\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}," +
            "\"Party\":[7002,7003,7001]}");
        Assert.Equal([false, true, false], old.PartyInstance.Units.Take(3).Select(u => u!.IsLeader));
    }

    [Fact]
    public void The_leader_tag_is_a_reserved_tag_the_tag_master_must_define()
    {
        Assert.Equal(2, GamePack.LeaderTagCode);
        var ex = Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n3\t메인 통화\n"))));
        Assert.Contains("리더", ex.Message);
    }
}
