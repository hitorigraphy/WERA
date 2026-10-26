using Wera.Core;

namespace Wera.Core.Tests;

// ADV 실행: 대사는 하나씩, 선택지는 묶음으로, 조건·점프·종료는 이벤트 실행과 같은 규칙
public class AdvExecutionTests
{
    const string FlowHeader = "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n";
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
        var s = Session("4001\t1\tTextMe\t0\t0\t0\t안녕\n4001\t2\tTextYou\t0\t0\t0\t반가워\n4001\t3\tTextNormal\t0\t0\t0\t밤이 되었다\n");
        var run = Run(s);

        Assert.Equal(new AdvStep(AdvStepKind.TextMe, 1, "안녕", []), run.Advance());
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
        var s = Session("4001\t1\tJump\t3\t0\t0\t\n4001\t2\tTextMe\t0\t0\t0\tskipped\n4001\t3\tTextMe\t0\t0\t0\tlanded\n");
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
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n" + advFlows),
        };
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
    }

    [Fact]
    public void The_adv_shows_each_line_then_choices_and_hands_back_to_the_event_when_it_ends()
    {
        var s = Session(
            "4001\t1\tTextMe\t0\t0\t0\t안녕\n4001\t2\tChoice\t1\t4\t0\t네\n4001\t3\tChoice\t1\t5\t0\t아니오\n" +
            "4001\t4\tTextYou\t0\t0\t0\t좋아요\n4001\t5\tTextNormal\t0\t0\t0\t침묵\n");
        s.Activate("103");
        s.Activate("3001");                                               // 첫 ADV(4002)는 보여 줄 것이 없어 건너뛰고 둘째 ADV로 간다

        Assert.Equal(Screen.EventPlay, s.Screen);
        Assert.Equal("ADV: 인사", s.CurrentStepText);
        Assert.Equal(AdvStepKind.TextMe, s.CurrentAdvStep!.Value.Kind);
        Assert.Equal("안녕", s.CurrentAdvStep!.Value.Text);
        Assert.Equal("당신", s.PlayerSpeakerName);
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
