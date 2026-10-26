using Wera.Core;

namespace Wera.Core.Tests;

// ADV 실행: 대사는 하나씩, 선택지는 묶음으로, 조건·점프·종료는 이벤트 실행과 같은 규칙
public class AdvExecutionTests
{
    const string FlowHeader = "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n";
    const string CondHeader = "adv_code\tflow_number\tgroup_number\tflag_code\tcondition_type\tcondition_value\n";

    static GameSession Session(string flows, string? conditions = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t인사\n4002\t다음\n"),
            ("master/event_flow_master_v1.tsv",
                "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n3001\t1\tAdv\t4001\t0\t0\n3001\t2\tAdv\t4002\t0\t0\n"),
            ("master/adv_flow_master_v1.tsv", FlowHeader + flows),
        };
        if (conditions is not null) files.Add(("master/adv_flow_condition_master_v1.tsv", CondHeader + conditions));
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files.ToArray())));
    }

    static AdvExecution Run(GameSession s, long adv = 4001) => new(s.Pack, adv, s.PlayerFlags);

    [Fact]
    public void Text_lines_come_back_one_at_a_time_with_their_speaker_kind_and_the_adv_ends_after_the_last()
    {
        var s = Session("4001\t1\tTextMe\t1\t0\t0\t안녕\n4001\t2\tTextYou\t0\t0\t0\t반가워\n4001\t3\tTextPlain\t0\t0\t0\t밤이 되었다\n");
        var run = Run(s);

        Assert.Equal(new AdvStep(AdvStepKind.TextMe, 1, "안녕", [], "당신", 1), run.Advance());
        Assert.Equal(AdvStepKind.TextYou, run.Advance().Kind);
        Assert.Equal("밤이 되었다", run.Advance().Text);
        Assert.Equal(AdvStepKind.Finished, run.Advance().Kind);
        Assert.True(run.IsFinished);
        Assert.Equal(AdvStepKind.Finished, run.Advance().Kind);
    }

    [Fact]
    public void Finish_ends_the_adv_and_flows_with_false_conditions_are_skipped()
    {
        var s = Session(
            "4001\t1\tTextMe\t0\t0\t0\ta\n4001\t2\tTextMe\t0\t0\t0\tb\n4001\t3\tFinish\t0\t0\t0\t\n4001\t4\tTextMe\t0\t0\t0\tc\n",
            "4001\t2\t1\t1001\tGreaterOrEqual\t1\n");
        var run = Run(s);
        Assert.Equal("a", run.Advance().Text);
        Assert.Equal(AdvStepKind.Finished, run.Advance().Kind);           // 2번은 건너뛰고 3번 Finish에서 끝난다

        s.PlayerFlags[1001] = 1;
        run = Run(s);
        Assert.Equal(["a", "b"], new[] { run.Advance().Text, run.Advance().Text });
    }

    [Fact]
    public void Jump_skips_ahead()
    {
        var s = Session("4001\t1\tJumpFlagGreaterOrEqual\t0\t-9223372036854775808\t3\t\n4001\t2\tTextMe\t0\t0\t0\tskipped\n4001\t3\tTextMe\t0\t0\t0\tlanded\n");
        Assert.Equal("landed", Run(s).Advance().Text);
    }

    [Fact]
    public void A_choice_group_comes_back_as_one_set_and_stays_until_one_is_chosen()
    {
        var s = Session(
            "4001\t1\tChoice\t1\t4\t0\t왼쪽\n4001\t2\tChoice\t1\t5\t0\t오른쪽\n4001\t3\tFinish\t0\t0\t0\t\n" +
            "4001\t4\tTextMe\t0\t0\t0\t왼쪽으로 갔다\n4001\t5\tTextMe\t0\t0\t0\t오른쪽으로 갔다\n");
        var run = Run(s);

        var step = run.Advance();
        Assert.Equal(AdvStepKind.Choice, step.Kind);
        Assert.Equal(["왼쪽", "오른쪽"], step.Choices.Select(c => c.Text));
        Assert.Equal(AdvStepKind.Choice, run.Advance().Kind);             // 고르기 전에는 같은 묶음이 계속 나온다

        Assert.False(run.Choose(2));
        Assert.False(run.Choose(-1));
        Assert.True(run.Choose(1));
        Assert.Equal("오른쪽으로 갔다", run.Advance().Text);
        Assert.False(run.Choose(0));                                      // 기다리는 선택지가 없으면 고를 수 없다
    }

    [Fact]
    public void Choices_with_false_conditions_are_left_out_and_a_group_with_none_left_is_skipped()
    {
        var s = Session(
            "4001\t1\tChoice\t1\t3\t0\t늘 있다\n4001\t2\tChoice\t1\t3\t0\t조건부\n4001\t3\tTextMe\t0\t0\t0\t끝\n",
            "4001\t2\t1\t1001\tGreaterOrEqual\t1\n");
        Assert.Equal(["늘 있다"], Run(s).Advance().Choices.Select(c => c.Text));
        s.PlayerFlags[1001] = 1;
        Assert.Equal(["늘 있다", "조건부"], Run(s).Advance().Choices.Select(c => c.Text));

        var none = Session(
            "4001\t1\tChoice\t1\t3\t0\ta\n4001\t2\tChoice\t1\t3\t0\tb\n4001\t3\tTextMe\t0\t0\t0\t끝\n",
            "4001\t1\t1\t1001\tEqual\t1\n4001\t2\t1\t1001\tEqual\t1\n");
        Assert.Equal("끝", Run(none).Advance().Text);                     // 고를 수 있는 선택지가 없으면 묶음째 건너뛴다
    }

    [Fact]
    public void Two_consecutive_groups_come_back_as_two_separate_sets()
    {
        var s = Session(
            "4001\t1\tChoice\t1\t3\t0\ta\n4001\t2\tChoice\t2\t3\t0\tb\n4001\t3\tTextMe\t0\t0\t0\t끝\n");
        var run = Run(s);
        Assert.Equal(["a"], run.Advance().Choices.Select(c => c.Text));
        run.Choose(0);
        Assert.Equal("끝", run.Advance().Text);                           // 3번으로 점프했으므로 둘째 묶음은 나오지 않는다
    }

    [Fact]
    public void An_adv_without_flows_finishes_at_once()
    {
        var s = Session("4001\t1\tTextMe\t0\t0\t0\ta\n");
        Assert.Equal(AdvStepKind.Finished, Run(s, 4002).Advance().Kind);
    }
}

