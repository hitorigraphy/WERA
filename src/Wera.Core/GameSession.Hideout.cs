using System.Globalization;

namespace Wera.Core;

/// <summary>아지트 화면의 한 줄(층): 보이는 줄 번호(0~2)와 그 줄이 무엇인지. Floor가 -1이면 "확장 가능" 줄, null이면 빈 줄이다.</summary>
public sealed record HideoutRowView(int Row, int? Floor, string FloorName, PartySlotView? Boss)
{
    /// <summary>줄의 슬롯 번호: 계층(왼쪽) 줄×3+1, 시설(가운데) +2, 보스(오른쪽) +3.</summary>
    public int FloorCode => Row * 3 + 1;
    public int FacilityCode => Row * 3 + 2;
    public int BossCode => Row * 3 + 3;
    public bool IsExpandRow => Floor == -1;
    public bool IsEmpty => Floor is null;
}

// 아지트: 층마다 계층(왼쪽), 시설(가운데), 보스(오른쪽) 슬롯이 있다. 처음에는 1층뿐이고, 확장하면 새 층이 1층이 되고 기존 층은 한 층씩 밀려난다(가장 오래된 층이 가장 아래).
// 보스 슬롯에는 캐릭터를 한 명 둘 수 있고, 캐릭터는 파티와 아지트(어느 층이든) 중 한 곳에만 있다. 시설은 아직 정의되지 않았다(공터).
public sealed partial class GameSession
{
    /// <summary>아지트 화면의 표시 범위 버튼(한 층씩 옮긴다): [1000] ▲, [1001] ▼.</summary>
    public const string HideoutScrollUpEntryCode = "1000";
    public const string HideoutScrollDownEntryCode = "1001";

    /// <summary>한 번에 보이는 줄 수.</summary>
    public const int HideoutVisibleRows = 3;

    /// <summary>층별 보스 슬롯의 캐릭터 코드(없으면 null). 맨 앞이 가장 새 층(1층)이다.</summary>
    readonly List<long?> hideoutBosses = [null];

    /// <summary>보이는 첫 줄의 위치: -1은 "확장 가능" 줄, 0은 1층, 1은 2층…</summary>
    int hideoutViewTop = -1;

    /// <summary>지금 층 수(처음에는 1).</summary>
    public int HideoutFloorCount => hideoutBosses.Count;

    /// <summary>층별 보스 슬롯의 캐릭터(1층부터).</summary>
    public IReadOnlyList<long?> HideoutBosses => hideoutBosses;

    /// <summary>이 캐릭터가 있는 아지트 층의 번호(0이 1층, 어느 층에도 없으면 -1).</summary>
    public int HideoutFloorOf(long characterCode) => hideoutBosses.IndexOf(characterCode);

    /// <summary>보이는 첫 줄의 위치: -1("확장 가능" 줄)부터 마지막 층이 맨 아래 줄에 올 때까지.</summary>
    public int HideoutViewTop => hideoutViewTop;

    int HideoutViewTopMax => Math.Max(-1, HideoutFloorCount - HideoutVisibleRows);

    /// <summary>다음 확장에 쓸 수 있는 재료 그룹들. 비어 있으면 더 확장할 수 없다.</summary>
    public IReadOnlyList<HideoutExpandGroup> HideoutExpandGroups => Pack.HideoutExpandGroupsOf(HideoutFloorCount);

    /// <summary>이 그룹의 재료를 모두 갖췄는가(장비에 끼워 둔 것은 셈에서 뺀다).</summary>
    public bool CanAffordHideoutExpand(HideoutExpandGroup group) =>
        group.Costs.All(c => CountInventory(c.ItemCode) - EquippedCount(EquipRef.Common(c.ItemCode)) >= c.Amount);

    /// <summary>이 그룹의 재료로 확장한다: 재료를 쓰고 새 1층이 생기며 기존 층은 한 층씩 밀려난다(보스와 시설도 함께). 할 수 없으면 false.</summary>
    public bool ExpandHideout(long groupCode)
    {
        if (HideoutExpandGroups.FirstOrDefault(g => g.GroupCode == groupCode) is not { } group || !CanAffordHideoutExpand(group)) return false;
        foreach (var cost in group.Costs) ChangeCommonItem(cost.ItemCode, -cost.Amount);
        hideoutBosses.Insert(0, null);
        hideoutViewTop = -1;
        return true;
    }

    /// <summary>
    /// 층(0이 1층)의 보스 슬롯에 캐릭터를 둔다(null이면 비운다). 보유한 캐릭터만 둘 수 있다. 다른 층에 있던 캐릭터는 이 슬롯의 캐릭터와 자리를 바꾸고,
    /// 파티에 있던 캐릭터는 파티에서 빠진다(장비는 보유로 돌아간다). 할 수 없으면 false.
    /// </summary>
    public bool SetHideoutBoss(int floor, long? characterCode)
    {
        if (floor < 0 || floor >= HideoutFloorCount) return false;
        if (characterCode is not { } code) { hideoutBosses[floor] = null; return true; }
        if (!Characters.ContainsKey(code)) return false;
        var at = HideoutFloorOf(code);
        if (at == floor) return true;
        if (at >= 0) (hideoutBosses[at], hideoutBosses[floor]) = (hideoutBosses[floor], hideoutBosses[at]);
        else
        {
            RemoveFromParties(code);
            hideoutBosses[floor] = code;
        }
        return true;
    }

