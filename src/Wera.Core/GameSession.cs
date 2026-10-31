using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wera.Core;

[JsonConverter(typeof(JsonStringEnumConverter<Screen>))]
public enum Screen
{
    Home,
    DungeonSelect,
    ShopList,
    /// <summary>파티 설정(캐릭터 배치): 3×3 슬롯. 슬롯을 고르면 그 슬롯에 배치할 캐릭터 목록으로 간다.</summary>
    PartySetup,
    /// <summary>파티 이름 변경: 입력칸에 쓴 글이 새 파티 이름이 된다(Enter). 돌아가기는 들어온 화면(파티 설정 또는 장비 착용).</summary>
    PartyRename,
    /// <summary>파티 설정(장비 착용): 같은 3×3 슬롯. 슬롯을 고르면 그 캐릭터의 장비 목록으로 간다. 돌아가기는 홈.</summary>
    EquipmentSetup,
    /// <summary>파티 설정(리더 지정): 같은 3×3 슬롯. 리더 태그를 가진 캐릭터의 슬롯을 고르면 리더로 지정하거나 해제한다. 돌아가기는 홈.</summary>
    LeaderSetup,
    /// <summary>파티 설정(스킬 설정): 같은 3×3 슬롯. 캐릭터가 있는 슬롯을 고르면 그 캐릭터의 스킬 설정 화면으로 간다. 돌아가기는 홈.</summary>
    SkillSetup,
    /// <summary>선택한 슬롯 캐릭터의 스킬 슬롯(장착한 스킬)을 순서대로 보여 주고, 구분선 아래에 스킬 풀 버튼이 있다. 돌아가기는 스킬 설정.</summary>
    SkillCharacter,
    /// <summary>선택한 스킬 슬롯에 넣을 수 있는 스킬 풀 안의 스킬 목록. 고르면 장착한다. 돌아가기는 캐릭터의 스킬 설정.</summary>
    SkillSlotSelect,
    /// <summary>선택한 캐릭터가 지금까지 익힌 스킬 목록(스킬 풀). 돌아가기는 캐릭터의 스킬 설정.</summary>
    SkillPoolList,
    /// <summary>전투(임시) 화면(110): 정해진 전투의 작전 화면(슬롯 네 줄과 가운데 영역). 돌아가기는 홈.</summary>
    Battle,
    /// <summary>전투 화면에서 슬롯 번호나 이미지를 눌러 간 캐릭터 상태 확인(상세) 페이지. 돌아가기는 전투.</summary>
    UnitStatus,
    /// <summary>선택한 슬롯 캐릭터의 장비 슬롯 목록. 돌아가기는 장비 착용 화면.</summary>
    EquipmentList,
    /// <summary>선택한 장비 슬롯에 낄 수 있는 장비 목록(슬롯 태그 마스터에 부합하는 장비). 돌아가기는 장비 슬롯 목록.</summary>
    EquipmentSelect,
    /// <summary>선택한 슬롯에 배치할 캐릭터 목록.</summary>
    CharacterSelect,
    /// <summary>이벤트 화면(103): 조건을 만족하는 이벤트 목록. 하나를 고르면 그 이벤트를 시작한다.</summary>
    Event,
    /// <summary>진행 중인 이벤트의 현재 장면(ADV 재생 또는 전투). [1] 다음으로 이벤트를 이어 가며, 끝나면 이벤트 화면으로 돌아간다.</summary>
    EventPlay,
    /// <summary>교류 화면(104): 교류 그룹과 그 그룹의 교류 목록.</summary>
    Interaction,
    /// <summary>교류 진행 화면: 위쪽 슬롯은 그대로 두고 아래에서 고른 교류의 흐름을 재생한다.</summary>
    CommunicationPlay,
    /// <summary>디버그 화면(400): 캐릭터 추가, 장비 추가, 아이템 추가 버튼. 돌아가기는 홈.</summary>
    Debug,
    /// <summary>디버그: 마스터의 모든 캐릭터를 페이지당 20개씩 보여 주고 누르면 추가한다. 돌아가기는 디버그 화면.</summary>
    DebugAddCharacter,
    /// <summary>디버그: 마스터의 모든 유니크 아이템(장비)을 페이지당 20개씩 보여 주고 누르면 추가한다. 돌아가기는 디버그 화면.</summary>
    DebugAddEquipment,
    /// <summary>디버그: 마스터의 모든 커먼 아이템을 페이지당 20개씩 보여 주고 누르면 추가한다. 돌아가기는 디버그 화면.</summary>
    DebugAddItem,
    /// <summary>디버그 스킬 추가의 1단계: 가진 캐릭터 중 스킬을 받을 캐릭터를 고른다. 돌아가기는 디버그.</summary>
    DebugSkillCharacter,
    /// <summary>디버그 스킬 추가의 2단계: 마스터의 모든 스킬을 늘어놓고, 누르면 고른 캐릭터의 스킬 풀에 더한다. 돌아가기는 캐릭터 고르기.</summary>
    DebugAddSkill,
    /// <summary>동료 확인 화면(201): 가진 캐릭터 목록.</summary>
    CompanionCheck,
    /// <summary>장비 확인 화면(202): 가진 유니크 아이템 목록.</summary>
    EquipmentCheck,
    /// <summary>아이템 확인 화면(203): 가진 커먼 아이템과 수량 목록.</summary>
    ItemCheck,
    /// <summary>홈 화면의 파트너/어시스트 슬롯에 지정할 캐릭터 목록(파티 설정과 독립). 돌아가기는 홈.</summary>
    SupportSelect,
    /// <summary>선택한 상점의 교환 목록.</summary>
    ShopTrades,
    Load,
}

public enum EntryAction
{
    /// <summary>Target 화면으로 이동한다.</summary>
    Navigate,
    /// <summary>현재 상태를 세이브 파일로 내보낸다. 파일 저장은 UI가 맡고 화면은 바뀌지 않는다.</summary>
    Save,
    /// <summary>항목의 코드에 해당하는 상점 교환을 실행한다. 결과는 <see cref="GameSession.Message"/>.</summary>
    Trade,
    /// <summary>리더 지정 화면의 슬롯: Arg의 칸에 있는 유닛을 리더로 지정하거나(이미 리더면) 해제한다.</summary>
    ToggleLeader,
    /// <summary>전투(임시)를 시작한다: 정해진 전투의 적 파티를 뽑고 작전 화면으로 간다.</summary>
    StartBattle,
    /// <summary>작전 화면의 커맨드 중 아직 동작이 없는 것([103] 상태 확인, [105] 아이템, [106] 실행).</summary>
    BattleCommand,
    /// <summary>이전(-1)/다음(1) 캐릭터(Arg)로 보는 캐릭터를 옮긴다.</summary>
    BattleSelectCharacter,
    /// <summary>행동 설정: 커맨드를 보는 캐릭터의 행동 후보로 바꾸고 화면을 적 3 / 아군 1로 바꾼다.</summary>
    BattleOpenActions,
    /// <summary>행동 후보에서 스킬(Arg)을 고른다.</summary>
    BattleChooseSkill,
    /// <summary>행동 후보에서 나와 기본 커맨드로 돌아간다.</summary>
    BattleCancelActions,
    /// <summary>대상 후보에서 유닛을 고른다(Arg = "E슬롯"(적) 또는 "A슬롯"(아군)).</summary>
    BattleChooseTarget,
    /// <summary>[106] 실행: 이번 턴의 행동을 순서대로 실행한다.</summary>
    BattleExecute,
    /// <summary>전투 화면의 슬롯 번호: 그 캐릭터의 상태 확인 페이지로 간다(Arg = "E슬롯"(적) 또는 "A슬롯"(아군)).</summary>
    BattleViewUnit,
    /// <summary>[107] 도망: 전투를 끝낸다.</summary>
    BattleRun,
    /// <summary>작전 화면을 위(-1)나 아래(1) 구성으로 바로 넘긴다(Arg): 적 3 / 아군 1 ↔ 적 1 / 아군 3.</summary>
    BattleScroll,
    /// <summary>파티 이름 변경 화면으로 간다(들어온 화면을 기억해 두었다가 끝나면 돌아간다).</summary>
    RenameParty,
    /// <summary>파티 선택 버튼: Arg의 번호(0이 파티-A)의 파티를 보는 파티로 한다. 화면은 그대로다.</summary>
    SelectParty,
    /// <summary>항목의 코드에 해당하는 캐릭터를 선택한 슬롯에 배치한다(이미 배치된 캐릭터면 위치 교환).</summary>
    PlaceCharacter,
    /// <summary>선택한 슬롯의 캐릭터를 파티에서 뺀다(슬롯이 빈다).</summary>
    ClearSlot,
    /// <summary>항목의 코드에 해당하는 캐릭터를 홈의 파트너/어시스트 슬롯에 지정한다(다른 쪽에 있던 캐릭터면 서로 바뀐다).</summary>
    SetSupport,
    /// <summary>선택한 파트너/어시스트 슬롯을 비운다.</summary>
    ClearSupport,
    /// <summary>항목의 코드에 해당하는 보유 장비를 선택한 캐릭터의 선택한 장비 슬롯에 끼운다(이미 끼고 있던 장비는 빠진다).</summary>
    EquipItem,
    /// <summary>선택한 캐릭터의 선택한 장비 슬롯의 장비를 뺀다.</summary>
    Unequip,
    /// <summary>항목의 코드에 해당하는 이벤트를 시작해 첫 장면으로 간다.</summary>
    StartEvent,
    /// <summary>진행 중인 이벤트를 다음 장면으로 이어 간다(더 없으면 이벤트 화면으로).</summary>
    NextEventStep,
    /// <summary>ADV의 선택지 장면에서 항목이 가리키는(Arg = 0부터 센 번호) 선택지를 고른다.</summary>
    ChooseAdv,
    /// <summary>교류 화면에서 항목의 코드(그룹 코드×100)에 해당하는 교류 그룹을 고른다. 화면은 그대로다.</summary>
    SelectCommunicationGroup,
    /// <summary>교류 화면에서 항목의 코드에 해당하는 교류를 시작해 흐름을 재생한다.</summary>
    StartCommunication,
    /// <summary>스킬 슬롯 선택 화면에서 스킬 풀의 스킬(Arg = 풀 번호)을 그 슬롯에 장착한다.</summary>
    EquipSkill,
    /// <summary>스킬 슬롯 선택 화면에서 그 슬롯의 스킬을 뺀다.</summary>
    ClearSkillSlot,
    /// <summary>디버그 추가 화면에서 항목(Arg = 마스터 코드)을 하나 추가한다. 화면은 그대로다.</summary>
    DebugAdd,
    /// <summary>디버그 스킬 추가에서 스킬을 받을 캐릭터(Arg = 캐릭터 코드)를 고른다.</summary>
    DebugSelectSkillCharacter,
    /// <summary>디버그 추가 화면의 페이지를 넘긴다(Arg = -1 이전, 1 다음).</summary>
    DebugPage,
}