// 이벤트 진행 화면에서의 ADV
public class AdvScreenTests
{
    static GameSession Session(string advFlows)
    {
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t인사\n4002\t빈 ADV\n"),
            ("master/event_flow_master_v1.tsv",
                "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n3001\t1\tAdv\t4002\t0\t0\n3001\t2\tAdv\t4001\t0\t0\n3001\t3\tAdv\t4001\t0\t0\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + advFlows),
        };
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
    }

    [Fact]
    public void The_adv_shows_each_line_then_choices_and_hands_back_to_the_event_when_it_ends()
    {
        var s = Session(
            "4001\t1\tTextMe\t1\t0\t0\t안녕\n4001\t2\tChoice\t1\t4\t0\t네\n4001\t3\tChoice\t1\t5\t0\t아니오\n" +
            "4001\t4\tTextYou\t0\t0\t0\t좋아요\n4001\t5\tTextPlain\t0\t0\t0\t침묵\n");
        s.Activate("103");
        s.Activate("3001");                                               // 첫 ADV(4002)는 보여 줄 것이 없어 건너뛰고 둘째 ADV로 간다

        Assert.Equal(Screen.EventPlay, s.Screen);
        Assert.Equal("ADV: 인사", s.CurrentStepText);
        Assert.Equal(AdvStepKind.TextMe, s.CurrentAdvStep!.Value.Kind);
        Assert.Equal("안녕", s.CurrentAdvStep!.Value.Text);
        Assert.Equal("당신", s.CurrentAdvStep!.Value.SpeakerName);
        Assert.Equal(["1"], s.Entries.Select(e => e.Code));

        s.Activate("1");                                                  // 선택지: [1] 네, [2] 아니오
        Assert.Equal(AdvStepKind.Choice, s.CurrentAdvStep!.Value.Kind);
        Assert.Equal(["네", "아니오"], s.Entries.Select(e => e.Label));
        Assert.Equal(["1", "2"], s.Entries.Select(e => e.Code));

        s.Activate("2");
        Assert.Equal("침묵", s.CurrentAdvStep!.Value.Text);

        s.Activate("1");                                                  // 이 ADV가 끝나면 이벤트의 다음 칸(같은 ADV 4001을 다시)
        Assert.Equal("ADV: 인사", s.CurrentStepText);
        Assert.Equal("안녕", s.CurrentAdvStep!.Value.Text);

        s.Activate("1");
        s.Activate("1");                                                  // 선택지 [1] 네 → 좋아요 → 끝
        s.Activate("1");
        s.Activate("1");
        Assert.Equal(Screen.Event, s.Screen);                             // 이벤트가 끝나 목록으로 돌아온다
        Assert.Null(s.CurrentAdv);
        Assert.Null(s.CurrentAdvStep);
    }
}

