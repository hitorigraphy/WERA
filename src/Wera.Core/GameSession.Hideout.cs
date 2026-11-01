using System.Globalization;

namespace Wera.Core;

/// <summary>아지트 화면의 한 줄(층): 보이는 줄 번호(0~2)와 그 줄이 무엇인지. Floor가 -1이면 "확장 가능" 줄, null이면 빈 줄이다.</summary>
public sealed record HideoutRowView(int Row, int? Floor, string FloorName, PartySlotView? Boss, string FacilityName = "공터", bool HasFacility = false)
{
    /// <summary>줄의 슬롯 번호: 계층(왼쪽) 줄×3+1, 시설(가운데) +2, 보스(오른쪽) +3.</summary>
    public int FloorCode => Row * 3 + 1;
    public int FacilityCode => Row * 3 + 2;
    public int BossCode => Row * 3 + 3;
    public bool IsExpandRow => Floor == -1;
    public bool IsEmpty => Floor is null;
}

// 아지트: 층마다 계층(왼쪽), 시설(가운데), 보스(오른쪽) 슬롯이 있다. 처음에는 1층뿐이고, 확장하면 새 층이 1층이 되고 기존 층은 한 층씩 밀려난다(가장 오래된 층이 가장 아래).
// 보스 슬롯은 그 층의 방어 파티(층마다 하나, 층을 확장할 때마다 늘어나는 고정 파티)이고 슬롯에는 파티의 리더가 보인다. 누르면 파티 설정 화면으로 간다(파티 선택 버튼 없이).
// 캐릭터(인스턴스)는 파티(방어 파티 포함)와 오퍼레이터 슬롯 중 한 곳에만 있다.
public sealed partial class GameSession
{
    /// <summary>아지트 화면의 표시 범위 버튼(한 층씩 옮긴다): [1000] ▲, [1001] ▼.</summary>
    public const string HideoutScrollUpEntryCode = "1000";
    public const string HideoutScrollDownEntryCode = "1001";

    /// <summary>한 번에 보이는 줄 수.</summary>
    public const int HideoutVisibleRows = 3;

    /// <summary>층별 방어 파티(보스 슬롯). 맨 앞이 가장 새 층(1층)이다. 이름은 층 이름("n층")이라 바꿀 수 없다.</summary>
    readonly List<PartyInstance> hideoutParties = [PartyInstance.Empty("")];

    /// <summary>파티 설정 화면에서 편집 중인 방어 파티의 층(0이 1층). null이면 보통 파티를 편집한다.</summary>
    int? hideoutEditFloor;

    /// <summary>파티 설정 화면이 방어 파티를 편집하는 중인가(파티 선택 버튼과 이름 바꾸기가 없고 돌아가기는 아지트).</summary>
    public bool IsHideoutPartyEdit => hideoutEditFloor is not null;

    /// <summary>모든 파티: 보통 파티들과 층별 방어 파티들.</summary>
    IEnumerable<PartyInstance> AllParties => Parties.Concat(hideoutParties);

    /// <summary>이 캐릭터 인스턴스가 지금 보는 파티가 아닌 다른 파티(방어 파티 포함)에 있는가.</summary>
    bool InOtherParty(string instanceId) => AllParties.Any(p => !ReferenceEquals(p, PartyInstance) && IndexOfId(p, instanceId) >= 0);

    /// <summary>이 층의 방어 파티. 없는 층이면 null.</summary>
    public PartyInstance? HideoutPartyOf(int floor) => floor >= 0 && floor < hideoutParties.Count ? hideoutParties[floor] : null;

    /// <summary>이 층의 방어 파티의 리더 캐릭터 인스턴스의 ID(보스 슬롯에 보인다). 리더가 없으면 null.</summary>
    string? HideoutLeaderOf(int floor) => hideoutParties[floor].Units.OfType<PartyUnit>().FirstOrDefault(u => u.IsLeader) is { } leader ? IdOf(leader) : null;

    /// <summary>층별로 유저가 지은(가운데 슬롯) 시설 코드(없으면 null). <see cref="hideoutBosses"/>와 같은 순서이고 층이 생기면 같이 늘어난다.</summary>
    readonly List<long?> hideoutFacilities = [null];