/// <summary>화면에 나오는 클릭 가능한 텍스트. 클릭하거나 Code를 입력하면 Action을 수행한다.</summary>
/// <param name="Arg">항목이 가리키는 추가 값(파티 슬롯 항목에서는 슬롯 번호 0~8).</param>
/// <param name="LabelHtml">꾸밈 문법(굵게, 색)이 적용된 문구의 HTML. 없으면 Label을 그대로 보여 준다(ADV 선택지가 쓴다).</param>
/// <param name="Enabled">false면 회색으로 보이고 눌러도(코드를 입력해도) 아무 일도 없다(지금 할 수 없는 상점 교환).</param>
public sealed record ScreenEntry(
    string Code, string Label, Screen Target, EntryAction Action = EntryAction.Navigate, string? Arg = null, string? LabelHtml = null,
    bool Enabled = true);

/// <param name="Version">릴리스 전이라 1로 유지한다. 지시가 있을 때까지 올리지 않는다.</param>
public sealed record SaveData(
    int Version,
    string PackId,
    Screen Screen = Screen.Home,
    Dictionary<long, long>? PlayerFlags = null,
    Dictionary<long, InventoryItem>? InventoryItems = null,
    Dictionary<long, CharacterInstance>? Characters = null,
    long?[]? Party = null,
    Dictionary<long, List<ActiveStatusEffect>>? StatusEffects = null,
    Dictionary<string, UniqueItemInstance>? UniqueItems = null,
    Dictionary<long, List<long>>? CharacterTags = null,
    Dictionary<long, Dictionary<long, EquipRef>>? EquippedItems = null,
    long? Partner = null,
    long? Assist = null,
    long? Turn = null,
    PartyInstance? PartyInstance = null,
    PartyInstance?[]? Parties = null,
    int? CurrentParty = null);

