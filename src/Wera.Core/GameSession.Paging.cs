using System.Globalization;
using System.Text.RegularExpressions;

namespace Wera.Core;

/// <summary>페이지 버튼 한 칸: 화면에는 "[Text]"로 나오고(Code가 null이면 "..."), 지금 페이지는 Current.</summary>
public sealed record PagerButton(string Text, string? Code, bool Current);

// 목록 화면(동료/장비/아이템 확인, 캐릭터/파트너 고르기, 장비 고르기, 상점/교환, 이벤트, 디버그 추가)은 한 페이지에 20개까지만 보여 주고
// 나머지는 하단의 페이지 버튼(게시판처럼 [1]...[N-2][N-1][N][N+1][N+2]...[마지막])으로 본다. 페이지 버튼의 코드는 "P" + 페이지 번호다(항목 코드와 겹치지 않게).
public sealed partial class GameSession
{
    /// <summary>목록 한 페이지에 나오는 항목 수.</summary>
    public const int ListPageSize = 20;

    /// <summary>목록 화면에서 보고 있는 페이지(0부터). 목록 화면에 들어올 때마다 0으로 돌아가고, 상세 화면에서 돌아올 때는 그대로다.</summary>
    public int ListPage { get; private set; }

    static readonly Regex PagerCode = new(@"^[Pp](\d+)$");

    static bool IsPagedScreen(Screen screen) => screen is Screen.CompanionCheck or Screen.EquipmentCheck or Screen.ItemCheck
        or Screen.CharacterSelect or Screen.SupportSelect or Screen.EquipmentSelect or Screen.ShopList or Screen.ShopTrades or Screen.Event;

    /// <summary>페이지와 상관없이 늘 나오는 항목: 돌아가기와, 고르는 화면의 "[0] 비우기/해제"(동료 확인의 0번 줄은 그냥 항목이다).</summary>
    bool IsFixedEntry(ScreenEntry e) =>
        e.Code == BackEntryCode || e.Code == ClearEntryCode && Screen is Screen.CharacterSelect or Screen.SupportSelect or Screen.EquipmentSelect;

    /// <summary>항목이 count개일 때의 페이지 수(항목이 없어도 1).</summary>
    public static int PageCountOf(int count) => Math.Max(1, (count + ListPageSize - 1) / ListPageSize);

    int ClampedPage(int count) => ListPage = Math.Clamp(ListPage, 0, PageCountOf(count) - 1);

    /// <summary>전체 목록(화면에 나오는 순서)에서 지금 페이지의 항목들. 화면이 항목 줄 대신 목록을 직접 그릴 때 쓴다.</summary>
    public IReadOnlyList<T> PageOf<T>(IReadOnlyList<T> all) => all.Skip(ClampedPage(all.Count) * ListPageSize).Take(ListPageSize).ToList();

    /// <summary>페이지로 자를 목록의 항목 코드들(화면에 나오는 순서).</summary>
    IReadOnlyList<string> PageKeys(IReadOnlyList<ScreenEntry> raw) => Screen switch
    {
        Screen.CharacterSelect => CharacterList.Select(c => Text(c.Code)).ToList(),
        Screen.SupportSelect => SupportList.Select(c => Text(c.Code)).ToList(),
        _ => raw.Where(e => !IsFixedEntry(e)).Select(e => e.Code).ToList(),
    };

    IReadOnlyList<ScreenEntry> Paginate(IReadOnlyList<ScreenEntry> raw)
    {
        if (!IsPagedScreen(Screen)) return raw;
        var keys = PageKeys(raw);
        var onPage = PageOf(keys).ToHashSet();
        var back = raw.Where(e => e.Code == BackEntryCode);
        return [.. raw.Where(e => e.Code != BackEntryCode && (IsFixedEntry(e) || onPage.Contains(e.Code))), .. PagerEntries(keys.Count), .. back];
    }

    /// <summary>하단의 페이지 버튼 줄: 첫 페이지, 지금 페이지 앞뒤 두 쪽, 마지막 페이지를 보이고 떨어진 사이는 "..."로 둔다. 페이지가 하나뿐이면 비어 있다.</summary>
    public IReadOnlyList<PagerButton> Pager
    {
        get
        {
            if (Screen is not (Screen.DebugAddCharacter or Screen.DebugAddEquipment or Screen.DebugAddItem or Screen.DebugSkillCharacter or Screen.DebugAddSkill)
                && !IsPagedScreen(Screen)) return [];
            return PagerFor(IsPagedScreen(Screen) ? PageKeys(RawEntries).Count : DebugList.Count);
        }
    }

    IReadOnlyList<PagerButton> PagerFor(int count)
    {
        var pages = PageCountOf(count);
        var current = ClampedPage(count) + 1;
        if (pages <= 1) return [];
        var shown = new SortedSet<int> { 1, pages };
        for (var p = current - 2; p <= current + 2; p++)
            if (p >= 1 && p <= pages) shown.Add(p);
        var buttons = new List<PagerButton>();
        var previous = 0;
        foreach (var p in shown)
        {
            if (p > previous + 1) buttons.Add(new PagerButton("...", null, false));
            buttons.Add(new PagerButton(p.ToString(CultureInfo.InvariantCulture), "P" + p.ToString(CultureInfo.InvariantCulture), p == current));
            previous = p;
        }
        return buttons;
    }

    IReadOnlyList<ScreenEntry> PagerEntries(int count) => PagerFor(count)
        .Where(b => b.Code is not null)
        .Select(b => new ScreenEntry(b.Code!, b.Text, Screen, EntryAction.ListPage, b.Text, Enabled: !b.Current))
        .ToList();

    /// <summary>입력한 "P페이지 번호"에 해당하는 페이지 항목: 보이지 않는 페이지도 코드로는 갈 수 있다. 목록 화면이 아니거나 없는 페이지면 null.</summary>
    ScreenEntry? PagerEntryFor(string code)
    {
        if (PagerCode.Match(code.Trim()) is not { Success: true } m || !int.TryParse(m.Groups[1].Value, out var page)) return null;
        int count;
        if (IsPagedScreen(Screen)) count = PageKeys(RawEntries).Count;
        else if (Screen is Screen.DebugAddCharacter or Screen.DebugAddEquipment or Screen.DebugAddItem or Screen.DebugSkillCharacter or Screen.DebugAddSkill) count = DebugList.Count;
        else return null;
        return page >= 1 && page <= PageCountOf(count)
            ? new ScreenEntry("P" + page.ToString(CultureInfo.InvariantCulture), page.ToString(CultureInfo.InvariantCulture), Screen, EntryAction.ListPage,
                page.ToString(CultureInfo.InvariantCulture), Enabled: page - 1 != ListPage)
            : null;
    }
}
