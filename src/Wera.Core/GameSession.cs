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
    /// <summary>파티 설정(장비 착용): 같은 3×3 슬롯. 슬롯을 고르면 그 캐릭터의 장비 목록으로 간다. 돌아가기는 홈.</summary>
    EquipmentSetup,
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
    long? Turn = null);

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
    public const string SaveEntryCode = "200";
    public const string CompanionCheckEntryCode = "201";
    public const string EquipmentCheckEntryCode = "202";
    public const string ItemCheckEntryCode = "203";
    public const string LoadEntryCode = "300";
    public const string BackEntryCode = "999";

    /// <summary>캐릭터 선택 화면의 "[0] 비우기": 선택한 슬롯에 캐릭터가 있을 때만 나온다.</summary>
    public const string ClearEntryCode = "0";

    /// <summary>파티 설정 화면 아래의 텍스트 버튼: 캐릭터 배치 화면 / 장비 착용 화면으로 바꾼다.</summary>
    public const string PlacementModeCode = "10";
    public const string EquipmentModeCode = "11";

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

    /// <summary>파티 배치: 길이 9, 칸 번호 0~2 전위, 3~5 중위, 6~8 후위. 빈 칸은 null.</summary>
    public long?[] Party { get; }

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
    public long? CurrentCharacterCode => CurrentSlot is { } slot ? Party[slot] : null;

    /// <summary>장비 가능 목록 화면에서 보고 있는 장비 슬롯 코드. 그 화면이 아니면 null.</summary>
    public long? CurrentEquipmentSlotCode { get; private set; }

    public EquipmentSlotEntry? CurrentEquipmentSlot =>
        CurrentEquipmentSlotCode is { } slotCode ? Pack.FindEquipmentSlot(slotCode) : null;

    /// <summary>선택한 캐릭터가 가진 장비 슬롯들.</summary>
    IReadOnlyList<EquipmentSlotEntry> CurrentEquipmentSlots =>
        CurrentCharacterCode is { } character ? Pack.EquipmentSlotsOf(character) : [];

    GameSession(
        GamePack pack, Screen screen, Dictionary<long, long> playerFlags,
        Dictionary<long, InventoryItem> inventoryItems, Dictionary<long, CharacterInstance> characters, long?[] party,
        Dictionary<long, List<ActiveStatusEffect>> statusEffects,
        Dictionary<string, UniqueItemInstance> uniqueItems, Dictionary<long, List<long>> characterTags,
        Dictionary<long, Dictionary<long, EquipRef>> equippedItems)
    {
        Pack = pack;
        Screen = screen;
        PlayerFlags = playerFlags;
        InventoryItems = inventoryItems;
        Characters = characters;
        Party = party;
        CharacterStatusEffects = statusEffects;
        UniqueItemInstances = uniqueItems;
        CharacterTagInstances = characterTags;
        EquippedItems = equippedItems;
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
        Characters[characterCode] = Pack.NewCharacterInstance(characterCode, NewInstanceId(), NextOrderNumber(Characters.Values));
        CharacterTagInstances[characterCode] = Pack.MasterTagsOfCharacter(characterCode).ToList();
        return true;
    }

    /// <summary>튜토리얼이 구현될 때까지 새 게임은 홈 화면으로 직행한다.</summary>
    public static GameSession NewGame(GamePack pack)
    {
        var session = new GameSession(pack, Screen.Home, [], [], [], new long?[PartySlotCount], [], [], [], []);
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

    PartySlotView SlotView(int slot) => CharacterView(slot, Party[slot]);

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
            return (Pack.CharacterInventory ?? []).Where(c => Characters.ContainsKey(c.Code) && Party[slot] != c.Code).Select(c =>
            {
                var placedAt = Array.IndexOf(Party, (long?)c.Code);
                var color = !Characters.ContainsKey(c.Code) ? CharacterListColor.Gray
                    : placedAt >= 0 ? CharacterListColor.Green
                    : CharacterListColor.White;
                return new CharacterListItem(c.Code, c.Name, color);
            }).ToList();
        }
    }

    /// <summary>
    /// 홈 화면의 메뉴: 구분선으로 나뉜 세 분류. [RPG] 던전(101, 입장 가능한 것이 없으면 회색), 상점(102, 열린 상점이 없으면 회색),
    /// 이벤트(103), 교류(104), 파티 설정(105) / [정보] 동료 확인(201), 장비 확인(202), 아이템 확인(203) / [시스템] 세이브(200), 로드(300).
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
                    return PartySlotViews
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.CharacterSelect, Arg: v.Slot.ToString()))
                        .Concat(ModeEntries(Screen.PartySetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.EquipmentSetup:
                    // 캐릭터가 있는 슬롯만 고를 수 있다. 이 화면에서 돌아가면 파티 설정이 아니라 홈으로 간다.
                    return PartySlotViews
                        .Where(v => !v.IsEmpty)
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.EquipmentList, Arg: v.Slot.ToString()))
                        .Concat(ModeEntries(Screen.EquipmentSetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
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
                    Screen.EquipmentSelect => CurrentSlot,      // 같은 캐릭터를 계속 본다
                    _ => null,
                };
                CurrentEquipmentSlotCode = entry.Target == Screen.EquipmentSelect ? CodeOf(entry) : null;
                CurrentSupportRole = entry.Target == Screen.SupportSelect ? Enum.Parse<SupportRole>(entry.Arg!) : null;
                if (entry.Target == Screen.Interaction && Screen != Screen.CommunicationPlay)
                {
                    CurrentCommunicationGroupCode = CommunicationGroups.FirstOrDefault()?.Code;   // 들어가면 가장 작은 그룹이 선택되고
                    communicationHistory.Clear();                                                  // 이력은 비어 있다
                }
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
            case EntryAction.ClearSlot:
                Party[CurrentSlot!.Value] = null;
                CurrentSlot = null;
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
        CurrentSlot is { } slot && Party[slot] is not null
            ? [new ScreenEntry(ClearEntryCode, "비우기", Screen.PartySetup, EntryAction.ClearSlot)]
            : [];

    /// <summary>파티 설정 화면 아래의 모드 버튼들: [10] 캐릭터 배치, [11] 장비 착용. 지금 보고 있는 모드의 버튼은 선택할 수 없다.</summary>
    static IEnumerable<ScreenEntry> ModeEntries(Screen current) =>
    [
        new ScreenEntry(PlacementModeCode, "캐릭터 배치", Screen.PartySetup, Enabled: current != Screen.PartySetup),
        new ScreenEntry(EquipmentModeCode, "장비 착용", Screen.EquipmentSetup, Enabled: current != Screen.EquipmentSetup),
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
    /// 선택한 슬롯에 캐릭터를 둔다. 이미 다른 슬롯에 있으면 그 슬롯과 이 슬롯의 내용을 맞바꾸고(빈 슬롯이면 이동),
    /// 어디에도 없으면 이 슬롯에 두며 원래 있던 캐릭터는 파티에서 빠진다. 이 슬롯의 캐릭터를 다시 고르면 그대로다.
    /// </summary>
    void PlaceCharacter(long code)
    {
        var slot = CurrentSlot!.Value;
        var at = Array.IndexOf(Party, (long?)code);
        if (at == slot) return;
        if (at >= 0) (Party[at], Party[slot]) = (Party[slot], Party[at]);
        else Party[slot] = code;
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
        for (var i = 0; i < Party.Length; i++)
            if (Party[i] == characterCode) Party[i] = null;
        CharacterStatusEffects.Remove(characterCode);
        CharacterTagInstances.Remove(characterCode);
        EquippedItems.Remove(characterCode);
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
                (long?[])Party.Clone(),
                CopyStatusEffects(CharacterStatusEffects),
                CopyUniqueItems(),
                CopyCharacterTags(),
                CopyEquippedItems(),
                PartnerCode, AssistCode, TurnCount),
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
            characters[code] = s with { InstanceId = instanceId, CharacterCode = code };
            if (s.Level < 1 || s.MaxHp < 0 || s.Hp < 0 || s.Hp > s.MaxHp || s.XpMax < 0 || s.Xp < 0 || s.Xp > s.XpMax)
                throw new SaveFormatException(
                    $"세이브의 캐릭터 '{code}' 상태가 올바르지 않습니다 (Lv {s.Level}, HP {s.Hp}/{s.MaxHp}, XP {s.Xp}/{s.XpMax}).");
        }

        // 플레이어(캐릭터 1)는 늘 보유한다. 세이브에 없으면(예전 세이브) 새 캐릭터 상태로 채운다.
        if (!characters.ContainsKey(GamePack.PlayerCharacterCode) && pack.FindCharacter(GamePack.PlayerCharacterCode) is not null)
            characters[GamePack.PlayerCharacterCode] = pack.NewCharacterInstance(
                GamePack.PlayerCharacterCode, NextInstanceId(usedIds), NextOrderNumber(characters.Values));
        if (characters.Values.GroupBy(v => v.InstanceId).Any(g => g.Count() > 1))
            throw new SaveFormatException("세이브의 캐릭터 인스턴스 ID가 두 번 이상 있습니다.");

        var party = new long?[PartySlotCount];
        if (save.Party is { } saved)
        {
            if (saved.Length > PartySlotCount)
                throw new SaveFormatException($"세이브의 파티 슬롯이 {PartySlotCount}칸보다 많습니다: {saved.Length}");
            for (var i = 0; i < saved.Length; i++) party[i] = saved[i];
        }
        foreach (var code in party.OfType<long>())
            if (!characters.ContainsKey(code))
                throw new SaveFormatException($"세이브의 파티 캐릭터 '{code}'가 보유 캐릭터에 없습니다.");
        if (party.OfType<long>().GroupBy(c => c).Any(g => g.Count() > 1))
            throw new SaveFormatException("세이브의 파티에 같은 캐릭터가 두 번 이상 있습니다.");

        var statusEffects = ValidateStatusEffects(pack, characters, save.StatusEffects);
        var (uniqueItems, characterTags, equippedItems) =
            ValidateEquipment(pack, characters, inventory, save.UniqueItems, save.CharacterTags, save.EquippedItems);

        foreach (var (what, code) in new[] { ("파트너", save.Partner), ("어시스트", save.Assist) })
            if (code is { } support && !characters.ContainsKey(support))
                throw new SaveFormatException($"세이브의 {what} 캐릭터 '{support}'가 보유 캐릭터에 없습니다.");
        if (save.Turn is { } turn && pack.FindTurn(turn) is null)
            throw new SaveFormatException($"세이브의 턴 '{turn}'이(가) 턴 마스터에 없습니다.");
        if (save.Partner is not null && save.Partner == save.Assist)
            throw new SaveFormatException("세이브의 파트너와 어시스트가 같은 캐릭터입니다.");

        // 불러오기는 저장된 화면과 상관없이 항상 홈으로 들어간다.
        return new GameSession(pack, Screen.Home, playerFlags, inventory, characters, party, statusEffects, uniqueItems, characterTags, equippedItems)
        {
            PartnerCode = save.Partner,
            AssistCode = save.Assist,
            TurnCount = save.Turn ?? pack.FirstTurn.TurnCount,
        };
    }
}
