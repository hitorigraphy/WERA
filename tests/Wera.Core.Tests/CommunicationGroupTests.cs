using Wera.Core;

namespace Wera.Core.Tests;

// communication_group_master(code, name; code가 키)와 교류 화면: 코드×100 항목, 코드 순서, 선택한 그룹은 선택할 수 없다.
public class CommunicationGroupTests
{
    static GamePack Pack(string? groups) =>
        GamePack.Load(CoreTests.PackZip(groups is null
            ? [("pack.tsv", "id\tdemo\ntitle\t데모\n")]
            : [("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.CommunicationGroupMasterPath, groups)]));

    static GameSession Interaction(string? groups)
    {
        var s = GameSession.NewGame(Pack(groups));
        s.Activate("104");
        Assert.Equal(Screen.Interaction, s.Screen);
        return s;
    }

    [Fact]
    public void The_master_is_optional_and_loads_code_and_name()
    {
        Assert.Null(Pack(null).CommunicationGroups);
        var pack = Pack("code\tname\tmemo\n1\t대화\t\n2\t선물\t메모\n");
        Assert.Equal([new CommunicationGroupEntry(1, "대화"), new CommunicationGroupEntry(2, "선물")], pack.CommunicationGroups);
        Assert.Equal(2, pack.Masters.CommunicationGroupRowTable.Count);
        Assert.True(pack.Masters.CommunicationGroupRowTable.TryFindByCode(2, out var row));
        Assert.Equal("선물", row.Name);
    }

    [Theory]
    [InlineData("code\tname\n1\t대화\n1\t선물\n", "중복")]
    [InlineData("code\tname\n0\t대화\n", "1 이상")]
    [InlineData("code\tname\n-3\t대화\n", "1 이상")]
    [InlineData("code\tname\n92233720368547759\t대화\n", "이하여야")]
    [InlineData("code\tname\nx\t대화\n", "정수")]
    public void Bad_masters_are_rejected(string groups, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Pack(groups)).Message);

    [Fact]
    public void Entering_the_screen_lists_groups_by_code_times_100_smallest_first_with_the_smallest_selected()
    {
        var s = Interaction("code\tname\n3\t교환\n1\t대화\n2\t선물\n");     // 파일 순서와 상관없이 코드 순서
        Assert.Equal(1, s.CurrentCommunicationGroupCode);
        Assert.Equal(
            [new ScreenEntry("100", "대화", Screen.Interaction, EntryAction.SelectCommunicationGroup, Enabled: false),
             new ScreenEntry("200", "선물", Screen.Interaction, EntryAction.SelectCommunicationGroup),
             new ScreenEntry("300", "교환", Screen.Interaction, EntryAction.SelectCommunicationGroup),
             new ScreenEntry("999", "돌아가기", Screen.Home)],
            s.Entries);
    }

    [Fact]
    public void Choosing_a_group_selects_it_and_stays_on_the_screen_and_the_chosen_one_is_inert()
    {
        var s = Interaction("code\tname\n1\t대화\n2\t선물\n");
        Assert.NotNull(s.Activate("200"));
        Assert.Equal(Screen.Interaction, s.Screen);
        Assert.Equal(2, s.CurrentCommunicationGroupCode);
        Assert.Equal([true, false], s.Entries.Where(e => e.Action == EntryAction.SelectCommunicationGroup).Select(e => e.Enabled));

        s.Activate("200");                                                          // 이미 고른 그룹은 눌러도 그대로
        Assert.Equal(2, s.CurrentCommunicationGroupCode);
        s.Activate("100");
        Assert.Equal(1, s.CurrentCommunicationGroupCode);
    }

    [Fact]
    public void Entering_again_selects_the_smallest_group_again()
    {
        var s = Interaction("code\tname\n1\t대화\n2\t선물\n");
        s.Activate("200");
        s.Activate("999");
        s.Activate("104");
        Assert.Equal(1, s.CurrentCommunicationGroupCode);
    }

    [Fact]
    public void The_slots_cannot_be_changed_on_this_screen()
    {
        var s = Interaction("code\tname\n1\t대화\n");
        Assert.Null(s.Activate("1"));                                               // 홈에서는 [1] 파트너, [2] 어시스트
        Assert.Null(s.Activate("2"));
        Assert.Equal(Screen.Interaction, s.Screen);
        Assert.Null(s.CurrentSupportRole);
    }

    [Fact]
    public void Without_the_master_only_back_is_there()
    {
        var s = Interaction(null);
        Assert.Null(s.CurrentCommunicationGroupCode);
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));
    }

    [Fact]
    public void The_sample_pack_has_the_talk_group()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Directory.GetParent(root)!.FullName;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var s = GameSession.NewGame(GamePack.Load(zip));
        Assert.Equal([new CommunicationGroupEntry(1, "대화")], s.Pack.CommunicationGroups);
        s.Activate("104");
        Assert.Equal("100", s.Entries[0].Code);
        Assert.Equal("대화", s.Entries[0].Label);
    }
}
