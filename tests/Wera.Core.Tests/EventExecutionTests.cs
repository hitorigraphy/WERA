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

// 103 이벤트 화면: 목록 → 시작 → 장면마다 [1] 다음 → 끝나면 이벤트 화면
public class EventScreenTests
{
    static GameSession Session(string flows, string? eventConditions = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t첫 이벤트\n3002\t조건 이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t인사\n"),
            ("master/battle_master_v1.tsv", "code\tname\n5001\t슬라임\n"),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n4001\t1\tTextPlain\t0\t0\t0\t안녕\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + flows),
        };
        if (eventConditions is not null)
            files.Add(("master/event_condition_master_v1.tsv", "event_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n" + eventConditions));
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files.ToArray())));
    }

    [Fact]
    public void The_event_screen_lists_only_events_whose_conditions_are_met()
    {
        var s = Session("3001\t1\tAdv\t4001\t0\t0\n", "3002\t1\t1001\tGreaterOrEqual\t1\n");
        s.Activate("103");
        Assert.Equal(["3001", "999"], s.Entries.Select(e => e.Code));
        s.PlayerFlags[1001] = 1;
        Assert.Equal(["3001", "3002", "999"], s.Entries.Select(e => e.Code));
    }

    [Fact]
    public void Playing_an_event_shows_each_scene_and_returns_to_the_list_when_finished()
    {
        var s = Session("3001\t1\tAdv\t4001\t0\t0\n3001\t2\tBattle\t5001\t0\t0\n");
        s.Activate("103");
        s.Activate("3001");

        Assert.Equal(Screen.EventPlay, s.Screen);
        Assert.Equal("ADV: 인사", s.CurrentStepText);
        Assert.Equal(["1"], s.Entries.Select(e => e.Code));

        s.Activate("1");
        Assert.Equal(Screen.EventPlay, s.Screen);
        Assert.Equal("전투: 슬라임", s.CurrentStepText);

        s.Activate("1");
        Assert.Equal(Screen.Event, s.Screen);
        Assert.Null(s.CurrentEvent);
        Assert.Null(s.CurrentStep);
        Assert.Null(s.Message);
    }

    [Fact]
    public void An_event_with_nothing_to_show_goes_straight_back_to_the_list()
    {
        var s = Session("3001\t1\tFinish\t0\t0\t0\n");
        s.Activate("103");
        s.Activate("3001");
        Assert.Equal(Screen.Event, s.Screen);
    }
}

// adv_flow_master / adv_flow_condition_master
public class AdvFlowMasterTests
{
    const string Header = "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n";

