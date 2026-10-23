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
    /// <summary>파티 설정: 3×3 슬롯.</summary>
    PartySetup,
    /// <summary>선택한 슬롯에 배치할 캐릭터 목록.</summary>
    CharacterSelect,
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
}

/// <summary>화면에 나오는 클릭 가능한 텍스트. 클릭하거나 Code를 입력하면 Action을 수행한다.</summary>
/// <param name="Arg">항목이 가리키는 추가 값(파티 슬롯 항목에서는 슬롯 번호 0~8).</param>
public sealed record ScreenEntry(
    string Code, string Label, Screen Target, EntryAction Action = EntryAction.Navigate, string? Arg = null);

/// <param name="Version">릴리스 전이라 1로 유지한다. 지시가 있을 때까지 올리지 않는다.</param>
public sealed record SaveData(
    int Version,
    string PackId,
    Screen Screen = Screen.Home,
    Dictionary<long, long>? PlayerFlags = null,
    Dictionary<long, InventoryItem>? InventoryItems = null,
    Dictionary<long, CharacterState>? Characters = null,
    long?[]? Party = null,
    Dictionary<long, List<ActiveStatusEffect>>? StatusEffects = null);

[JsonSerializable(typeof(SaveData))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class SaveJsonContext : JsonSerializerContext;

public sealed class SaveFormatException(string message) : Exception(message);

public sealed class GameSession
{
    public const int SaveVersion = 1;

    public const string DungeonEntryCode = "101";
    public const string ShopEntryCode = "102";
    public const string PartyEntryCode = "103";
    public const string SaveEntryCode = "200";
    public const string LoadEntryCode = "300";
    public const string BackEntryCode = "999";

    /// <summary>파티 슬롯 수: 전위/중위/후위 3줄 × 3칸.</summary>
    public const int PartySlotCount = 9;

    public GamePack Pack { get; }
    public Screen Screen { get; private set; }
    /// <summary>플레이어_플래그: 플래그 코드 → long 값.</summary>
    public Dictionary<long, long> PlayerFlags { get; }

    /// <summary>인벤토리_아이템: 아이템 코드 → 구조체.</summary>
    public Dictionary<long, InventoryItem> InventoryItems { get; }

    /// <summary>보유 캐릭터: 캐릭터 코드 → 현재 상태. 코드는 캐릭터 인벤토리 마스터에 있어야 한다.</summary>
    public Dictionary<long, CharacterState> Characters { get; }

    /// <summary>파티 배치: 길이 9, 칸 번호 0~2 전위, 3~5 중위, 6~8 후위. 빈 칸은 null.</summary>
    public long?[] Party { get; }

    /// <summary>
    /// 캐릭터에 걸려 있는 상태이상: 캐릭터 코드 → 상태이상 목록. 각 이펙트의 시점, 우선도, 이펙트 코드, 밸류 1~3, 카운터, 유효 플래그를
    /// 그대로 들고 있어 세이브에서 복구할 수 있다. 비어 있는 캐릭터는 키가 없다.
    /// </summary>
    public Dictionary<long, List<ActiveStatusEffect>> CharacterStatusEffects { get; }

    /// <summary>교환 목록 화면에서 보고 있는 상점 코드. 그 화면이 아니면 null.</summary>
    public long? CurrentShopCode { get; private set; }

    /// <summary>캐릭터 선택 화면에서 배치 중인 슬롯(0~8). 그 화면이 아니면 null.</summary>
    public int? CurrentSlot { get; private set; }

    GameSession(
        GamePack pack, Screen screen, Dictionary<long, long> playerFlags,
        Dictionary<long, InventoryItem> inventoryItems, Dictionary<long, CharacterState> characters, long?[] party,
        Dictionary<long, List<ActiveStatusEffect>> statusEffects)
    {
        Pack = pack;
        Screen = screen;
        PlayerFlags = playerFlags;
        InventoryItems = inventoryItems;
        Characters = characters;
        Party = party;
        CharacterStatusEffects = statusEffects;
    }

    /// <summary>튜토리얼이 구현될 때까지 새 게임은 홈 화면으로 직행한다.</summary>
    public static GameSession NewGame(GamePack pack) =>
        new(pack, Screen.Home, [], [], [], new long?[PartySlotCount], []);

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
                    Pack.ItemName(t.CostItemCode), t.CostAmount,
                    Pack.ItemName(t.ReceiveItemCode), t.ReceiveAmount))
                .ToList() ?? [];

    string CharacterName(long code) =>
        Pack.CharacterInventory?.FirstOrDefault(c => c.Code == code)?.Name ?? code.ToString(CultureInfo.InvariantCulture);

    /// <summary>파티 설정 화면의 9개 슬롯. 비었거나 상태를 알 수 없으면 "빈 슬롯"(회색 0/0).</summary>
    public IReadOnlyList<PartySlotView> PartySlotViews =>
        Enumerable.Range(0, PartySlotCount).Select(SlotView).ToList();

    PartySlotView SlotView(int slot)
    {
        if (Party[slot] is not { } code || !Characters.TryGetValue(code, out var state))
        {
            var empty = Gauge.Render(0, 0, gray: true);
            return new PartySlotView(slot, null, "", PartySlotView.EmptyName, empty, empty, [], []);
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
                Pack.StatusEffects?.FirstOrDefault(e => e.Code == s.StatusEffectCode)?.Name
                    ?? s.StatusEffectCode.ToString(CultureInfo.InvariantCulture)))
            .ToList();

    /// <summary>캐릭터의 유리속성(⭕), 불리속성(❌) 순서의 배지.</summary>
    IReadOnlyList<AttributeBadge> AttributeBadges(long characterCode)
    {
        var character = Pack.CharacterInventory?.FirstOrDefault(c => c.Code == characterCode);
        if (character is null) return [];
        string NameOf(long attribute) =>
            Pack.Attributes?.FirstOrDefault(a => a.Code == attribute)?.Name ?? attribute.ToString(CultureInfo.InvariantCulture);
        return
        [
            new AttributeBadge(character.AdvantageAttributeCode, NameOf(character.AdvantageAttributeCode), Advantage: true),
            new AttributeBadge(character.DisadvantageAttributeCode, NameOf(character.DisadvantageAttributeCode), Advantage: false),
        ];
    }

    /// <summary>
    /// 캐릭터 선택 화면의 목록(캐릭터 인벤토리 마스터 순서).
    /// 보유하지 않은 캐릭터는 회색, 이 슬롯의 캐릭터는 하늘색, 다른 슬롯에 있는 파티원은 초록, 나머지는 흰색.
    /// </summary>
    public IReadOnlyList<CharacterListItem> CharacterList
    {
        get
        {
            if (Screen != Screen.CharacterSelect || CurrentSlot is not { } slot) return [];
            return (Pack.CharacterInventory ?? []).Select(c =>
            {
                var placedAt = Array.IndexOf(Party, (long?)c.Code);
                var color = !Characters.ContainsKey(c.Code) ? CharacterListColor.Gray
                    : placedAt == slot ? CharacterListColor.SkyBlue
                    : placedAt >= 0 ? CharacterListColor.Green
                    : CharacterListColor.White;
                return new CharacterListItem(c.Code, c.Name, color);
            }).ToList();
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
                    var list = new List<ScreenEntry>();
                    if (AvailableDungeons.Count > 0)
                        list.Add(new ScreenEntry(DungeonEntryCode, "던전", Screen.DungeonSelect));
                    if (AvailableShops.Count > 0)
                        list.Add(new ScreenEntry(ShopEntryCode, "상점가", Screen.ShopList));
                    list.Add(new ScreenEntry(PartyEntryCode, "파티 설정", Screen.PartySetup));
                    list.Add(new ScreenEntry(SaveEntryCode, "세이브", Screen.Home, EntryAction.Save));
                    list.Add(new ScreenEntry(LoadEntryCode, "로드", Screen.Load));
                    return list;
                case Screen.ShopList:
                    // 상점 코드를 입력하거나 클릭하면 그 상점의 교환 목록으로 간다.
                    return AvailableShops
                        .Select(s => new ScreenEntry(Text(s.Code), s.Name, Screen.ShopTrades))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.ShopTrades:
                    // 교환 코드를 입력하거나 클릭하면 그 교환을 실행한다.
                    return CurrentShopTrades
                        .Select(t => new ScreenEntry(Text(t.Code), t.Text, Screen.ShopTrades, EntryAction.Trade))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.ShopList))
                        .ToList();
                case Screen.PartySetup:
                    // 슬롯 번호 1~9를 입력하거나 슬롯의 이름/아이콘을 클릭하면 그 슬롯에 배치할 캐릭터 목록으로 간다.
                    return PartySlotViews
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.CharacterSelect, Arg: v.Slot.ToString()))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.CharacterSelect:
                    // 고를 수 있는 캐릭터(회색 제외)의 코드를 입력하거나 클릭하면 배치한다.
                    return CharacterList
                        .Where(c => c.Selectable)
                        .Select(c => new ScreenEntry(Text(c.Code), c.Name, Screen.PartySetup, EntryAction.PlaceCharacter))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.PartySetup))
                        .ToList();
                case Screen.DungeonSelect:
                case Screen.Load:
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
        var entry = Pack.CharacterInventory?.FirstOrDefault(c => c.Code == targetCharacterCode);
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
        var entry = Entries.FirstOrDefault(e => e.Code == code);
        switch (entry?.Action)
        {
            case EntryAction.Navigate:
                CurrentShopCode = entry.Target == Screen.ShopTrades ? CodeOf(entry) : null;
                CurrentSlot = entry.Target == Screen.CharacterSelect ? int.Parse(entry.Arg!, CultureInfo.InvariantCulture) : null;
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
        }
        return entry;
    }

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
            trade.Code, Pack.ItemName(trade.CostItemCode), trade.CostAmount,
            Pack.ItemName(trade.ReceiveItemCode), trade.ReceiveAmount).Text;

        try
        {
            return ExecuteTrade(trade, text);
        }
        catch (OverflowException)
        {
            return $"교환 실패 - 수량이 너무 커서 계산할 수 없습니다. [{text}]";
        }
    }

    string ExecuteTrade(ShopTrade trade, string text)
    {
        var cost = Pack.Expand(trade.CostItemCode, trade.CostAmount);
        var gain = Pack.Expand(trade.ReceiveItemCode, trade.ReceiveAmount);

        // 코스트는 받기 전의 보유량으로 판단한다.
        foreach (var (item, need) in cost)
        {
            var have = InventoryItems.GetValueOrDefault(item).Amount;
            if (have < need) return $"교환 실패 - 부족: {Pack.ItemName(item)} (보유 {have} / 필요 {need}) [{text}]";
        }

        // 합산은 checked라 long 범위를 넘으면 OverflowException이 난다(바깥에서 실패로 처리).
        var net = new Dictionary<long, long>();
        foreach (var (item, n) in cost) net[item] = checked(net.GetValueOrDefault(item) - n);
        foreach (var (item, n) in gain) net[item] = checked(net.GetValueOrDefault(item) + n);

        // 아이템 인벤토리 마스터의 소지제한을 넘게 되면 교환하지 않는다. 마스터에 없는 아이템은 long 한도까지.
        foreach (var (item, delta) in net)
        {
            if (delta <= 0) continue;
            var limit = Pack.ItemInventory?.FirstOrDefault(i => i.Code == item)?.CarryLimit ?? long.MaxValue;
            if (checked(InventoryItems.GetValueOrDefault(item).Amount + delta) > limit)
                return $"교환 실패 - 소지 한도 초과: {Pack.ItemName(item)} (한도 {limit}) [{text}]";
        }

        foreach (var (item, delta) in net)
        {
            var after = InventoryItems.GetValueOrDefault(item).Amount + delta;
            if (after == 0) InventoryItems.Remove(item);
            else InventoryItems[item] = new InventoryItem(after);
        }
        return $"교환 완료: {text}";
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
        if (Pack.StatusEffects?.Any(s => s.Code == statusEffectCode) != true) return false;

        var effects = (Pack.StatusEffectEffects ?? [])
            .Where(r => r.StatusEffectCode == statusEffectCode)
            .Select(ActiveEffect.From)
            .ToList();
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
        GamePack pack, Dictionary<long, CharacterState> characters, Dictionary<long, List<ActiveStatusEffect>>? saved)
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
                if (pack.StatusEffects?.Any(s => s.Code == status.StatusEffectCode) != true)
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
                new Dictionary<long, CharacterState>(Characters),
                (long?[])Party.Clone(),
                CopyStatusEffects(CharacterStatusEffects)),
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

        var characters = save.Characters is null ? [] : new Dictionary<long, CharacterState>(save.Characters);
        foreach (var (code, s) in characters)
        {
            if (pack.CharacterInventory?.Any(c => c.Code == code) != true)
                throw new SaveFormatException($"세이브의 캐릭터 '{code}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (s.Level < 1 || s.MaxHp < 0 || s.Hp < 0 || s.Hp > s.MaxHp || s.XpMax < 0 || s.Xp < 0 || s.Xp > s.XpMax)
                throw new SaveFormatException(
                    $"세이브의 캐릭터 '{code}' 상태가 올바르지 않습니다 (Lv {s.Level}, HP {s.Hp}/{s.MaxHp}, XP {s.Xp}/{s.XpMax}).");
        }

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

        // 불러오기는 저장된 화면과 상관없이 항상 홈으로 들어간다.
        return new GameSession(pack, Screen.Home, playerFlags, inventory, characters, party, statusEffects);
    }
}