[JsonSerializable(typeof(SaveData))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class SaveJsonContext : JsonSerializerContext;

public sealed class SaveFormatException(string message) : Exception(message);

/// <summary>메뉴의 한 분류: 이름(화면에는 [이름]으로 나온다)과 그 아래 항목들.</summary>
public sealed record MenuGroup(string Name, IReadOnlyList<ScreenEntry> Entries);

public sealed partial class GameSession
{
    public const int SaveVersion = 1;

    public const string DungeonEntryCode = "101";
    public const string ShopEntryCode = "102";
    public const string EventEntryCode = "103";
    public const string InteractionEntryCode = "104";
    public const string PartyEntryCode = "105";
    public const string BattleEntryCode = "110";
    public const string SaveEntryCode = "200";
    public const string CompanionCheckEntryCode = "201";
    public const string EquipmentCheckEntryCode = "202";
    public const string ItemCheckEntryCode = "203";
    public const string LoadEntryCode = "300";
    public const string DebugEntryCode = "400";
    public const string BackEntryCode = "999";

    /// <summary>캐릭터 선택 화면의 "[0] 비우기": 선택한 슬롯에 캐릭터가 있을 때만 나온다.</summary>
    public const string ClearEntryCode = "0";

    /// <summary>파티 설정 화면 아래의 텍스트 버튼: 캐릭터 배치 화면 / 장비 착용 화면으로 바꾼다.</summary>
    public const string PlacementModeCode = "101";
    public const string EquipmentModeCode = "102";
    public const string SkillModeCode = "103";
    public const string LeaderModeCode = "104";

    /// <summary>파티 선택 버튼의 코드는 파티 A가 11이고 파티 P가 26이다(<see cref="PartySelectCodeBase"/> + 파티 번호).</summary>
    public const int PartySelectCodeBase = 11;

    /// <summary>파티 설정 화면 제목의 "[0] 파티명": 누르면 파티 이름을 바꾼다.</summary>
    public const string RenamePartyEntryCode = "0";

    /// <summary>파티 이름의 최대 글자 수.</summary>
    public const int MaxPartyNameLength = 20;

    /// <summary>파티 이름을 바꾸고 돌아갈 화면.</summary>
    Screen partyRenameReturn = Screen.PartySetup;

    /// <summary>플레이어가 가진 파티 수: 파티-A ~ 파티-P. 파티끼리는 캐릭터도 장비도 공유할 수 없다(다른 전투에 파견하기 위함).</summary>
    public const int PartyCount = 16;

    /// <summary>파티 슬롯 수: 전위/중위/후위 3줄 × 3칸.</summary>
    public const int PartySlotCount = 9;

    public GamePack Pack { get; }
    public Screen Screen { get; private set; }
    /// <summary>플레이어_플래그: 플래그 코드 → long 값.</summary>
    public Dictionary<long, long> PlayerFlags { get; }

    /// <summary>인벤토리_아이템: 아이템 코드 → 구조체.</summary>
    public Dictionary<long, InventoryItem> InventoryItems { get; }

    /// <summary>보유 캐릭터: 캐릭터 코드 → 현재 상태. 코드는 캐릭터 인벤토리 마스터에 있어야 한다.</summary>
    public Dictionary<long, CharacterInstance> Characters { get; }

    /// <summary>
    /// 파티: 이름과 유닛 아홉 칸(유닛 = 캐릭터 + 장비). 칸 번호 0~2 전위, 3~5 중위, 6~8 후위, 빈 칸은 null.
    /// 장비는 유닛에 속하므로 유닛이 파티에서 빠지면 장비는 보유로 돌아간다.
    /// </summary>
    public PartyInstance PartyInstance => Parties[CurrentPartyIndex];

    /// <summary>
    /// 파티 열여섯 개(파티-A ~ 파티-P). 파티끼리는 캐릭터와 장비를 공유할 수 없다. 파티 설정과 캐릭터 배치, 장비 착용은
    /// 지금 보는 파티(<see cref="CurrentPartyIndex"/>)를 따른다.
    /// </summary>
    public PartyInstance[] Parties { get; }

    /// <summary>지금 보는 파티의 번호(0이 파티-A). 새 게임은 파티-A다.</summary>
    public int CurrentPartyIndex { get; private set; }

    /// <summary>지금 보는 파티의 이름.</summary>
    public string PartyName { get => PartyInstance.Name; set => Parties[CurrentPartyIndex] = PartyInstance with { Name = value }; }

    /// <summary>파티의 처음 이름: "파티-A" ~ "파티-P".</summary>
    public static string DefaultPartyNameOf(int index) => "파티-" + (char)('A' + index);

    /// <summary>이 캐릭터의 유닛이 있는 파티의 번호(어느 파티에도 없으면 -1).</summary>
    public int PartyIndexOf(long characterCode)
    {
        for (var i = 0; i < Parties.Length; i++)
            if (Parties[i].IndexOf(characterCode) >= 0) return i;
        return -1;
    }

    /// <summary>이 캐릭터의 유닛(어느 파티든). 파티에 없으면 null.</summary>
    public PartyUnit? UnitOf(long characterCode) =>
        PartyIndexOf(characterCode) is var at and >= 0 ? Parties[at].UnitOf(characterCode) : null;

    /// <summary>칸 번호로 캐릭터 코드를 읽고 쓰는 창(<see cref="PartySlots"/>): 길이 9, 빈 칸은 null.</summary>
    public PartySlots Party => new(PartyInstance);

    /// <summary>
    /// 캐릭터에 걸려 있는 상태이상: 캐릭터 코드 → 상태이상 목록. 각 이펙트의 시점, 우선도, 이펙트 코드, 밸류 1~3, 카운터, 유효 플래그를
    /// 그대로 들고 있어 세이브에서 복구할 수 있다. 비어 있는 캐릭터는 키가 없다.
    /// </summary>
    public Dictionary<long, List<ActiveStatusEffect>> CharacterStatusEffects { get; }

    /// <summary>교환 목록 화면에서 보고 있는 상점 코드. 그 화면이 아니면 null.</summary>
    public long? CurrentShopCode { get; private set; }

    /// <summary>교류 화면의 항목 코드는 그룹 코드에 이 값을 곱한 것이다("[{code * 100}] {name}").</summary>
    public const long CommunicationGroupEntryFactor = 100;

    /// <summary>교류 그룹들(코드 순서: 가장 작은 코드가 위).</summary>
    public IReadOnlyList<CommunicationGroupEntry> CommunicationGroups =>
        (Pack.CommunicationGroups ?? []).OrderBy(g => g.Code).ToList();

    /// <summary>교류 화면에서 고른 그룹의 코드. 화면에 들어가면 가장 작은 그룹이고 그룹이 없으면 null. 저장하지 않는다.</summary>
    public long? CurrentCommunicationGroupCode { get; private set; }

    /// <summary>캐릭터 선택 화면에서 배치 중인 슬롯(0~8). 그 화면이 아니면 null.</summary>
    public int? CurrentSlot { get; private set; }

    /// <summary>선택한 슬롯(<see cref="CurrentSlot"/>)에 있는 캐릭터의 이름. 슬롯이 없으면 null, 빈 슬롯이면 "빈 슬롯".</summary>
    public string? CurrentSlotCharacterName => CurrentSlot is { } slot ? PartySlotViews[slot].Name : null;

    /// <summary>선택한 슬롯(<see cref="CurrentSlot"/>)에 있는 캐릭터의 코드. 슬롯이 없거나 비어 있으면 null.</summary>
    public long? CurrentCharacterCode => CurrentSlot is { } slot ? PartyInstance.Units[slot]?.CharacterCode : null;

    /// <summary>장비 가능 목록 화면에서 보고 있는 장비 슬롯 코드. 그 화면이 아니면 null.</summary>
    public long? CurrentEquipmentSlotCode { get; private set; }

    public EquipmentSlotEntry? CurrentEquipmentSlot =>
        CurrentEquipmentSlotCode is { } slotCode ? Pack.FindEquipmentSlot(slotCode) : null;

    /// <summary>선택한 캐릭터가 가진 장비 슬롯들.</summary>
    IReadOnlyList<EquipmentSlotEntry> CurrentEquipmentSlots =>
        CurrentCharacterCode is { } character ? Pack.EquipmentSlotsOf(character) : [];

    GameSession(
        GamePack pack, Screen screen, Dictionary<long, long> playerFlags,
        Dictionary<long, InventoryItem> inventoryItems, Dictionary<long, CharacterInstance> characters, PartyInstance[] parties,
        Dictionary<long, List<ActiveStatusEffect>> statusEffects,
        Dictionary<string, UniqueItemInstance> uniqueItems, Dictionary<long, List<long>> characterTags)
    {
        Pack = pack;
        Screen = screen;
        PlayerFlags = playerFlags;
        InventoryItems = inventoryItems;
        Characters = characters;
        Parties = parties;
        CharacterStatusEffects = statusEffects;
        UniqueItemInstances = uniqueItems;
        CharacterTagInstances = characterTags;
    }

    /// <summary>
    /// 캐릭터를 새로 얻는다: 캐릭터 인벤토리 마스터에 있고 아직 보유하지 않았을 때, 캐릭터 스테이터스 마스터의 레벨 0 값으로
    /// 상태를 채워 보유 캐릭터에 넣는다. 태그는 캐릭터 태그 마스터에서 복사한다. 마스터에 없거나 이미 보유하면 false.
    /// </summary>
    /// <summary>다음에 얻는 캐릭터의 OrderNumber: 소지 캐릭터 수(0부터 시작). 순서를 바꿀 때는 OrderNumber를 직접 바꾼다.</summary>
    static long NextOrderNumber(IEnumerable<CharacterInstance> owned) => owned.Count();

    /// <summary>
    /// 지금 가진 캐릭터 인스턴스 전부(플레이어 포함), OrderNumber순이고 같으면 InstanceId순(동료 확인 화면의 목록).
    /// 마스터 코드는 보유 목록의 키로 맞춘다.
    /// </summary>
    public IReadOnlyList<CharacterInstance> OwnedCharacters =>
        Characters.Select(kv => kv.Value with { CharacterCode = kv.Key })
            .OrderBy(c => c.OrderNumber).ThenBy(c => c.InstanceId, StringComparer.Ordinal).ToList();

    /// <summary>동료 확인 코드 칸("[OrderNumber]")의 너비(반각 칸 수). 코드 뒤는 이름 앞까지 공백으로 채운다.</summary>
    public const int CompanionCodeColumns = 8;

    /// <summary>동료 확인 이름 칸의 너비(반각 칸 수; 한글 등 전각 문자는 2칸).</summary>
    public const int CompanionNameColumns = 32;

    /// <summary>동료 확인 Lv, Hp, Xp 칸 하나의 너비(반각 칸 수). 칸 안에는 "Lv 12"처럼 이름표와 값이 왼쪽 맞춤으로 들어간다.</summary>
    public const int CompanionStatColumns = 8;

    /// <summary>동료 확인 화면의 한 줄: 코드 칸("[순서 번호]"), 이름, 그리고 "Lv 값", "Hp 값", "Xp 값" 칸.</summary>
    public sealed record CompanionRow(string Order, string Name, string Level, string Hp, string Xp);

    /// <summary>
    /// 동료 확인 화면의 줄들: <see cref="OwnedCharacters"/> 순서대로 "[OrderNumber] 이름 Lv 레벨 Hp HP Xp XP".
    /// 코드 칸은 반각 <see cref="CompanionCodeColumns"/>칸, 이름 칸은 반각 <see cref="CompanionNameColumns"/>칸, Lv, Hp, Xp 칸은 반각 <see cref="CompanionStatColumns"/>칸씩이고 모두 왼쪽 맞춤이다
    /// (코드와 이름 사이는 공백으로 채운다). 화면은 이 칸 수(ch) 고정 너비로 그려서 글꼴과 상관없이 칸이 맞는다.
    /// </summary>
    public IReadOnlyList<CompanionRow> Companions
    {
        get
        {
            return OwnedCharacters.Select(c => new CompanionRow(
                $"[{c.OrderNumber.ToString(CultureInfo.InvariantCulture)}]",
                Pack.FindCharacter(c.CharacterCode)?.Name ?? Text(c.CharacterCode),
                $"Lv {c.Level.ToString(CultureInfo.InvariantCulture)}",
                $"Hp {c.Hp.ToString(CultureInfo.InvariantCulture)}",
                $"Xp {c.Xp.ToString(CultureInfo.InvariantCulture)}")).ToList();
        }
    }

    /// <summary>동료 확인 화면의 줄들을 글로 쓴 것(각 칸을 정해진 칸 수까지 공백으로 채움). 화면은 <see cref="Companions"/>로 같은 줄을 그린다.</summary>
    public IReadOnlyList<string> CompanionLines =>
        Companions.Select(r => $"{PadColumns(r.Order, CompanionCodeColumns)} {PadColumns(r.Name, CompanionNameColumns)} {PadColumns(r.Level, CompanionStatColumns)} " +
                               $"{PadColumns(r.Hp, CompanionStatColumns)} {PadColumns(r.Xp, CompanionStatColumns)}").ToList();

    /// <summary>아이템 확인 수량 칸의 너비(반각 칸 수). 칸 안에는 "수량 99"처럼 이름표와 값이 왼쪽 맞춤으로 들어간다.</summary>
    public const int ItemAmountColumns = 16;

    /// <summary>아이템 확인 화면의 한 줄: 코드 칸("[아이템 코드]"), 이름, "수량 값" 칸.</summary>
    public sealed record ItemRow(string Code, string Name, string Amount);

    /// <summary>
    /// 아이템 확인 화면의 줄들: 지금 가진(수량이 1 이상인) 커먼 아이템을 커먼 아이템 인벤토리 마스터 순서대로 "[아이템 코드] 이름 수량 N".
    /// 칸 너비는 동료 확인과 같고(코드 <see cref="CompanionCodeColumns"/>, 이름 <see cref="CompanionNameColumns"/>) 수량 칸은 <see cref="ItemAmountColumns"/>칸이다.
    /// 장비에 끼워 둔 것도 가진 수량에 포함된다.
    /// </summary>
    public IReadOnlyList<ItemRow> Items =>
        (Pack.ItemInventory ?? [])
            .Where(i => InventoryItems.GetValueOrDefault(i.Code).Amount > 0)
            .Select(i => new ItemRow(
                $"[{i.Code.ToString(CultureInfo.InvariantCulture)}]",
                i.Name,
                $"수량 {InventoryItems[i.Code].Amount.ToString(CultureInfo.InvariantCulture)}")).ToList();

    static string PadColumns(string text, int columns) => text + new string(' ', Math.Max(0, columns - DisplayColumns(text)));

    /// <summary>글이 차지하는 칸 수(한글 등 전각 문자는 2칸).</summary>
    static int DisplayColumns(string text) => text.EnumerateRunes().Sum(RichText.ColumnsOf);

    public bool AddCharacter(long characterCode)
    {
        if (Pack.FindCharacter(characterCode) is null || Characters.ContainsKey(characterCode)) return false;
        Characters[characterCode] = new CharacterAfterCreateCircuit(Pack,
            Pack.NewCharacterInstance(characterCode, NewInstanceId(), NextOrderNumber(Characters.Values)),
            UniqueItemInstances, code => AddUniqueItem(code) is { } id ? UniqueItemInstances[id] : null).Execute();
        CharacterTagInstances[characterCode] = Pack.MasterTagsOfCharacter(characterCode).ToList();
        return true;
    }

    static PartyInstance[] NewParties() =>
        Enumerable.Range(0, PartyCount).Select(i => PartyInstance.Empty(DefaultPartyNameOf(i))).ToArray();

    /// <summary>튜토리얼이 구현될 때까지 새 게임은 홈 화면으로 직행한다.</summary>
    public static GameSession NewGame(GamePack pack)
    {
        var session = new GameSession(pack, Screen.Home, [], [], [], NewParties(), [], [], []);
        session.AddCharacter(GamePack.PlayerCharacterCode);      // 플레이어(캐릭터 1)는 처음부터 보유한다
        foreach (var itemCode in pack.DebugStartUniqueItems) session.AddUniqueItem(itemCode);   // 디버깅용 초기 부여
        session.TurnCount = pack.FirstTurn.TurnCount;            // 가장 작은 턴에서 시작한다
        return session;
    }

    /// <summary>입장 조건을 하나라도 만족하는 던전.</summary>
    public IReadOnlyList<Dungeon> AvailableDungeons =>
        Pack.Dungeons?.Where(d => d.IsAvailable(PlayerFlags)).ToList() ?? [];

    /// <summary>입장 조건을 하나라도 만족하는 상점.</summary>
    public IReadOnlyList<Shop> AvailableShops =>
        Pack.Shops?.Where(s => s.IsAvailable(PlayerFlags)).ToList() ?? [];

    public Shop? CurrentShop => Pack.Shops?.FirstOrDefault(s => s.Code == CurrentShopCode);

    /// <summary>현재 상점에서 조건을 만족하는 교환. 아이템 코드는 이름으로 풀어 둔다.</summary>
    public IReadOnlyList<TradeLine> CurrentShopTrades =>
        CurrentShopCode is null
            ? []
            : Pack.Trades?
                .Where(t => t.ShopCode == CurrentShopCode && t.IsAvailable(PlayerFlags))
                .Select(t => new TradeLine(
                    t.Code,
                    Pack.TradeTargetName(t.CostKind, t.CostItemCode), t.CostAmount,
                    Pack.TradeTargetName(t.ReceiveKind, t.ReceiveItemCode), t.ReceiveAmount))
                .ToList() ?? [];

    string CharacterName(long code) =>
        Pack.FindCharacter(code)?.Name ?? code.ToString(CultureInfo.InvariantCulture);

    /// <summary>파티 설정 화면의 9개 슬롯. 비었거나 상태를 알 수 없으면 "빈 슬롯"(회색 0/0).</summary>
    public IReadOnlyList<PartySlotView> PartySlotViews =>
        Enumerable.Range(0, PartySlotCount).Select(SlotView).ToList();

    PartySlotView SlotView(int slot) =>
        CharacterView(slot, Party[slot]) with { IsLeader = PartyInstance.Units[slot]?.IsLeader == true };

    /// <summary>캐릭터 한 명의 슬롯 표시. 코드가 없거나 보유하지 않으면 빈 슬롯. 슬롯 번호가 없는 슬롯은 -1.</summary>
    PartySlotView CharacterView(int slot, long? characterCode)
    {
        if (characterCode is not { } code || !Characters.TryGetValue(code, out var state))
        {
            return PartySlotView.Empty(slot);
        }
        return new PartySlotView(
            slot, code, $"Lv.{state.Level}", CharacterName(code),
            Gauge.Render(state.Hp, state.MaxHp, gray: state.IsDead),
            Gauge.Render(state.Xp, state.XpMax, gray: state.IsDead),
            AttributeBadges(code),
            StatusBadges(code));
    }

    /// <summary>캐릭터에 걸려 있는 상태이상의 배지(걸린 순서). 이름은 상태이상 마스터에서 찾는다.</summary>
    IReadOnlyList<StatusBadge> StatusBadges(long characterCode) =>
        StatusEffectsOf(characterCode)
            .Select(s => new StatusBadge(
                s.StatusEffectCode,
                Pack.FindStatusEffect(s.StatusEffectCode)?.Name
                    ?? s.StatusEffectCode.ToString(CultureInfo.InvariantCulture)))
            .ToList();

    /// <summary>캐릭터의 유리속성(⭕), 불리속성(❌) 순서의 배지.</summary>
    IReadOnlyList<AttributeBadge> AttributeBadges(long characterCode)
    {
        var character = Pack.FindCharacter(characterCode);
        if (character is null) return [];
        string NameOf(long attribute) =>
            Pack.FindAttribute(attribute)?.Name ?? attribute.ToString(CultureInfo.InvariantCulture);
        return
        [
            new AttributeBadge(character.AdvantageAttributeCode, NameOf(character.AdvantageAttributeCode), Advantage: true),
            new AttributeBadge(character.DisadvantageAttributeCode, NameOf(character.DisadvantageAttributeCode), Advantage: false),
        ];
    }

    /// <summary>
    /// 캐릭터 선택 화면의 목록(캐릭터 인벤토리 마스터 순서). 보유하지 않은 캐릭터는 목록에 나오지 않는다.
    /// 이 슬롯에 이미 있는 캐릭터는 목록에 나오지 않는다. 다른 슬롯에 있는 파티원은 파란색, 나머지는 흰색.
    /// </summary>
    public IReadOnlyList<CharacterListItem> CharacterList
    {
        get
        {
            if (Screen == Screen.SupportSelect) return SupportList;
            if (Screen != Screen.CharacterSelect || CurrentSlot is not { } slot) return [];
            // 파티에 아무도 없을 때와 리더의 슬롯을 바꿀 때는 리더 태그를 가진 캐릭터만 나온다(파티에는 리더가 한 명 이상 있어야 하므로).
            var leaderOnly = IsLeaderOnlySlot(slot);
            return (Pack.CharacterInventory ?? []).Where(c => Characters.ContainsKey(c.Code) && PartyInstance.Units[slot]?.CharacterCode != c.Code
                && (!leaderOnly || HasLeaderTag(c.Code))).Select(c =>
            {
                var placedAt = PartyInstance.IndexOf(c.Code);
                var color = !Characters.ContainsKey(c.Code) ? CharacterListColor.Gray
                    : placedAt >= 0 ? CharacterListColor.Green
                    : PartyIndexOf(c.Code) >= 0 ? CharacterListColor.Gray          // 다른 파티의 유닛: 파티끼리는 캐릭터를 공유할 수 없다
                    : CharacterListColor.White;
                return new CharacterListItem(c.Code, c.Name, color);
            }).ToList();
        }
    }

    /// <summary>
    /// 홈 화면의 메뉴: 구분선으로 나뉜 세 분류. [RPG] 던전(101, 입장 가능한 것이 없으면 회색), 상점(102, 열린 상점이 없으면 회색),
    /// 이벤트(103), 교류(104), 파티 설정(105), 전투(임시)(110; 지금 보는 파티를 파견할 수 없으면 회색) / [정보] 동료 확인(201), 장비 확인(202), 아이템 확인(203) / [시스템] 세이브(200), 로드(300), 디버그(400).
    /// 홈의 <see cref="Entries"/>는 이 분류들을 순서대로 이어 붙인 것이다.
    /// </summary>
    public IReadOnlyList<MenuGroup> HomeMenu
    {
        get
        {
            var rpg = new List<ScreenEntry>();
            // 입장할 수 있는 던전/상점이 없으면 메뉴에서 빼지 않고 회색(눌러도 반응 없음)으로 둔다.
            rpg.Add(new ScreenEntry(DungeonEntryCode, "던전", Screen.DungeonSelect, Enabled: AvailableDungeons.Count > 0));
            rpg.Add(new ScreenEntry(ShopEntryCode, "상점", Screen.ShopList, Enabled: AvailableShops.Count > 0));
            rpg.Add(new ScreenEntry(EventEntryCode, "이벤트", Screen.Event));
            rpg.Add(new ScreenEntry(InteractionEntryCode, "교류", Screen.Interaction));
            rpg.Add(new ScreenEntry(PartyEntryCode, "파티 설정", Screen.PartySetup));
            // 리더가 없는 파티는 던전(전투)에 파견할 수 없다: 지금 보는 파티가 파견 가능하지 않으면 회색이다.
            rpg.Add(new ScreenEntry(BattleEntryCode, "전투(임시)", Screen.Battle, EntryAction.StartBattle, Enabled: IsDispatchable(CurrentPartyIndex)));

            return
            [
                new MenuGroup("RPG", rpg),
                new MenuGroup("정보",
                [
                    new ScreenEntry(CompanionCheckEntryCode, "동료 확인", Screen.CompanionCheck),
                    new ScreenEntry(EquipmentCheckEntryCode, "장비 확인", Screen.EquipmentCheck),
                    new ScreenEntry(ItemCheckEntryCode, "아이템 확인", Screen.ItemCheck),
                ]),
                new MenuGroup("시스템",
                [
                    new ScreenEntry(SaveEntryCode, "세이브", Screen.Home, EntryAction.Save),
                    new ScreenEntry(LoadEntryCode, "로드", Screen.Load),
                    new ScreenEntry(DebugEntryCode, "디버그", Screen.Debug),
                ]),
            ];
        }
    }

    /// <summary>현재 화면에서 클릭하거나 코드를 입력할 수 있는 텍스트들.</summary>
    public IReadOnlyList<ScreenEntry> Entries
    {
        get
        {
            switch (Screen)
            {
                case Screen.Home:
                    return HomeMenu.SelectMany(group => group.Entries).ToList();
                case Screen.ShopList:
                    // 상점 코드를 입력하거나 클릭하면 그 상점의 교환 목록으로 간다.
                    return AvailableShops
                        .Select(s => new ScreenEntry(Text(s.Code), s.Name, Screen.ShopTrades))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.ShopTrades:
                    // 교환 코드를 입력하거나 클릭하면 그 교환을 실행한다.
                    return CurrentShopTrades
                        .Select(t => new ScreenEntry(Text(t.Code), t.Text, Screen.ShopTrades, EntryAction.Trade, Enabled: CanTrade(t.Code)))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.ShopList))
                        .ToList();
                case Screen.PartySetup:
                    // 슬롯 번호 1~9를 입력하거나 슬롯의 이름/아이콘을 클릭하면 그 슬롯에 배치할 캐릭터 목록으로 간다.
                    return RenamePartyEntries()
                        .Concat(PartySlotViews
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.CharacterSelect, Arg: v.Slot.ToString())))
                        .Concat(PartySelectEntries(Screen.PartySetup))
                        .Concat(ModeEntries(Screen.PartySetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.EquipmentSetup:
                    // 캐릭터가 있는 슬롯만 고를 수 있다. 이 화면에서 돌아가면 파티 설정이 아니라 홈으로 간다.
                    return RenamePartyEntries()
                        .Concat(PartySlotViews
                        .Where(v => !v.IsEmpty)
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.EquipmentList, Arg: v.Slot.ToString())))
                        .Concat(PartySelectEntries(Screen.EquipmentSetup))
                        .Concat(ModeEntries(Screen.EquipmentSetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.LeaderSetup:
                    // 리더 태그를 가진 캐릭터의 슬롯만 고를 수 있다(고르면 리더로 지정하거나 해제). 돌아가면 홈으로 간다.
                    return RenamePartyEntries()
                        .Concat(PartySlotViews
                        .Where(v => !v.IsEmpty)
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.LeaderSetup, EntryAction.ToggleLeader, v.Slot.ToString(),
                            Enabled: HasLeaderTag(v.CharacterCode!.Value))))
                        .Concat(PartySelectEntries(Screen.LeaderSetup))
                        .Concat(ModeEntries(Screen.LeaderSetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.SkillSetup:
                    // 캐릭터가 있는 슬롯만 고를 수 있고 고르면 그 캐릭터의 스킬 설정으로 간다. 돌아가면 홈으로 간다.
                    return RenamePartyEntries()
                        .Concat(PartySlotViews
                        .Where(v => !v.IsEmpty)
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.SkillCharacter, Arg: v.Slot.ToString())))
                        .Concat(PartySelectEntries(Screen.SkillSetup))
                        .Concat(ModeEntries(Screen.SkillSetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.PartyRename:
                    return [new ScreenEntry(BackEntryCode, "돌아가기", partyRenameReturn)];
                case Screen.EquipmentList:
                    // 그 캐릭터가 가진 장비 슬롯(슬롯 코드를 입력하거나 클릭하면 그 슬롯에 낄 수 있는 장비 목록으로 간다).
                    return CurrentEquipmentSlots
                        .Select(s => new ScreenEntry(Text(s.Code), EquipmentSlotLabel(s), Screen.EquipmentSelect))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.EquipmentSetup))
                        .ToList();
                case Screen.EquipmentSelect:
                    return EquipmentSelectEntries();
                case Screen.CharacterSelect:
                    // 맨 위에 "[0] 비우기"(이 슬롯에 캐릭터가 있을 때만), 그다음 고를 수 있는 캐릭터(회색 제외)를 코드로 입력하거나 클릭하면 배치한다.
                    return ClearEntries()
                        .Concat(CharacterList
                            .Where(c => c.Selectable)
                            .Select(c => new ScreenEntry(Text(c.Code), c.Name, Screen.PartySetup, EntryAction.PlaceCharacter)))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.PartySetup))
                        .ToList();
                case Screen.SupportSelect:
                    return SupportSelectEntries();
                case Screen.DungeonSelect:
                    // 던전 입장은 아직 구현되지 않았다: 돌아가기만 있다.
                    return [new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home)];
                case Screen.Event:
                    // 조건을 만족하는 이벤트를 코드로 입력하거나 클릭하면 시작한다.
                    return AvailableEvents
                        .Select(e => new ScreenEntry(Text(e.Code), e.Name, Screen.EventPlay, EntryAction.StartEvent))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.EventPlay:
                case Screen.CommunicationPlay:
                    return EventPlayEntries();
                case Screen.Interaction:
                    // 교류 그룹을 코드(그룹 코드×100)로 입력하거나 클릭해 고른다. 고른 그룹의 항목은 선택할 수 없다.
                    // 그다음 고른 그룹의 교류들(코드순)이 교류 코드로 나오고, 고르면 그 교류의 흐름을 재생한다.
                    return CommunicationGroups
                        .Select(g => new ScreenEntry(Text(g.Code * CommunicationGroupEntryFactor), g.Name, Screen.Interaction,
                            EntryAction.SelectCommunicationGroup, Enabled: g.Code != CurrentCommunicationGroupCode))
                        .Concat(CurrentGroupCommunications
                            .Select(c => new ScreenEntry(Text(c.Code), c.Name, Screen.CommunicationPlay, EntryAction.StartCommunication)))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.Load:
                case Screen.CompanionCheck:
                case Screen.EquipmentCheck:
                case Screen.ItemCheck:
                    return [new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home)];
                case Screen.Battle:
                    return BattleEntries();
                case Screen.UnitStatus:
                    return [new ScreenEntry(BackEntryCode, "돌아가기", Screen.Battle)];
                case Screen.SkillCharacter:
                    return SkillCharacterEntries();
                case Screen.SkillSlotSelect:
                    return SkillSlotSelectEntries();
                case Screen.SkillPoolList:
                    return [new ScreenEntry(BackEntryCode, "돌아가기", Screen.SkillCharacter)];
                case Screen.Debug:
                case Screen.DebugAddCharacter:
                case Screen.DebugAddEquipment:
                case Screen.DebugAddItem:
                case Screen.DebugSkillCharacter:
                case Screen.DebugAddSkill:
                    return DebugEntries();
                default:
                    return [];
            }
        }
    }

    /// <summary>
    /// 임베디드 이펙트를 보유 캐릭터에게 실행한다. 이펙트가 바꾼 상태는 세이브가 받아들이는 범위로 보정해 저장한다.
    /// 이펙트 코드나 캐릭터를 찾을 수 없으면(보유하지 않았거나 마스터에 없음) 아무것도 하지 않고 false.
    /// </summary>
    public bool ApplyEffect(long effectCode, long targetCharacterCode, long value1 = 0, long value2 = 0, long value3 = 0)
    {
        var entry = Pack.FindCharacter(targetCharacterCode);
        if (!Pack.EmbeddedEffects.Contains(effectCode)
            || entry is null
            || !Characters.TryGetValue(targetCharacterCode, out var state))
            return false;

        // 타겟과 밸류를 넘겨 이펙트를 만들고(저장), 파라미터 없이 실행한다.
        var target = new Character(entry, state);
        var effect = Pack.EmbeddedEffects.Create(effectCode, target, value1, value2, value3)!;
        effect.Execute();
        Characters[targetCharacterCode] = target.State.Clamped();
        return true;
    }

    /// <summary>직전 Activate의 결과 안내(교환 성공/실패 등). 없으면 null. Activate마다 지워진다.</summary>
    public string? Message { get; private set; }

    /// <summary>
    /// 코드(공백 무시)에 해당하는 항목을 수행하고 그 항목을 돌려준다. 없으면 null.
    /// 이동 항목은 화면을 바꾸고, 교환 항목은 인벤토리를 바꾸며, 세이브 항목은 호출한 쪽(UI)이 파일로 내보낸다.
    /// </summary>
    public ScreenEntry? Activate(string code)
    {
        Message = null;
        if (Screen == Screen.PartyRename && code.Trim() != BackEntryCode) return RenamePartyTo(code);   // 쓴 글이 새 이름이다
        code = Normalize(code);
        var entry = Entries.Concat(HomeSlotEntries).FirstOrDefault(e => e.Code == code);
        if (entry is { Enabled: false }) return entry;       // 회색 항목은 눌러도 아무 반응이 없다
        RecordCommunicationCommand(entry);
        switch (entry?.Action)
        {
            case EntryAction.Navigate:
                CurrentShopCode = entry.Target == Screen.ShopTrades ? CodeOf(entry) : null;
                CurrentSlot = entry.Target switch
                {
                    Screen.CharacterSelect or Screen.EquipmentList => int.Parse(entry.Arg!, CultureInfo.InvariantCulture),
                    Screen.SkillCharacter when entry.Arg is not null => int.Parse(entry.Arg, CultureInfo.InvariantCulture),
                    Screen.EquipmentSelect or Screen.SkillCharacter or Screen.SkillSlotSelect or Screen.SkillPoolList => CurrentSlot,      // 같은 캐릭터를 계속 본다
                    _ => null,
                };
                CurrentEquipmentSlotCode = entry.Target == Screen.EquipmentSelect ? CodeOf(entry) : null;
                CurrentSkillSlotCode = entry.Target == Screen.SkillSlotSelect ? CodeOf(entry) : null;
                CurrentSupportRole = entry.Target == Screen.SupportSelect ? Enum.Parse<SupportRole>(entry.Arg!) : null;
                if (entry.Target == Screen.Interaction && Screen != Screen.CommunicationPlay)
                {
                    CurrentCommunicationGroupCode = CommunicationGroups.FirstOrDefault()?.Code;   // 들어가면 가장 작은 그룹이 선택되고
                    communicationHistory.Clear();                                                  // 이력은 비어 있다
                }
                if (IsDebugAddScreen(entry.Target)) DebugPage = 0;       // 추가 화면에 들어오면 첫 페이지다
                Screen = entry.Target;
                break;
            case EntryAction.SelectCommunicationGroup:
                CurrentCommunicationGroupCode = CodeOf(entry) / CommunicationGroupEntryFactor;
                break;
            case EntryAction.StartCommunication:
                BeginCommunication(CodeOf(entry));
                break;
            case EntryAction.SetSupport:
                SetSupport(CurrentSupportRole!.Value, CodeOf(entry));
                CurrentSupportRole = null;
                Screen = entry.Target;
                break;
            case EntryAction.ClearSupport:
                SetSupport(CurrentSupportRole!.Value, null);
                CurrentSupportRole = null;
                Screen = entry.Target;
                break;
            case EntryAction.Trade:
                Message = ExecuteTrade(CodeOf(entry));
                break;
            case EntryAction.PlaceCharacter:
                PlaceCharacter(CodeOf(entry));
                CurrentSlot = null;
                Screen = entry.Target;
                break;
            case EntryAction.RenameParty:
                partyRenameReturn = Screen;
                Screen = entry.Target;
                break;
            case EntryAction.SelectParty:
                CurrentPartyIndex = int.Parse(entry.Arg!, CultureInfo.InvariantCulture);
                Screen = entry.Target;
                break;
            case EntryAction.ClearSlot:
                Party[CurrentSlot!.Value] = null;
                CurrentSlot = null;
                Screen = entry.Target;
                break;
            case EntryAction.EquipSkill:
                Message = EquipSkillFromScreen(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                CurrentSkillSlotCode = null;
                Screen = entry.Target;                  // 같은 캐릭터의 스킬 슬롯 목록(CurrentSlot 유지)
                break;
            case EntryAction.ClearSkillSlot:
                Message = ClearSkillSlotFromScreen();
                CurrentSkillSlotCode = null;
                Screen = entry.Target;
                break;
            case EntryAction.DebugAdd:
                Message = DebugAddOne(long.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.DebugSelectSkillCharacter:
                DebugSkillTarget = long.Parse(entry.Arg!, CultureInfo.InvariantCulture);
                DebugPage = 0;
                Screen = entry.Target;
                break;
            case EntryAction.DebugPage:
                ChangeDebugPage(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.ToggleLeader:
                ToggleLeader(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                Screen = entry.Target;
                break;
            case EntryAction.EquipItem:
                Message = EquipFromScreen(ParseRef(entry.Arg!));
                CurrentEquipmentSlotCode = null;
                Screen = entry.Target;                  // 같은 캐릭터의 장비 슬롯 목록(CurrentSlot 유지)
                break;
            case EntryAction.Unequip:
                Message = UnequipFromScreen();
                CurrentEquipmentSlotCode = null;
                Screen = entry.Target;
                break;
            case EntryAction.StartBattle:
                BeginBattle();
                Screen = entry.Target;
                break;
            case EntryAction.BattleCommand:
                break;                                 // 아직 동작이 없다
            case EntryAction.BattleSelectCharacter:
                SelectBattleCharacter(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.BattleOpenActions:
                OpenBattleActionCandidates();
                break;
            case EntryAction.BattleChooseSkill:
                ChooseBattleSkill(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.BattleExecute:
                ExecuteBattleTurn();
                break;
            case EntryAction.BattleViewUnit:
                ViewBattleUnit(entry.Arg!);
                Screen = Screen.UnitStatus;
                break;
            case EntryAction.BattleRun:
                RunFromBattle();
                break;
            case EntryAction.BattleChooseTarget:
                ChooseBattleTarget(entry.Arg!);
                break;
            case EntryAction.BattleCancelActions:
                battlePendingSkill = null;
                BattleCommands = BattleCommandMode.Basic;
                break;
            case EntryAction.BattleScroll:
                ScrollBattle(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.StartEvent:
                BeginEvent(CodeOf(entry));
                break;
            case EntryAction.NextEventStep:
                ContinueEvent();
                break;
            case EntryAction.ChooseAdv:
                ChooseAdv(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
        }
        return entry;
    }

    /// <summary>현재 선택한 슬롯에 캐릭터가 있으면 "[0] 비우기" 항목 하나, 없으면 없음.</summary>
    IEnumerable<ScreenEntry> ClearEntries() =>
        CurrentSlot is { } slot && PartyInstance.Units[slot] is { } unit && !IsLastLeaderWithCompany(unit)
            ? [new ScreenEntry(ClearEntryCode, "비우기", Screen.PartySetup, EntryAction.ClearSlot)]
            : [];

    /// <summary>다른 유닛이 남아 있는데 이 유닛이 마지막 리더인가(비우면 파티에 리더가 없어지므로 비울 수 없다).</summary>
    bool IsLastLeaderWithCompany(PartyUnit unit) =>
        unit.IsLeader && LeaderCount(PartyInstance) <= 1 && PartyInstance.Units.Count(u => u is not null) > 1;

    /// <summary>
    /// 파티 설정 화면의 파티 선택 버튼 열여섯 개: [11] 파티-A ~ [26] 파티-P. 지금 보는 파티의 버튼은 선택할 수 없다(파란색).
    /// 고르면 그 파티를 보고(같은 화면에 머문다) 슬롯과 캐릭터 배치, 장비 착용은 그 파티를 따른다.
    /// </summary>
    /// <summary>파티 설정 화면 제목의 "[0] 파티명" 항목 하나: 누르면 파티 이름 변경 화면으로 간다.</summary>
    IEnumerable<ScreenEntry> RenamePartyEntries() =>
        [new ScreenEntry(RenamePartyEntryCode, PartyName, Screen.PartyRename, EntryAction.RenameParty)];

    /// <summary>
    /// 파티 이름 변경 화면에서 입력칸에 쓴 글을 새 이름으로 받는다: 앞뒤 공백을 없앤 글이 비어 있으면 그대로 두고,
    /// <see cref="MaxPartyNameLength"/>자를 넘으면 안내만 하고, 아니면 이름을 바꿔 들어온 화면으로 돌아간다.
    /// </summary>
    ScreenEntry? RenamePartyTo(string typed)
    {
        var name = typed.Trim();
        if (name.Length == 0) return null;
        if (name.Length > MaxPartyNameLength)
        {
            Message = $"파티 이름은 {MaxPartyNameLength}자까지입니다.";
            return null;
        }
        PartyName = name;
        Screen = partyRenameReturn;
        return new ScreenEntry(RenamePartyEntryCode, name, Screen, EntryAction.RenameParty);
    }

    IEnumerable<ScreenEntry> PartySelectEntries(Screen screen) =>
        Parties.Select((p, i) => new ScreenEntry(
            (PartySelectCodeBase + i).ToString(CultureInfo.InvariantCulture), p.Name, screen, EntryAction.SelectParty,
            i.ToString(CultureInfo.InvariantCulture), Enabled: i != CurrentPartyIndex));

    /// <summary>파티 설정 화면 아래의 모드 버튼들: [101] 캐릭터 배치, [102] 장비 착용, [103] 스킬 설정, [104] 리더 지정. 지금 보고 있는 모드의 버튼은 선택할 수 없다.</summary>
    static IEnumerable<ScreenEntry> ModeEntries(Screen current) =>
    [
        new ScreenEntry(PlacementModeCode, "캐릭터 배치", Screen.PartySetup, Enabled: current != Screen.PartySetup),
        new ScreenEntry(EquipmentModeCode, "장비 착용", Screen.EquipmentSetup, Enabled: current != Screen.EquipmentSetup),
        new ScreenEntry(SkillModeCode, "스킬 설정", Screen.SkillSetup, Enabled: current != Screen.SkillSetup),
        new ScreenEntry(LeaderModeCode, "리더 지정", Screen.LeaderSetup, Enabled: current != Screen.LeaderSetup),
    ];

    static string Text(long code) => code.ToString(CultureInfo.InvariantCulture);

    static long CodeOf(ScreenEntry entry) => long.Parse(entry.Code, CultureInfo.InvariantCulture);

    /// <summary>입력을 다듬는다. 앞뒤 공백을 없애고, 숫자면 앞자리 0을 없앤다("007001" → "7001").</summary>
    static string Normalize(string typed)
    {
        typed = typed.Trim();
        return long.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? Text(n) : typed;
    }

    /// <summary>
    /// 선택한 슬롯에 캐릭터를 둔다. 이미 다른 슬롯에 있으면 그 슬롯과 이 슬롯의 유닛을 맞바꾸고(빈 슬롯이면 이동; 장비는 유닛과 함께 간다),
    /// 어디에도 없으면 이 슬롯에 장비 없는 새 유닛으로 두며 원래 있던 유닛은 장비와 함께 파티에서 빠진다(장비는 보유로 돌아간다).
    /// 이 슬롯의 캐릭터를 다시 고르면 그대로다.
    /// </summary>
    void PlaceCharacter(long code)
    {
        var slot = CurrentSlot!.Value;
        var units = PartyInstance.Units;
        var at = PartyInstance.IndexOf(code);
        if (at == slot) return;
        if (at >= 0) (units[at], units[slot]) = (units[slot], units[at]);
        else units[slot] = new PartyUnit(code, [], IsLeader: IsLeaderOnlySlot(slot));     // 아무도 없던 파티의 첫 유닛이나 리더 자리를 이은 유닛은 리더다
    }

    /// <summary>
    /// 이 슬롯에 두는 캐릭터가 리더 태그를 가져야 하는가: 지금 보는 파티에 아무도 없거나 이 슬롯이 리더의 슬롯일 때.
    /// 이때 새로 두는 유닛은 리더가 된다.
    /// </summary>
    bool IsLeaderOnlySlot(int slot) =>
        PartyInstance.Units.All(u => u is null) || PartyInstance.Units[slot]?.IsLeader == true;

    /// <summary>이 캐릭터가 리더 태그(<see cref="GamePack.LeaderTagCode"/>)를 가졌는가(캐릭터 인스턴스의 지금 태그 기준).</summary>
    public bool HasLeaderTag(long characterCode) => CharacterTags(characterCode).Contains(GamePack.LeaderTagCode);

    /// <summary>이 파티의 리더 수.</summary>
    static int LeaderCount(PartyInstance party) => party.Units.Count(u => u is { IsLeader: true });

    /// <summary>
    /// 이 파티를 던전(전투)에 파견할 수 있는가: 리더가 한 명 이상 있어야 한다. 어떤 이유로 리더가 없는 파티(빈 파티, 리더가 리더 태그를 잃음,
    /// 리더가 파티에서 빠짐)는 다른 유닛이 있어도 파견할 수 없고, 다른 유닛이 리더가 되지도 않는다(리더 지정에서 다시 지정해야 한다).
    /// </summary>
    public bool IsDispatchable(int partyIndex) => LeaderCount(Parties[partyIndex]) > 0;

    /// <summary>리더가 리더 태그를 잃으면 리더에서 빠진다(리더 태그가 없는 리더는 없다). 다른 유닛이 대신 리더가 되지는 않는다.</summary>
    void DropLeadersWithoutTag()
    {
        foreach (var party in Parties)
            for (var i = 0; i < party.Units.Length; i++)
                if (party.Units[i] is { IsLeader: true } leader && !HasLeaderTag(leader.CharacterCode))
                    party.Units[i] = leader with { IsLeader = false };
    }

    /// <summary>
    /// 이 슬롯의 유닛을 리더로 지정하거나(이미 리더면) 해제한다. 리더 태그가 없으면 할 수 없고, 마지막 리더는 해제할 수 없다
    /// (파티에는 리더가 한 명 이상 있어야 한다).
    /// </summary>
    void ToggleLeader(int slot)
    {
        if (PartyInstance.Units[slot] is not { } unit || !HasLeaderTag(unit.CharacterCode)) return;
        if (unit.IsLeader && LeaderCount(PartyInstance) <= 1)
        {
            Message = "파티에는 리더가 한 명 이상 있어야 합니다.";
            return;
        }
        PartyInstance.Units[slot] = unit with { IsLeader = !unit.IsLeader };
    }

    /// <summary>
    /// 코스트를 인벤토리에서 내고 리시브를 받는다. 번치 아이템은 부품으로 풀어서 처리한다.
    /// 코스트가 모자라거나 소지제한을 넘거나 수량이 long 범위를 넘으면 아무것도 바꾸지 않고 사유를 돌려준다.
    /// </summary>
    string ExecuteTrade(long tradeCode)
    {
        var trade = Pack.Trades!.First(t => t.Code == tradeCode);
        var text = new TradeLine(
            trade.Code, Pack.TradeTargetName(trade.CostKind, trade.CostItemCode), trade.CostAmount,
            Pack.TradeTargetName(trade.ReceiveKind, trade.ReceiveItemCode), trade.ReceiveAmount).Text;

        try
        {
            return ExecuteTrade(trade, text);
        }
        catch (OverflowException)
        {
            return $"교환 실패 - 수량이 너무 커서 계산할 수 없습니다. [{text}]";
        }
    }

    string? PlanTrade(ShopTrade trade, string text, out TradePlan plan)
    {
        plan = default;
        // 커먼 아이템은 번치를 부품으로 풀어 한꺼번에 계산한다. 유니크 아이템과 캐릭터는 확인한 뒤 따로 처리한다.
        var net = new Dictionary<long, long>();
        string[] paidInstances = [];
        long? paidCharacter = null;

        // 코스트는 받기 전의 보유량으로 판단한다.
        switch (trade.CostKind)
        {
            case TradeKind.CommonItem:
                foreach (var (item, need) in Pack.Expand(trade.CostItemCode, trade.CostAmount))
                {
                    // 장비 슬롯에 끼워 둔 커먼 아이템은 쓸 수 없다(끼운 개수가 보유 개수를 넘지 않게 한다).
                    var have = InventoryItems.GetValueOrDefault(item).Amount - EquippedCount(EquipRef.Common(item));
                    if (have < need) return $"교환 실패 - 부족: {Pack.ItemName(item)} (보유 {have} / 필요 {need}) [{text}]";
                    // 합산은 checked라 long 범위를 넘으면 OverflowException이 난다(바깥에서 실패로 처리).
                    net[item] = checked(net.GetValueOrDefault(item) - need);
                }
                break;
            case TradeKind.UniqueItem:
                // 장비 슬롯에 끼지 않은 인스턴스만 낼 수 있다(작은 ID부터).
                var spare = InstancesOf(trade.CostItemCode).Where(id => EquippedCount(EquipRef.Unique(id)) == 0).ToList();
                if (spare.Count < trade.CostAmount)
                    return $"교환 실패 - 부족: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} (보유 {spare.Count} / 필요 {trade.CostAmount}) [{text}]";
                paidInstances = spare.Take((int)trade.CostAmount).ToArray();
                break;
            case TradeKind.Character:
                if (trade.CostItemCode == GamePack.PlayerCharacterCode)
                    return $"교환 실패 - 플레이어는 낼 수 없습니다: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} [{text}]";
                if (!Characters.ContainsKey(trade.CostItemCode))
                    return $"교환 실패 - 부족: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} (보유 0 / 필요 1) [{text}]";
                if (PartyIndexOf(trade.CostItemCode) >= 0)
                    return $"교환 실패 - 파티에 편성된 캐릭터: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} [{text}]";
                if (EquippedCount(EquipRef.Char(trade.CostItemCode)) > 0)
                    return $"교환 실패 - 장비 슬롯에 끼워 둔 캐릭터: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} [{text}]";
                paidCharacter = trade.CostItemCode;
                break;
        }

        switch (trade.ReceiveKind)
        {
            case TradeKind.CommonItem:
                foreach (var (item, n) in Pack.Expand(trade.ReceiveItemCode, trade.ReceiveAmount))
                    net[item] = checked(net.GetValueOrDefault(item) + n);
                break;
            case TradeKind.Character:
                // 같은 캐릭터를 내고 받는 교환이면 낸 뒤에는 보유하지 않으므로 받을 수 있다.
                if (Characters.ContainsKey(trade.ReceiveItemCode) && paidCharacter != trade.ReceiveItemCode)
                    return $"교환 실패 - 이미 보유한 캐릭터: {Pack.TradeTargetName(trade.ReceiveKind, trade.ReceiveItemCode)} [{text}]";
                break;
        }

        // 커먼 아이템 인벤토리 마스터의 소지제한을 넘게 되면 교환하지 않는다. 마스터에 없는 아이템은 long 한도까지.
        foreach (var (item, delta) in net)
        {
            if (delta <= 0) continue;
            var limit = Pack.FindItem(item)?.CarryLimit ?? long.MaxValue;
            if (checked(InventoryItems.GetValueOrDefault(item).Amount + delta) > limit)
                return $"교환 실패 - 소지 한도 초과: {Pack.ItemName(item)} (한도 {limit}) [{text}]";
        }

        plan = new TradePlan(net, paidInstances, paidCharacter);
        return null;
    }

    /// <summary>교환을 해 보지 않고 미리 계산한 결과: 커먼 아이템 증감, 낼 유니크 아이템 인스턴스, 낼 캐릭터.</summary>
    readonly record struct TradePlan(Dictionary<long, long> Net, string[] PaidInstances, long? PaidCharacter);

    /// <summary>
    /// 상점 교환 화면의 이 교환을 지금 할 수 있는가(코스트가 모자라거나 소지 한도를 넘거나 이미 가진 캐릭터를 받는 등이면 거짓).
    /// 인벤토리는 바꾸지 않고 확인만 한다. 마스터에 없는 교환 코드는 거짓.
    /// </summary>
    public bool CanTrade(long tradeCode) => TradeBlockedReason(tradeCode) is null;

    /// <summary>이 교환을 지금 할 수 없는 까닭("교환 실패 - 부족: …" 형식). 할 수 있으면 null. 인벤토리는 바꾸지 않는다.</summary>
    public string? TradeBlockedReason(long tradeCode)
    {
        var trade = Pack.Trades?.FirstOrDefault(t => t.Code == tradeCode);
        if (trade is null) return "교환 실패 - 없는 교환입니다.";
        var text = new TradeLine(
            trade.Code, Pack.TradeTargetName(trade.CostKind, trade.CostItemCode), trade.CostAmount,
            Pack.TradeTargetName(trade.ReceiveKind, trade.ReceiveItemCode), trade.ReceiveAmount).Text;
        try
        {
            return PlanTrade(trade, text, out _);
        }
        catch (OverflowException)
        {
            return $"교환 실패 - 수량이 너무 커서 계산할 수 없습니다. [{text}]";
        }
    }

    string ExecuteTrade(ShopTrade trade, string text)
    {
        if (PlanTrade(trade, text, out var plan) is { } failure) return failure;
        var (net, paidInstances, paidCharacter) = plan;

        foreach (var (item, delta) in net)
        {
            var after = InventoryItems.GetValueOrDefault(item).Amount + delta;
            if (after == 0) InventoryItems.Remove(item);
            else InventoryItems[item] = new InventoryItem(after);
        }
        foreach (var id in paidInstances) RemoveUniqueItem(id);
        if (paidCharacter is { } leaving) RemoveCharacter(leaving);

        if (trade.ReceiveKind == TradeKind.UniqueItem)
            for (var i = 0L; i < trade.ReceiveAmount; i++) AddUniqueItem(trade.ReceiveItemCode);
        else if (trade.ReceiveKind == TradeKind.Character)
            AddCharacter(trade.ReceiveItemCode);
        return $"교환 완료: {text}";
    }

    /// <summary>
    /// 캐릭터를 내보낸다: 보유 캐릭터에서 빠지고, 파티 슬롯과 상태이상, 태그가 지워지며, 끼고 있던 장비는 빠져 보유로 돌아간다.
    /// 다른 캐릭터에게 장비로 끼워져 있으면 하지 않고 false(보유하지 않아도 false).
    /// </summary>
    public bool RemoveCharacter(long characterCode)
    {
        if (characterCode == GamePack.PlayerCharacterCode) return false;       // 플레이어는 내보낼 수 없다
        if (!Characters.ContainsKey(characterCode) || EquippedCount(EquipRef.Char(characterCode)) > 0) return false;
        Characters.Remove(characterCode);
        foreach (var party in Parties)
            for (var i = 0; i < party.Units.Length; i++)
                if (party.Units[i]?.CharacterCode == characterCode) party.Units[i] = null;     // 유닛과 함께 장비가 빠져 보유로 돌아간다
        CharacterStatusEffects.Remove(characterCode);
        CharacterTagInstances.Remove(characterCode);
        return true;
    }

    // ---- 상태이상 ----

    /// <summary>캐릭터에 걸려 있는 상태이상들. 없으면 빈 목록.</summary>
    public IReadOnlyList<ActiveStatusEffect> StatusEffectsOf(long characterCode) =>
        CharacterStatusEffects.GetValueOrDefault(characterCode) ?? [];

    /// <summary>
    /// 보유 캐릭터에게 상태이상을 건다. 상태이상 이펙트 마스터의 값(시점, 우선도, 이펙트 코드, 밸류 1~3)을 복사해 두므로
    /// 카운터 0, 유효 상태로 시작하고, 이후 게임팩 마스터가 바뀌어도 걸린 값은 그대로다.
    /// 이미 같은 상태이상이 걸려 있으면 새로 걸어 갱신한다. 캐릭터를 보유하지 않았거나 상태이상 코드가 없으면 false.
    /// </summary>
    public bool ApplyStatusEffect(long characterCode, long statusEffectCode)
    {
        if (!Characters.ContainsKey(characterCode)) return false;
        if (Pack.FindStatusEffect(statusEffectCode) is null) return false;

        var effects = Pack.StatusEffectEffectsOf(statusEffectCode).Select(ActiveEffect.From).ToList();
        if (!CharacterStatusEffects.TryGetValue(characterCode, out var list))
            CharacterStatusEffects[characterCode] = list = [];
        list.RemoveAll(s => s.StatusEffectCode == statusEffectCode);
        list.Add(new ActiveStatusEffect(statusEffectCode, effects));
        return true;
    }

    /// <summary>걸려 있는 상태이상을 푼다. 걸려 있지 않았으면 false.</summary>
    public bool RemoveStatusEffect(long characterCode, long statusEffectCode)
    {
        if (!CharacterStatusEffects.TryGetValue(characterCode, out var list)) return false;
        var removed = list.RemoveAll(s => s.StatusEffectCode == statusEffectCode) > 0;
        if (list.Count == 0) CharacterStatusEffects.Remove(characterCode);
        return removed;
    }

    /// <summary>저장용 복사본(이펙트 목록도 새로 만든다). 비어 있는 캐릭터는 뺀다.</summary>
    static Dictionary<long, List<ActiveStatusEffect>> CopyStatusEffects(Dictionary<long, List<ActiveStatusEffect>> source) =>
        source.Where(kv => kv.Value.Count > 0).ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(s => new ActiveStatusEffect(s.StatusEffectCode, s.EffectList.ToList())).ToList());

    /// <summary>세이브의 상태이상을 검증하고 복사한다. 보유 캐릭터, 상태이상 마스터, 임베디드 이펙트에 없는 것은 오류.</summary>
    static Dictionary<long, List<ActiveStatusEffect>> ValidateStatusEffects(
        GamePack pack, Dictionary<long, CharacterInstance> characters, Dictionary<long, List<ActiveStatusEffect>>? saved)
    {
        var result = new Dictionary<long, List<ActiveStatusEffect>>();
        foreach (var (characterCode, list) in saved ?? [])
        {
            if (!characters.ContainsKey(characterCode))
                throw new SaveFormatException($"세이브의 상태이상이 보유하지 않은 캐릭터 '{characterCode}'에 걸려 있습니다.");

            var seen = new HashSet<long>();
            var copy = new List<ActiveStatusEffect>();
            foreach (var status in list ?? [])
            {
                if (status is null) throw new SaveFormatException($"세이브의 캐릭터 '{characterCode}'의 상태이상 목록에 빈 항목이 있습니다.");
                if (pack.FindStatusEffect(status.StatusEffectCode) is null)
                    throw new SaveFormatException($"세이브의 상태이상 '{status.StatusEffectCode}'가 상태이상 마스터에 없습니다.");
                if (!seen.Add(status.StatusEffectCode))
                    throw new SaveFormatException(
                        $"세이브의 캐릭터 '{characterCode}'에 상태이상 '{status.StatusEffectCode}'가 두 번 이상 있습니다.");

                foreach (var effect in status.EffectList)
                {
                    if (effect is null)
                        throw new SaveFormatException($"세이브의 상태이상 '{status.StatusEffectCode}'에 빈 이펙트 항목이 있습니다.");
                    if (!Enum.IsDefined(effect.Trigger))
                        throw new SaveFormatException($"세이브의 상태이상 '{status.StatusEffectCode}'의 트리거 타입이 올바르지 않습니다: {effect.Trigger}");
                    if (!pack.EmbeddedEffects.Contains(effect.EffectCode))
                        throw new SaveFormatException(
                            $"세이브의 상태이상 '{status.StatusEffectCode}'의 이펙트 코드 '{effect.EffectCode}'가 임베디드 이펙트에 없습니다.");
                    if (effect.Counter < 0)
                        throw new SaveFormatException($"세이브의 상태이상 '{status.StatusEffectCode}'의 카운터가 음수입니다: {effect.Counter}");
                }
                copy.Add(new ActiveStatusEffect(status.StatusEffectCode, status.EffectList.ToList()));
            }
            if (copy.Count > 0) result[characterCode] = copy;
        }
        return result;
    }

    public string ToSaveJson() =>
        JsonSerializer.Serialize(
            new SaveData(
                SaveVersion, Pack.Id, Screen,
                new Dictionary<long, long>(PlayerFlags),
                new Dictionary<long, InventoryItem>(InventoryItems),
                new Dictionary<long, CharacterInstance>(Characters),
                null,
                CopyStatusEffects(CharacterStatusEffects),
                CopyUniqueItems(),
                CopyCharacterTags(),
                null,
                PartnerCode, AssistCode, TurnCount,
                null,
                Parties.Select(p => p.Copy()).ToArray(),
                CurrentPartyIndex),
            SaveJsonContext.Default.SaveData);

    public static GameSession FromSaveJson(GamePack pack, string json)
    {
        SaveData? save;
        try { save = JsonSerializer.Deserialize(json, SaveJsonContext.Default.SaveData); }
        catch (JsonException) { throw new SaveFormatException("세이브 파일이 올바른 JSON이 아닙니다."); }

        if (save is null) throw new SaveFormatException("세이브 파일이 비어 있습니다.");
        if (save.Version is < 1 or > SaveVersion) throw new SaveFormatException($"지원하지 않는 세이브 버전입니다: {save.Version}");
        if (save.PackId != pack.Id) throw new SaveFormatException($"다른 게임팩의 세이브입니다: {save.PackId}");
        var playerFlags = save.PlayerFlags is null ? [] : new Dictionary<long, long>(save.PlayerFlags);
        var inventory = save.InventoryItems is null ? [] : new Dictionary<long, InventoryItem>(save.InventoryItems);
        foreach (var (code, item) in inventory)
            if (item.Amount < 0) throw new SaveFormatException($"세이브의 인벤토리 아이템 {code}의 수량이 음수입니다: {item.Amount}");

        var characters = save.Characters is null ? [] : new Dictionary<long, CharacterInstance>(save.Characters);
        // 인스턴스 ID가 없던 예전 세이브는 캐릭터 코드 순서대로 새 ID를 채운다(ID는 만든 순서와 같게 늘어난다).
        var usedIds = (save.UniqueItems?.Keys ?? Enumerable.Empty<string>())
            .Concat(characters.Values.Select(v => v.InstanceId).Where(id => !string.IsNullOrEmpty(id))).ToList();
        foreach (var (code, s) in characters.OrderBy(kv => kv.Key).ToList())
        {
            if (pack.FindCharacter(code) is null)
                throw new SaveFormatException($"세이브의 캐릭터 '{code}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (s.CharacterCode != 0 && s.CharacterCode != code)
                throw new SaveFormatException($"세이브의 캐릭터 '{code}'에 적힌 마스터 코드 '{s.CharacterCode}'가 키와 다릅니다.");
            string instanceId;
            if (string.IsNullOrEmpty(s.InstanceId))
            {
                instanceId = NextInstanceId(usedIds);
                usedIds.Add(instanceId);
            }
            else if (!Ulid.TryParse(s.InstanceId, out var ulid))
                throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 인스턴스 ID '{s.InstanceId}'가 ULID가 아닙니다.");
            else instanceId = ulid.ToString();
            // 고정 필드(Level, Hp, MaxHp, Xp, XpMax)로 저장한 예전 세이브는 Statuses로 옮긴다.
            // (JSON에 없는 항목은 null로 올 수 있다)
            var statuses = new Dictionary<long, long>((Dictionary<long, long>?)s.Statuses ?? []);
            if (s.LegacyLevel is not null || s.LegacyHp is not null || s.LegacyMaxHp is not null || s.LegacyXp is not null || s.LegacyXpMax is not null)
            {
                statuses.TryAdd(GamePack.LevelStatusCode, s.LegacyLevel ?? 1);
                statuses.TryAdd(GamePack.HpStatusCode, s.LegacyHp ?? 0);
                statuses.TryAdd(GamePack.MaxHpStatusCode, s.LegacyMaxHp ?? 0);
                statuses.TryAdd(GamePack.XpStatusCode, s.LegacyXp ?? 0);
                statuses.TryAdd(GamePack.XpMaxStatusCode, s.LegacyXpMax ?? 0);
            }
            var loaded = s with
            {
                InstanceId = instanceId, CharacterCode = code, Statuses = statuses,
                SkillPool = (List<SkillInstance>?)s.SkillPool ?? [], SkillSlots = (Dictionary<long, long>?)s.SkillSlots ?? [],
                LegacyLevel = null, LegacyHp = null, LegacyMaxHp = null, LegacyXp = null, LegacyXpMax = null,
            };
            if (loaded.Level < 1 || loaded.MaxHp < 0 || loaded.Hp < 0 || loaded.Hp > loaded.MaxHp
                || loaded.XpMax < 0 || loaded.Xp < 0 || loaded.Xp > loaded.XpMax)
                throw new SaveFormatException(
                    $"세이브의 캐릭터 '{code}' 상태가 올바르지 않습니다 (Lv {loaded.Level}, HP {loaded.Hp}/{loaded.MaxHp}, XP {loaded.Xp}/{loaded.XpMax}).");
            foreach (var status in loaded.Statuses.Keys)
                if (pack.FindStatus(status) is null && pack.Statuses is not null && status > GamePack.XpMaxStatusCode)
                    throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 스테이터스 '{status}'가 스테이터스 마스터에 없습니다.");
            loaded = loaded with { SkillPool = loaded.SkillPool.Select(skill =>
            {
                if (skill is null || pack.FindSkill(skill.SkillCode) is null)
                    throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 스킬 '{skill?.SkillCode}'가 스킬 마스터에 없습니다.");
                return skill with { Statuses = CheckOwnerStatuses(pack, $"캐릭터 '{code}'의 스킬 '{skill.SkillCode}'", skill.Statuses, pack.MasterStatusesOfSkill(skill.SkillCode)) };
            }).ToList() };
            foreach (var (slot, index) in loaded.SkillSlots)
            {
                if (pack.FindSkillSlot(slot) is null)
                    throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 스킬 슬롯 '{slot}'가 스킬 슬롯 마스터에 없습니다.");
                if (index < 0 || index >= loaded.SkillPool.Count)
                    throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 스킬 슬롯 '{slot}'이 가리키는 번호 {index}가 스킬 풀(스킬 {loaded.SkillPool.Count}개)에 없습니다.");
            }
            characters[code] = loaded;
        }

        // 플레이어(캐릭터 1)는 늘 보유한다. 세이브에 없으면(예전 세이브) 새 캐릭터 상태로 채운다.
        if (!characters.ContainsKey(GamePack.PlayerCharacterCode) && pack.FindCharacter(GamePack.PlayerCharacterCode) is not null)
            characters[GamePack.PlayerCharacterCode] = pack.NewCharacterInstance(
                GamePack.PlayerCharacterCode, NextInstanceId(usedIds), NextOrderNumber(characters.Values));
        if (characters.Values.GroupBy(v => v.InstanceId).Any(g => g.Count() > 1))
            throw new SaveFormatException("세이브의 캐릭터 인스턴스 ID가 두 번 이상 있습니다.");

        // 불러온 뒤 보유 캐릭터마다 로드 후 캐릭터 서킷을 돌려 게임팩의 정의대로 캐릭터를 조작한다.
        foreach (var code in characters.Keys.ToList())
            characters[code] = new AfterLoadCharacterCircuit(pack, characters[code]).Execute();

        // 파티: 새 형식은 Parties(파티-A ~ 파티-P, 각각 이름 + 유닛[])와 CurrentParty, 그 전 형식은 PartyInstance 하나(파티-A),
        // 가장 예전 세이브는 Party(캐릭터 코드 배열)와 EquippedItems(캐릭터별 장비)다. 예전 세이브에서 파티에 없는 캐릭터가
        // 끼고 있던 장비는 보유로 돌아간다. 파티끼리는 캐릭터를 공유할 수 없다.
        var partyCodes = Enumerable.Range(0, PartyCount).Select(_ => new long?[PartySlotCount]).ToArray();
        var partyNames = Enumerable.Range(0, PartyCount).Select(DefaultPartyNameOf).ToArray();
        var partyEquipment = new Dictionary<long, Dictionary<long, EquipRef>>();
        var partyLeaders = Enumerable.Range(0, PartyCount).Select(_ => new bool[PartySlotCount]).ToArray();

        void ReadParty(int index, PartyInstance? saved)
        {
            if (saved is null) return;
            if (!string.IsNullOrWhiteSpace(saved.Name)) partyNames[index] = saved.Name;
            var savedUnits = saved.Units ?? [];
            if (savedUnits.Length > PartySlotCount)
                throw new SaveFormatException($"세이브의 파티 슬롯이 {PartySlotCount}칸보다 많습니다: {savedUnits.Length}");
            for (var i = 0; i < savedUnits.Length; i++)
            {
                if (savedUnits[i] is not { } unit) continue;
                partyCodes[index][i] = unit.CharacterCode;
                partyLeaders[index][i] = unit.IsLeader;
                if (unit.Equipment is { Count: > 0 }) partyEquipment[unit.CharacterCode] = unit.Equipment;
            }
        }

        if (save.Parties is { } savedParties)
        {
            if (savedParties.Length > PartyCount)
                throw new SaveFormatException($"세이브의 파티가 {PartyCount}개보다 많습니다: {savedParties.Length}");
            for (var p = 0; p < savedParties.Length; p++) ReadParty(p, savedParties[p]);
        }
        else if (save.PartyInstance is not null) ReadParty(0, save.PartyInstance);
        else
        {
            if (save.Party is { } saved)
            {
                if (saved.Length > PartySlotCount)
                    throw new SaveFormatException($"세이브의 파티 슬롯이 {PartySlotCount}칸보다 많습니다: {saved.Length}");
                for (var i = 0; i < saved.Length; i++) partyCodes[0][i] = saved[i];
            }
            partyEquipment = save.EquippedItems ?? [];       // 전부 검증하고, 파티에 없는 캐릭터의 장비는 아래에서 뺀다
        }
        var currentParty = save.CurrentParty ?? 0;
        if (currentParty is < 0 or >= PartyCount)
            throw new SaveFormatException($"세이브의 현재 파티 '{currentParty}'가 0~{PartyCount - 1} 범위를 벗어납니다.");

        var allCodes = partyCodes.SelectMany(p => p).OfType<long>().ToList();
        foreach (var code in allCodes)
            if (!characters.ContainsKey(code))
                throw new SaveFormatException($"세이브의 파티 캐릭터 '{code}'가 보유 캐릭터에 없습니다.");
        if (allCodes.GroupBy(c => c).Any(g => g.Count() > 1))
            throw new SaveFormatException("세이브의 파티에 같은 캐릭터가 두 번 이상 있습니다(파티끼리도 공유할 수 없습니다).");

        var statusEffects = ValidateStatusEffects(pack, characters, save.StatusEffects);
        var (uniqueItems, characterTags, equippedItems) =
            ValidateEquipment(pack, characters, inventory, save.UniqueItems, save.CharacterTags, partyEquipment);
        var parties = partyCodes.Select((codes, p) => new PartyInstance(partyNames[p],
            codes.Select((c, i) => c is { } code
                ? new PartyUnit(code, equippedItems.TryGetValue(code, out var slots) ? slots : [], partyLeaders[p][i]) : null).ToArray())).ToArray();

        foreach (var (what, code) in new[] { ("파트너", save.Partner), ("어시스트", save.Assist) })
            if (code is { } support && !characters.ContainsKey(support))
                throw new SaveFormatException($"세이브의 {what} 캐릭터 '{support}'가 보유 캐릭터에 없습니다.");
        if (save.Turn is { } turn && pack.FindTurn(turn) is null)
            throw new SaveFormatException($"세이브의 턴 '{turn}'이(가) 턴 마스터에 없습니다.");
        if (save.Partner is not null && save.Partner == save.Assist)
            throw new SaveFormatException("세이브의 파트너와 어시스트가 같은 캐릭터입니다.");

        // 불러오기는 저장된 화면과 상관없이 항상 홈으로 들어간다.
        return new GameSession(pack, Screen.Home, playerFlags, inventory, characters, parties, statusEffects, uniqueItems, characterTags)
        {
            CurrentPartyIndex = currentParty,
            PartnerCode = save.Partner,
            AssistCode = save.Assist,
            TurnCount = save.Turn ?? pack.FirstTurn.TurnCount,
        };
    }
}
