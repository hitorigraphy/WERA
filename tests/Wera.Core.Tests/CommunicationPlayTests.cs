using Wera.Core;

namespace Wera.Core.Tests;

// 교류 화면: 고른 그룹의 교류들이 교류 코드로 나열되고, 하나를 고르면 흐름 재생 화면으로 바뀐다.
public class CommunicationPlayTests
{
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
        "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n";
    const string Groups = "code\tname\n1\t대화\n2\t선물\n";
    const string Comms =
        "code\tgroup_code\tname\tis_assist\n" +
        "12\t1\t칭찬하기\tfalse\n11\t1\t인사하기\tfalse\n21\t2\t건네기\ttrue\n13\t1\t빈 교류\tfalse\n";
    const string DefaultHeader = "communication_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n";

    static GameSession Session(string defaults, bool withSupport = true)
    {
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/character_inventory_master_v1.tsv", Characters),
            (GamePack.CommunicationGroupMasterPath, Groups), (GamePack.CommunicationMasterPath, Comms),
            (GamePack.CommunicationDefaultFlowMasterPath, DefaultHeader + defaults),
        };
        var s = GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
        if (withSupport)
        {
            s.AddCharacter(7001);
            s.AddCharacter(7002);
            s.SetSupport(SupportRole.Partner, 7001);
            s.SetSupport(SupportRole.Assist, 7002);
        }
        s.Activate("104");
        return s;
    }

    const string Greeting =
        "11\t1\tText\t\t\t\t안녕\n" +
        "11\t2\tTextLog\t\t\t\t로그\n" +
        "11\t3\tTextPlain\t\t\t\t상자\n" +
        "12\t1\tText\t\t\t\t멋져\n" +
        "21\t1\tText\t\t\t\t이거 받아\n";

    [Fact]
    public void The_selected_groups_communications_are_listed_by_code_after_the_groups_and_before_back()
    {
        var s = Session(Greeting);
        Assert.Equal(["100", "200", "11", "12", "13", "999"], s.Entries.Select(e => e.Code));
        Assert.Equal(["대화", "선물", "인사하기", "칭찬하기", "빈 교류", "돌아가기"], s.Entries.Select(e => e.Label));
        Assert.Equal(EntryAction.StartCommunication, s.Entries[2].Action);

        s.Activate("200");                                                          // 다른 그룹을 고르면 그 그룹의 교류가 나온다
        Assert.Equal(["100", "200", "21", "999"], s.Entries.Select(e => e.Code));
        s.Activate("100");
        Assert.Equal(["100", "200", "11", "12", "13", "999"], s.Entries.Select(e => e.Code));
    }

    [Fact]
    public void Choosing_a_communication_plays_its_flow_in_place_of_the_list()
    {
        var s = Session(Greeting);
        Assert.NotNull(s.Activate("11"));
        Assert.Equal(Screen.CommunicationPlay, s.Screen);
        Assert.Equal(11, s.CurrentCommunicationCode);
        Assert.True(s.AdvanceOnAnyKey);                                              // 아무 키나 누르면 다음으로
        Assert.Equal(["1"], s.Entries.Select(e => e.Code));                          // 코드 1은 다음

        // Text는 파트너(교류가 is_assist가 아니므로)의 대사로 나온다
        var first = s.AdvLog.Single();
        Assert.Equal((AdvStepKind.TextYou, "안녕", "용사", 7001L), (first.Kind, first.Text, first.SpeakerName, first.CharacterCode));

        s.Activate("1");
        s.Activate("1");
        Assert.Equal([AdvStepKind.TextYou, AdvStepKind.TextLog, AdvStepKind.TextPlain], s.AdvLog.Select(l => l.Kind));

        s.Activate("1");                                                             // 끝: 교류 화면으로 돌아온다(고른 그룹 그대로)
        Assert.Equal(Screen.Interaction, s.Screen);
        Assert.Null(s.CurrentCommunicationCode);
        Assert.Empty(s.AdvLog);
        Assert.Equal(1, s.CurrentCommunicationGroupCode);
        Assert.Equal(["100", "200", "11", "12", "13", "999"], s.Entries.Select(e => e.Code));
    }

    [Fact]
    public void An_assist_communication_is_spoken_by_the_assist()
    {
        var s = Session(Greeting);
        s.Activate("200");
        s.Activate("21");
        var line = s.AdvLog.Single();
        Assert.Equal(("마법사", 7002L), (line.SpeakerName, line.CharacterCode));
    }

    [Fact]
    public void An_empty_slot_makes_the_speaker_unknown()
    {
        var s = Session(Greeting, withSupport: false);
        s.Activate("11");
        Assert.Equal(("???", 0L), (s.AdvLog.Single().SpeakerName, s.AdvLog.Single().CharacterCode));
    }

    [Fact]
    public void A_communication_without_a_flow_stays_on_the_list_with_a_message()
    {
        var s = Session(Greeting);
        Assert.NotNull(s.Activate("13"));
        Assert.Equal(Screen.Interaction, s.Screen);
        Assert.Equal("교류 내용이 없습니다.", s.Message);
    }

    [Fact]
    public void Choices_work_in_a_communication_flow()
    {
        var s = Session(
            "11\t1\tChoice\t1\t3\t\t[b]좋아[/b]\n" +
            "11\t2\tChoice\t1\t4\t\t싫어\n" +
            "11\t3\tText\t\t\t\t고마워\n" +
            "11\t4\tFinish\t\t\t\t\n");
        s.Activate("11");
        Assert.False(s.AdvanceOnAnyKey);
        Assert.Equal(["1", "2"], s.Entries.Select(e => e.Code));
        Assert.Equal(["좋아", "싫어"], s.Entries.Select(e => e.Label));
        Assert.All(s.Entries, e => Assert.Equal(Screen.CommunicationPlay, e.Target));

        s.Activate("1");
        Assert.Equal([AdvStepKind.Chosen, AdvStepKind.TextYou], s.AdvLog.Select(l => l.Kind));
        Assert.Equal(Screen.CommunicationPlay, s.Screen);
        s.Activate("1");
        Assert.Equal(Screen.Interaction, s.Screen);
    }

    [Fact]
    public void Flag_and_inventory_steps_run_without_stopping_and_jumps_follow_the_flow()
    {
        var s = Session(
            "11\t1\tSetFlag\t5\t1\t\t\n" +
            "11\t2\tJumpFlagEqual\t5\t1\t4\t\n" +
            "11\t3\tText\t\t\t\t건너뜀\n" +
            "11\t4\tAddInventory\t2\t3\t\t\n" +
            "11\t5\tText\t\t\t\t도착\n");
        var coin = s.CountInventory(2);
        s.Activate("11");
        Assert.Equal(["도착"], s.AdvLog.Select(l => l.Text));
        Assert.Equal(1, s.PlayerFlags[5]);
        Assert.Equal(coin + 3, s.CountInventory(2));
    }

    [Fact]
    public void The_slots_stay_inert_during_playback()
    {
        var s = Session(Greeting);
        s.Activate("11");
        Assert.Equal(Screen.CommunicationPlay, s.Screen);
        Assert.Equal(7001, s.PartnerCode);
        s.Activate("1");
        Assert.Equal(Screen.CommunicationPlay, s.Screen);
        Assert.Equal(7001, s.PartnerCode);
        Assert.Null(s.CurrentSupportRole);
    }

    [Fact]
    public void Entering_the_screen_again_after_playing_selects_the_smallest_group()
    {
        var s = Session(Greeting);
        s.Activate("200");
        s.Activate("21");
        s.Activate("1");
        Assert.Equal(Screen.Interaction, s.Screen);
        Assert.Equal(2, s.CurrentCommunicationGroupCode);                           // 끝난 뒤에는 고른 그룹 그대로
        s.Activate("999");
        s.Activate("104");
        Assert.Equal(1, s.CurrentCommunicationGroupCode);
    }
}
