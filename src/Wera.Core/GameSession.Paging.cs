using System.Globalization;

namespace Wera.Core;

/// <summary>페이지 버튼 한 칸: 화면에는 "[Code] Text"로 나오고, 코드 없이 지금 페이지(Current)이거나 "..."(<see cref="Ellipsis"/>)인 칸도 있다.</summary>
public sealed record PagerButton(string Text, string? Code, bool Current)
{
    public const string Ellipsis = "...";
}

// 목록 화면(동료/장비/아이템 확인, 캐릭터/파트너 고르기, 장비/스킬 슬롯과 고르기, 스킬 풀, 던전/파티 고르기, 상점/교환, 이벤트, 디버그 추가)은 한 페이지에 20개까지만 보여 주고
// 나머지는 하단의 페이지 버튼(게시판처럼 [101] 1 ... [102] N-2 [103] N-1 N [104] N+1 [105] N+2 ... [106] 마지막)으로 본다.
// 코드는 자리마다 정해져 있다: 101 첫 페이지, 102 지금-2, 103 지금-1, 104 지금+1, 105 지금+2, 106 마지막 페이지(지금 페이지에는 코드가 없다).
public sealed partial class GameSession
{
    /// <summary>목록 한 페이지에 나오는 항목 수.</summary>
    public int ListPageSize => Pack.ListPageSize;

    /// <summary>목록 화면에서 보고 있는 페이지(0부터). 목록 화면에 들어올 때마다 0으로 돌아가고, 상세 화면에서 돌아올 때는 그대로다.</summary>
    public int ListPage { get; private set; }

    /// <summary>페이지 버튼의 코드와 그 코드가 가는 페이지(지금 페이지 current, 전체 pages쪽)를 정하는 규칙.</summary>
    static readonly (string Code, Func<int, int, int> Page)[] PagerRoles =
    [
        ("101", (current, pages) => 1),
        ("102", (current, pages) => current - 2),
        ("103", (current, pages) => current - 1),
        ("104", (current, pages) => current + 1),
        ("105", (current, pages) => current + 2),
        ("106", (current, pages) => pages),
    ];

    static bool IsPagedScreen(Screen screen) => screen is Screen.CompanionCheck or Screen.EquipmentCheck or Screen.ItemCheck
        or Screen.CharacterSelect or Screen.SupportSelect or Screen.EquipmentSelect or Screen.ShopList or Screen.ShopTrades or Screen.Event
        or Screen.EquipmentList or Screen.SkillCharacter or Screen.SkillSlotSelect or Screen.SkillPoolList or Screen.DungeonSelect or Screen.DungeonPartySelect
        or Screen.EquipmentEnhance;

    static bool IsDebugListScreen(Screen screen) =>
        screen is Screen.DebugAddCharacter or Screen.DebugAddEquipment or Screen.DebugAddItem or Screen.DebugSkillCharacter or Screen.DebugAddSkill;

    /// <summary>페이지와 상관없이 늘 나오는 항목: 돌아가기와, 고르는 화면의 "[0] 비우기/해제"(동료 확인의 0번 줄은 그냥 항목이다).</summary>
    bool IsFixedEntry(ScreenEntry e) =>
        e.Code == BackEntryCode
        || e.Code is EnhanceEntryCode or ModifyEntryCode && Screen == Screen.EquipmentEnhance
        || e.Code == ClearEntryCode && Screen is Screen.CharacterSelect or Screen.SupportSelect or Screen.EquipmentSelect or Screen.SkillSlotSelect or Screen.SkillCharacter;

    /// <summary>항목이 count개일 때의 페이지 수(항목이 없어도 1).</summary>
    public int PageCountOf(int count) => Math.Max(1, (count + ListPageSize - 1) / ListPageSize);

    int ClampedPage(int count) => ListPage = Math.Clamp(ListPage, 0, PageCountOf(count) - 1);

    /// <summary>전체 목록(화면에 나오는 순서)에서 지금 페이지의 항목들. 화면이 항목 줄 대신 목록을 직접 그릴 때 쓴다.</summary>
    public IReadOnlyList<T> PageOf<T>(IReadOnlyList<T> all) => all.Skip(ClampedPage(all.Count) * ListPageSize).Take(ListPageSize).ToList();

