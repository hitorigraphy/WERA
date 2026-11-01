using Wera.Core;

namespace Wera.Core.Tests;

// 목록 화면은 한 페이지에 20개까지만 보여 주고, 하단의 페이지 버튼([1]...[N-2][N-1][N][N+1][N+2]...[마지막])으로 나머지를 본다.
public class PagerTests
{
    // 캐릭터 1(당신)과 7001~7000+extra: 합계 1+extra명
    static GameSession SessionWith(int owned)
    {
        var rows = "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n" +
            string.Concat(Enumerable.Range(7001, owned - 1).Select(c => $"{c}\t용사{c}\t1\t1\t2\n"));
        var pack = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/character_inventory_master_v1.tsv", rows)));
        var s = GameSession.NewGame(pack);
        foreach (var c in pack.CharacterInventory!) s.AddCharacter(c.Code);
        return s;
    }

    static string Layout(GameSession s) => string.Concat(s.Pager.Select(b => b.Code is null ? "..." : $"[{b.Text}]"));

    [Fact]
    public void A_list_up_to_twenty_has_one_page_and_no_pager()
    {
        var s = SessionWith(20);
        s.Activate("201");
        Assert.Empty(s.Pager);
        Assert.Equal(20, s.Entries.Count(e => e.Action == EntryAction.OpenDetail));
        Assert.DoesNotContain(s.Entries, e => e.Action == EntryAction.ListPage);
    }

    [Fact]
    public void A_longer_list_shows_twenty_per_page_and_the_pager_below_it()
    {
        var s = SessionWith(45);
        s.Activate("201");
        Assert.Equal(20, s.PageOf(s.Companions).Count);
        Assert.Equal(20, s.Entries.Count(e => e.Action == EntryAction.OpenDetail));
        Assert.Equal("[1][2][3]", Layout(s));
        Assert.Equal("999", s.Entries[^1].Code);                                    // 돌아가기는 늘 맨 끝

        s.Activate("P3");
        Assert.Equal(5, s.PageOf(s.Companions).Count);                              // 마지막 페이지는 나머지 5명
        Assert.Equal(5, s.Entries.Count(e => e.Action == EntryAction.OpenDetail));
        Assert.Equal(s.PageOf(s.Companions).Select(c => c.Order.Trim('[', ']')), s.Entries.Where(e => e.Action == EntryAction.OpenDetail).Select(e => e.Code));
    }

    [Fact]
    public void The_pager_shows_the_first_page_two_pages_either_side_and_the_last_page_with_dots_between()
    {
        var s = SessionWith(20 * 12);                                                // 12쪽
        s.Activate("201");
        Assert.Equal("[1][2][3]...[12]", Layout(s));
        s.Activate("P2");
        Assert.Equal("[1][2][3][4]...[12]", Layout(s));
        s.Activate("P6");
        Assert.Equal("[1]...[4][5][6][7][8]...[12]", Layout(s));
        Assert.Equal([false, false, false, true, false, false, false], s.Pager.Where(b => b.Code is not null).Select(b => b.Current));
        s.Activate("P11");
        Assert.Equal("[1]...[9][10][11][12]", Layout(s));
        s.Activate("P12");
        Assert.Equal("[1]...[10][11][12]", Layout(s));
    }

    [Fact]
    public void Any_page_can_be_typed_and_entering_the_list_again_starts_on_the_first_page()
    {
        var s = SessionWith(100);
        s.Activate("201");
        s.Activate("P5");                                                            // 보이지 않는 페이지도 코드로 간다
        Assert.Equal(4, s.ListPage);
        Assert.Null(s.Activate("P6"));
        Assert.Equal(4, s.ListPage);

        s.Activate(s.Entries.First(e => e.Action == EntryAction.OpenDetail).Code);   // 상세에서 돌아오면 보던 페이지
        Assert.Equal(Screen.CharacterDetail, s.Screen);
        s.Activate("999");
        Assert.Equal(4, s.ListPage);
        s.Activate("999");                                                           // 목록에서 나갔다 다시 들어오면 첫 페이지
        s.Activate("201");
        Assert.Equal(0, s.ListPage);
    }

    [Fact]
    public void Pages_use_the_whole_list_for_the_character_picker_and_the_support_picker_too()
    {
        var s = SessionWith(45);
        s.Activate("1");                                                             // 홈의 파트너 슬롯: 지정할 캐릭터 고르기
        Assert.Equal(Screen.SupportSelect, s.Screen);
        Assert.Equal(20, s.PageOf(s.SupportList).Count);
        Assert.Equal("[1][2][3]", Layout(s));
        s.Activate("P2");
        Assert.Equal(s.PageOf(s.SupportList).Where(c => c.Selectable).Select(c => c.Code.ToString()),
            s.Entries.Where(e => e.Action == EntryAction.SetSupport).Select(e => e.Code));
        Assert.NotEmpty(s.Entries.Where(e => e.Action == EntryAction.SetSupport));
    }
}