public class AdvSpeakerNameTests
{
    static AdvStep First(string flows)
    {
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t인사\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + flows),
        };
        var s = GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
        return new AdvExecution(s.Pack, 4001, s.PlayerFlags).Advance();
    }

    [Fact]
    public void TextMe_and_TextYou_both_speak_as_param_1_with_its_variation()
    {
        var me = First("4001\t1\tTextMe\t1\t2\t0\t안녕\t\n");
        Assert.Equal(("당신", 1L, 2L), (me.SpeakerName, me.CharacterCode, me.Variation));

        var you = First("4001\t1\tTextYou\t7001\t3\t0\t안녕\t\n");
        Assert.Equal(("용사", 7001L, 3L), (you.SpeakerName, you.CharacterCode, you.Variation));
    }

    [Fact]
    public void TextYou_with_zero_or_an_unknown_character_is_question_marks_and_no_image()
    {
        foreach (var type in new[] { "TextMe", "TextYou" })
        {
            foreach (var code in new[] { "0", "9999", "-5" })
            {
                var step = First($"4001\t1\t{type}\t{code}\t3\t0\t안녕\t\n");
                Assert.Equal(("???", 0L, 0L), (step.SpeakerName, step.CharacterCode, step.Variation));
            }
            Assert.Equal("???", First($"4001\t1\t{type}\t\t\t\t안녕\t\n").SpeakerName);
        }          // 비워 둔 인자도 같다
    }

    [Fact]
    public void A_force_name_replaces_the_name_but_empty_means_the_character_name()
    {
        Assert.Equal("복면인", First("4001\t1\tTextYou\t7001\t0\t0\t안녕\t복면인\n").SpeakerName);
        Assert.Equal("복면인", First("4001\t1\tTextYou\t0\t0\t0\t안녕\t복면인\n").SpeakerName);      // 캐릭터가 없어도 강제 이름이 이긴다
        Assert.Equal("가면", First("4001\t1\tTextMe\t1\t0\t0\t안녕\t가면\n").SpeakerName);
        Assert.Equal("가면", First("4001\t1\tTextMe\t0\t0\t0\t안녕\t가면\n").SpeakerName);
        Assert.Equal("용사", First("4001\t1\tTextYou\t7001\t0\t0\t안녕\t\n").SpeakerName);
        Assert.Equal("용사", First("4001\t1\tTextYou\t7001\t0\t0\t안녕\n").SpeakerName);          // 칸이 아예 없어도 같다
    }

    [Fact]
    public void The_force_name_column_is_required_in_the_header_and_loaded_into_the_master()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t인사\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n4001\t1\tTextMe\t0\t0\t0\t안녕\t가면\n")));
        Assert.Equal("가면", pack.FindAdvFlow(4001, 1)!.ForceName);
        Assert.Equal("가면", pack.Masters.AdvFlowRowTable.FindByAdvCodeAndFlowNumber((4001, 1)).ForceName);

        Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t인사\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n4001\t1\tTextMe\t0\t0\t0\t안녕\n"))));
    }
}

