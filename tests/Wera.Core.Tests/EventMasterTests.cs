using Wera.Core;

namespace Wera.Core.Tests;

// event_master(code, name), event_condition_master(조건표), event_flow_master((event_code, flow_number, group_number)가 키,
// flow_type enum과 long 인자 param_1~3), adv_master(code, name), battle_master(code, name). 모두 선택이고 아직 게임 진행에는 쓰이지 않는다.
public class EventMasterTests
{
    const string Events = "code\tname\n3002\t두 번째\n3001\t첫 이벤트\n";
    const string FlowHeader = "event_code\tflow_number\tgroup_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
    const string CondHeader = "event_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n";

    static GamePack Load(
        string? events = Events, string? conditions = null, string? flows = null, string? advs = null, string? battles = null)
    {
        var files = new List<(string, string)> { ("pack.tsv", "id\tdemo\ntitle\t데모\n") };
        if (events is not null) files.Add(("master/event_master_v1.tsv", events));
        if (conditions is not null) files.Add(("master/event_condition_master_v1.tsv", conditions));
        if (flows is not null) files.Add(("master/event_flow_master_v1.tsv", flows));
        if (advs is not null) files.Add(("master/adv_master_v1.tsv", advs));
        if (battles is not null) files.Add(("master/battle_master_v1.tsv", battles));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    // ---- 모두 선택 ----

    [Fact]
    public void All_five_masters_are_optional()
    {
        var pack = Load(events: null);
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
        Assert.False(gated.IsAvailable(new Dictionary<long, long> { [1001] = 1 }));                    // 그룹 1이 반만 만족
        Assert.True(gated.IsAvailable(new Dictionary<long, long> { [1001] = 1, [1002] = 3 }));         // 그룹 1 만족
        Assert.True(gated.IsAvailable(new Dictionary<long, long> { [1003] = 1 }));                     // 그룹 2 만족
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
    [InlineData("9999\t1\t1001\tEqual\t1\n")]                  // 없는 이벤트
    [InlineData("3001\tx\t1001\tEqual\t1\n")]
    [InlineData("3001\t1\t1001\tBigger\t1\n")]                  // 모르는 조건 타입
    public void Bad_event_conditions_are_rejected(string row) =>
        Assert.Throws<PackFormatException>(() => Load(conditions: CondHeader + row));

    [Fact]
    public void Event_conditions_need_the_event_master()
    {
        Assert.Throws<PackFormatException>(() => Load(events: null, conditions: CondHeader + "3001\t1\t1001\tEqual\t1\n"));
    }

    // ---- event_flow_master ----

    [Fact]
    public void Flows_load_all_seven_columns_and_the_enum_is_read_by_name()
    {
        var pack = Load(flows: FlowHeader + "3001\t2\t1\tnone\t10\t20\t30\n3001\t1\t2\tNone\t-1\t0\t5000000000\n");
        Assert.Equal(new EventFlow(3001, 2, 1, EventFlowType.None, 10, 20, 30), pack.EventFlows![0]);
        Assert.Equal(new EventFlow(3001, 1, 2, EventFlowType.None, -1, 0, 5000000000), pack.EventFlows[1]);
        Assert.Equal(0, (int)EventFlowType.None);
    }

    [Fact]
    public void Flows_are_keyed_by_event_flow_number_and_group_and_listed_in_key_order()
    {
        var pack = Load(flows: FlowHeader +
            "3001\t2\t1\tNone\t0\t0\t0\n3001\t1\t2\tNone\t0\t0\t0\n3001\t1\t1\tNone\t0\t0\t0\n3002\t1\t1\tNone\t7\t0\t0\n");
        Assert.Equal([(1L, 1), (1L, 2), (2L, 1)], pack.FlowsOf(3001).Select(f => (f.FlowNumber, f.GroupNumber)));
        Assert.Equal(7, Assert.Single(pack.FlowsOf(3002)).Param1);
        Assert.Empty(pack.FlowsOf(9999));
        Assert.Equal(4, pack.Masters.EventFlowRowTable.Count);
        Assert.Equal(7, pack.Masters.EventFlowRowTable.FindByEventCodeAndFlowNumberAndGroupNumber((3002, 1, 1)).Param1);
    }

    [Fact]
    public void Empty_params_are_zero_and_the_same_key_twice_is_rejected()
    {
        var pack = Load(flows: FlowHeader + "3001\t1\t1\tNone\t\t\t\n");
        var flow = Assert.Single(pack.EventFlows!);
        Assert.Equal((0, 0, 0), (flow.Param1, flow.Param2, flow.Param3));

        var error = Assert.Throws<PackFormatException>(() => Load(flows: FlowHeader + "3001\t1\t1\tNone\t1\t0\t0\n3001\t1\t1\tNone\t2\t0\t0\n"));
        Assert.Contains("두 번", error.Message);
    }

    [Theory]
    [InlineData("9999\t1\t1\tNone\t0\t0\t0\n", "이벤트 '9999'")]                  // 없는 이벤트
    [InlineData("3001\t1\t1\tJump\t0\t0\t0\n", "흐름 종류 'Jump'")]                // 아직 없는 종류
    [InlineData("3001\t1\t1\t0\t0\t0\t0\n", "흐름 종류 '0'")]                     // 숫자는 안 된다
    [InlineData("3001\tx\t1\tNone\t0\t0\t0\n", "흐름 번호")]
    [InlineData("3001\t1\t1.5\tNone\t0\t0\t0\n", "그룹넘버")]                     // 그룹 넘버는 int
    [InlineData("3001\t1\t1\tNone\ta\t0\t0\n", "인자 1")]
    [InlineData("3001\t1\t1\tNone\t0\t1.5\t0\n", "인자 2")]
    [InlineData("3001\t1\t1\tNone\t0\t0\tx\n", "인자 3")]
    public void Bad_flow_rows_are_rejected(string row, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(flows: FlowHeader + row)).Message);

    [Fact]
    public void Flows_need_the_event_master_and_the_right_header()
    {
        Assert.Throws<PackFormatException>(() => Load(events: null, flows: FlowHeader + "3001\t1\t1\tNone\t0\t0\t0\n"));
        Assert.Throws<PackFormatException>(() => Load(flows: "event_code\tflow_number\tgroup_number\tflow_type\tparam_1\tparam_2\n3001\t1\t1\tNone\t0\t0\n"));
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
            flows: "event_code\tflow_number\tgroup_number\tflow_type\tparam_1\tparam_2\tparam_3\tmemo\n3001\t1\t1\tNone\t0\t0\t0\t메모\n",
            advs: "code\tname\tmemo\n4001\tadv\t메모\n", battles: "code\tname\tmemo\n5001\tbattle\t메모\n");
        Assert.Single(pack.Events!);
        Assert.Single(pack.EventFlows!);
        Assert.Single(pack.Advs!);
        Assert.Single(pack.Battles!);
    }

    [Fact]
    public void The_default_pack_has_one_test_row_in_each_new_master()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.zip"));
        var pack = GamePack.Load(zip);

        Assert.Equal("테스트 이벤트", pack.FindEvent(3001)!.Name);
        Assert.Equal(EventFlowType.None, Assert.Single(pack.FlowsOf(3001)).FlowType);
        Assert.Equal("테스트 ADV", pack.FindAdv(4001)!.Name);
        Assert.Equal("테스트 전투", pack.FindBattle(5001)!.Name);
        Assert.True(pack.FindEvent(3001)!.IsAvailable(new Dictionary<long, long>()));            // 조건이 없다
    }
}
