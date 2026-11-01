using Wera.Core;

namespace Wera.Core.Tests;

// 흐름 서킷의 서브모듈: 이벤트, 던전 이벤트, ADV, 교류, 시설, 방어력 흐름이 JumpToSubmodule로 같은 종류의 서브모듈 마스터(submodule_code, flow_number, ...)를 부른다.
// 서브모듈은 부른 서킷의 상태를 그대로 쓰고, 서브모듈 안에서 멈춰도(ADV, 전투, 선택지) 그 자리에서 이어 가며, 안의 Finish는 서브모듈만 끝낸다.
public class FlowSubmoduleTests
{
    const string EventHeader = "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
    const string EventSubHeader = "submodule_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
    const string AdvHeader = "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n";
    const string AdvSubHeader = "submodule_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n";
    const string CommunicationSubHeader = "submodule_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n";

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(params (string Path, string Text)[] extra)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            (GamePack.CharacterInventoryMasterPath,
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
            (GamePack.EventMasterPath, "code\tname\n3001\t이벤트\n"),
            (GamePack.DungeonEventMasterPath, "code\tname\n6001\t상자\n"),
            (GamePack.AdvMasterPath, "code\tname\n4001\tA\n4002\tB\n"),
            (GamePack.BattleMasterPath, "code\tname\n5001\t전투\n"),
            (GamePack.FacilityMasterPath, "code\tname\n1\t정문\n"),
            (GamePack.OperatorSlotMasterPath, "code\tname\n1\t경비\n"),
        };
        files.RemoveAll(f => extra.Any(e => e.Path == f.Item1));
        files.AddRange(extra.Select(e => (e.Path, e.Text)));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    static string Message(params (string Path, string Text)[] extra) => Assert.Throws<PackFormatException>(() => Pack(extra)).Message;

    // ---- 이벤트 ----

    [Fact]
    public void An_event_submodule_pauses_on_adv_and_battle_inside_it_and_returns_to_the_caller_after_it_finishes()
    {
        var pack = Pack(
            (GamePack.EventFlowMasterPath, EventHeader + Rows("3001 1 SetFlag 1 1 0", "3001 2 JumpToSubmodule 9 0 0", "3001 3 SetFlag 2 1 0")),
            (GamePack.EventSubmoduleMasterPath, EventSubHeader + Rows("9 1 Adv 4001 0 0", "9 2 Battle 5001 0 0", "9 3 SetFlag 3 1 0", "9 4 Finish 0 0 0", "9 5 SetFlag 4 1 0")));
        var flags = new Dictionary<long, long>();
        var run = new EventFlowCircuit(pack, 3001, flags);
        Assert.Equal(new EventStep(EventStepKind.Adv, 1, 4001), run.Advance());      // 서브모듈 안의 ADV에서 멈춘다
        Assert.Equal(1, flags[1]);
        Assert.False(flags.ContainsKey(2));
        Assert.Equal(new EventStep(EventStepKind.Battle, 2, 5001), run.Advance());   // 다음 호출은 그 자리에서 이어 간다
        Assert.Equal(EventStepKind.Finished, run.Advance().Kind);
        Assert.Equal(1, flags[3]);                                                    // 서브모듈의 Finish 앞까지 실행했다
        Assert.False(flags.ContainsKey(4));                                           // Finish 뒤는 실행하지 않는다
        Assert.Equal(1, flags[2]);                                                    // 돌아와 호출 다음 칸을 실행했다
        Assert.True(run.IsFinished);
    }

