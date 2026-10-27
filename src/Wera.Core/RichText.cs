using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Wera.Core;

/// <summary>꾸밈이 적용된 글자 덩어리 하나. Color는 "#rrggbb"(소문자) 또는 null(기본색).</summary>
public readonly record struct RichRun(string Text, bool Bold, string? Color);

/// <summary>
/// ADV 대사 같은 게임팩 글의 꾸밈 문법(BBCode 스타일). TSV 한 칸에는 탭과 실제 줄바꿈을 넣을 수 없어 개행은 <c>\n</c>으로 적는다.
/// <list type="bullet">
/// <item><c>\n</c> 줄바꿈. <c>\\</c> 글자 <c>\</c>, <c>\[</c> 글자 <c>[</c>, <c>\]</c> 글자 <c>]</c>. 그 밖의 <c>\</c> 뒤 글자는 오류다.</item>
/// <item><c>[b]…[/b]</c> 굵게.</item>
/// <item><c>[color=이름]…[/color]</c> 글자색. 이름은 <see cref="ColorNames"/>의 이름이거나 <c>#rgb</c>, <c>#rrggbb</c>.</item>
/// </list>
/// 태그는 올바르게 겹쳐야 하고(닫는 순서가 여는 순서의 반대), 모르는 태그, 닫히지 않은 태그, 이스케이프 없는 <c>[</c>는 오류다.
/// 화면에는 파서가 허용한 태그만 HTML로 바뀌어 나가므로 게임팩의 글이 HTML이나 스크립트로 해석되지 않는다.
/// </summary>
public sealed class RichText
{
    /// <summary>색 이름 → 색. 검은 배경에서 읽히는 색만 둔다.</summary>
    public static readonly IReadOnlyDictionary<string, string> ColorNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["red"] = "#ff5555", ["orange"] = "#ffaa33", ["yellow"] = "#ffee55", ["green"] = "#55dd66", ["cyan"] = "#55dddd",
        ["blue"] = "#6699ff", ["purple"] = "#bb77ff", ["pink"] = "#ff88cc", ["white"] = "#ffffff", ["gray"] = "#999999",
    };

    static readonly Regex HexColor = new("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.Compiled);

    /// <summary>줄 목록. 줄마다 꾸밈이 같은 글자 덩어리들(빈 줄은 덩어리가 없다).</summary>
    public IReadOnlyList<IReadOnlyList<RichRun>> Lines { get; }

    RichText(List<List<RichRun>> lines) => Lines = lines.Select(l => (IReadOnlyList<RichRun>)l).ToList();

    /// <summary>꾸밈을 모두 뗀 글(줄은 \n으로 이은 실제 줄바꿈).</summary>
    public string PlainText => string.Join("\n", Lines.Select(line => string.Concat(line.Select(r => r.Text))));

    /// <summary>문법이 맞으면 파싱한 글, 아니면 null과 오류 설명.</summary>
    public static RichText? TryParse(string source, out string? error)
    {
        var lines = new List<List<RichRun>> { new() };
        var open = new List<(string Tag, bool Bold, string? Color)>();      // 열려 있는 태그(바깥부터)
        bool bold = false;
        string? color = null;
        var text = new StringBuilder();

        void Flush()
        {
            if (text.Length == 0) return;
            lines[^1].Add(new RichRun(text.ToString(), bold, color));
            text.Clear();
        }

        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '\\')
            {
                if (i + 1 >= source.Length) return Fail($"글 끝의 \\ 뒤에 글자가 없습니다.", out error);
                var next = source[++i];
                switch (next)
                {
                    case 'n': Flush(); lines.Add([]); break;
                    case '\\': case '[': case ']': text.Append(next); break;
                    default: return Fail($"알 수 없는 이스케이프 '\\{next}'입니다(\\n, \\\\, \\[, \\]만 쓸 수 있습니다).", out error);
                }
            }
            else if (c == '[')
            {
                var end = source.IndexOf(']', i);
                if (end < 0) return Fail($"닫히지 않은 '['가 있습니다(글자 [는 \\[로 적습니다).", out error);
                var tag = source[(i + 1)..end];
                i = end;
                Flush();
                if (tag.Equals("b", StringComparison.OrdinalIgnoreCase))
                {
                    open.Add(("b", bold, color));
                    bold = true;
                }
                else if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
                {
                    var hex = ResolveColor(tag[6..]);
                    if (hex is null)
                        return Fail($"알 수 없는 색 '{tag[6..]}'입니다(이름: {string.Join(", ", ColorNames.Keys)} 또는 #rgb, #rrggbb).", out error);
                    open.Add(("color", bold, color));
                    color = hex;
                }
                else if (tag.Length > 1 && tag[0] == '/' && (tag[1..].Equals("b", StringComparison.OrdinalIgnoreCase) || tag[1..].Equals("color", StringComparison.OrdinalIgnoreCase)))
                {
                    var name = tag[1..].ToLowerInvariant();
                    if (open.Count == 0 || open[^1].Tag != name)
                        return Fail($"[{tag}]가 여는 태그와 짝이 맞지 않습니다(태그는 열린 순서의 반대로 닫아야 합니다).", out error);
                    (_, bold, color) = open[^1];
                    open.RemoveAt(open.Count - 1);
                }
                else
                    return Fail($"알 수 없는 태그 [{tag}]입니다(b, color만 쓸 수 있고, 글자 [는 \\[로 적습니다).", out error);
            }
            else
                text.Append(c);
        }

        Flush();
        if (open.Count > 0) return Fail($"[{open[^1].Tag}]가 닫히지 않았습니다.", out error);
        error = null;
        return new RichText(lines);
    }

    static RichText? Fail(string message, out string? error)
    {
        error = message;
        return null;
    }

    static string? ResolveColor(string value)
    {
        if (ColorNames.TryGetValue(value, out var named)) return named;
        if (!HexColor.IsMatch(value)) return null;
        var digits = value[1..].ToLowerInvariant();
        return digits.Length == 3 ? "#" + string.Concat(digits.Select(d => $"{d}{d}")) : "#" + digits;
    }

    /// <summary>화면에 낼 HTML. 글자는 모두 이스케이프하고, 태그는 b와 색 span, 줄바꿈 br만 만든다.</summary>
    public string ToHtml()
    {
        var html = new StringBuilder();
        for (var i = 0; i < Lines.Count; i++)
        {
            if (i > 0) html.Append("<br>");
            foreach (var run in Lines[i])
            {
                var text = WebUtility.HtmlEncode(run.Text);
                if (run.Color is { } color) text = $"<span style=\"color:{color}\">{text}</span>";
                if (run.Bold) text = $"<b>{text}</b>";
                html.Append(text);
            }
        }
        return html.ToString();
    }

    /// <summary>
    /// 칸 수 폭의 상자에 글을 넣었을 때의 줄 수. 줄마다 글자를 앞에서부터 채우다가 넘치는 글자부터 다음 줄로 내려간다
    /// (화면의 줄바꿈 규칙과 같다: 어느 글자에서나 줄을 바꾼다). 빈 줄도 한 줄이다.
    /// </summary>
    public int CountLines(int columns)
    {
        var total = 0;
        foreach (var line in Lines)
        {
            var rows = 1;
            var used = 0;
            foreach (var run in line)
                foreach (var rune in run.Text.EnumerateRunes())
                {
                    var width = ColumnsOf(rune);
                    if (used + width > columns)
                    {
                        rows++;
                        used = 0;
                    }
                    used += width;
                }
            total += rows;
        }
        return total;
    }

    /// <summary>글자 하나가 차지하는 칸 수: 한글, 한자, 일본어, 전각 문자, 이모지는 2칸, 나머지는 1칸.</summary>
    public static int ColumnsOf(Rune rune)
    {
        var v = rune.Value;
        return v is >= 0x1100 and <= 0x115F                // 한글 자모
            or >= 0x2E80 and <= 0x303E                     // CJK 부수, 기호
            or >= 0x3041 and <= 0x33FF                     // 가나, 호환 자모, CJK 기호
            or >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF                     // 한자
            or >= 0xA960 and <= 0xA97F
            or >= 0xAC00 and <= 0xD7A3                     // 한글 음절
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE6F
            or >= 0xFF00 and <= 0xFF60                     // 전각
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x1F300 and <= 0x1FAFF                   // 이모지
            or >= 0x20000 and <= 0x3FFFD
            ? 2 : 1;
    }
}

