using Wera.Core;

namespace Wera.Core.Tests;

// communication_master(code, group_code, name; code가 키)와
// communication_flow_master(communication_code, character_code, flow_number, flow_type, param_1~3, text; 앞의 세 열이 키).
public class CommunicationMasterTests
{
    const string Groups = "code\tname\n1\t대화\n2\t선물\n";
    const string Comms = "code\tgroup_code\tname\n1\t1\t인사하기\n2\t2\t건네기\n";
    const string FlowHeader = "communication_code\tcharacter_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\n";

    static GamePack Load(string? groups = Groups, string? comms = Comms, string? flows = null)
    {
        var files = new List<(string, string)> { ("pack.tsv", "id\tdemo\ntitle\t데모\n") };
        if (groups is not null) files.Add((GamePack.CommunicationGroupMasterPath, groups));
        if (comms is not null) files.Add((GamePack.CommunicationMasterPath, comms));
        if (flows is not null) files.Add((GamePack.CommunicationFlowMasterPath, flows));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    static string Error(string? groups = Groups, string? comms = Comms, string? flows = null) =>
        Assert.Throws<PackFormatException>(() => Load(groups, comms, flows)).Message;

    [Fact]
    public void The_masters_are_optional()
    {
        var pack = Load(null, null);
        Assert.Null(pack.Communications);
        Assert.Null(pack.CommunicationFlows);
        Assert.Empty(pack.Masters.CommunicationRowTable.All);
        Assert.Empty(pack.Masters.CommunicationFlowRowTable.All);
    }

    [Fact]
    public void Communications_load_code_group_code_and_name()
    {
        var pack = Load();
        Assert.Equal([new CommunicationEntry(1, 1, "인사하기"), new CommunicationEntry(2, 2, "건네기")], pack.Communications);
        Assert.True(pack.Masters.CommunicationRowTable.TryFindByCode(2, out var row));
        Assert.Equal((2L, "건네기"), (row.GroupCode, row.Name));
        Assert.Equal([1L], pack.Masters.CommunicationRowTable.FindByGroupCode(1).Select(c => c.Code));
    }

    [Theory]
    [InlineData("code\tgroup_code\tname\n1\t1\ta\n1\t2\tb\n", "중복")]
    [InlineData("code\tgroup_code\tname\n1\t9\ta\n", "교류 그룹 마스터에 없습니다")]
    [InlineData("code\tgroup_code\tname\nx\t1\ta\n", "정수")]
    [InlineData("code\tgroup_code\tname\n1\t\ta\n", "비어")]
    public void Bad_communications_are_rejected(string comms, string message) =>
        Assert.Contains(message, Error(comms: comms));

    [Fact]
    public void Communications_need_the_group_master()
    {
        Assert.Contains(GamePack.CommunicationGroupMasterPath, Error(groups: null));
    }

    [Fact]
    public void Flows_need_the_communication_master()
    {
        Assert.Contains(GamePack.CommunicationMasterPath, Error(comms: null, flows: FlowHeader));
    }

    [Fact]
    public void Flow_rows_are_keyed_by_communication_character_and_flow_number()
    {
        var pack = Load(flows: FlowHeader +
            "1\t1\t2\tNone\t\t\t\t두 번째\n" +
            "1\t1\t1\tnone\t5\t6\t7\t첫째\n" +
            "2\t1\t1\tNone\t\t\t\t다른 교류\n");
        Assert.Equal(["첫째", "두 번째"], pack.CommunicationFlowsOf(1, 1).Select(f => f.Text));
        var first = pack.CommunicationFlowsOf(1, 1)[0];
        Assert.Equal(new CommunicationFlow(1, 1, 1, CommunicationFlowType.None, 5, 6, 7, "첫째"), first);
        Assert.Equal(["다른 교류"], pack.CommunicationFlowsOf(2, 1).Select(f => f.Text));
        Assert.Empty(pack.CommunicationFlowsOf(1, 99));
        Assert.True(pack.Masters.CommunicationFlowRowTable.TryFindByCommunicationCodeAndCharacterCodeAndFlowNumber((1, 1, 2), out var row));
        Assert.Equal("두 번째", row.Text);
    }

    [Theory]
    [InlineData("9\t1\t1\tNone\t\t\t\tx\n", "교류 마스터에 없습니다")]
    [InlineData("1\t4242\t1\tNone\t\t\t\tx\n", "캐릭터 인벤토리 마스터에 없습니다")]
    [InlineData("1\t1\t1\tBogus\t\t\t\tx\n", "알 수 없습니다")]
    [InlineData("1\t1\t1\t0\t\t\t\tx\n", "알 수 없습니다")]
    [InlineData("1\t1\t1\tNone\t\t\t\tx\n1\t1\t1\tNone\t\t\t\ty\n", "두 번")]
    [InlineData("1\t1\tx\tNone\t\t\t\tx\n", "정수")]
    [InlineData("1\t1\t1\tNone\tq\t\t\tx\n", "정수")]
    public void Bad_flows_are_rejected(string row, string message) =>
        Assert.Contains(message, Error(flows: FlowHeader + row));

    [Fact]
    public void A_flow_character_must_be_a_character_not_an_item()
    {
        // 인벤토리 코드 2는 메인 통화(커먼 아이템)다
        Assert.Contains("캐릭터 인벤토리 마스터에 없습니다", Error(flows: FlowHeader + "1\t2\t1\tNone\t\t\t\tx\n"));
    }

    [Fact]
    public void The_enum_numbers_are_fixed()
    {
        Assert.Equal(0, (int)CommunicationFlowType.None);
        Assert.Equal(["None"], Enum.GetNames<CommunicationFlowType>());
    }

    [Fact]
    public void The_sample_pack_has_the_greeting_and_an_empty_flow_master()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Directory.GetParent(root)!.FullName;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(zip);
        Assert.Equal([new CommunicationEntry(1, 1, "인사하기")], pack.Communications);
        Assert.Empty(pack.CommunicationFlows!);
    }
}