    [Fact]
    public void A_submodule_that_runs_to_its_end_returns_and_the_main_Finish_still_ends_the_event()
    {
        var pack = Pack(
            (GamePack.EventFlowMasterPath, EventHeader + Rows("3001 1 JumpToSubmodule 9 0 0", "3001 2 JumpToSubmodule 9 0 0", "3001 3 Finish 0 0 0", "3001 4 SetFlag 7 1 0")),
            (GamePack.EventSubmoduleMasterPath, EventSubHeader + Rows("9 1 ModFlag 5 1 0", "9 2 JumpFlagGreaterOrEqual 5 2 4", "9 3 SetFlag 6 1 0", "9 4 ModFlag 8 1 0")));
        var flags = new Dictionary<long, long>();
        Assert.Equal(EventStepKind.Finished, new EventFlowCircuit(pack, 3001, flags).Advance().Kind);
        Assert.Equal(2, flags[5]);
        Assert.Equal(1, flags[6]);                                                    // 첫 호출에서만(플래그 5가 1일 때) 3번을 실행했다
        Assert.Equal(2, flags[8]);                                                    // 두 번 다 4번으로 끝났다: 서브모듈 안의 점프는 안에서만
        Assert.False(flags.ContainsKey(7));                                           // 메인의 Finish가 이벤트를 끝냈다
    }

