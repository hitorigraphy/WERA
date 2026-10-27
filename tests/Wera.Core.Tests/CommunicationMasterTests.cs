using Wera.Core;

namespace Wera.Core.Tests;

// communication_master(code, group_code, name, is_assist; code가 키)와 세 흐름 마스터:
// solo(communication_code, character_code, flow_number, ...), duo(communication_code, partner_code, assist_code, flow_number, ...),
// default(communication_code, flow_number, ...). 흐름은 duo → solo → default 순으로 찾는다.
public class CommunicationMasterTests
{
    const string Groups = "code\tname\n1\t대화\n2\t선물\n";
    const string Comms = "code\tgroup_code\tname\tis_assist\n1\t1\t인사하기\tfalse\n2\t2\t건네기\ttrue\n";
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
        "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n";
    const string SoloHeader = "communication_code\tcharacter_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n";
    const string DuoHeader = "communication_code\tpartner_code\tassist_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n";
    const string DefaultHeader = "communication_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n";

    static GamePack Load(string? groups = Groups, string? comms = Comms, string? solo = null, string? duo = null, string? dflt = null)
    {
        var files = new List<(string, string)>
            { ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/character_inventory_master_v1.tsv", Characters) };
        if (groups is not null) files.Add((GamePack.CommunicationGroupMasterPath, groups));
        if (comms is not null) files.Add((GamePack.CommunicationMasterPath, comms));
        if (solo is not null) files.Add((GamePack.CommunicationSoloFlowMasterPath, solo));
        if (duo is not null) files.Add((GamePack.CommunicationDuoFlowMasterPath, duo));
        if (dflt is not null) files.Add((GamePack.CommunicationDefaultFlowMasterPath, dflt));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    static string Error(string? groups = Groups, string? comms = Comms, string? solo = null, string? duo = null, string? dflt = null) =>
        Assert.Throws<PackFormatException>(() => Load(groups, comms, solo, duo, dflt)).Message;

    // ---- 교류 마스터 ----

    [Fact]
    public void The_masters_are_optional()
    {
        var pack = Load(null, null);
        Assert.Null(pack.Communications);
        Assert.Null(pack.CommunicationSoloFlows);
        Assert.Null(pack.CommunicationDuoFlows);
        Assert.Null(pack.CommunicationDefaultFlows);
        Assert.Empty(pack.Masters.CommunicationRowTable.All);
        Assert.Empty(pack.Masters.CommunicationSoloFlowRowTable.All);
        Assert.Empty(pack.Masters.CommunicationDuoFlowRowTable.All);
        Assert.Empty(pack.Masters.CommunicationDefaultFlowRowTable.All);
    }

    [Fact]
    public void Communications_load_code_group_code_name_and_is_assist()
    {
        var pack = Load();
        Assert.Equal([new CommunicationEntry(1, 1, "인사하기", false), new CommunicationEntry(2, 2, "건네기", true)], pack.Communications);
        Assert.True(pack.Masters.CommunicationRowTable.TryFindByCode(2, out var row));
        Assert.Equal((2L, "건네기", true), (row.GroupCode, row.Name, row.IsAssist));
        Assert.Equal([1L], pack.Masters.CommunicationRowTable.FindByGroupCode(1).Select(c => c.Code));
    }

    [Theory]
    [InlineData("code\tgroup_code\tname\tis_assist\n1\t1\ta\tfalse\n1\t2\tb\tfalse\n", "중복")]
    [InlineData("code\tgroup_code\tname\tis_assist\n1\t9\ta\tfalse\n", "교류 그룹 마스터에 없습니다")]
    [InlineData("code\tgroup_code\tname\tis_assist\nx\t1\ta\tfalse\n", "정수")]
    [InlineData("code\tgroup_code\tname\tis_assist\n1\t\ta\tfalse\n", "비어")]
    [InlineData("code\tgroup_code\tname\tis_assist\n1\t1\ta\t\n", "is_assist")]
    [InlineData("code\tgroup_code\tname\tis_assist\n1\t1\ta\tmaybe\n", "true/false")]
    [InlineData("code\tgroup_code\tname\n1\t1\ta\n", "헤더")]
    public void Bad_communications_are_rejected(string comms, string message) =>
        Assert.Contains(message, Error(comms: comms));

    [Fact]
    public void Communications_need_the_group_master() =>
        Assert.Contains(GamePack.CommunicationGroupMasterPath, Error(groups: null));

    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    public void Is_assist_accepts_true_and_one(string value) =>
        Assert.True(Load(comms: $"code\tgroup_code\tname\tis_assist\n1\t1\ta\t{value}\n").Communications![0].IsAssist);

    // ---- 흐름 마스터 공통: 키와 검증 ----

    [Fact]
    public void Flow_masters_need_the_communication_master()
    {
        Assert.Contains(GamePack.CommunicationMasterPath, Error(comms: null, solo: SoloHeader));
        Assert.Contains(GamePack.CommunicationMasterPath, Error(comms: null, duo: DuoHeader));
        Assert.Contains(GamePack.CommunicationMasterPath, Error(comms: null, dflt: DefaultHeader));
    }

    [Fact]
    public void The_old_flow_master_name_gets_a_rename_message()
    {
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.CommunicationGroupMasterPath, Groups), (GamePack.CommunicationMasterPath, Comms),
            ("master/communication_flow_master_v1.tsv", SoloHeader),
        };
        var message = Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(files))).Message;
        Assert.Contains("communication_solo_flow_master", message);
    }

    [Fact]
    public void The_three_masters_load_all_columns_with_their_own_keys()
    {
        var pack = Load(
            solo: SoloHeader + "1\t7001\t2\tNone\t\t\t\t둘\n1\t7001\t1\tNone\t5\t6\t7\t하나\n1\t7002\t1\tNone\t\t\t\t다른 캐릭터\n",
            duo: DuoHeader + "1\t7001\t7002\t1\tNone\t\t\t\t짝\n1\t7002\t7001\t1\tNone\t\t\t\t뒤바뀐 짝\n",
            dflt: DefaultHeader + "1\t1\tNone\t\t\t\t기본\n2\t1\tNone\t\t\t\t다른 교류\n");

        Assert.Equal(new CommunicationSoloFlow(1, 7001, 1, CommunicationFlowType.None, 5, 6, 7, "하나"), pack.CommunicationSoloFlows![1]);
        Assert.Equal(3, pack.CommunicationSoloFlows.Count);
        Assert.Equal(new CommunicationDuoFlow(1, 7001, 7002, 1, CommunicationFlowType.None, 0, 0, 0, "짝"), pack.CommunicationDuoFlows![0]);
        Assert.Equal(new CommunicationDefaultFlow(1, 1, CommunicationFlowType.None, 0, 0, 0, "기본"), pack.CommunicationDefaultFlows![0]);

        Assert.True(pack.Masters.CommunicationSoloFlowRowTable.TryFindByCommunicationCodeAndCharacterCodeAndFlowNumber((1, 7001, 2), out var solo));
        Assert.Equal("둘", solo.Text);
        Assert.True(pack.Masters.CommunicationDuoFlowRowTable.TryFindByCommunicationCodeAndPartnerCodeAndAssistCodeAndFlowNumber((1, 7002, 7001, 1), out var duo));
        Assert.Equal("뒤바뀐 짝", duo.Text);
        Assert.True(pack.Masters.CommunicationDefaultFlowRowTable.TryFindByCommunicationCodeAndFlowNumber((2, 1), out var dflt));
        Assert.Equal("다른 교류", dflt.Text);
    }

    [Theory]
    [InlineData("9\t7001\t1\tNone\t\t\t\t", "교류 마스터에 없습니다")]
    [InlineData("1\t4242\t1\tNone\t\t\t\t", "캐릭터 인벤토리 마스터에 없습니다")]
    [InlineData("1\t2\t1\tNone\t\t\t\t", "캐릭터 인벤토리 마스터에 없습니다")]              // 2는 메인 통화(아이템)
    [InlineData("1\t7001\t1\tBogus\t\t\t\t", "알 수 없습니다")]
    [InlineData("1\t7001\t1\t0\t\t\t\t", "알 수 없습니다")]
    [InlineData("1\t7001\t1\tNone\t\t\t\t\n1\t7001\t1\tNone\t\t\t\t", "두 번")]
    [InlineData("1\t7001\tx\tNone\t\t\t\t", "정수")]
    [InlineData("1\t7001\t1\tNone\tq\t\t\t", "정수")]
    public void Bad_solo_rows_are_rejected(string rows, string message) =>
        Assert.Contains(message, Error(solo: SoloHeader + rows + "\n"));

    [Theory]
    [InlineData("1\t7001\t4242\t1\tNone\t\t\t\t", "assist_code '4242'")]
    [InlineData("1\t4242\t7001\t1\tNone\t\t\t\t", "partner_code '4242'")]
    [InlineData("9\t7001\t7002\t1\tNone\t\t\t\t", "교류 마스터에 없습니다")]
    [InlineData("1\t7001\t7002\t1\tNone\t\t\t\t\n1\t7001\t7002\t1\tNone\t\t\t\t", "두 번")]
    public void Bad_duo_rows_are_rejected(string rows, string message) =>
        Assert.Contains(message, Error(duo: DuoHeader + rows + "\n"));

    [Fact]
    public void Duo_keys_distinguish_the_order_of_partner_and_assist() =>
        Assert.Equal(2, Load(duo: DuoHeader + "1\t7001\t7002\t1\tNone\t\t\t\t\n1\t7002\t7001\t1\tNone\t\t\t\t\n").CommunicationDuoFlows!.Count);

    [Theory]
    [InlineData("9\t1\tNone\t\t\t\t", "교류 마스터에 없습니다")]
    [InlineData("1\t1\tNone\t\t\t\t\n1\t1\tNone\t\t\t\t", "두 번")]
    public void Bad_default_rows_are_rejected(string rows, string message) =>
        Assert.Contains(message, Error(dflt: DefaultHeader + rows + "\n"));

    // ---- 종류별 검증 (세 마스터가 같은 규칙) ----

    [Theory]
    [InlineData("1\tText\t\t\t\t안녕", "")]
    [InlineData("1\tText\t\t\t\t", "text가 있어야")]
    [InlineData("1\tTextPlain\t\t\t\t  ", "text가 있어야")]
    [InlineData("1\tTextLog\t\t\t\t  ", "text가 있어야")]
    [InlineData("1\tTextLog\t\t\t\t로그", "")]
    [InlineData("1\tTextLog\t\t\t\t[b]열림", "text가 올바르지")]
    [InlineData("1\tText\t\t\t\t[b]열림", "text가 올바르지")]
    [InlineData("1\tChoice\t1\t2\t\t고른다\n2\tFinish\t\t\t\t", "")]
    [InlineData("1\tChoice\t1\t2\t\t\n2\tFinish\t\t\t\t", "text가 있어야")]
    [InlineData("1\tChoice\t1\t2\t\ta\\nb\n2\tFinish\t\t\t\t", "줄바꿈")]
    [InlineData("1\tChoice\t1\t1\t\t고른다", "보다 커야")]
    [InlineData("1\tChoice\t1\t9\t\t고른다", "같은 흐름 묶음에 없습니다")]
    [InlineData("1\tChoice\t1\t3\t\ta\n2\tText\t\t\t\tb\n3\tChoice\t1\t4\t\tc\n4\tFinish\t\t\t\t", "연속하지")]
    [InlineData("1\tJump\t2\t\t\t\n2\tFinish\t\t\t\t", "")]
    [InlineData("1\tJump\t1\t\t\t", "보다 커야")]
    [InlineData("1\tJump\t5\t\t\t", "같은 흐름 묶음에 없습니다")]
    [InlineData("1\tJumpFlagEqual\t5\t1\t2\t\n2\tFinish\t\t\t\t", "")]
    [InlineData("1\tJumpFlagEqual\t5\t1\t1\t", "보다 커야")]
    [InlineData("1\tJumpFlagLessThan\t5\t1\t8\t", "같은 흐름 묶음에 없습니다")]
    [InlineData("1\tJumpRandom\t30\t100\t2\t\n2\tFinish\t\t\t\t", "")]
    [InlineData("1\tJumpRandom\t30\t0\t2\t\n2\tFinish\t\t\t\t", "모수")]
    [InlineData("1\tJumpInventoryGreaterOrEqual\t2\t1\t2\t\n2\tFinish\t\t\t\t", "")]
    [InlineData("1\tJumpInventoryEqual\t4242\t1\t2\t\n2\tFinish\t\t\t\t", "인벤토리 마스터에 없습니다")]
    [InlineData("1\tJumpCharacterHaveTag\t7001\t1\t2\t\n2\tFinish\t\t\t\t", "")]
    [InlineData("1\tJumpCharacterHaveTag\t2\t1\t2\t\n2\tFinish\t\t\t\t", "캐릭터 인벤토리 마스터에 없습니다")]
    [InlineData("1\tAddInventory\t2\t5\t\t", "")]
    [InlineData("1\tAddInventory\t7001\t2\t\t", "1 또는 -1")]
    [InlineData("1\tSetFlag\t5\t1\t\t", "")]
    [InlineData("1\tModFlag\t5\t-1\t\t", "")]
    public void Flow_rules_are_the_same_as_the_adv_ones(string rows, string message)
    {
        // 한 줄 앞에 교류 코드(1)를 붙인다. 문구 안의 \n(역슬래시 n)은 이스케이프 그대로 TSV에 들어간다.
        var all = string.Concat(rows.Split('\n').Select(r => "1\t" + r + "\n"));
        if (message == "") Assert.NotNull(Load(dflt: DefaultHeader + all).CommunicationDefaultFlows);
        else Assert.Contains(message, Error(dflt: DefaultHeader + all));
    }

    [Fact]
    public void Jumps_stay_inside_one_flow_set()
    {
        // 솔로 7001의 흐름 묶음 안에서만 점프할 수 있다: 같은 번호가 다른 캐릭터에 있어도 소용없다
        var rows = SoloHeader + "1\t7001\t1\tJump\t2\t\t\t\n1\t7002\t2\tFinish\t\t\t\t\n";
        Assert.Contains("같은 흐름 묶음에 없습니다", Error(solo: rows));
    }

    // ---- 찾기: duo → solo → default ----

    static GamePack Lookup() => Load(
        solo: SoloHeader +
              "1\t7001\t1\tText\t\t\t\t솔로 용사\n1\t7001\t2\tText\t\t\t\t솔로 용사 둘\n" +
              "1\t7002\t1\tText\t\t\t\t솔로 마법사\n" +
              "2\t7001\t1\tText\t\t\t\t어시스트 솔로 용사\n",
        duo: DuoHeader +
             "1\t7001\t7002\t1\tText\t\t\t\t듀오 용사+마법사\n" +
             "2\t7002\t7001\t1\tText\t\t\t\t어시스트 듀오(파트너 자리에 어시스트)\n",
        dflt: DefaultHeader + "1\t1\tText\t\t\t\t기본\n2\t1\tText\t\t\t\t어시스트 기본\n");

    static string[] Texts(IReadOnlyList<CommunicationFlowStep> steps) => steps.Select(s => s.Text).ToArray();

    [Fact]
    public void The_duo_is_searched_first() =>
        Assert.Equal(["듀오 용사+마법사"], Texts(Lookup().FindCommunicationFlow(1, 7001, 7002)));

    [Fact]
    public void Without_a_duo_the_solo_of_the_partner_is_used_as_a_whole()
    {
        var pack = Lookup();
        Assert.Equal(["솔로 용사", "솔로 용사 둘"], Texts(pack.FindCommunicationFlow(1, 7001, null)));
        Assert.Equal(["솔로 마법사"], Texts(pack.FindCommunicationFlow(1, 7002, 7001)));          // (7002, 7001) 듀오는 없다
    }

    [Fact]
    public void Without_a_duo_or_solo_the_default_is_used()
    {
        var pack = Lookup();
        Assert.Equal(["기본"], Texts(pack.FindCommunicationFlow(1, null, null)));
        Assert.Equal(["기본"], Texts(pack.FindCommunicationFlow(1, 1, 7002)));                    // 플레이어(1)에게는 솔로도 듀오도 없다
    }

    [Fact]
    public void An_assist_communication_swaps_partner_and_assist_for_the_duo_and_uses_the_assist_for_the_solo()
    {
        var pack = Lookup();
        // 파트너 7001, 어시스트 7002 → 듀오는 (partner_code = 7002, assist_code = 7001)로 찾는다
        Assert.Equal(["어시스트 듀오(파트너 자리에 어시스트)"], Texts(pack.FindCommunicationFlow(2, 7001, 7002)));
        // 듀오가 없으면 어시스트(7001)의 솔로
        Assert.Equal(["어시스트 솔로 용사"], Texts(pack.FindCommunicationFlow(2, 7002, 7001)));
        // 어시스트가 비면 솔로도 못 찾아 기본으로
        Assert.Equal(["어시스트 기본"], Texts(pack.FindCommunicationFlow(2, 7001, null)));
    }

    [Fact]
    public void An_unknown_communication_or_no_flow_at_all_gives_an_empty_list()
    {
        Assert.Empty(Lookup().FindCommunicationFlow(99, 7001, 7002));
        Assert.Empty(Load().FindCommunicationFlow(1, 7001, 7002));                                // 흐름 마스터가 없다
    }

    [Fact]
    public void The_session_looks_flows_up_with_its_partner_and_assist()
    {
        var s = GameSession.NewGame(Lookup());
        Assert.Equal(["기본"], Texts(s.CommunicationFlowOf(1)));
        s.AddCharacter(7001);
        s.AddCharacter(7002);
        s.SetSupport(SupportRole.Partner, 7001);
        Assert.Equal(["솔로 용사", "솔로 용사 둘"], Texts(s.CommunicationFlowOf(1)));
        s.SetSupport(SupportRole.Assist, 7002);
        Assert.Equal(["듀오 용사+마법사"], Texts(s.CommunicationFlowOf(1)));
        Assert.Equal(["어시스트 듀오(파트너 자리에 어시스트)"], Texts(s.CommunicationFlowOf(2)));
    }

    [Fact]
    public void The_enum_numbers_are_fixed()
    {
        Assert.Equal(0, (int)CommunicationFlowType.None);
        Assert.Equal(1, (int)CommunicationFlowType.Finish);
        Assert.Equal(2, (int)CommunicationFlowType.Jump);
        Assert.Equal(3, (int)CommunicationFlowType.Text);
        Assert.Equal(4, (int)CommunicationFlowType.Choice);
        Assert.Equal(5, (int)CommunicationFlowType.TextPlain);
        Assert.Equal(6, (int)CommunicationFlowType.SetFlag);
        Assert.Equal(7, (int)CommunicationFlowType.ModFlag);
        Assert.Equal(8, (int)CommunicationFlowType.AddInventory);
        Assert.Equal(9, (int)CommunicationFlowType.JumpFlagEqual);
        Assert.Equal(14, (int)CommunicationFlowType.JumpFlagLessOrEqual);
        Assert.Equal(15, (int)CommunicationFlowType.JumpInventoryEqual);
        Assert.Equal(20, (int)CommunicationFlowType.JumpInventoryLessOrEqual);
        Assert.Equal(21, (int)CommunicationFlowType.JumpCharacterHaveTag);
        Assert.Equal(22, (int)CommunicationFlowType.JumpUniqueItemHaveTag);
        Assert.Equal(23, (int)CommunicationFlowType.JumpRandom);
        Assert.Equal(24, (int)CommunicationFlowType.TextLog);
    }

    [Fact]
    public void The_sample_pack_has_the_greeting_with_a_default_flow()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Directory.GetParent(root)!.FullName;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(zip);
        Assert.Equal([new CommunicationEntry(1, 1, "인사하기", false)], pack.Communications);
        Assert.Empty(pack.CommunicationSoloFlows!);
        Assert.Empty(pack.CommunicationDuoFlows!);
        Assert.Equal(["Text", "TextLog", "Finish"], pack.CommunicationDefaultFlows!.Select(f => f.FlowType.ToString()));
    }
}
