using Wera.Core;

namespace Wera.Core.Tests;

public class RichTextTests
{
    static RichText Parse(string source)
    {
        var rich = RichText.TryParse(source, out var error);
        Assert.True(rich is not null, error);
        return rich!;
    }

    static string Error(string source)
    {
        Assert.Null(RichText.TryParse(source, out var error));
        return error!;
    }

    [Fact]
    public void Plain_text_is_one_line_with_one_run()
    {
        var rich = Parse("안녕하세요");
        Assert.Single(rich.Lines);
        Assert.Equal([new RichRun("안녕하세요", false, null)], rich.Lines[0]);
        Assert.Equal("안녕하세요", rich.PlainText);
    }

    [Fact]
    public void Backslash_n_breaks_the_line_and_an_empty_line_stays()
    {
        var rich = Parse(@"첫째\n\n셋째");
        Assert.Equal(["첫째", "", "셋째"], rich.PlainText.Split('\n'));
        Assert.Empty(rich.Lines[1]);
    }

    [Fact]
    public void Bold_and_color_apply_to_the_text_between_the_tags_and_may_nest()
    {
        var rich = Parse("가[b]나[color=red]다[/color]라[/b]마");
        Assert.Equal(
            [
                new RichRun("가", false, null),
                new RichRun("나", true, null),
                new RichRun("다", true, RichText.ColorNames["red"]),
                new RichRun("라", true, null),
                new RichRun("마", false, null),
            ],
            rich.Lines[0]);
        Assert.Equal("가나다라마", rich.PlainText);
    }

    [Fact]
    public void Colors_can_be_names_or_hex_and_are_normalised()
    {
        Assert.Equal("#aabbcc", Parse("[color=#AABBCC]x[/color]").Lines[0][0].Color);
        Assert.Equal("#ffcc00", Parse("[color=#fc0]x[/color]").Lines[0][0].Color);
        Assert.Equal(RichText.ColorNames["green"], Parse("[COLOR=Green]x[/COLOR]").Lines[0][0].Color);
    }

    [Fact]
    public void A_style_continues_across_a_line_break_and_closing_restores_the_outer_style()
    {
        var rich = Parse(@"[b]가\n나[/b]다");
        Assert.True(rich.Lines[0][0].Bold);
        Assert.True(rich.Lines[1][0].Bold);
        Assert.False(rich.Lines[1][1].Bold);
    }

    [Fact]
    public void Escapes_give_literal_characters()
    {
        Assert.Equal(@"[b]\]", Parse(@"\[b\]\\\]").PlainText);
    }