public class AdvanceOnAnyKeyTests
{
    [Fact]
    public void Only_non_choice_scenes_of_the_event_play_screen_advance_on_any_key()
    {
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t인사\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n3001\t1\tAdv\t4001\t0\t0\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" +
                "4001\t1\tTextPlain\t0\t0\t0\t안녕\t\n4001\t2\tChoice\t1\t3\t0\t네\t\n4001\t3\tTextPlain\t0\t0\t0\t끝\t\n"),
        };
        var s = GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
        Assert.False(s.AdvanceOnAnyKey);                                  // 홈
        s.Activate("103");
        Assert.False(s.AdvanceOnAnyKey);                                  // 이벤트 목록

        s.Activate("3001");
        Assert.True(s.AdvanceOnAnyKey);                                   // 대사
        s.Activate("1");
        Assert.False(s.AdvanceOnAnyKey);                                  // 선택지
        s.Activate("1");
        Assert.True(s.AdvanceOnAnyKey);
        s.Activate("1");                                                  // 이벤트가 끝나 목록으로
        Assert.False(s.AdvanceOnAnyKey);
    }
}

public class AdvLogTests
{
    static GameSession Session()
    {
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t첫째\n4002\t둘째\n"),
            ("master/event_flow_master_v1.tsv",
                "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n3001\t1\tAdv\t4001\t0\t0\n3001\t2\tAdv\t4002\t0\t0\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" +
                "4001\t1\tTextPlain\t0\t0\t0\t하나\t\n4001\t2\tTextMe\t1\t0\t0\t둘\t\n4001\t3\tChoice\t1\t4\t0\t선택\t\n4001\t4\tTextYou\t1\t0\t0\t셋\t\n" +
                "4002\t1\tTextPlain\t0\t0\t0\t다른 ADV\t\n"),
        };
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
    }

    [Fact]
    public void Lines_of_one_adv_pile_up_and_the_choice_list_is_not_part_of_the_log()
    {
        var s = Session();
        s.Activate("103");
        s.Activate("3001");
        Assert.Equal(["하나"], s.AdvLog.Select(l => l.Text));
        s.Activate("1");
        Assert.Equal(["하나", "둘"], s.AdvLog.Select(l => l.Text));
        s.Activate("1");                                                  // 선택지 장면: 대사들은 그대로 남는다
        Assert.Equal(AdvStepKind.Choice, s.CurrentAdvStep!.Value.Kind);
        Assert.Equal(["하나", "둘"], s.AdvLog.Select(l => l.Text));
        s.Activate("1");                                                  // 선택지를 고르면 고른 선택지가 로그에 남는다
        Assert.Equal(["하나", "둘", "선택", "셋"], s.AdvLog.Select(l => l.Text));
        Assert.Equal(
            [AdvStepKind.TextPlain, AdvStepKind.TextMe, AdvStepKind.Chosen, AdvStepKind.TextYou], s.AdvLog.Select(l => l.Kind));
    }

    [Fact]
    public void A_new_adv_or_the_end_of_the_event_clears_the_log()
    {
        var s = Session();
        s.Activate("103");
        s.Activate("3001");
        s.Activate("1");
        s.Activate("1");
        s.Activate("1");
        s.Activate("1");                                                  // 첫째 ADV가 끝나고 둘째 ADV의 첫 대사
        Assert.Equal(["다른 ADV"], s.AdvLog.Select(l => l.Text));
        s.Activate("1");                                                  // 이벤트 끝
        Assert.Empty(s.AdvLog);
        Assert.Equal(Screen.Event, s.Screen);
    }
}

