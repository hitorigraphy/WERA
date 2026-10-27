namespace Wera.Core;

/// <summary>홈 화면 위쪽의 지정 슬롯. 파티 설정과는 독립이다.</summary>
public enum SupportRole
{
    Partner,
    Assist,
}

// 홈 화면 위쪽의 슬롯: 당신(변경 불가), 파트너, 어시스트. 파트너와 어시스트는 슬롯을 고르면 보유 캐릭터 목록이 나오고, 파티 설정과는 독립이다
// (파티에 있는 캐릭터를 파트너나 어시스트로 지정해도 파티는 그대로). 파트너와 어시스트가 같은 캐릭터일 수는 없다.
// 지정만 하고 아직 쓰이는 기능은 없다(추후 확장).
public sealed partial class GameSession
{
    /// <summary>홈 화면의 파트너 슬롯 코드(타이핑 가능). 홈 항목 목록에는 나오지 않고 슬롯을 눌러서 쓴다.</summary>
    public const string PartnerEntryCode = "1";

    /// <summary>홈 화면의 어시스트 슬롯 코드(타이핑 가능).</summary>
    public const string AssistEntryCode = "2";

    /// <summary>지정한 파트너 캐릭터. 없으면 null.</summary>
    public long? PartnerCode { get; private set; }

    /// <summary>지정한 어시스트 캐릭터. 없으면 null.</summary>
    public long? AssistCode { get; private set; }

    /// <summary>캐릭터 선택 화면(파트너/어시스트)에서 지정 중인 슬롯. 그 화면이 아니면 null.</summary>
    public SupportRole? CurrentSupportRole { get; private set; }

    public long? SupportOf(SupportRole role) => role == SupportRole.Partner ? PartnerCode : AssistCode;

    /// <summary>
    /// 파트너/어시스트를 지정한다(null이면 비운다). 보유한 캐릭터여야 하고, 다른 쪽 슬롯에 이미 있는 캐릭터를 고르면
    /// 두 슬롯의 캐릭터가 서로 바뀐다. 보유하지 않은 캐릭터면 false.
    /// </summary>
    public bool SetSupport(SupportRole role, long? code)
    {
        if (code is { } owned && (!Characters.ContainsKey(owned) || owned == GamePack.PlayerCharacterCode)) return false;   // 플레이어는 파트너/어시스트가 될 수 없다

        var other = role == SupportRole.Partner ? SupportRole.Assist : SupportRole.Partner;
        var previous = SupportOf(role);
        if (code is not null && SupportOf(other) == code) Put(other, previous);   // 서로 바꾼다
        Put(role, code);
        return true;
    }

    void Put(SupportRole role, long? code)
    {
        if (role == SupportRole.Partner) PartnerCode = code;
        else AssistCode = code;
    }

    /// <summary>홈 화면 위쪽 왼쪽의 "당신" 슬롯: 플레이어(캐릭터 1)를 보여 준다. 눌러도 바뀌지 않는다.</summary>
    public PartySlotView PlayerSlot
    {
        get
        {
            var view = CharacterView(-1, GamePack.PlayerCharacterCode);
            return view.IsEmpty ? view with { Name = PlayerSlotName } : view;
        }
    }

    public const string PlayerSlotName = "당신";

    /// <summary>홈 화면의 파트너 슬롯.</summary>
    public PartySlotView PartnerSlot => CharacterView(-1, PartnerCode);

    /// <summary>홈 화면의 어시스트 슬롯.</summary>
    public PartySlotView AssistSlot => CharacterView(-1, AssistCode);

    /// <summary>홈 화면에서만 쓸 수 있는 슬롯 항목(홈 항목 목록에는 나오지 않는다): [1] 파트너, [2] 어시스트.</summary>
    IReadOnlyList<ScreenEntry> HomeSlotEntries => Screen != Screen.Home
        ? []
        :
        [
            new ScreenEntry(PartnerEntryCode, "파트너", Screen.SupportSelect, Arg: nameof(SupportRole.Partner)),
            new ScreenEntry(AssistEntryCode, "어시스트", Screen.SupportSelect, Arg: nameof(SupportRole.Assist)),
        ];

    /// <summary>
    /// 지금 파트너/어시스트로 교류의 흐름을 찾는다(듀오 → 솔로 → 기본 순, 교류가 is_assist면 파트너와 어시스트 자리를 뒤바꿈).
    /// 자세한 규칙은 <see cref="GamePack.FindCommunicationFlow"/>.
    /// </summary>
    public IReadOnlyList<CommunicationFlowStep> CommunicationFlowOf(long communicationCode) =>
        Pack.FindCommunicationFlow(communicationCode, PartnerCode, AssistCode);

    static string RoleName(SupportRole role) => role == SupportRole.Partner ? "파트너" : "어시스트";

    /// <summary>선택 화면의 제목용 슬롯 이름.</summary>
    public string? CurrentSupportRoleName => CurrentSupportRole is { } role ? RoleName(role) : null;

    /// <summary>
    /// 파트너/어시스트 캐릭터 선택 화면의 목록(캐릭터 인벤토리 마스터 순서).
    /// 보유하지 않은 캐릭터는 목록에 나오지 않는다. 플레이어와 이 슬롯의 캐릭터는 나오지 않는다,
    /// 다른 쪽 슬롯의 캐릭터는 파란색(고르면 서로 바뀐다), 나머지는 흰색.
    /// </summary>
    IReadOnlyList<CharacterListItem> SupportCharacterList()
    {
        if (CurrentSupportRole is not { } role) return [];
        var mine = SupportOf(role);
        var theirs = SupportOf(role == SupportRole.Partner ? SupportRole.Assist : SupportRole.Partner);
        return (Pack.CharacterInventory ?? []).Where(c => Characters.ContainsKey(c.Code) && mine != c.Code && c.Code != GamePack.PlayerCharacterCode).Select(c =>
        {
            var color = theirs == c.Code ? CharacterListColor.Green : CharacterListColor.White;
            return new CharacterListItem(c.Code, c.Name, color);
        }).ToList();
    }

    /// <summary>맨 위 "[0] 비우기"(이 슬롯에 캐릭터가 있을 때만), 고를 수 있는 캐릭터, 돌아가기(홈).</summary>
    IReadOnlyList<ScreenEntry> SupportSelectEntries()
    {
        var entries = new List<ScreenEntry>();
        if (CurrentSupportRole is { } role && SupportOf(role) is not null)
            entries.Add(new ScreenEntry(ClearEntryCode, "비우기", Screen.Home, EntryAction.ClearSupport));
        entries.AddRange(SupportCharacterList()
            .Where(c => c.Selectable)
            .Select(c => new ScreenEntry(Text(c.Code), c.Name, Screen.Home, EntryAction.SetSupport)));
        entries.Add(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home));
        return entries;
    }

    /// <summary>파트너/어시스트 선택 화면의 캐릭터 목록(화면 표시용).</summary>
    public IReadOnlyList<CharacterListItem> SupportList => Screen == Screen.SupportSelect ? SupportCharacterList() : [];
}