    /// <summary>시설의 오퍼레이터 슬롯에 선 캐릭터: (층이 생긴 순서, 지은 시설인가(아니면 기본 시설), 오퍼레이터 슬롯 코드) → 캐릭터 인스턴스 ID.</summary>
    readonly Dictionary<(long FloorOrder, bool Built, long Slot), string> hideoutOperators = [];

    /// <summary>오퍼레이터 슬롯을 고르는 중인 슬롯(오퍼레이터 선택 화면에서만 있다): 층은 CurrentSlot, 지은 시설인가, 슬롯 코드.</summary>
    bool hideoutOperatorBuilt;
    long hideoutOperatorSlot;

    /// <summary>시설을 고르는 중인 시설의 코드(재료 그룹을 고르는 화면에서만 있다).</summary>
    long? selectedFacilityCode;

    /// <summary>보이는 첫 줄의 위치: -1은 "확장 가능" 줄, 0은 1층, 1은 2층…</summary>
    int hideoutViewTop = -1;

    /// <summary>지금 층 수(처음에는 1).</summary>
    public int HideoutFloorCount => hideoutParties.Count;

    /// <summary>층별 방어 파티(1층부터).</summary>
    public IReadOnlyList<PartyInstance> HideoutParties => hideoutParties;

    /// <summary>층별로 지은 시설(1층부터). 가운데 슬롯이고 비어 있으면 null.</summary>
    public IReadOnlyList<long?> HideoutFacilities => hideoutFacilities;

    /// <summary>이 층(0이 1층)이 생긴 순서: 처음부터 있는 층이 1, 첫 확장으로 생긴 층이 2… 층계 마스터가 기본 시설을 찾는 키다(확장해도 변하지 않는다).</summary>
    public int HideoutFloorOrderOf(int floor) => HideoutFloorCount - floor;

    /// <summary>이 층의 기본 시설(층계 마스터, 계층 슬롯). 정하지 않았으면 null.</summary>
    public FacilityEntry? BaseFacilityOfFloor(int floor) =>
        Pack.BaseFacilityOf(HideoutFloorOrderOf(floor)) is { } code ? Pack.FindFacility(code) : null;

    /// <summary>이 층의 가운데 슬롯에 지은 시설. 없으면 null.</summary>
    public FacilityEntry? BuiltFacilityOfFloor(int floor) =>
        floor >= 0 && floor < HideoutFloorCount && hideoutFacilities[floor] is { } code ? Pack.FindFacility(code) : null;

    /// <summary>이 캐릭터 코드의 유닛이 방어 파티에 있는 층의 번호(0이 1층, 어느 방어 파티에도 없으면 -1).</summary>
    public int HideoutFloorOf(long characterCode) => hideoutParties.FindIndex(p => p.IndexOf(characterCode) >= 0);