    static GamePack Load(string flows, string? conditions = null, bool withAdvMaster = true)
    {
        var files = new List<(string, string)> { ("pack.tsv", "id\tdemo\ntitle\t데모\n") };
        if (withAdvMaster) files.Add(("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"));
        files.Add(("master/adv_flow_master_v1.tsv", Header + flows));
        if (conditions is not null)
            files.Add(("master/adv_flow_condition_master_v1.tsv", "adv_code\tflow_number\tgroup_number\tflag_code\tcondition_type\tcondition_value\n" + conditions));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    static string Error(string flows, string? conditions = null) =>
        Assert.Throws<PackFormatException>(() => Load(flows, conditions)).Message;

    [Fact]
    public void Flows_load_with_their_types_params_and_conditions_and_come_back_in_flow_number_order()
    {
        var pack = Load(
            "4001\t3\tFinish\t0\t0\t0\n4001\t1\ttextme\t0\t0\t0\t내 말\n4001\t2\tChoice\t7\t3\t0\n4002\t1\tNone\t\t\t\n",
            "4001\t2\t1\t1001\tGreaterOrEqual\t1\n");
        Assert.Equal(4, pack.AdvFlows!.Count);
        Assert.Equal([1L, 2L, 3L], pack.AdvFlowsOf(4001).Select(f => f.FlowNumber));
        var choice = pack.FindAdvFlow(4001, 2)!;
        Assert.Equal(AdvFlowType.Choice, choice.FlowType);
        Assert.Equal((7L, 3L), (choice.Param1, choice.Param2));
        Assert.Single(choice.Groups);
        Assert.False(choice.IsAvailable(new Dictionary<long, long>()));
        Assert.True(choice.IsAvailable(new Dictionary<long, long> { [1001] = 1 }));
        Assert.True(pack.FindAdvFlow(4001, 1)!.IsAvailable(new Dictionary<long, long>()));          // 조건이 없으면 참
        Assert.Equal(AdvFlowType.None, pack.FindAdvFlow(4002, 1)!.FlowType);
        Assert.Null(pack.FindAdvFlow(4002, 9));
        Assert.Equal(4, pack.Masters.AdvFlowRowTable.Count);                                         // 마스터메모리 테이블에도 들어간다
    }

    [Fact]
    public void The_enum_numbers_are_fixed()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5, 6],
            new[] { AdvFlowType.None, AdvFlowType.Finish, AdvFlowType.Jump, AdvFlowType.TextMe, AdvFlowType.Choice, AdvFlowType.TextYou, AdvFlowType.TextPlain }
                .Select(t => (int)t));
    }

    [Fact]
    public void The_text_column_is_loaded_and_the_three_text_types_need_it()
    {
        var pack = Load("4001\t1\tTextMe\t0\t0\t0\t안녕\n4001\t2\tTextYou\t0\t0\t0\t반가워\n4001\t3\tTextPlain\t0\t0\t0\t밤이 되었다\n4001\t4\tChoice\t1\t5\t0\t고른다\n4001\t5\tFinish\t0\t0\t0\t\n");
        Assert.Equal(["안녕", "반가워", "밤이 되었다", "고른다", ""], pack.AdvFlowsOf(4001).Select(f => f.Text));
        Assert.Equal("반가워", pack.Masters.AdvFlowRowTable.FindByAdvCodeAndFlowNumber((4001, 2)).Text);

        foreach (var type in new[] { "TextMe", "TextYou", "TextPlain" })
        {
            Assert.Contains("text가 있어야", Error($"4001\t1\t{type}\t0\t0\t0\t\n"));
            Assert.Contains("text가 있어야", Error($"4001\t1\t{type}\t0\t0\t0\t  \n"));
            Assert.Contains("text가 있어야", Error($"4001\t1\t{type}\t0\t0\t0\n"));          // 칸이 아예 없어도 같다
        }
        Assert.Contains("흐름 종류", Error("4001\t1\tText\t0\t0\t0\t안녕\n"));                  // 옛 이름 Text는 없다
    }

    [Fact]
    public void A_pack_without_the_flow_master_has_none_and_the_condition_master_alone_is_rejected()
    {
        var files = new[] { ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n") };
        Assert.Null(GamePack.Load(CoreTests.PackZip(files)).AdvFlows);

        var withCondition = files.Append(("master/adv_flow_condition_master_v1.tsv",
            "adv_code\tflow_number\tgroup_number\tflag_code\tcondition_type\tcondition_value\n4001\t1\t1\t1\tEqual\t1\n")).ToArray();
        Assert.Contains("adv_flow_master_v1.tsv도 필요", Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(withCondition))).Message);
    }

    [Fact]
    public void The_flow_master_needs_the_adv_master_and_a_known_adv()
    {
        Assert.Contains("adv_master_v1.tsv도 필요", Assert.Throws<PackFormatException>(() => Load("4001\t1\tTextPlain\t0\t0\t0\t문장\n", withAdvMaster: false)).Message);
        Assert.Contains("ADV '9'가 ADV 마스터에 없습니다", Error("9\t1\tTextPlain\t0\t0\t0\t문장\n"));
    }

    [Fact]
    public void Duplicate_keys_unknown_types_and_bad_numbers_are_rejected()
    {
        Assert.Contains("두 번", Error("4001\t1\tTextPlain\t0\t0\t0\t문장\n4001\t1\tFinish\t0\t0\t0\n"));
        Assert.Contains("흐름 종류", Error("4001\t1\tAdv\t0\t0\t0\n"));
        Assert.Contains("흐름 종류", Error("4001\t1\t3\t0\t0\t0\n"));                                   // 숫자로는 못 쓴다
        Assert.Contains("흐름 번호", Error("4001\tx\tTextPlain\t0\t0\t0\t문장\n"));
    }

    [Fact]
    public void Jump_must_go_forward_to_a_flow_of_the_same_adv()
    {
        Assert.Contains("보다 커야", Error("4001\t1\tJump\t1\t0\t0\n"));
        Assert.Contains("보다 커야", Error("4001\t1\tTextPlain\t0\t0\t0\t문장\n4001\t2\tJump\t1\t0\t0\n"));
        Assert.Contains("같은 ADV의 흐름에 없습니다", Error("4001\t1\tJump\t5\t0\t0\n4002\t5\tTextPlain\t0\t0\t0\t문장\n"));
        Assert.NotNull(Load("4001\t1\tJump\t5\t0\t0\n4001\t5\tFinish\t0\t0\t0\n"));
    }

    [Fact]
    public void Choice_param_2_must_go_forward_to_a_flow_of_the_same_adv()
    {
        Assert.Contains("보다 커야", Error("4001\t1\tChoice\t1\t1\t0\n"));
        Assert.Contains("보다 커야", Error("4001\t1\tTextPlain\t0\t0\t0\t문장\n4001\t2\tChoice\t1\t1\t0\n"));
        Assert.Contains("같은 ADV의 흐름에 없습니다", Error("4001\t1\tChoice\t1\t5\t0\n4002\t5\tTextPlain\t0\t0\t0\t문장\n"));
        Assert.NotNull(Load("4001\t1\tChoice\t1\t5\t0\n4001\t5\tFinish\t0\t0\t0\n"));
    }

    [Fact]
    public void Choice_rows_of_the_same_group_must_be_consecutive()
    {
        // 같은 그룹이 이어지면 된다(그룹 1: 1~2번, 그룹 2: 3~4번)
        Assert.NotNull(Load(
            "4001\t1\tChoice\t1\t5\t0\n4001\t2\tChoice\t1\t5\t0\n4001\t3\tChoice\t2\t5\t0\n4001\t4\tChoice\t2\t5\t0\n4001\t5\tFinish\t0\t0\t0\n"));

        // 다른 종류의 칸이 끼어들면 안 된다
        Assert.Contains("연속하지 않습니다", Error(
            "4001\t1\tChoice\t1\t5\t0\n4001\t2\tTextPlain\t0\t0\t0\t문장\n4001\t3\tChoice\t1\t5\t0\n4001\t5\tFinish\t0\t0\t0\n"));
        // 다른 그룹의 선택지가 끼어들어도 안 된다
        Assert.Contains("연속하지 않습니다", Error(
            "4001\t1\tChoice\t1\t5\t0\n4001\t2\tChoice\t2\t5\t0\n4001\t3\tChoice\t1\t5\t0\n4001\t5\tFinish\t0\t0\t0\n"));
        // 파일에 적힌 순서가 아니라 흐름 번호 순으로 본다
        Assert.NotNull(Load(
            "4001\t5\tFinish\t0\t0\t0\n4001\t2\tChoice\t1\t5\t0\n4001\t1\tChoice\t1\t5\t0\n"));
        Assert.Contains("연속하지 않습니다", Error(
            "4001\t3\tChoice\t1\t5\t0\n4001\t1\tChoice\t1\t5\t0\n4001\t2\tTextPlain\t0\t0\t0\t문장\n4001\t5\tFinish\t0\t0\t0\n"));
    }

    [Fact]
    public void The_same_group_id_in_different_advs_is_fine()
    {
        Assert.NotNull(Load(
            "4001\t1\tChoice\t1\t2\t0\n4001\t2\tFinish\t0\t0\t0\n4002\t1\tChoice\t1\t2\t0\n4002\t2\tFinish\t0\t0\t0\n"));
    }

    [Fact]
    public void Flow_conditions_need_a_known_flow_and_unique_keys()
    {
        const string flows = "4001\t1\tTextPlain\t0\t0\t0\t문장\n";
        Assert.Contains("알 수 없는 흐름", Error(flows, "4001\t2\t1\t1\tEqual\t1\n"));
        Assert.Contains("두 번", Error(flows, "4001\t1\t1\t1\tEqual\t1\n4001\t1\t1\t1\tEqual\t2\n"));
        Assert.Contains("컨디션 타입", Error(flows, "4001\t1\t1\t1\tNope\t1\n"));
        Assert.Contains("그룹넘버", Error(flows, "4001\t1\tx\t1\tEqual\t1\n"));
    }

    [Fact]
    public void The_default_pack_has_an_adv_flow_with_a_choice()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.zip"));
        var pack = GamePack.Load(zip);
        Assert.Equal(7, pack.AdvFlowsOf(4001).Count);
        Assert.Equal(2, pack.AdvFlowsOf(4001).Count(f => f.FlowType == AdvFlowType.Choice));
        Assert.False(pack.FindAdvFlow(4001, 4)!.IsAvailable(new Dictionary<long, long>()));          // 튜토리얼 플래그가 있어야 두 번째 선택지
    }
}