    void RemoveFromParties(long characterCode)
    {
        foreach (var party in Parties)
            for (var i = 0; i < party.Units.Length; i++)
                if (party.Units[i]?.CharacterCode == characterCode) party.Units[i] = null;     // 유닛과 함께 장비가 빠져 보유로 돌아간다
    }

    void RemoveFromHideout(long characterCode)
    {
        for (var i = 0; i < hideoutBosses.Count; i++)
            if (hideoutBosses[i] == characterCode) hideoutBosses[i] = null;
    }

    /// <summary>아지트 화면의 보이는 세 줄(위에서부터).</summary>
    public IReadOnlyList<HideoutRowView> HideoutRows
    {
        get
        {
            hideoutViewTop = Math.Clamp(hideoutViewTop, -1, HideoutViewTopMax);
            return Enumerable.Range(0, HideoutVisibleRows).Select(row =>
            {
                var at = hideoutViewTop + row;
                if (at == -1) return new HideoutRowView(row, HideoutExpandGroups.Count > 0 ? -1 : null, HideoutExpandGroups.Count > 0 ? "확장 가능" : "----", null);
                if (at < 0 || at >= HideoutFloorCount) return new HideoutRowView(row, null, "----", null);
                return new HideoutRowView(row, at, $"{at + 1}층", CharacterView(-1, hideoutBosses[at]));
            }).ToList();
        }
    }

    IReadOnlyList<ScreenEntry> HideoutEntries()
    {
        var entries = new List<ScreenEntry>();
        foreach (var row in HideoutRows)
        {
            if (row.IsExpandRow) entries.Add(new ScreenEntry(Text(row.FloorCode), "확장 가능", Screen.HideoutExpand));
            else if (row.Floor is { } floor and >= 0)
                entries.Add(new ScreenEntry(Text(row.BossCode), row.Boss!.Name, Screen.HideoutBossSelect, Arg: Text(floor)));
        }
        entries.Add(new ScreenEntry(HideoutScrollUpEntryCode, "▲", Screen.Hideout, EntryAction.HideoutScroll, "-1", Enabled: hideoutViewTop > -1));
        entries.Add(new ScreenEntry(HideoutScrollDownEntryCode, "▼", Screen.Hideout, EntryAction.HideoutScroll, "1", Enabled: hideoutViewTop < HideoutViewTopMax));
        entries.Add(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home));
        return entries;
    }

    /// <summary>확장 화면의 재료 그룹 한 줄의 글: "이름 ×개수, 이름 ×개수".</summary>
    public string HideoutExpandGroupText(HideoutExpandGroup group) =>
        string.Join(", ", group.Costs.Select(c => $"{Pack.FindItem(c.ItemCode)?.Name ?? Text(c.ItemCode)} ×{Text(c.Amount)}"));

    IReadOnlyList<ScreenEntry> HideoutExpandEntries() =>
    [
        .. HideoutExpandGroups.Select(g => new ScreenEntry(Text(g.GroupCode), HideoutExpandGroupText(g), Screen.Hideout, EntryAction.ExpandHideout, Enabled: CanAffordHideoutExpand(g))),
        new ScreenEntry(BackEntryCode, "돌아가기", Screen.Hideout),
    ];

    /// <summary>지금 고르는 층의 이름("2층"): 보스 선택 화면의 제목에 쓴다.</summary>
    public string? CurrentHideoutFloorName => Screen == Screen.HideoutBossSelect && CurrentSlot is { } floor ? $"{floor + 1}층" : null;

    /// <summary>아지트 보스 선택 화면의 목록(캐릭터 인벤토리 마스터 순서): 보유한 캐릭터 중 이 슬롯의 캐릭터를 뺀 전부. 다른 층의 보스는 파란색(고르면 서로 바뀐다), 파티원과 나머지는 흰색(파티에서 빠진다).</summary>
    IReadOnlyList<CharacterListItem> HideoutBossCandidates()
    {
        if (CurrentSlot is not { } floor || floor >= HideoutFloorCount) return [];
        var mine = hideoutBosses[floor];
        return (Pack.CharacterInventory ?? []).Where(c => Characters.ContainsKey(c.Code) && mine != c.Code)
            .Select(c => new CharacterListItem(c.Code, c.Name, HideoutFloorOf(c.Code) >= 0 ? CharacterListColor.Green : CharacterListColor.White)).ToList();
    }

    IReadOnlyList<ScreenEntry> HideoutBossSelectEntries()
    {
        var entries = new List<ScreenEntry>();
        if (CurrentSlot is { } floor && floor < HideoutFloorCount && hideoutBosses[floor] is not null)
            entries.Add(new ScreenEntry(ClearEntryCode, "비우기", Screen.Hideout, EntryAction.ClearHideoutBoss));
        entries.AddRange(HideoutBossCandidates().Select(c => new ScreenEntry(Text(c.Code), c.Name, Screen.Hideout, EntryAction.SetHideoutBoss)));
        entries.Add(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Hideout));
        return entries;
    }
}