public class AdvChoiceMarkupTests
{
    static GameSession Session(string choiceText)
    {
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t첫째\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n3001\t1\tAdv\t4001\t0\t0\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" +
                $"4001\t1\tChoice\t1\t2\t0\t{choiceText}\t\n4001\t2\tTextPlain\t0\t0\t0\t끝\t\n"),
        };
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
    }

    [Fact]
    public void Choice_text_may_use_markup_and_the_entry_carries_the_html_and_the_plain_label()
    {
        var s = Session("[b]강하게[/b] [color=red]나선다[/color]");
        s.Activate("103");
        s.Activate("3001");
        var entry = s.Entries.Single();
        Assert.Equal("강하게 나선다", entry.Label);
        Assert.Equal("<b>강하게</b> <span style=\"color:#ff5555\">나선다</span>", entry.LabelHtml);

        s.Activate("1");                                                  // 고른 선택지도 같은 꾸밈으로 로그에 남는다
        var chosen = s.AdvLog[0];
        Assert.Equal(AdvStepKind.Chosen, chosen.Kind);
        Assert.Equal(entry.LabelHtml, chosen.Rich.ToHtml());
    }

    [Fact]
    public void Choice_text_with_bad_markup_or_a_line_break_fails_the_load()
    {
        Assert.Contains("선택지 text가 올바르지 않습니다", Assert.Throws<PackFormatException>(() => Session("[i]기울임[/i]")).Message);
        Assert.Contains("줄바꿈", Assert.Throws<PackFormatException>(() => Session(@"한 줄\n두 줄")).Message);
        Assert.NotNull(Session(@"대괄호 \[그대로\]"));
    }
}

