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

    /// <summary>"[101] 1 ... [102] 4 [103] 5 6 [104] 7 [105] 8 ... [106] 12"처럼: 코드가 있는 버튼은 "[코드] 쪽", 지금 쪽과 "..."는 글자만.</summary>
    static string Layout(GameSession s) => string.Join(" ", s.Pager.Select(b => b.Code is null ? b.Text : $"[{b.Code}] {b.Text}"));

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
        Assert.Equal("1 [104] 2 [106] 3", Layout(s));
        Assert.Equal(["104", "105", "106"], s.Entries.Where(e => e.Action == EntryAction.ListPage).Select(e => e.Code));
        Assert.Equal("999", s.Entries[^1].Code);                                    // 돌아가기는 늘 맨 끝

        s.Activate("106");                                                          // 마지막 페이지
        Assert.Equal(5, s.PageOf(s.Companions).Count);                              // 나머지 5명
        Assert.Equal(5, s.Entries.Count(e => e.Action == EntryAction.OpenDetail));
        Assert.Equal(s.PageOf(s.Companions).Select(c => c.Order.Trim('[', ']')), s.Entries.Where(e => e.Action == EntryAction.OpenDetail).Select(e => e.Code));
        Assert.Equal("[101] 1 [103] 2 3", Layout(s));
    }

    [Fact]
    public void The_pager_codes_are_fixed_by_position_and_dots_fill_the_gaps()
    {
        var s = SessionWith(20 * 12);                                                // 12쪽
        s.Activate("201");
        Assert.Equal("1 [104] 2 [105] 3 ... [106] 12", Layout(s));
        s.Activate("104");                                                           // 2쪽
        Assert.Equal("[101] 1 2 [104] 3 [105] 4 ... [106] 12", Layout(s));
        s.Activate("105"); s.Activate("105"); s.Activate("104");                     // +2, +2, +1: 2쪽에서 4쪽, 6쪽, 7쪽
        Assert.Equal(7, s.ListPage + 1);
        s.Activate("103");                                                           // 6쪽
        Assert.Equal("[101] 1 ... [102] 4 [103] 5 6 [104] 7 [105] 8 ... [106] 12", Layout(s));
        Assert.Equal([false, false, false, true, false, false, false], s.Pager.Where(b => b.Text != "...").Select(b => b.Current));
        s.Activate("105"); s.Activate("105"); s.Activate("104");                     // 8쪽, 10쪽, 11쪽
        Assert.Equal("[101] 1 ... [102] 9 [103] 10 11 [106] 12", Layout(s));
        s.Activate("106");
        Assert.Equal("[101] 1 ... [102] 10 [103] 11 12", Layout(s));
        s.Activate("101");
        Assert.Equal(0, s.ListPage);
    }

    [Fact]
    public void A_page_button_code_typed_wins_over_an_item_with_the_same_code_but_clicking_the_item_still_opens_it()
    {
        var s = SessionWith(120);                                                    // 동료 번호 0~119, 6쪽(100~119)에는 101~106도 항목이다
        s.Activate("201");
        s.Activate("106");
        Assert.Equal(5, s.ListPage);
        Assert.Contains(s.Entries, e => e.Action == EntryAction.OpenDetail && e.Code == "103");
        s.Activate("103", typed: false);                                             // 눌렀을 때는 103번 동료의 상세
        Assert.Equal(Screen.CharacterDetail, s.Screen);
        s.Activate("999");
        s.Activate("103");                                                           // 입력했을 때는 페이지 버튼: 5-1... 지금(6)-1 = 5쪽
        Assert.Equal(Screen.CompanionCheck, s.Screen);
        Assert.Equal(4, s.ListPage);
    }

    [Fact]
    public void Entering_the_list_again_starts_on_the_first_page_and_a_detail_keeps_the_page()
    {
        var s = SessionWith(100);
        s.Activate("201");
        s.Activate("106");
        Assert.Equal(4, s.ListPage);

        s.Activate(s.Entries.First(e => e.Action == EntryAction.OpenDetail).Code, typed: false);   // 상세에서 돌아오면 보던 페이지
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
        Assert.Equal("1 [104] 2 [106] 3", Layout(s));
        s.Activate("104");
        Assert.Equal(s.PageOf(s.SupportList).Where(c => c.Selectable).Select(c => c.Code.ToString()),
            s.Entries.Where(e => e.Action == EntryAction.SetSupport).Select(e => e.Code));
        Assert.NotEmpty(s.Entries.Where(e => e.Action == EntryAction.SetSupport));
    }

    [Fact]
    public void The_skill_pool_and_the_slot_lists_are_paged_tables_too()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(read);
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);
        s.Party[0] = 7001;
        s.Characters[7001] = s.Characters[7001] with { SkillPool = [.. Enumerable.Range(0, 45).Select(_ => pack.NewSkillInstance(1001))] };
        s.Activate("105"); s.Activate("103"); s.Activate("1");                      // 스킬 설정: 1번 슬롯의 캐릭터
        Assert.Equal(Screen.SkillCharacter, s.Screen);
        Assert.Empty(s.Pager);                                                       // 스킬 슬롯은 20개 이하
        s.Activate(GameSession.SkillPoolEntryCode);
        Assert.Equal(Screen.SkillPoolList, s.Screen);
        Assert.Equal(20, s.PageOf(s.SkillPoolRows).Count);
        Assert.Equal("1 [104] 2 [106] 3", Layout(s));
        s.Activate("106");
        Assert.Equal([40, 41, 42, 43, 44], s.PageOf(s.SkillPoolRows).Select(r => r.Index));
        Assert.Equal("999", s.Entries[^1].Code);
    }
}