    /// <summary>이 캐릭터 인스턴스가 방어 파티에 있는 층의 번호(0이 1층, 어느 방어 파티에도 없으면 -1).</summary>
    public int HideoutFloorOf(string instanceId) => hideoutParties.FindIndex(p => IndexOfId(p, instanceId) >= 0);

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
        hideoutParties.Insert(0, PartyInstance.Empty(""));
        hideoutFacilities.Insert(0, null);
        hideoutViewTop = -1;
        return true;
    }

    void RemoveFromParties(string instanceId)
    {
        foreach (var party in AllParties)
            for (var i = 0; i < party.Units.Length; i++)
                if (party.Units[i] is { } unit && IdOf(unit) == instanceId) party.Units[i] = null;     // 유닛과 함께 장비가 빠져 보유로 돌아간다
    }

    void RemoveFromHideout(string instanceId)
    {
        foreach (var key in hideoutOperators.Where(o => o.Value == instanceId).Select(o => o.Key).ToList()) hideoutOperators.Remove(key);
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
                var built = BuiltFacilityOfFloor(at);
                return new HideoutRowView(row, at, $"{at + 1}층", CharacterView(-1, HideoutLeaderOf(at)), built?.Name ?? "공터", built is not null);
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
            {
                entries.Add(new ScreenEntry(Text(row.FloorCode), row.FloorName, Screen.HideoutFloorDetail, Arg: Text(floor)));
                if (row.HasFacility) entries.Add(new ScreenEntry(Text(row.FacilityCode), row.FacilityName, Screen.HideoutFacilityDetail, Arg: Text(floor)));
                else if (Pack.Facilities is { Count: > 0 }) entries.Add(new ScreenEntry(Text(row.FacilityCode), row.FacilityName, Screen.HideoutFacilitySelect, Arg: Text(floor)));
                entries.Add(new ScreenEntry(Text(row.BossCode), row.Boss!.Name, Screen.PartySetup, EntryAction.EditHideoutParty, Arg: Text(floor)));
            }
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

    // ---- 시설 ----

    /// <summary>지금 설치 조건(시설 조건 마스터)을 만족해 지을 수 있는 시설들(시설 마스터 순서).</summary>
    public IReadOnlyList<FacilityEntry> AvailableFacilities => Pack.Facilities?.Where(f => f.IsAvailable(PlayerFlags)).ToList() ?? [];

    /// <summary>이 그룹의 재료를 모두 갖췄는가(장비에 끼워 둔 것은 셈에서 뺀다).</summary>
    public bool CanAffordFacilityBuild(FacilityBuildGroup group) =>
        group.Costs.All(c => CountInventory(c.ItemCode) - EquippedCount(EquipRef.Common(c.ItemCode)) >= c.Amount);

    /// <summary>이 시설을 지금 지을 재료를 갖췄는가: 재료 그룹이 없으면 늘 true, 있으면 한 그룹이라도 갖춰야 한다.</summary>
    public bool CanAffordFacility(long facilityCode) =>
        Pack.FacilityBuildGroupsOf(facilityCode) is { Count: > 0 } groups ? groups.Any(CanAffordFacilityBuild) : true;

    /// <summary>
    /// 층(0이 1층)의 가운데 슬롯에 시설을 짓는다: 슬롯이 비어 있고 시설이 설치 조건을 만족해야 하며, 재료 그룹이 있으면 갖춘 그룹(groupCode)의 재료를 쓴다
    /// (재료 그룹이 없는 시설은 groupCode를 무시하고 재료 없이 짓는다). 할 수 없으면 false.
    /// </summary>
    public bool BuildFacility(int floor, long facilityCode, long? groupCode = null)
    {
        if (floor < 0 || floor >= HideoutFloorCount || hideoutFacilities[floor] is not null) return false;
        if (AvailableFacilities.All(f => f.Code != facilityCode)) return false;
        var groups = Pack.FacilityBuildGroupsOf(facilityCode);
        if (groups.Count > 0)
        {
            if (groups.FirstOrDefault(g => g.GroupCode == groupCode) is not { } group || !CanAffordFacilityBuild(group)) return false;
            foreach (var cost in group.Costs) ChangeCommonItem(cost.ItemCode, -cost.Amount);
        }
        hideoutFacilities[floor] = facilityCode;
        return true;
    }

    /// <summary>층의 가운데 슬롯에 지은 시설을 철거한다(재료는 돌려받지 않는다). 지은 시설이 없으면 false.</summary>
    public bool DemolishFacility(int floor)
    {
        if (floor < 0 || floor >= HideoutFloorCount || hideoutFacilities[floor] is null) return false;
        hideoutFacilities[floor] = null;
        foreach (var key in hideoutOperators.Keys.Where(k => k.FloorOrder == HideoutFloorOrderOf(floor) && k.Built).ToList()) hideoutOperators.Remove(key);     // 오퍼레이터도 일을 그만둔다
        return true;
    }

    /// <summary>시설 고르는 화면의 제목에 쓰는 층 이름: 시설 선택, 시설 재료 선택, 층 상세, 시설 상세 화면에서만 있다.</summary>
    public string? CurrentHideoutFacilityFloorName =>
        Screen is Screen.HideoutFacilitySelect or Screen.HideoutBuild or Screen.HideoutFloorDetail or Screen.HideoutFacilityDetail && CurrentSlot is { } floor ? $"{floor + 1}층" : null;

    /// <summary>층 상세 화면의 안내: 기본 시설의 이름(없으면 "기본 시설 없음").</summary>
    public string? HideoutFloorDetailText => Screen == Screen.HideoutFloorDetail && CurrentSlot is { } floor && floor < HideoutFloorCount
        ? BaseFacilityOfFloor(floor) is { } facility ? $"기본 시설: {facility.Name}" : "기본 시설 없음" : null;

    /// <summary>시설 상세 화면의 안내: 지은 시설의 이름.</summary>
    public string? HideoutFacilityDetailText => Screen == Screen.HideoutFacilityDetail && CurrentSlot is { } floor && BuiltFacilityOfFloor(floor) is { } facility
        ? $"시설: {facility.Name}" : null;

    /// <summary>시설을 고르는 중인 시설의 이름과 재료 그룹 화면의 제목에 쓴다.</summary>
    public string? SelectedFacilityName => selectedFacilityCode is { } code ? Pack.FindFacility(code)?.Name : null;

    /// <summary>시설 재료 그룹 한 줄의 글: "이름 ×개수, 이름 ×개수".</summary>
    public string FacilityBuildGroupText(FacilityBuildGroup group) =>
        string.Join(", ", group.Costs.Select(c => $"{Pack.FindItem(c.ItemCode)?.Name ?? Text(c.ItemCode)} ×{Text(c.Amount)}"));

    IReadOnlyList<ScreenEntry> HideoutFacilitySelectEntries() =>
    [
        .. AvailableFacilities.Select(f => new ScreenEntry(Text(f.Code), f.Name, Screen.HideoutBuild, EntryAction.SelectFacility, Enabled: CanAffordFacility(f.Code))),
        new ScreenEntry(BackEntryCode, "돌아가기", Screen.Hideout),
    ];

    IReadOnlyList<ScreenEntry> HideoutBuildEntries() =>
    [
        .. (selectedFacilityCode is { } code ? Pack.FacilityBuildGroupsOf(code) : [])
            .Select(g => new ScreenEntry(Text(g.GroupCode), FacilityBuildGroupText(g), Screen.Hideout, EntryAction.BuildFacility, Enabled: CanAffordFacilityBuild(g))),
        new ScreenEntry(BackEntryCode, "돌아가기", Screen.HideoutFacilitySelect),
    ];

    /// <summary>시설 상세 화면의 철거 항목 코드.</summary>
    public const string DemolishEntryCode = "1";

    IReadOnlyList<ScreenEntry> HideoutFacilityDetailEntries() =>
    [
        new ScreenEntry(DemolishEntryCode, "철거", Screen.Hideout, EntryAction.DemolishFacility),
        .. CurrentSlot is { } floor ? HideoutOperatorEntries(floor, true, 2) : [],
        new ScreenEntry(BackEntryCode, "돌아가기", Screen.Hideout),
    ];

    // ---- 오퍼레이터 ----

    /// <summary>층(0이 1층)의 기본 시설(built가 false) 또는 지은 시설(true)의 오퍼레이터 슬롯에 선 캐릭터의 코드. 비었으면 null.</summary>
    public long? HideoutOperatorOf(int floor, bool built, long slotCode) => CodeOfInstance(HideoutOperatorInstanceOf(floor, built, slotCode));

    /// <summary>오퍼레이터 슬롯에 선 캐릭터 인스턴스의 ID. 비었으면 null.</summary>
    public string? HideoutOperatorInstanceOf(int floor, bool built, long slotCode) =>
        hideoutOperators.TryGetValue((HideoutFloorOrderOf(floor), built, slotCode), out var id) ? id : null;

    FacilityEntry? FacilityOfFloor(int floor, bool built) => built ? BuiltFacilityOfFloor(floor) : BaseFacilityOfFloor(floor);

    /// <summary>이 캐릭터 코드의 인스턴스 중 하나라도 이 오퍼레이터 슬롯에 설 수 있는가: 보유한 캐릭터이고 슬롯이 요구하는 태그를 모두 가져야 한다.</summary>
    public bool CanOperate(long characterCode, long operatorSlotCode) =>
        Characters.InstancesOf(characterCode).Any(c => CanOperate(c.InstanceId, operatorSlotCode));

    /// <summary>이 캐릭터 인스턴스가 이 오퍼레이터 슬롯에 설 수 있는가: 보유한 캐릭터이고 슬롯이 요구하는 태그를 모두 가져야 한다.</summary>
    public bool CanOperate(string instanceId, long operatorSlotCode) =>
        Characters.ContainsKey(instanceId) && CharacterTags(instanceId) is var have && Pack.OperatorSlotTagsOf(operatorSlotCode).All(have.Contains);

    /// <summary>
    /// 오퍼레이터 슬롯에 캐릭터를 세운다(null이면 비운다): 그 시설이 가진 슬롯이고 캐릭터가 태그 조건을 만족해야 한다. 캐릭터 코드는 그 코드의
    /// 아직 어디에도 서지 않은 첫 인스턴스(모두 어딘가에 서 있으면 슬롯 조건을 만족하는 첫 인스턴스)를 뜻한다. 할 수 없으면 false.
    /// </summary>
    public bool SetHideoutOperator(int floor, bool built, long slotCode, long? characterCode)
    {
        if (characterCode is not { } code) return SetHideoutOperatorInstance(floor, built, slotCode, null);
        var able = Characters.InstancesOf(code).Where(c => CanOperate(c.InstanceId, slotCode)).ToList();
        var pick = able.FirstOrDefault(c => !IsPlaced(c.InstanceId)) ?? able.FirstOrDefault();
        return pick is not null && SetHideoutOperatorInstance(floor, built, slotCode, pick.InstanceId);
    }

    /// <summary>
    /// 오퍼레이터 슬롯에 캐릭터 인스턴스를 세운다(null이면 비운다): 그 시설이 가진 슬롯이고 캐릭터가 태그 조건을 만족해야 한다. 캐릭터 인스턴스는 파티(방어 파티 포함)와
    /// 오퍼레이터 슬롯 중 한 곳에만 있으므로 다른 곳에 있던 인스턴스는 거기서 빠진다(파티에서는 장비가 보유로 돌아간다). 할 수 없으면 false.
    /// </summary>
    public bool SetHideoutOperatorInstance(int floor, bool built, long slotCode, string? instanceId)
    {
        if (floor < 0 || floor >= HideoutFloorCount || FacilityOfFloor(floor, built) is not { } facility) return false;
        if (Pack.OperatorSlotsOf(facility.Code).All(s => s.Code != slotCode)) return false;
        var key = (HideoutFloorOrderOf(floor), built, slotCode);
        if (instanceId is null) { hideoutOperators.Remove(key); return true; }
        if (!CanOperate(instanceId, slotCode)) return false;
        RemoveFromParties(instanceId);
        RemoveFromHideout(instanceId);
        hideoutOperators[key] = instanceId;
        return true;
    }

    /// <summary>시설 상세 화면의 오퍼레이터 슬롯 항목들: "슬롯 이름: 캐릭터 이름(없으면 미지정)", 코드는 firstCode부터 차례로.</summary>
    IEnumerable<ScreenEntry> HideoutOperatorEntries(int floor, bool built, int firstCode) =>
        (FacilityOfFloor(floor, built) is { } facility ? Pack.OperatorSlotsOf(facility.Code) : []).Select((slot, i) =>
            new ScreenEntry(Text(firstCode + i),
                $"{slot.Name}: {(HideoutOperatorOf(floor, built, slot.Code) is { } who ? CharacterName(who) : "미지정")}",
                Screen.HideoutOperatorSelect, Arg: $"{floor}:{(built ? 1 : 0)}:{slot.Code}"));

    /// <summary>오퍼레이터 선택 화면의 제목: "n층 시설이름 - 슬롯이름".</summary>
    public string? CurrentHideoutOperatorTitle =>
        Screen == Screen.HideoutOperatorSelect && CurrentSlot is { } floor && FacilityOfFloor(floor, hideoutOperatorBuilt) is { } facility
            ? $"{floor + 1}층 {facility.Name} - {Pack.FindOperatorSlot(hideoutOperatorSlot)?.Name}" : null;

    /// <summary>오퍼레이터 선택 화면의 목록(캐릭터 인벤토리 마스터 순서, 같은 캐릭터를 여럿 가졌으면 인스턴스마다 한 줄): 슬롯의 태그 조건을 만족하는 보유 캐릭터. 이 슬롯의 캐릭터는 빠지고, 이미 아지트에 있는 캐릭터는 파란색(고르면 그 자리에서 옮겨 온다), 파티원과 나머지는 흰색(파티에서 빠진다).</summary>
    IReadOnlyList<CharacterListItem> HideoutOperatorCandidates()
    {
        if (CurrentSlot is not { } floor || floor >= HideoutFloorCount) return [];
        var mine = HideoutOperatorInstanceOf(floor, hideoutOperatorBuilt, hideoutOperatorSlot);
        return OwnedByMasterOrder().Where(x => mine != x.Instance.InstanceId && CanOperate(x.Instance.InstanceId, hideoutOperatorSlot))
            .Select(x => new CharacterListItem(x.Entry.Code, x.Entry.Name,
                HideoutFloorOf(x.Instance.InstanceId) >= 0 || hideoutOperators.ContainsValue(x.Instance.InstanceId) ? CharacterListColor.Green : CharacterListColor.White, x.Instance.InstanceId)).ToList();
    }

    Screen HideoutOperatorReturnScreen => hideoutOperatorBuilt ? Screen.HideoutFacilityDetail : Screen.HideoutFloorDetail;

    IReadOnlyList<ScreenEntry> HideoutOperatorSelectEntries()
    {
        var entries = new List<ScreenEntry>();
        var back = CurrentSlot is { } floor ? new ScreenEntry(BackEntryCode, "돌아가기", HideoutOperatorReturnScreen, Arg: Text(floor)) : new ScreenEntry(BackEntryCode, "돌아가기", Screen.Hideout);
        if (CurrentSlot is { } at && HideoutOperatorInstanceOf(at, hideoutOperatorBuilt, hideoutOperatorSlot) is not null)
            entries.Add(new ScreenEntry(ClearEntryCode, "비우기", HideoutOperatorReturnScreen, EntryAction.ClearHideoutOperator));
        entries.AddRange(HideoutOperatorCandidates().Select(c => new ScreenEntry(c.InstanceId, c.Name, HideoutOperatorReturnScreen, EntryAction.SetHideoutOperator)));
        entries.Add(back);
        return entries;
    }

    // ---- 시설 생산 ----

    /// <summary>방어전에서 밀려 적이 침입했는가: true면 다음 턴 종료의 시설 생산을 건너뛴다(방어전은 아직 없어 늘 false). 건너뛴 뒤 false로 돌아간다.</summary>
    public bool HideoutInvaded { get; set; }

    /// <summary>
    /// 턴 종료 뒤의 시설 서킷: 아지트의 층을 1층부터 차례로, 층마다 기본 시설 다음에 지은 시설의 시설 흐름을 실행해 아이템 등을 생산한다. 흐름이 없는 시설은 아무것도 하지 않는다.
    /// 침입(<see cref="HideoutInvaded"/>)이 있으면 하나도 실행하지 않는다. 바뀐 커먼 아이템 수를 "이름 +n, 이름 -n"으로 돌려준다(바뀐 것이 없으면 null; 침입이면 그 안내).
    /// </summary>
    public string? RunFacilityCircuits()
    {
        if (HideoutInvaded)
        {
            HideoutInvaded = false;
            return "적이 침입해 시설이 생산하지 못했습니다";
        }
        var before = InventoryItems.ToDictionary(p => p.Key, p => p.Value.Amount);
        for (var floor = 0; floor < HideoutFloorCount; floor++)
            foreach (var built in new[] { false, true })
            {
                if (FacilityOfFloor(floor, built) is not { } facility || Pack.FacilityFlowsOf(facility.Code) is not { Count: > 0 } flows) continue;
                var at = floor;
                new FacilityFlowCircuit(facility.Code, flows, slot => HideoutOperatorOf(at, built, slot), PlayerFlags,
                    AddInventory, CountInventory, CharacterHasTag, UniqueItemHasTag, NextRandom).Execute();
            }
        var changes = InventoryItems.Keys.Union(before.Keys).Order()
            .Select(code => (code, delta: InventoryItems.GetValueOrDefault(code).Amount - before.GetValueOrDefault(code))).Where(x => x.delta != 0)
            .Select(x => $"{Pack.FindItem(x.code)?.Name ?? Text(x.code)} {(x.delta > 0 ? "+" : "")}{Text(x.delta)}").ToList();
        return changes.Count == 0 ? null : "시설 생산: " + string.Join(", ", changes);
    }

    /// <summary>오퍼레이터 슬롯에 선 캐릭터의 최대 방어력: 그 슬롯의 방어력 흐름(서킷)을 실행한 결과. 흐름이 없거나 보유하지 않은 캐릭터면 0.</summary>
    public long OperatorDefenseOf(long characterCode, long operatorSlotCode) =>
        InstanceIdOf(characterCode) is { } id ? OperatorDefenseOf(id, operatorSlotCode) : 0;

    /// <summary>오퍼레이터 슬롯에 선 캐릭터 인스턴스의 최대 방어력(<see cref="OperatorDefenseOf(long, long)"/>와 같다).</summary>
    public long OperatorDefenseOf(string instanceId, long operatorSlotCode) =>
        Characters.TryGetValue(instanceId, out var character) && Pack.DefenseFlowsOf(operatorSlotCode) is { Count: > 0 } flows
            ? new DefenseFlowCircuit(flows, character.CharacterCode, (_, status) => character.Statuses.GetValueOrDefault(status), CharacterHasTag, PlayerFlags, NextRandom).Execute()
            : 0;

    /// <summary>현재 위협도(게임 설정 ThreatFlagCode의 플레이어 플래그 값). 설정이 없으면 0.</summary>
    public long Threat => Pack.ThreatFlagCode is { } flag ? PlayerFlags.GetValueOrDefault(flag) : 0;

    /// <summary>
    /// 방어전에 등장할 적 파티를 정한다: priority가 작은 행부터 위협도가 min_threat 이상인 것만 chance(%) 확률로 검사해, 처음 통과한 행이 등장하고
    /// 그 consume_threat만큼 위협도가 줄어든다(0 아래로는 내려가지 않는다). 통과한 행이 없으면(평화로운 날) null.
    /// 통과한 행의 프리셋 파티를 만들 수 없으면(<see cref="CanCreateEnemyParty"/>) 침입 실패로 null이고 위협도는 줄지 않는다.
    /// </summary>
    public DefenseEnemy? RollDefenseEnemy()
    {
        if (Pack.ThreatFlagCode is not { } flag) return null;
        foreach (var enemy in (Pack.DefenseEnemies ?? []).OrderBy(e => e.Priority))
        {
            if (Threat < enemy.MinThreat || !RandomJump.Hit(enemy.Chance, RandomJump.Roll(100, NextRandom))) continue;
            if (!CanCreateEnemyParty(enemy.PresetPartyCode)) return null;
            PlayerFlagOps.Set(PlayerFlags, flag, Math.Max(0, Threat - enemy.ConsumeThreat));
            return enemy;
        }
        return null;
    }

    /// <summary>
    /// 프리셋 파티를 적으로 만들 수 있는가: 프리셋 유닛의 캐릭터 코드마다 (파티에 든 수 + 지금 보유한 수)가 그 캐릭터의 최대 보유 수(carry_limit)를 넘으면 만들지 않는다
    /// (잡혀 와도 보유할 수 없는 캐릭터가 섞인 파티는 침입하지 못한다).
    /// </summary>
    public bool CanCreateEnemyParty(long presetPartyCode)
    {
        if (Pack.FindPresetParty(presetPartyCode) is not { } party) return false;
        return party.UnitCodes.OfType<long>()
            .Select(unit => Pack.FindPresetUnit(unit)?.CharacterCode)
            .OfType<long>()
            .GroupBy(code => code)
            .All(g => OwnedCountOf(g.Key) + g.Count() <= CharacterLimitOf(g.Key));
    }
}