public class AdvFlagFlowTests
{
    static GameSession Session(string flows, string? conditions = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t첫째\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n3001\t1\tAdv\t4001\t0\t0\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + flows),
        };
        if (conditions is not null)
            files.Add(("master/adv_flow_condition_master_v1.tsv", "adv_code\tflow_number\tgroup_number\tflag_code\tcondition_type\tcondition_value\n" + conditions));
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files.ToArray())));
    }

    [Fact]
    public void SetFlag_and_ModFlag_change_the_player_flags_without_stopping_or_showing_anything()
    {
        var s = Session("4001\t1\tSetFlag\t1001\t5\t0\t\t\n4001\t2\tModFlag\t1001\t-2\t0\t\t\n4001\t3\tModFlag\t2002\t7\t0\t\t\n4001\t4\tTextPlain\t0\t0\t0\t끝\t\n");
        s.PlayerFlags[1001] = 100;
        var step = new AdvExecution(s.Pack, 4001, s.PlayerFlags).Advance();

        Assert.Equal("끝", step.Text);                                    // 플래그 칸에서는 멈추지 않고 첫 대사까지 간다
        Assert.Equal(3, s.PlayerFlags[1001]);                             // 100 → Set 5 → Mod -2
        Assert.Equal(7, s.PlayerFlags[2002]);                             // 없던 플래그는 0에서 시작한다
    }

    [Fact]
    public void A_flag_flow_with_a_false_condition_is_skipped_and_later_flows_see_the_new_value()
    {
        var s = Session(
            "4001\t1\tSetFlag\t1001\t1\t0\t\t\n4001\t2\tSetFlag\t3003\t9\t0\t\t\n4001\t3\tTextPlain\t0\t0\t0\t조건부\t\n4001\t4\tTextPlain\t0\t0\t0\t끝\t\n",
            "4001\t2\t1\t9999\tEqual\t1\n4001\t3\t1\t1001\tGreaterOrEqual\t1\n");
        var run = new AdvExecution(s.Pack, 4001, s.PlayerFlags);

        Assert.Equal("조건부", run.Advance().Text);                       // 1번이 바꾼 플래그를 3번의 조건이 바로 본다
        Assert.False(s.PlayerFlags.ContainsKey(3003));                    // 조건이 거짓인 2번은 실행되지 않았다
    }

    [Fact]
    public void ModFlag_stops_at_the_ends_of_the_long_range_instead_of_wrapping()
    {
        var s = Session($"4001\t1\tModFlag\t1\t{long.MaxValue}\t0\t\t\n4001\t2\tModFlag\t1\t5\t0\t\t\n4001\t3\tModFlag\t2\t{long.MinValue}\t0\t\t\n4001\t4\tModFlag\t2\t-5\t0\t\t\n");
        Assert.Equal(AdvStepKind.Finished, new AdvExecution(s.Pack, 4001, s.PlayerFlags).Advance().Kind);
        Assert.Equal(long.MaxValue, s.PlayerFlags[1]);
        Assert.Equal(long.MinValue, s.PlayerFlags[2]);
    }

    [Fact]
    public void Flag_flows_need_no_text_and_take_any_integers_but_not_blank_junk()
    {
        Assert.NotNull(Session("4001\t1\tSetFlag\t0\t0\t0\t\t\n4001\t2\tModFlag\t-5\t-5\t0\t\t\n"));
        Assert.Throws<PackFormatException>(() => Session("4001\t1\tSetFlag\tabc\t0\t0\t\t\n"));
    }

    [Fact]
    public void Playing_an_adv_applies_the_flag_flows_to_the_session_and_they_are_saved()
    {
        var s = Session("4001\t1\tSetFlag\t1001\t1\t0\t\t\n4001\t2\tTextPlain\t0\t0\t0\t안녕\t\n");
        s.Activate("103");
        s.Activate("3001");
        Assert.Equal(1, s.PlayerFlags[1001]);
        Assert.Equal(["안녕"], s.AdvLog.Select(l => l.Text));              // 플래그 칸은 로그에 남지 않는다
        Assert.Equal(1, GameSession.FromSaveJson(s.Pack, s.ToSaveJson()).PlayerFlags[1001]);
    }
}

// JumpFlag*: 플래그를 견주어 참이면 target_flow_number(param_3)로 점프, 거짓이면 다음 칸으로
public class JumpFlagTests
{
    static readonly string[] Kinds = ["Equal", "NotEqual", "GreaterThan", "GreaterOrEqual", "LessThan", "LessOrEqual"];

    // 플래그 1001 = 5와 값 5를 견준 결과(5 vs 5), 값 3과 견준 결과(5 vs 3), 값 9와 견준 결과(5 vs 9)
    public static IEnumerable<object[]> Cases =>
    [
        ["Equal", true, false, false],
        ["NotEqual", false, true, true],
        ["GreaterThan", false, true, false],
        ["GreaterOrEqual", true, true, false],
        ["LessThan", false, false, true],
        ["LessOrEqual", true, false, true],
    ];