    /// <summary>페이지로 자를 목록의 항목 코드들(화면에 나오는 순서).</summary>
    IReadOnlyList<string> PageKeys(IReadOnlyList<ScreenEntry> raw) => Screen switch
    {
        Screen.CharacterSelect => CharacterList.Select(c => Text(c.Code)).ToList(),
        Screen.SupportSelect => SupportList.Select(c => Text(c.Code)).ToList(),
        Screen.SkillPoolList => SkillPoolRows.Select(r => Text(r.Index)).ToList(),
        _ => raw.Where(e => !IsFixedEntry(e)).Select(e => e.Code).ToList(),
    };

    /// <summary>지금 화면의 목록 항목 수: 목록 화면이 아니면 -1.</summary>
    int ListCount => IsPagedScreen(Screen) ? PageKeys(RawEntries).Count : IsDebugListScreen(Screen) ? DebugList.Count : -1;

    IReadOnlyList<ScreenEntry> Paginate(IReadOnlyList<ScreenEntry> raw)
    {
        if (!IsPagedScreen(Screen)) return raw;
        var keys = PageKeys(raw);
        var onPage = PageOf(keys).ToHashSet();
        var back = raw.Where(e => e.Code == BackEntryCode);
        return [.. raw.Where(e => e.Code != BackEntryCode && (IsFixedEntry(e) || onPage.Contains(e.Code))), .. PagerEntries(keys.Count), .. back];
    }

    /// <summary>
    /// 하단의 페이지 버튼 줄: 첫 페이지, 지금 페이지 앞뒤 두 쪽, 마지막 페이지를 보이고 떨어진 사이는 "..."로 둔다. 지금 페이지는 코드 없이 보인다.
    /// 한 쪽이 여러 자리에 걸치면(예: 첫 페이지가 지금-2이기도 함) 한 번만 보이고, 첫 페이지는 늘 101, 마지막 페이지는 늘 106이다.
    /// 페이지가 하나뿐이면 비어 있다.
    /// </summary>
    public IReadOnlyList<PagerButton> Pager => ListCount is var count and >= 0 ? PagerFor(count) : [];

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
            if (p > previous + 1) buttons.Add(new PagerButton(PagerButton.Ellipsis, null, false));
            var code = p == current ? null : p == 1 ? "101" : p == pages ? "106" : PagerRoles.First(r => r.Page(current, pages) == p).Code;
            buttons.Add(new PagerButton(p.ToString(CultureInfo.InvariantCulture), code, p == current));
            previous = p;
        }
        return buttons;
    }

    /// <summary>입력할 수 있는 페이지 항목들: 101~106 중 갈 수 있는 페이지(첫 쪽~마지막 쪽, 지금 페이지 제외)가 있는 자리.</summary>
    IReadOnlyList<ScreenEntry> PagerEntries(int count)
    {
        var pages = PageCountOf(count);
        var current = ClampedPage(count) + 1;
        if (pages <= 1) return [];
        return PagerRoles
            .Select(r => (r.Code, Page: r.Page(current, pages)))
            .Where(r => r.Page >= 1 && r.Page <= pages && r.Page != current)
            .Select(r => new ScreenEntry(r.Code, Text(r.Page), Screen, EntryAction.ListPage, Text(r.Page)))
            .ToList();
    }

    /// <summary>
    /// 입력한 코드가 페이지 버튼(101~106)이면 그 항목. 페이지 버튼이 없는 화면이거나 갈 곳이 없는 자리면 null.
    /// 항목 코드와 겹치면 입력은 페이지 버튼이 먼저다(버튼을 누른 경우는 <see cref="Activate"/>의 typed가 false라 항목이 먼저).
    /// </summary>
    ScreenEntry? PagerEntryFor(string code) =>
        ListCount is var count and >= 0 ? PagerEntries(count).FirstOrDefault(e => e.Code == code) : null;
}