/// <summary>
/// ADV 대사 상자의 크기 계산. 화면 너비는 960px로 고정이라 한 줄에 들어가는 칸 수가 늘 같다.
/// 아래 값은 CSS(app.css의 main, .adv-line, .adv-box)와 같아야 한다.
/// 한 칸은 고정폭 글꼴의 영문 한 글자 폭이고(16px 글꼴의 0.6em = 9.6px, 흔한 고정폭 글꼴 중 가장 넓은 편), 한글은 2칸이다.
/// </summary>
public static class AdvLayout
{
    public const double PageWidthPx = 960;
    const double PagePaddingPx = 32;      // main의 좌우 padding 1rem씩
    const double IconPx = 96;             // 슬롯과 같은 이미지: 줄 높이(24px)의 4배
    const double GapPx = 8;               // 이미지와 상자 사이 .5rem
    const double BorderPx = 2;            // 상자 테두리 1px씩
    const double ColumnPx = 9.6;
    const double BoxPaddingColumns = 2;   // 상자 안쪽 좌우 1ch씩

    /// <summary>대사 상자 안에 한 줄로 들어가는 칸 수.</summary>
    public static int BoxColumns => (int)Math.Floor((PageWidthPx - PagePaddingPx - IconPx - GapPx - BorderPx) / ColumnPx - BoxPaddingColumns);

    /// <summary>가로 전체 상자(TextPlain) 안에 한 줄로 들어가는 칸 수: 아이콘 자리와 간격이 없어 페이지 폭 전체에서 테두리와 안쪽 여백만 뺀다.</summary>
    public static int PlainBoxColumns => (int)Math.Floor((PageWidthPx - PagePaddingPx - BorderPx) / ColumnPx - BoxPaddingColumns);

    /// <summary>대사 상자에 들어가는 줄 수(상자 높이는 이미지와 같은 4줄 높이이고 위쪽 반 줄은 이름 자리라 3줄).</summary>
    public const int BoxLines = 3;
}