    static GameSession Session(bool viaAdv, string kind, long value)
    {
        var eventRows = viaAdv
            ? "3001\t1\tAdv\t4001\t0\t0\n"
            : $"3001\t1\tJumpFlag{kind}\t1001\t{value}\t3\n3001\t2\tAdv\t4001\t0\t0\n3001\t3\tAdv\t4002\t0\t0\n";
        var advRows = viaAdv
            ? $"4001\t1\tJumpFlag{kind}\t1001\t{value}\t3\t\t\n4001\t2\tTextPlain\t0\t0\t0\t건너뛴 칸\t\n4001\t3\tTextPlain\t0\t0\t0\t도착한 칸\t\n"
            : "";
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + eventRows),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + advRows),
        };
        var s = GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
        s.PlayerFlags[1001] = 5;
        return s;
    }

    /// <summary>점프했으면 true(도착한 칸이 나온다), 점프하지 않았으면 false.</summary>
    static bool Jumped(bool viaAdv, string kind, long value)
    {
        var s = Session(viaAdv, kind, value);
        if (viaAdv) return new AdvExecution(s.Pack, 4001, s.PlayerFlags).Advance().Text == "도착한 칸";
        var step = s.StartEvent(3001)!.Advance();
        return step.Code == 4002;                                                    // 점프하면 3번 칸(ADV 4002), 아니면 2번 칸(ADV 4001)
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Each_comparison_jumps_only_when_it_is_true_in_events_and_advs(string kind, bool equal, bool greater, bool less)
    {
        foreach (var viaAdv in new[] { false, true })
        {
            Assert.Equal(equal, Jumped(viaAdv, kind, 5));
            Assert.Equal(greater, Jumped(viaAdv, kind, 3));
            Assert.Equal(less, Jumped(viaAdv, kind, 9));
        }
    }

    [Fact]
    public void A_missing_flag_counts_as_zero_and_always_true_comparisons_make_an_unconditional_jump()
    {
        var s = Session(true, "Equal", 0);
        s.PlayerFlags.Remove(1001);
        Assert.Equal("도착한 칸", new AdvExecution(s.Pack, 4001, s.PlayerFlags).Advance().Text);

        var unconditional = Session(true, "GreaterOrEqual", long.MinValue);
        Assert.Equal("도착한 칸", new AdvExecution(unconditional.Pack, 4001, unconditional.PlayerFlags).Advance().Text);
    }

    [Fact]
    public void The_jump_flows_are_the_only_jumps_and_the_old_Jump_type_is_gone()
    {
        Assert.All(Kinds, k => Assert.NotNull(FlagJump.Of("JumpFlag" + k)));
        Assert.Null(FlagJump.Of("Jump"));
        Assert.Null(FlagJump.Of("TextMe"));
        Assert.Null(FlagJump.Of("JumpFlagNope"));
        Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n4001\t1\tJump\t2\t0\t0\t\t\n4001\t2\tFinish\t0\t0\t0\t\t\n"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_target_must_be_a_larger_flow_number_of_the_same_event_or_adv(bool viaAdv)
    {
        string Load(string row, string extra = "")
        {
            try
            {
                var files = viaAdv
                    ? new (string, string)[]
                    {
                        ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"),
                        ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + row + extra),
                    }
                    : new (string, string)[]
                    {
                        ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/event_master_v1.tsv", "code\tname\n3001\tA\n3002\tB\n"),
                        ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + row + extra),
                    };
                GamePack.Load(CoreTests.PackZip(files));
                return "";
            }
            catch (PackFormatException ex) { return ex.Message; }
        }
        string Row(long owner, long flow, long target) => viaAdv
            ? $"{4000 + owner}\t{flow}\tJumpFlagEqual\t1\t1\t{target}\t\t\n"
            : $"{3000 + owner}\t{flow}\tJumpFlagEqual\t1\t1\t{target}\n";
        string Fin(long owner, long flow) => viaAdv ? $"{4000 + owner}\t{flow}\tFinish\t0\t0\t0\t\t\n" : $"{3000 + owner}\t{flow}\tFinish\t0\t0\t0\n";

        Assert.Equal("", Load(Row(1, 1, 5), Fin(1, 5)));
        Assert.Contains("보다 커야", Load(Row(1, 1, 1)));                              // 자기 자신
        Assert.Contains("보다 커야", Load(Fin(1, 1) + Row(1, 2, 1)));                  // 앞쪽으로
        Assert.Contains("없습니다", Load(Row(1, 1, 9)));                               // 없는 흐름
        Assert.Contains("없습니다", Load(Row(1, 1, 5), Fin(2, 5)));                    // 다른 이벤트/ADV의 흐름
        Assert.Equal("", Load(Row(1, 1, 3) + Row(1, 3, 5), Fin(1, 5)));               // 이어서 점프해도 늘 앞으로라 끝난다
    }
}
