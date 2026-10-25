using Wera.Core;

namespace Wera.Core.Tests;

// event_master(code, name), event_condition_master(조건표), event_flow_master(키 (event_code, flow_number), flow_type enum과 long param_1~3),
// event_flow_condition_master(키 (event_code, flow_number, group_number, flag_code), condition_type, value),
// adv_master(code, name), battle_master(code, name). 모두 선택이고 아직 게임 진행에는 쓰이지 않는다.
public class EventMasterTests
{
    const string Events = "code\tname\n3002\t두 번째\n3001\t첫 이벤트\n";
    const string Advs = "code\tname\n4001\t첫째\n";
    const string Battles = "code\tname\n5001\t첫 전투\n";
    const string FlowHeader = "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
    const string FlowCondHeader = "event_code\tflow_number\tgroup_number\tflag_code\tcondition_type\tcondition_value\n";
    const string CondHeader = "event_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n";

    static GamePack Load(
        string? events = Events, string? conditions = null, string? flows = null, string? flowConditions = null,
        string? advs = Advs, string? battles = Battles)
    {
        var files = new List<(string, string)> { ("pack.tsv", "id\tdemo\ntitle\t데모\n") };
        if (events is not null) files.Add(("master/event_master_v1.tsv", events));
        if (conditions is not null) files.Add(("master/event_condition_master_v1.tsv", conditions));
        if (flows is not null) files.Add(("master/event_flow_master_v1.tsv", flows));
        if (flowConditions is not null) files.Add(("master/event_flow_condition_master_v1.tsv", flowConditions));
        if (advs is not null) files.Add(("master/adv_master_v1.tsv", advs));
        if (battles is not null) files.Add(("master/battle_master_v1.tsv", battles));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    // ---- 모두 선택 ----

    [Fact]
    public void All_the_masters_are_optional()
    {
        var pack = Load(events: null, advs: null, battles: null);
        Assert.Null(pack.Events);
        Assert.Null(pack.EventFlows);
        Assert.Null(pack.Advs);
        Assert.Null(pack.Battles);
        Assert.False(GamePack.RequiredMasterPaths.Any(p => p.Contains("event") || p.Contains("adv") || p.Contains("battle")));
    }

    // ---- event_master ----

    [Fact]
    public void Events_load_code_and_name_in_file_order()
    {
        var pack = Load();
        Assert.Equal([3002L, 3001L], pack.Events!.Select(e => e.Code));
        Assert.Equal("첫 이벤트", pack.FindEvent(3001)!.Name);
        Assert.Null(pack.FindEvent(9));
        Assert.Equal(2, pack.Masters.EventRowTable.Count);
    }

    [Theory]
    [InlineData("code\tname\n1\ta\n1\tb\n", "중복")]
    [InlineData("code\tname\nx\ta\n", "정수")]
    [InlineData("id\tname\n1\ta\n", "헤더")]
    public void Bad_event_masters_are_rejected(string events, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(events: events)).Message);

    // ---- event_condition_master ----

    [Fact]
    public void An_event_without_conditions_is_always_available_and_groups_are_and_or()
    {
        var pack = Load(conditions: CondHeader +
            "3001\t1\t1001\tGreaterOrEqual\t1\n" +
            "3001\t1\t1002\tEqual\t3\n" +            // 그룹 1: 둘 다
            "3001\t2\t1003\tEqual\t1\n");            // 그룹 2: 이것만으로도
        var always = pack.FindEvent(3002)!;
        var gated = pack.FindEvent(3001)!;

        Assert.True(always.IsAvailable(new Dictionary<long, long>()));
        Assert.False(gated.IsAvailable(new Dictionary<long, long>()));
        Assert.False(gated.IsAvailable(new Dictionary<long, long> { [1001] = 1 }));
        Assert.True(gated.IsAvailable(new Dictionary<long, long> { [1001] = 1, [1002] = 3 }));
        Assert.True(gated.IsAvailable(new Dictionary<long, long> { [1003] = 1 }));
    }

    [Fact]
    public void Event_conditions_have_the_composite_key_event_group_flag()
    {
        var pack = Load(conditions: CondHeader + "3001\t1\t1001\tEqual\t1\n3001\t2\t1001\tEqual\t2\n");
        var table = pack.Masters.EventConditionRowTable;
        Assert.Equal(2, table.Count);
        Assert.Equal(2, table.FindByEventCode(3001).Count);
        Assert.Equal(2, table.FindByEventCodeAndGroupNumberAndFlagCode((3001, 2, 1001)).Value);
        Assert.Throws<PackFormatException>(() => Load(conditions: CondHeader + "3001\t1\t1001\tEqual\t1\n3001\t1\t1001\tLessThan\t5\n"));
    }