    [Theory]
    [InlineData("[i]x[/i]", "알 수 없는 태그")]
    [InlineData("[color=nope]x[/color]", "알 수 없는 색")]
    [InlineData("[color=#12]x[/color]", "알 수 없는 색")]
    [InlineData("[color=black]x[/color]", "알 수 없는 색")]
    [InlineData("[b]x", "닫히지 않았습니다")]
    [InlineData("x[/b]", "짝이 맞지 않습니다")]
    [InlineData("[b][color=red]x[/b][/color]", "짝이 맞지 않습니다")]
    [InlineData("[b[b]", "알 수 없는 태그")]
    [InlineData("대괄호 [ 하나", "닫히지 않은 '['")]
    [InlineData(@"끝의 \", "뒤에 글자가 없습니다")]
    [InlineData(@"\q", "알 수 없는 이스케이프")]
    [InlineData("[ 이름 ]", "알 수 없는 태그")]
    public void Bad_markup_is_rejected_with_a_reason(string source, string reason) =>
        Assert.Contains(reason, Error(source));

    [Fact]
    public void Html_output_escapes_the_text_and_only_emits_the_allowed_tags()
    {
        var rich = Parse(@"<script>alert(1)</script> & ""따옴표""\n[b][color=red]x[/color][/b]");
        var html = rich.ToHtml();
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&amp;", html);
        Assert.Contains("<br>", html);
        Assert.Contains("<b><span style=\"color:#ff5555\">x</span></b>", html);
    }

    // ---- 대사 상자 크기 ----

    [Fact]
    public void The_box_is_83_columns_wide_at_the_fixed_960px_page_and_three_lines_tall()
    {
        Assert.Equal(83, AdvLayout.BoxColumns);
        Assert.Equal(3, AdvLayout.BoxLines);
    }

    [Fact]
    public void Latin_counts_one_column_and_hangul_two()
    {
        Assert.Equal(1, Parse(new string('a', 83)).CountLines(83));
        Assert.Equal(2, Parse(new string('a', 84)).CountLines(83));
        Assert.Equal(1, Parse(new string('가', 41)).CountLines(83));        // 82칸
        Assert.Equal(2, Parse(new string('가', 42)).CountLines(83));        // 84칸
    }

    [Fact]
    public void A_wide_character_that_does_not_fit_goes_to_the_next_line_whole()
    {
        Assert.Equal(1, Parse(new string('가', 41) + "a").CountLines(83));  // 83칸에 딱 맞는다
        Assert.Equal(2, Parse(new string('가', 41) + "나").CountLines(83)); // 85칸이 되므로 '나'가 다음 줄로
    }

    [Fact]
    public void Tags_take_no_room_and_every_explicit_line_counts_even_when_empty()
    {
        Assert.Equal(1, Parse("[b][color=red]" + new string('a', 83) + "[/color][/b]").CountLines(83));
        Assert.Equal(3, Parse(@"가\n\n나").CountLines(83));
        Assert.Equal(4, Parse(@"가\n나\n다\n라").CountLines(83));
        Assert.Equal(5, Parse(new string('a', 84) + @"\n" + new string('b', 84) + @"\n").CountLines(83));
    }
}

// 게임팩 로드 때 대사 검증
public class AdvTextValidationTests
{
    static GamePack Load(string type, string text)
    {
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t인사\n"),
            ("master/adv_flow_master_v1.tsv",
                "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + $"4001\t1\t{type}\t0\t0\t0\t{text}\t\n"),
        };
        return GamePack.Load(CoreTests.PackZip(files));
    }

    static string Error(string type, string text) =>
        Assert.Throws<PackFormatException>(() => Load(type, text)).Message;

    [Theory]
    [InlineData("TextMe")]
    [InlineData("TextYou")]
    public void A_speech_box_holds_three_lines_but_not_four(string type)
    {
        Assert.NotNull(Load(type, @"하나\n둘\n셋"));
        Assert.NotNull(Load(type, new string('가', 41) + new string('나', 41) + new string('다', 41)));         // 줄마다 41자 = 82칸
        Assert.Contains("4줄입니다(최대 3줄", Error(type, @"하나\n둘\n셋\n넷"));
        Assert.Contains("4줄입니다", Error(type, new string('a', 83) + new string('b', 83) + new string('c', 83) + "d"));
        Assert.Contains("4줄입니다", Error(type, new string('가', 42) + new string('나', 42) + new string('다', 42) + "라"));
    }

    [Theory]
    [InlineData("TextMe")]
    [InlineData("TextYou")]
    [InlineData("TextPlain")]
    [InlineData("TextLog")]
    public void Bad_markup_fails_the_load_with_the_location(string type)
    {
        var message = Error(type, "[i]기울임[/i]");
        Assert.Contains("ADV '4001'의 흐름 1", message);
        Assert.Contains("알 수 없는 태그", message);
        Assert.Contains("[b]가", Load(type, "[b]가[/b]").FindAdvFlow(4001, 1)!.Text);
    }

    [Fact]
    public void A_log_line_has_no_box_so_it_may_run_past_three_lines()
    {
        Assert.NotNull(Load("TextLog", @"하나\n둘\n셋\n넷\n다섯"));
        Assert.NotNull(Load("TextLog", new string('a', 500)));
    }

    [Fact]
    public void A_plain_text_box_spans_the_full_width_and_holds_three_lines()
    {
        Assert.Equal(94, AdvLayout.PlainBoxColumns);
        Assert.NotNull(Load("TextPlain", @"하나\n둘\n셋"));
        Assert.NotNull(Load("TextPlain", new string('가', 47) + new string('나', 47) + new string('다', 47)));      // 줄마다 47자 = 94칸
        Assert.Contains("4줄입니다(최대 3줄, 한 줄 94칸", Error("TextPlain", @"하나\n둘\n셋\n넷"));
        Assert.Contains("4줄입니다", Error("TextPlain", new string('a', 95) + new string('b', 95) + new string('c', 95) + "d"));
        Assert.Contains("4줄입니다", Error("TextPlain", new string('가', 48) + new string('나', 48) + new string('다', 48) + "라"));
    }

    [Theory]
    [InlineData("TextLog")]
    [InlineData("TextPlain")]
    public void Plain_and_log_text_need_text(string type) =>
        Assert.Contains("text가 있어야", Error(type, " "));

    [Fact]
    public void A_step_exposes_the_parsed_text()
    {
        var pack = Load("TextMe", @"[b]굵게[/b]\n보통");
        var step = new AdvExecution(pack, 4001, new Dictionary<long, long>()).Advance();
        Assert.Equal("굵게\n보통", step.Rich.PlainText);
        Assert.Contains("<b>굵게</b><br>보통", step.Rich.ToHtml());
    }
}