    [Fact]
    public void A_dungeon_event_calls_the_dungeon_event_submodules_and_a_circuit_without_them_ignores_the_call()
    {
        var pack = Pack(
            (GamePack.DungeonEventFlowMasterPath, "dungeon_event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + Rows("6001 1 JumpToSubmodule 2 0 0", "6001 2 SetFlag 1 1 0")),
            (GamePack.DungeonEventSubmoduleMasterPath, EventSubHeader + Rows("2 1 SetFlag 9 9 0")));
        var flags = new Dictionary<long, long>();
        new EventFlowCircuit(pack, 6001, flags, flows: pack.DungeonEventFlowsOf(6001), submodules: pack.DungeonEventSubmodules).Advance();
        Assert.Equal(9, flags[9]);
        Assert.Equal(1, flags[1]);

        var other = new Dictionary<long, long>();
        new EventFlowCircuit(pack, 6001, other, flows: pack.DungeonEventFlowsOf(6001)).Advance();   // 서브모듈을 넘기지 않으면 호출은 아무 일도 하지 않는다
        Assert.False(other.ContainsKey(9));
        Assert.Equal(1, other[1]);
    }

    [Fact]
    public void Event_submodules_are_validated_at_load_time()
    {
        var calling = (GamePack.EventFlowMasterPath, EventHeader + Rows("3001 1 JumpToSubmodule 9 0 0"));
        Assert.Contains("event_submodule_master_v1.tsv가 필요", Message(calling));                       // 서브모듈 마스터가 없다
        Assert.Contains("서브모듈 '9'가", Message(calling, (GamePack.EventSubmoduleMasterPath, EventSubHeader + Rows("8 1 Finish 0 0 0"))));   // 없는 서브모듈 코드
        Assert.Contains("서브모듈 안에서는 JumpToSubmodule", Message(
            (GamePack.EventSubmoduleMasterPath, EventSubHeader + Rows("9 1 JumpToSubmodule 9 0 0"))));        // 서브모듈이 서브모듈을 부른다
        Assert.Contains("'3'가 같은 서브모듈의 흐름에 없", Message(
            (GamePack.EventSubmoduleMasterPath, EventSubHeader + Rows("9 1 Jump 3 0 0", "10 3 Finish 0 0 0"))));  // 다른 서브모듈의 번호로 점프
        Assert.Contains("커야", Message(
            (GamePack.EventSubmoduleMasterPath, EventSubHeader + Rows("9 1 Finish 0 0 0", "9 2 Jump 1 0 0"))));   // 뒤로 점프
        Assert.Contains("두 번", Message(
            (GamePack.EventSubmoduleMasterPath, EventSubHeader + Rows("9 1 Finish 0 0 0", "9 1 None 0 0 0"))));
        Assert.Contains("ADV '9'", Message(
            (GamePack.EventSubmoduleMasterPath, EventSubHeader + Rows("9 1 Adv 9 0 0"))));                    // 종류별 검증은 이벤트 흐름과 같다
        Assert.Contains("서브모듈 '9'", Message(
            (GamePack.DungeonEventSubmoduleMasterPath, EventSubHeader + Rows("9 1 Adv 9 0 0"))));
    }

    // ---- ADV ----

    [Fact]
    public void An_adv_submodule_can_show_text_and_choices_and_a_choice_jumps_inside_the_submodule()
    {
        var pack = Pack(
            (GamePack.AdvFlowMasterPath, AdvHeader + "4001\t1\tJumpToSubmodule\t7\t0\t0\t\t\n4001\t2\tTextLog\t0\t0\t0\t이후\t\n"),
            (GamePack.AdvSubmoduleMasterPath, AdvSubHeader +
                "7\t1\tTextLog\t0\t0\t0\t안\t\n7\t2\tChoice\t1\t4\t0\t간다\t\n7\t3\tChoice\t1\t5\t0\t건너뛴다\t\n7\t4\tTextLog\t0\t0\t0\t넷\t\n7\t5\tFinish\t0\t0\t0\t\t\n"));
        var run = new AdvFlowCircuit(pack, 4001, new Dictionary<long, long>());
        Assert.Equal("안", run.Advance().Text);
        var choice = run.Advance();
        Assert.Equal((AdvStepKind.Choice, 2), (choice.Kind, choice.Choices.Count));
        Assert.True(run.Choose(0));                                                   // 4로 점프(서브모듈 안)
        Assert.Equal("넷", run.Advance().Text);
        Assert.Equal("이후", run.Advance().Text);                                     // 서브모듈이 Finish로 끝나고 호출 다음 칸으로 돌아왔다
        Assert.Equal(AdvStepKind.Finished, run.Advance().Kind);

        var skip = new AdvFlowCircuit(pack, 4001, new Dictionary<long, long>());
        skip.Advance();
        skip.Advance();
        Assert.True(skip.Choose(1));                                                  // 5(Finish)로 점프: 서브모듈만 끝난다
        Assert.Equal("이후", skip.Advance().Text);
    }

    [Fact]
    public void Adv_submodules_are_validated_at_load_time()
    {
        var calling = (GamePack.AdvFlowMasterPath, AdvHeader + "4001\t1\tJumpToSubmodule\t7\t0\t0\t\t\n");
        Assert.Contains("adv_submodule_master_v1.tsv가 필요", Message(calling));
        Assert.Contains("서브모듈 '7'가", Message(calling, (GamePack.AdvSubmoduleMasterPath, AdvSubHeader + "8\t1\tFinish\t0\t0\t0\t\t\n")));
        Assert.Contains("서브모듈 안에서는 JumpToSubmodule", Message(
            (GamePack.AdvSubmoduleMasterPath, AdvSubHeader + "7\t1\tJumpToSubmodule\t7\t0\t0\t\t\n")));
        Assert.Contains("선택지가 점프할 흐름 번호 '3'가 같은", Message(
            (GamePack.AdvSubmoduleMasterPath, AdvSubHeader + "7\t1\tChoice\t1\t3\t0\t고르기\t\n8\t3\tFinish\t0\t0\t0\t\t\n")));
        Assert.Contains("text가 있어야", Message(
            (GamePack.AdvSubmoduleMasterPath, AdvSubHeader + "7\t1\tTextLog\t0\t0\t0\t\t\n")));
    }

    // ---- 교류 ----

    [Fact]
    public void A_communication_submodule_is_shared_by_every_communication_flow_and_runs_on_the_callers_state()
    {
        var pack = Pack((GamePack.CommunicationSubmoduleMasterPath, CommunicationSubHeader +
            "2\t1\tText\t0\t0\t0\t안녕\n2\t2\tSetFlag\t4\t1\t0\t\n2\t3\tFinish\t0\t0\t0\t\n2\t4\tTextLog\t0\t0\t0\t안 나옴\n"));
        var flags = new Dictionary<long, long>();
        CommunicationFlowStep[] flows =
        [
            new(1, CommunicationFlowType.JumpToSubmodule, 2, 0, 0, ""),
            new(2, CommunicationFlowType.TextLog, 0, 0, 0, "다음"),
        ];
        var run = new CommunicationFlowCircuit(pack, 1, flows, 7001, flags);
        var first = run.Advance();
        Assert.Equal((AdvStepKind.TextYou, "용사", "안녕"), (first.Kind, first.SpeakerName, first.Text));   // 서브모듈 안의 대사: 화자는 호출한 교류의 상대
        Assert.False(flags.ContainsKey(4));
        Assert.Equal("다음", run.Advance().Text);
        Assert.Equal(1, flags[4]);
        Assert.Equal(AdvStepKind.Finished, run.Advance().Kind);
    }

    [Fact]
    public void Communication_submodules_are_validated_and_the_three_flow_masters_all_check_their_calls()
    {
        const string Sub = "master/communication_submodule_master_v1.tsv";
        var group = ("master/communication_group_master_v1.tsv", "code\tname\n1\t대화\n");
        var communication = ("master/communication_master_v1.tsv", "code\tgroup_code\tname\tis_assist\n10\t1\t인사\tfalse\n");
        var defaultCalling = ("master/communication_default_flow_master_v1.tsv",
            "communication_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n10\t1\tJumpToSubmodule\t2\t0\t0\t\n");
        Assert.Contains("communication_submodule_master_v1.tsv가 필요", Message(group, communication, defaultCalling));
        Assert.Contains("서브모듈 '2'가", Message(group, communication, defaultCalling, (Sub, CommunicationSubHeader + "3\t1\tFinish\t0\t0\t0\t\n")));
        Assert.Contains("서브모듈 안에서는 JumpToSubmodule", Message((Sub, CommunicationSubHeader + "2\t1\tJumpToSubmodule\t2\t0\t0\t\n")));
        Assert.Contains("같은 흐름 묶음에 없", Message((Sub, CommunicationSubHeader + "2\t1\tJump\t5\t0\t0\t\n")));
        Assert.Contains("text가 있어야", Message((Sub, CommunicationSubHeader + "2\t1\tText\t0\t0\t0\t\n")));
        Pack(group, communication, defaultCalling, (Sub, CommunicationSubHeader + "2\t1\tFinish\t0\t0\t0\t\n"));          // 있으면 로드된다
    }

    // ---- 시설 ----

    [Fact]
    public void A_facility_submodule_runs_on_the_facilitys_state_and_its_Finish_returns_to_the_caller()
    {
        FacilityFlow[] flows =
        [
            new(1, 1, FacilityFlowType.JumpToSubmodule, 3, 0, 0),
            new(1, 2, FacilityFlowType.ModFlag, 6, 1, 0),
        ];
        FacilityFlow[] sub =
        [
            new(3, 1, FacilityFlowType.JumpOperatorExists, 1, 0, 3),
            new(3, 2, FacilityFlowType.ModFlag, 5, 1, 0),
            new(3, 3, FacilityFlowType.Finish, 0, 0, 0),
            new(3, 4, FacilityFlowType.ModFlag, 5, 100, 0),
        ];
        var submodules = new Dictionary<long, IReadOnlyList<FacilityFlow>> { [3] = sub };
        long? Operator(long slot) => slot == 1 ? 7001 : null;
        var withOperator = new Dictionary<long, long>();
        new FacilityFlowCircuit(1, flows, Operator, withOperator, (_, _) => { }, _ => 0, (_, _) => false, (_, _) => false, submodules: submodules).Execute();
        Assert.False(withOperator.ContainsKey(5));                                    // 오퍼레이터가 있어 3번(Finish)으로 점프했다
        Assert.Equal(1, withOperator[6]);                                             // 서브모듈의 Finish 뒤 호출 다음 칸이 실행됐다
        var without = new Dictionary<long, long>();
        new FacilityFlowCircuit(1, flows, _ => null, without, (_, _) => { }, _ => 0, (_, _) => false, (_, _) => false, submodules: submodules).Execute();
        Assert.Equal(1, without[5]);
        Assert.Equal(1, without[6]);
    }

    [Fact]
    public void Facility_submodules_are_validated_at_load_time()
    {
        const string Header = "facility_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
        const string Sub = "master/facility_submodule_master_v1.tsv";
        const string SubHeader = "submodule_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
        var calling = (GamePack.FacilityFlowMasterPath, Header + Rows("1 1 JumpToSubmodule 4 0 0"));
        Assert.Contains("facility_submodule_master_v1.tsv가 필요", Message(calling));
        Assert.Contains("서브모듈 '4'가", Message(calling, (Sub, SubHeader + Rows("5 1 Finish 0 0 0"))));
        Assert.Contains("서브모듈 안에서는 JumpToSubmodule", Message((Sub, SubHeader + Rows("4 1 JumpToSubmodule 4 0 0"))));
        Assert.Contains("오퍼레이터 슬롯 '9'", Message((Sub, SubHeader + Rows("4 1 JumpOperatorExists 9 0 2", "4 2 Finish 0 0 0"))));
        Pack(calling, (Sub, SubHeader + Rows("4 1 Finish 0 0 0")));                   // 있으면 로드된다
    }

    // ---- 방어력 ----

    [Fact]
    public void A_defense_submodule_adds_to_the_same_defense_and_its_Finish_returns_to_the_caller()
    {
        DefenseFlow[] flows =
        [
            new(1, 1, DefenseFlowType.AddDefense, 1, 0, 0),
            new(1, 2, DefenseFlowType.JumpToSubmodule, 3, 0, 0),
            new(1, 3, DefenseFlowType.AddDefense, 100, 0, 0),
        ];
        DefenseFlow[] sub =
        [
            new(3, 1, DefenseFlowType.AddDefense, 10, 0, 0),
            new(3, 2, DefenseFlowType.Finish, 0, 0, 0),
            new(3, 3, DefenseFlowType.AddDefense, 1000, 0, 0),
        ];
        var submodules = new Dictionary<long, IReadOnlyList<DefenseFlow>> { [3] = sub };
        long Run(IReadOnlyDictionary<long, IReadOnlyList<DefenseFlow>>? s) =>
            new DefenseFlowCircuit(flows, 7001, (_, _) => 0, (_, _) => false, new Dictionary<long, long>(), submodules: s).Execute();
        Assert.Equal(111, Run(submodules));                                           // 1 + 10(서브모듈) + 100
        Assert.Equal(101, Run(null));                                                 // 서브모듈이 없으면 호출은 아무 일도 하지 않는다
    }

    [Fact]
    public void Defense_submodules_are_validated_at_load_time_and_reach_the_session()
    {
        const string Header = "operator_slot_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
        const string Sub = "master/operator_slot_defense_submodule_master_v1.tsv";
        const string SubHeader = "submodule_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
        var calling = (GamePack.DefenseFlowMasterPath, Header + Rows("1 1 AddDefense 2 0 0", "1 2 JumpToSubmodule 4 0 0"));
        Assert.Contains("operator_slot_defense_submodule_master_v1.tsv가 필요", Message(calling));
        Assert.Contains("서브모듈 '4'가", Message(calling, (Sub, SubHeader + Rows("5 1 Finish 0 0 0"))));
        Assert.Contains("서브모듈 안에서는 JumpToSubmodule", Message((Sub, SubHeader + Rows("4 1 JumpToSubmodule 4 0 0"))));
        Assert.Contains("태그 코드 '9'", Message((Sub, SubHeader + Rows("4 1 JumpOperatorHaveTag 9 0 2", "4 2 Finish 0 0 0"))));
        var pack = Pack(calling, (Sub, SubHeader + Rows("4 1 AddDefense 30 0 0")));
        var session = GameSession.NewGame(pack);
        session.AddCharacter(7001);
        Assert.Equal(32, session.OperatorDefenseOf(7001, 1));                         // 세션이 서브모듈을 넘겨 준다: 2 + 30
    }
}
