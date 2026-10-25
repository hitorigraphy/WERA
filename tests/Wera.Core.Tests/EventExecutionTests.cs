using Wera.Core;

namespace Wera.Core.Tests;

// 이벤트는 (1) 더 진행할 flow_number가 없거나 (2) Finish 칸을 만나면 끝난다.
// 흐름 조건이 거짓이면 그 칸은 건너뛰고, 조건이 없으면 참으로 본다.
public class EventExecutionTests
{
    const string FlowHeader = "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
    const string CondHeader = "event_code\tflow_number\tgroup_number\tflag_code\tcondition_type\tcondition_value\n";

    static GameSession Session(string flows, string? flowConditions = null, string? eventConditions = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n3002\t빈 이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n4003\tC\n"),
            ("master/battle_master_v1.tsv", "code\tname\n5001\t전투\n5002\t전투 2\n"),
            ("master/event_flow_master_v1.tsv", FlowHeader + flows),
        };
        if (flowConditions is not null) files.Add(("master/event_flow_condition_master_v1.tsv", CondHeader + flowConditions));
        if (eventConditions is not null)
            files.Add(("master/event_condition_master_v1.tsv", "event_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n" + eventConditions));
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files.ToArray())));
    }

    /// <summary>끝날 때까지 돌려서 (종류, 코드) 목록을 모은다.</summary>
    static List<(EventStepKind Kind, long Code)> RunAll(EventExecution run)
    {
        var steps = new List<(EventStepKind, long)>();
        for (var i = 0; i < 100; i++)
        {
            var step = run.Advance();
            if (step.Kind == EventStepKind.Finished) return steps;
            steps.Add((step.Kind, step.Code));
        }
        throw new Exception("끝나지 않는다");
    }

    // ---- 끝나는 두 경우 ----

    [Fact]
    public void An_event_ends_when_there_is_no_further_flow_number()
    {
        var s = Session("3001\t1\tAdv\t4001\t0\t0\n3001\t2\tBattle\t5001\t0\t0\n");
        var run = s.StartEvent(3001)!;

        Assert.Equal(new EventStep(EventStepKind.Adv, 1, 4001), run.Advance());
        Assert.Equal(new EventStep(EventStepKind.Battle, 2, 5001), run.Advance());
        Assert.False(run.IsFinished);                                      // 마지막 일을 돌려준 직후에는 아직 끝나지 않았다
        Assert.Equal(EventStepKind.Finished, run.Advance().Kind);          // 더 진행할 칸이 없으니 끝난다
        Assert.True(run.IsFinished);
    }

    [Fact]
    public void An_event_ends_at_a_finish_flow_and_ignores_the_flows_after_it()
    {
        var s = Session("3001\t1\tAdv\t4001\t0\t0\n3001\t2\tFinish\t0\t0\t0\n3001\t3\tAdv\t4002\t0\t0\n3001\t4\tBattle\t5001\t0\t0\n");
        var run = s.StartEvent(3001)!;

        Assert.Equal([(EventStepKind.Adv, 4001L)], RunAll(run));
        Assert.True(run.IsFinished);
    }

    [Fact]
    public void Once_finished_it_stays_finished()
    {
        var s = Session("3001\t1\tFinish\t0\t0\t0\n3001\t2\tAdv\t4001\t0\t0\n");
        var run = s.StartEvent(3001)!;
        Assert.Equal(EventStepKind.Finished, run.Advance().Kind);
        Assert.Equal(EventStepKind.Finished, run.Advance().Kind);
        Assert.Equal(EventStepKind.Finished, run.Advance().Kind);
    }

    [Fact]
    public void An_event_without_flows_finishes_at_once()
    {
        var s = Session("3001\t1\tAdv\t4001\t0\t0\n");
        var run = s.StartEvent(3002)!;                                    // 흐름이 없는 이벤트
        Assert.Equal(EventStepKind.Finished, run.Advance().Kind);
    }

    // ---- 순서 ----

    [Fact]
    public void Flows_run_in_flow_number_order_not_file_order_and_none_does_nothing()
    {
        var s = Session("3001\t30\tAdv\t4003\t0\t0\n3001\t10\tAdv\t4001\t0\t0\n3001\t15\tNone\t0\t0\t0\n3001\t20\tBattle\t5001\t0\t0\n");
        Assert.Equal(
            [(EventStepKind.Adv, 4001L), (EventStepKind.Battle, 5001L), (EventStepKind.Adv, 4003L)],
            RunAll(s.StartEvent(3001)!));
    }

    // ---- 조건 ----

    [Fact]
    public void A_flow_without_conditions_runs_and_a_flow_with_false_conditions_is_skipped()
    {
        var s = Session(
            "3001\t1\tAdv\t4001\t0\t0\n3001\t2\tAdv\t4002\t0\t0\n3001\t3\tAdv\t4003\t0\t0\n",
            "3001\t2\t1\t1001\tGreaterOrEqual\t1\n");
        Assert.Equal([(EventStepKind.Adv, 4001L), (EventStepKind.Adv, 4003L)], RunAll(s.StartEvent(3001)!));      // 2번은 건너뛴다

        s.PlayerFlags[1001] = 1;
        Assert.Equal([(EventStepKind.Adv, 4001L), (EventStepKind.Adv, 4002L), (EventStepKind.Adv, 4003L)], RunAll(s.StartEvent(3001)!));
    }

    [Fact]
    public void Flow_condition_groups_are_and_within_a_group_and_or_between_groups()
    {
        var s = Session(
            "3001\t1\tAdv\t4001\t0\t0\n",
            "3001\t1\t1\t1001\tEqual\t1\n3001\t1\t1\t1002\tEqual\t1\n3001\t1\t2\t1003\tEqual\t1\n");
        Assert.Empty(RunAll(s.StartEvent(3001)!));

        s.PlayerFlags[1001] = 1;                                           // 그룹 1이 반만 만족
        Assert.Empty(RunAll(s.StartEvent(3001)!));
        s.PlayerFlags[1002] = 1;                                           // 그룹 1 만족
        Assert.Single(RunAll(s.StartEvent(3001)!));

        s.PlayerFlags.Clear();
        s.PlayerFlags[1003] = 1;                                           // 그룹 2만 만족
        Assert.Single(RunAll(s.StartEvent(3001)!));
    }

    [Fact]
    public void A_none_condition_counts_as_true()
    {
        var s = Session("3001\t1\tAdv\t4001\t0\t0\n", "3001\t1\t1\t\tNone\t\n");
        Assert.Single(RunAll(s.StartEvent(3001)!));
    }

    [Fact]
    public void A_finish_flow_with_a_false_condition_is_skipped_and_the_event_goes_on()
    {
        var s = Session(
            "3001\t1\tAdv\t4001\t0\t0\n3001\t2\tFinish\t0\t0\t0\n3001\t3\tAdv\t4002\t0\t0\n",
            "3001\t2\t1\t1001\tGreaterOrEqual\t1\n");
        Assert.Equal(2, RunAll(s.StartEvent(3001)!).Count);               // 종료 칸의 조건이 거짓이라 건너뛰고 3번까지 간다

        s.PlayerFlags[1001] = 1;
        Assert.Equal([(EventStepKind.Adv, 4001L)], RunAll(s.StartEvent(3001)!));       // 조건이 참이면 거기서 끝난다
    }

    [Fact]
    public void Flags_changed_between_steps_are_seen_by_the_flows_that_follow()
    {
        var s = Session(
            "3001\t1\tAdv\t4001\t0\t0\n3001\t2\tBattle\t5001\t0\t0\n",
            "3001\t2\t1\t1001\tGreaterOrEqual\t1\n");
        var run = s.StartEvent(3001)!;

        Assert.Equal(EventStepKind.Adv, run.Advance().Kind);
        s.PlayerFlags[1001] = 1;                                           // ADV를 보는 동안 플래그가 바뀌었다
        Assert.Equal(new EventStep(EventStepKind.Battle, 2, 5001), run.Advance());
    }

    // ---- 점프 ----

    [Fact]
    public void Jump_continues_from_the_target_flow_and_skips_the_flows_between()
    {
        var s = Session("3001\t1\tAdv\t4001\t0\t0\n3001\t2\tJump\t4\t0\t0\n3001\t3\tAdv\t4002\t0\t0\n3001\t4\tAdv\t4003\t0\t0\n");
        Assert.Equal([(EventStepKind.Adv, 4001L), (EventStepKind.Adv, 4003L)], RunAll(s.StartEvent(3001)!));
    }

    [Fact]
    public void A_jump_target_with_a_false_condition_is_skipped_and_the_next_larger_flow_runs()
    {
        var s = Session(
            "3001\t1\tJump\t3\t0\t0\n3001\t2\tAdv\t4001\t0\t0\n3001\t3\tAdv\t4002\t0\t0\n3001\t4\tAdv\t4003\t0\t0\n",
            "3001\t3\t1\t1001\tGreaterOrEqual\t1\n");
        Assert.Equal([(EventStepKind.Adv, 4003L)], RunAll(s.StartEvent(3001)!));
    }

    [Fact]
    public void A_jump_with_a_false_condition_does_not_jump()
    {
        var s = Session(
            "3001\t1\tJump\t3\t0\t0\n3001\t2\tAdv\t4001\t0\t0\n3001\t3\tAdv\t4002\t0\t0\n",
            "3001\t1\t1\t1001\tGreaterOrEqual\t1\n");
        Assert.Equal([(EventStepKind.Adv, 4001L), (EventStepKind.Adv, 4002L)], RunAll(s.StartEvent(3001)!));      // 점프가 건너뛰어져 2번도 실행된다
    }

    [Fact]
    public void A_jump_can_skip_over_a_finish_and_numbers_may_have_gaps()
    {
        var s = Session("3001\t5\tJump\t50\t0\t0\n3001\t10\tFinish\t0\t0\t0\n3001\t50\tAdv\t4001\t0\t0\n3001\t60\tFinish\t0\t0\t0\n3001\t70\tAdv\t4002\t0\t0\n");
        Assert.Equal([(EventStepKind.Adv, 4001L)], RunAll(s.StartEvent(3001)!));
    }

    [Fact]
    public void Chained_jumps_always_end_because_they_only_go_forward()
    {
        var s = Session("3001\t1\tJump\t2\t0\t0\n3001\t2\tJump\t3\t0\t0\n3001\t3\tJump\t4\t0\t0\n3001\t4\tAdv\t4001\t0\t0\n");
        Assert.Equal([(EventStepKind.Adv, 4001L)], RunAll(s.StartEvent(3001)!));
    }

    // ---- 시작 ----

    [Fact]
    public void Starting_an_unknown_event_or_one_whose_conditions_are_not_met_gives_nothing()
    {
        var s = Session("3001\t1\tAdv\t4001\t0\t0\n", eventConditions: "3001\t1\t1001\tGreaterOrEqual\t1\n");
        Assert.Null(s.StartEvent(9999));
        Assert.Null(s.StartEvent(3001));                                   // 이벤트 조건이 거짓
        s.PlayerFlags[1001] = 1;
        Assert.NotNull(s.StartEvent(3001));
    }

    [Fact]
    public void The_step_remembers_which_flow_asked_for_it()
    {
        var s = Session("3001\t7\tBattle\t5002\t0\t0\n");
        var step = s.StartEvent(3001)!.Advance();
        Assert.Equal(7, step.FlowNumber);
        Assert.Equal(5002, step.Code);
    }

    // ---- 기본 게임팩의 테스트 이벤트 ----

    [Fact]
    public void The_default_packs_test_event_plays_the_adv_then_the_battle_only_after_the_tutorial_flag()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.zip"));
        var s = GameSession.NewGame(GamePack.Load(zip));

        Assert.Equal([(EventStepKind.Adv, 4001L)], RunAll(s.StartEvent(3001)!));                  // 전투 칸은 플래그 1001이 필요하다
        s.PlayerFlags[1001] = 1;
        Assert.Equal([(EventStepKind.Adv, 4001L), (EventStepKind.Battle, 5001L)], RunAll(s.StartEvent(3001)!));
    }
}