    [Theory]
    [InlineData("9999\t1\t1001\tEqual\t1\n")]
    [InlineData("3001\tx\t1001\tEqual\t1\n")]
    [InlineData("3001\t1\t1001\tBigger\t1\n")]
    public void Bad_event_conditions_are_rejected(string row) =>
        Assert.Throws<PackFormatException>(() => Load(conditions: CondHeader + row));

    [Fact]
    public void Event_conditions_need_the_event_master()
    {
        Assert.Throws<PackFormatException>(() => Load(events: null, conditions: CondHeader + "3001\t1\t1001\tEqual\t1\n"));
    }

    // ---- event_flow_master ----

    [Fact]
    public void The_flow_type_numbers_are_fixed()
    {
        Assert.Equal(0, (int)EventFlowType.None);
        Assert.Equal(1, (int)EventFlowType.Finish);
        Assert.Equal(2, (int)EventFlowType.Jump);
        Assert.Equal(3, (int)EventFlowType.Adv);
        Assert.Equal(4, (int)EventFlowType.Battle);
    }

    [Fact]
    public void Flows_load_six_columns_and_the_type_is_read_by_name()
    {
        var pack = Load(flows: FlowHeader +
            "3001\t1\tAdv\t4001\t7\t8\n" +
            "3001\t2\tbattle\t5001\t0\t5000000000\n" +
            "3001\t3\tJump\t4\t\t\n" +
            "3001\t4\tFinish\t0\t0\t0\n" +
            "3001\t5\tNone\t-1\t2\t3\n");
        Assert.Equal(new EventFlow(3001, 1, EventFlowType.Adv, 4001, 7, 8, []), pack.EventFlows![0] with { Groups = [] });
        Assert.Equal(new EventFlow(3001, 2, EventFlowType.Battle, 5001, 0, 5000000000, []), pack.EventFlows[1] with { Groups = [] });
        Assert.Equal((4, 0, 0), (pack.EventFlows[2].Param1, pack.EventFlows[2].Param2, pack.EventFlows[2].Param3));   // 빈 인자는 0
        Assert.Equal(EventFlowType.Finish, pack.EventFlows[3].FlowType);
        Assert.Equal(-1, pack.EventFlows[4].Param1);                          // None은 인자를 보지 않는다
    }

    [Fact]
    public void Flows_are_keyed_by_event_and_flow_number_and_listed_in_flow_order()
    {
        var pack = Load(flows: FlowHeader +
            "3001\t3\tFinish\t0\t0\t0\n3001\t1\tAdv\t4001\t0\t0\n3002\t1\tFinish\t0\t0\t0\n3001\t2\tJump\t3\t0\t0\n");
        Assert.Equal([1L, 2L, 3L], pack.FlowsOf(3001).Select(f => f.FlowNumber));         // 키 순서
        Assert.Equal(EventFlowType.Finish, Assert.Single(pack.FlowsOf(3002)).FlowType);
        Assert.Empty(pack.FlowsOf(9999));
        Assert.Equal(4, pack.Masters.EventFlowRowTable.Count);
        Assert.Equal(4001, pack.Masters.EventFlowRowTable.FindByEventCodeAndFlowNumber((3001, 1)).Param1);
        Assert.Equal(3, pack.Masters.EventFlowRowTable.FindByEventCode(3001).Count);
        Assert.Equal(EventFlowType.Jump, pack.FindEventFlow(3001, 2)!.FlowType);
        Assert.Null(pack.FindEventFlow(3001, 9));
    }

    [Fact]
    public void The_same_event_and_flow_number_twice_is_rejected_and_the_group_column_is_gone()
    {
        var error = Assert.Throws<PackFormatException>(() => Load(flows: FlowHeader + "3001\t1\tFinish\t0\t0\t0\n3001\t1\tFinish\t1\t0\t0\n"));
        Assert.Contains("두 번", error.Message);
        // group_number 열이 있던 예전 형식은 헤더가 맞지 않는다
        Assert.Throws<PackFormatException>(() => Load(flows:
            "event_code\tflow_number\tgroup_number\tflow_type\tparam_1\tparam_2\tparam_3\n3001\t1\t1\tFinish\t0\t0\t0\n"));
    }

    [Theory]
    [InlineData("9999\t1\tFinish\t0\t0\t0\n", "이벤트 '9999'")]                  // 없는 이벤트
    [InlineData("3001\t1\tWarp\t0\t0\t0\n", "흐름 종류 'Warp'")]                  // 모르는 종류
    [InlineData("3001\t1\t1\t0\t0\t0\n", "흐름 종류 '1'")]                        // 숫자는 안 된다
    [InlineData("3001\tx\tFinish\t0\t0\t0\n", "흐름 번호")]
    [InlineData("3001\t1\tFinish\ta\t0\t0\n", "인자 1")]
    [InlineData("3001\t1\tFinish\t0\t1.5\t0\n", "인자 2")]
    [InlineData("3001\t1\tFinish\t0\t0\tx\n", "인자 3")]
    public void Bad_flow_rows_are_rejected(string row, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(flows: FlowHeader + row)).Message);

    // ---- 종류별 param_1 검증 ----

    [Fact]
    public void Jump_goes_only_to_a_larger_flow_number_of_the_same_event()
    {
        Load(flows: FlowHeader + "3001\t1\tJump\t2\t0\t0\n3001\t2\tFinish\t0\t0\t0\n");                         // 뒤로(큰 번호로) 점프
        Load(flows: FlowHeader + "3001\t1\tJump\t3\t0\t0\n3001\t2\tFinish\t0\t0\t0\n3001\t3\tFinish\t0\t0\t0\n");   // 중간 칸을 건너뛰어도 된다
        Load(flows: FlowHeader + "3001\t5\tJump\t9\t0\t0\n3001\t9\tFinish\t0\t0\t0\n");                         // 번호가 띄엄띄엄이어도

        Assert.Contains("점프할 흐름 번호 '9'가 같은 이벤트의 흐름에 없습니다", Assert.Throws<PackFormatException>(() =>
            Load(flows: FlowHeader + "3001\t1\tJump\t9\t0\t0\n")).Message);                                    // 없는 흐름
        Assert.Throws<PackFormatException>(() =>                                                               // 다른 이벤트의 흐름은 안 된다
            Load(flows: FlowHeader + "3001\t1\tJump\t2\t0\t0\n3002\t2\tFinish\t0\t0\t0\n"));
    }

    [Theory]
    [InlineData("3001\t1\tJump\t1\t0\t0\n3001\t2\tFinish\t0\t0\t0\n")]                  // 자기 자신
    [InlineData("3001\t1\tFinish\t0\t0\t0\n3001\t2\tJump\t1\t0\t0\n")]                  // 앞쪽(작은 번호)으로
    [InlineData("3001\t3\tJump\t2\t0\t0\n3001\t2\tFinish\t0\t0\t0\n")]                  // 파일에 적힌 순서와 상관없이 번호가 기준
    [InlineData("3001\t2\tJump\t0\t0\t0\n3001\t0\tFinish\t0\t0\t0\n")]
    [InlineData("3001\t-1\tJump\t-1\t0\t0\n")]
    public void Jumping_to_the_same_or_a_smaller_flow_number_is_rejected_so_loops_cannot_happen(string rows)
    {
        var error = Assert.Throws<PackFormatException>(() => Load(flows: FlowHeader + rows));
        Assert.Contains("보다 커야 합니다", error.Message);
    }

    [Fact]
    public void Two_jumps_can_never_form_a_loop_because_every_jump_moves_forward()
    {
        // 서로를 가리키는 점프(1→2, 2→1)는 두 번째가 거부되므로 만들 수 없다.
        Assert.Throws<PackFormatException>(() => Load(flows: FlowHeader + "3001\t1\tJump\t2\t0\t0\n3001\t2\tJump\t1\t0\t0\n"));
        // 같은 방향의 점프 사슬은 괜찮다.
        var pack = Load(flows: FlowHeader + "3001\t1\tJump\t2\t0\t0\n3001\t2\tJump\t3\t0\t0\n3001\t3\tFinish\t0\t0\t0\n");
        Assert.Equal(3, pack.EventFlows!.Count);
    }

    [Fact]
    public void Adv_needs_a_code_of_the_adv_master()
    {
        Load(flows: FlowHeader + "3001\t1\tAdv\t4001\t0\t0\n");
        Assert.Contains("재생할 ADV '9'", Assert.Throws<PackFormatException>(() =>
            Load(flows: FlowHeader + "3001\t1\tAdv\t9\t0\t0\n")).Message);
        Assert.Contains("Adv가 있으려면", Assert.Throws<PackFormatException>(() =>
            Load(advs: null, flows: FlowHeader + "3001\t1\tAdv\t4001\t0\t0\n")).Message);
    }

    [Fact]
    public void Battle_needs_a_code_of_the_battle_master()
    {
        Load(flows: FlowHeader + "3001\t1\tBattle\t5001\t0\t0\n");
        Assert.Contains("개시할 전투 '9'", Assert.Throws<PackFormatException>(() =>
            Load(flows: FlowHeader + "3001\t1\tBattle\t9\t0\t0\n")).Message);
        Assert.Contains("Battle이 있으려면", Assert.Throws<PackFormatException>(() =>
            Load(battles: null, flows: FlowHeader + "3001\t1\tBattle\t5001\t0\t0\n")).Message);
    }

    [Fact]
    public void Finish_none_and_the_unused_params_are_not_checked()
    {
        Load(advs: null, battles: null, flows: FlowHeader + "3001\t1\tFinish\t999\t999\t999\n3001\t2\tNone\t-5\t0\t0\n");
        Load(flows: FlowHeader + "3001\t1\tAdv\t4001\t999\t-1\n");                     // param_2, param_3은 무엇이든
    }

    // ---- event_flow_condition_master ----

    [Fact]
    public void A_flow_without_conditions_is_always_available_and_groups_are_and_or()
    {
        var pack = Load(
            flows: FlowHeader + "3001\t1\tFinish\t0\t0\t0\n3001\t2\tFinish\t0\t0\t0\n",
            flowConditions: FlowCondHeader +
                "3001\t2\t1\t1001\tGreaterOrEqual\t1\n" +
                "3001\t2\t1\t1002\tEqual\t3\n" +
                "3001\t2\t2\t1003\tEqual\t1\n");
        var open = pack.FindEventFlow(3001, 1)!;
        var gated = pack.FindEventFlow(3001, 2)!;

        Assert.True(open.IsAvailable(new Dictionary<long, long>()));
        Assert.False(gated.IsAvailable(new Dictionary<long, long> { [1001] = 1 }));
        Assert.True(gated.IsAvailable(new Dictionary<long, long> { [1001] = 1, [1002] = 3 }));
        Assert.True(gated.IsAvailable(new Dictionary<long, long> { [1003] = 1 }));
        Assert.Equal(2, gated.Groups.Count);
    }

    [Fact]
    public void Flow_conditions_have_a_four_column_key_and_a_secondary_key_per_flow()
    {
        var pack = Load(
            flows: FlowHeader + "3001\t1\tFinish\t0\t0\t0\n3001\t2\tFinish\t0\t0\t0\n",
            flowConditions: FlowCondHeader +
                "3001\t1\t1\t1001\tEqual\t1\n3001\t1\t2\t1001\tEqual\t2\n3001\t2\t1\t1001\tLessThan\t5\n");
        var table = pack.Masters.EventFlowConditionRowTable;
        Assert.Equal(3, table.Count);
        Assert.Equal(2, table.FindByEventCodeAndFlowNumber((3001, 1)).Count);
        Assert.Equal(2, table.FindByEventCodeAndFlowNumberAndGroupNumberAndFlagCode((3001, 1, 2, 1001)).Value);
        Assert.Equal(ConditionType.LessThan, table.FindByEventCodeAndFlowNumberAndGroupNumberAndFlagCode((3001, 2, 1, 1001)).Type);
    }

    [Fact]
    public void The_same_flow_condition_key_twice_is_rejected_even_with_a_different_value()
    {
        var error = Assert.Throws<PackFormatException>(() => Load(
            flows: FlowHeader + "3001\t1\tFinish\t0\t0\t0\n",
            flowConditions: FlowCondHeader + "3001\t1\t1\t1001\tEqual\t1\n3001\t1\t1\t1001\tLessThan\t5\n"));
        Assert.Contains("두 번", error.Message);
    }

    [Theory]
    [InlineData("3001\t9\t1\t1001\tEqual\t1\n", "알 수 없는 흐름")]                  // 흐름 마스터에 없는 흐름
    [InlineData("9999\t1\t1\t1001\tEqual\t1\n", "알 수 없는 흐름")]                  // 없는 이벤트
    [InlineData("3001\t1\tx\t1001\tEqual\t1\n", "그룹넘버")]
    [InlineData("3001\t1\t1\t1001\tBigger\t1\n", "컨디션 타입")]
    [InlineData("3001\t1\t1\t\tEqual\t1\n", "플래그 코드")]
    [InlineData("3001\t1\t1\t1001\tEqual\t\n", "컨디션 밸류")]
    public void Bad_flow_conditions_are_rejected(string row, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(
            flows: FlowHeader + "3001\t1\tFinish\t0\t0\t0\n", flowConditions: FlowCondHeader + row)).Message);

    [Fact]
    public void A_none_condition_may_leave_flag_and_value_empty_and_the_master_needs_the_flow_master()
    {
        var pack = Load(flows: FlowHeader + "3001\t1\tFinish\t0\t0\t0\n", flowConditions: FlowCondHeader + "3001\t1\t1\t\tNone\t\n");
        Assert.True(pack.FindEventFlow(3001, 1)!.IsAvailable(new Dictionary<long, long>()));

        Assert.Throws<PackFormatException>(() => Load(flowConditions: FlowCondHeader + "3001\t1\t1\t1001\tEqual\t1\n"));   // 흐름 마스터가 없다
    }

    // ---- adv_master, battle_master ----

    [Fact]
    public void Adv_and_battle_masters_load_code_and_name()
    {
        var pack = Load(advs: "code\tname\n4002\t둘째\n4001\t첫째\n", battles: "code\tname\n5001\t첫 전투\n");
        Assert.Equal([4002L, 4001L], pack.Advs!.Select(a => a.Code));
        Assert.Equal("첫째", pack.FindAdv(4001)!.Name);
        Assert.Equal("첫 전투", pack.FindBattle(5001)!.Name);
        Assert.Null(pack.FindAdv(1));
        Assert.Null(pack.FindBattle(1));
        Assert.Equal(2, pack.Masters.AdvRowTable.Count);
        Assert.Equal(1, pack.Masters.BattleRowTable.Count);
    }

    [Theory]
    [InlineData("code\tname\n1\ta\n1\tb\n")]
    [InlineData("code\tname\nx\ta\n")]
    [InlineData("code\ttitle\n1\ta\n")]
    public void Bad_adv_and_battle_masters_are_rejected(string rows)
    {
        Assert.Throws<PackFormatException>(() => Load(advs: rows));
        Assert.Throws<PackFormatException>(() => Load(battles: rows));
    }

    // ---- memo와 기본 게임팩 ----

    [Fact]
    public void The_memo_column_works_on_the_new_masters()
    {
        var pack = Load(
            events: "code\tname\tmemo\n3001\t이벤트\t메모\n",
            flows: "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\tmemo\n3001\t1\tFinish\t0\t0\t0\t메모\n",
            flowConditions: "event_code\tflow_number\tgroup_number\tflag_code\tcondition_type\tcondition_value\tmemo\n3001\t1\t1\t1001\tEqual\t1\t메모\n",
            advs: "code\tname\tmemo\n4001\tadv\t메모\n", battles: "code\tname\tmemo\n5001\tbattle\t메모\n");
        Assert.Single(pack.Events!);
        Assert.Single(pack.EventFlows!);
        Assert.Single(pack.Masters.EventFlowConditionRowTable.All);
        Assert.Single(pack.Advs!);
        Assert.Single(pack.Battles!);
    }

    [Fact]
    public void The_default_pack_has_a_test_event_with_adv_battle_and_finish_flows()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.zip"));
        var pack = GamePack.Load(zip);

        Assert.Equal("테스트 이벤트", pack.FindEvent(3001)!.Name);
        Assert.Equal(
            [(1L, EventFlowType.Adv, 4001L), (2L, EventFlowType.Battle, 5001L), (3L, EventFlowType.Finish, 0L)],
            pack.FlowsOf(3001).Select(f => (f.FlowNumber, f.FlowType, f.Param1)));
        Assert.Equal("테스트 ADV", pack.FindAdv(4001)!.Name);
        Assert.Equal("테스트 전투", pack.FindBattle(5001)!.Name);
        Assert.True(pack.FindEventFlow(3001, 1)!.IsAvailable(new Dictionary<long, long>()));                       // 조건이 없다
        Assert.False(pack.FindEventFlow(3001, 2)!.IsAvailable(new Dictionary<long, long>()));                      // 전투는 플래그 1001이 필요
        Assert.True(pack.FindEventFlow(3001, 2)!.IsAvailable(new Dictionary<long, long> { [1001] = 1 }));
    }
}
