using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Wera.Core;

public sealed class PackFormatException(string message) : Exception(message);

/// <summary>
/// 게임팩: TSV 묶음 zip. 첫 줄은 헤더. 모든 코드와 수량은 long 정수, 그룹넘버(group_no)는 int.
///   pack.tsv                              key/value 행 (id, title)
///   master/dungeon_master_v1.tsv          (선택) code, name
///   master/dungeon_flag_master_v1.tsv     (선택) dungeon_code, group_no, flag_code, condition_type, condition_value
///   master/shop_master_v1.tsv             (선택) code, name
///   master/shop_condition_master_v1.tsv   (선택) shop_code, group_no, flag_code, condition_type, condition_value
///   master/common_item_inventory_master_v1.tsv      (선택) item_code, item_name, carry_limit
///   master/bunch_item_inventory_master_v1.tsv     (선택) item_code, item_name, part_item_code, part_amount
///   master/character_inventory_master_v1.tsv (선택) character_code, character_name, carry_limit, advantage_attribute_code, disadvantage_attribute_code
///   master/attribute_master_v1.tsv        (선택) attribute_code, attribute_name  (캐릭터 마스터가 있으면 필수)
///   master/status_effect_master_v1.tsv    (선택) status_effect_code, status_effect_name
///   master/status_effect_effect_master_v1.tsv (선택, 상태이상 마스터 필요) status_effect_code, trigger_type, priority, effect_code, effect_value_1, effect_value_2, effect_value_3
///   master/shop_trade_master_v1.tsv       (선택) code, shop_code, cost_item_code, cost_amount, receive_item_code, receive_amount
///   master/shop_trade_condition_master_v1.tsv (선택) trade_code, group_no, flag_code, condition_type, condition_value
///   master/equipment_slot_master_v1.tsv   (선택) equipment_slot_code, equipment_slot_name
///   master/tag_master_v1.tsv              (선택) tag_code, tag_name
///   master/equipment_master_v1.tsv        (선택) item_code, item_name
///   master/equipment_tag_master_v1.tsv    (선택, 장비/태그 마스터 필요) item_code, tag_code
///   master/equipment_slot_tag_master_v1.tsv (선택, 캐릭터/슬롯/태그 마스터 필요) character_code, equipment_slot_code, tag_code, group_number, is_positive
/// </summary>
public sealed partial class GamePack
{
    public const string DungeonMasterPath = "master/dungeon_master_v1.tsv";
    public const string DungeonFlagMasterPath = "master/dungeon_flag_master_v1.tsv";
    public const string ShopMasterPath = "master/shop_master_v1.tsv";
    public const string ShopConditionMasterPath = "master/shop_condition_master_v1.tsv";
    public const string CommonItemInventoryMasterPath = "master/common_item_inventory_master_v1.tsv";
    public const string CharacterInventoryMasterPath = "master/character_inventory_master_v1.tsv";
    public const string AttributeMasterPath = "master/attribute_master_v1.tsv";
    public const string StatusEffectMasterPath = "master/status_effect_master_v1.tsv";
    public const string StatusEffectEffectMasterPath = "master/status_effect_effect_master_v1.tsv";
    public const string BunchItemInventoryMasterPath = "master/bunch_item_inventory_master_v1.tsv";
    public const string ShopTradeMasterPath = "master/shop_trade_master_v1.tsv";
    public const string ShopTradeConditionMasterPath = "master/shop_trade_condition_master_v1.tsv";

    public required string Id { get; init; }
    public required string Title { get; init; }

    /// <summary>던전 마스터가 게임팩에 없으면 null.</summary>
    public IReadOnlyList<Dungeon>? Dungeons { get; init; }

    /// <summary>상점 마스터가 게임팩에 없으면 null.</summary>
    public IReadOnlyList<Shop>? Shops { get; init; }

    /// <summary>아이템 인벤토리 마스터가 없으면 null.</summary>
    public IReadOnlyList<ItemEntry>? ItemInventory { get; init; }

    /// <summary>번치 인벤토리 마스터가 없으면 null.</summary>
    public IReadOnlyList<BunchEntry>? BunchInventory { get; init; }

    /// <summary>캐릭터 인벤토리 마스터가 없으면 null.</summary>
    public IReadOnlyList<CharacterEntry>? CharacterInventory { get; init; }

    /// <summary>속성 마스터가 없으면 null.</summary>
    public IReadOnlyList<AttributeEntry>? Attributes { get; init; }

    /// <summary>상태이상 마스터가 없으면 null.</summary>
    public IReadOnlyList<StatusEffectEntry>? StatusEffects { get; init; }

    /// <summary>상태이상 이펙트 마스터가 없으면 null. 게임팩에 적힌 순서 그대로다.</summary>
    public IReadOnlyList<StatusEffectEffect>? StatusEffectEffects { get; init; }

    /// <summary>이 게임팩을 읽을 때 쓴 임베디드 이펙트 등록부(구동기에 하드코딩된 이펙트들).</summary>
    public EmbeddedEffectRegistry EmbeddedEffects { get; init; } = EmbeddedEffectRegistry.Default;

    /// <summary>상점 교환 마스터가 없으면 null.</summary>
    public IReadOnlyList<ShopTrade>? Trades { get; init; }

    /// <summary>아이템/번치 인벤토리의 아이템 코드의 이름. 마스터에 없으면 코드를 그대로 문자열로 돌려준다.</summary>
    /// <remarks>캐릭터 인벤토리는 별도 마스터라 여기서 찾지 않는다.</remarks>
    public string ItemName(long itemCode) =>
        FindItem(itemCode)?.Name
        ?? FindBunch(itemCode)?.Name
        ?? itemCode.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// 아이템 코드와 개수를 실제로 인벤토리에 오가는 아이템 인벤토리의 아이템들로 푼다.
    /// 번치는 부품 × 부품 어마운트 × 개수로, 일반 아이템은 그대로 돌려준다.
    /// </summary>
    /// <exception cref="OverflowException">곱이 long 범위를 넘을 때.</exception>
    public Dictionary<long, long> Expand(long itemCode, long amount)
    {
        var bunch = FindBunch(itemCode);
        return bunch is null
            ? new Dictionary<long, long> { [itemCode] = amount }
            : bunch.Parts.ToDictionary(p => p.ItemCode, p => checked(p.Amount * amount));
    }

    /// <param name="embeddedEffects">상태이상 이펙트가 가리킬 수 있는 임베디드 이펙트들. 생략하면 구동기의 기본 마스터.</param>
    public static GamePack Load(Stream zipStream, EmbeddedEffectRegistry? embeddedEffects = null)
    {
        embeddedEffects ??= EmbeddedEffectRegistry.Default;
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in Tsv.Parse(ReadEntry(zip, "pack.tsv")))
            if (r.Length >= 2) meta[r[0]] = r[1];

        string Need(string key) =>
            meta.TryGetValue(key, out var v) && v.Length > 0
                ? v
                : throw new PackFormatException($"pack.tsv에 '{key}' 항목이 없습니다.");

        var dungeonConditions = new List<FlatCondition>();
        var shopConditions = new List<FlatCondition>();
        var tradeConditions = new List<FlatCondition>();
        var dungeons = LoadConditioned(zip, DungeonMasterPath, ["code", "name"], DungeonFlagMasterPath, "던전", "dungeon_code",
            dungeonConditions, (code, r, groups) => new Dungeon(code, r[1], groups));
        var shops = LoadConditioned(zip, ShopMasterPath, ["code", "name"], ShopConditionMasterPath, "상점", "shop_code",
            shopConditions, (code, r, groups) => new Shop(code, r[1], groups));
        var itemInventory = LoadItemInventory(zip);
        var bunchInventory = LoadBunchInventory(zip, itemInventory);
        var attributes = LoadAttributes(zip);
        var characterInventory = LoadCharacterInventory(zip, attributes);
        CheckItemCodesAreUnique(itemInventory, bunchInventory);
        var trades = LoadTrades(zip, tradeConditions);
        CheckTrades(trades, shops, itemInventory, bunchInventory);
        var statusEffects = LoadStatusEffects(zip);
        var statusEffectEffects = LoadStatusEffectEffects(zip, statusEffects, embeddedEffects);
        var equipmentSlots = LoadEquipmentSlots(zip);
        var tags = LoadTags(zip);
        var uniqueItems = LoadUniqueItems(zip);
        var uniqueItemTags = LoadUniqueItemTags(zip, uniqueItems, tags);
        var commonItemTags = LoadCommonItemTags(zip, itemInventory, tags);
        var characterTags = LoadCharacterTags(zip, characterInventory, tags);
        var equipmentSlotTags = LoadEquipmentSlotTags(zip, characterInventory, equipmentSlots, tags);
        var statuses = LoadStatuses(zip);
        var characterStatuses = LoadCharacterStatuses(zip, characterInventory, statuses);

        return new GamePack
        {
            Id = Need("id"),
            Title = Need("title"),
            Dungeons = dungeons,
            Shops = shops,
            ItemInventory = itemInventory,
            BunchInventory = bunchInventory,
            CharacterInventory = characterInventory,
            Attributes = attributes,
            Trades = trades,
            StatusEffects = statusEffects,
            StatusEffectEffects = statusEffectEffects,
            EquipmentSlots = equipmentSlots,
            Tags = tags,
            UniqueItems = uniqueItems,
            UniqueItemTags = uniqueItemTags,
            CommonItemTags = commonItemTags,
            CharacterTags = characterTags,
            EquipmentSlotTags = equipmentSlotTags,
            Statuses = statuses,
            CharacterStatuses = characterStatuses,
            Masters = BuildMasters(
                dungeons, dungeonConditions, shops, shopConditions, itemInventory, bunchInventory, characterInventory, attributes,
                trades, tradeConditions, statusEffects, statusEffectEffects,
                equipmentSlots, tags, uniqueItems, uniqueItemTags, commonItemTags, characterTags, equipmentSlotTags,
                statuses, characterStatuses),
            EmbeddedEffects = embeddedEffects,
        };
    }

    /// <summary>정수(long) 칸을 읽는다. 비었거나 정수가 아니면 어디의 무엇인지 알려 주는 오류를 낸다.</summary>
    static long ParseLong(string file, string what, string value)
    {
        if (value.Length == 0) throw new PackFormatException($"{file}: {what}이(가) 비어 있습니다.");
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : throw new PackFormatException($"{file}: {what} '{value}'이(가) 정수가 아닙니다.");
    }

    static long ParsePositive(string file, string what, string value)
    {
        var n = ParseLong(file, what, value);
        return n > 0 ? n : throw new PackFormatException($"{file}: {what} '{value}'이(가) 1 이상의 정수가 아닙니다.");
    }

    /// <summary>
    /// 마스터(첫 열이 고유코드)와 조건표(같은 형식의 5열)를 읽는다. 마스터 파일이 없으면 null.
    /// 조건표가 없으면 모든 항목이 조건 없이 열린다. 행 값 검증은 create가 맡는다.
    /// </summary>
    static List<T>? LoadConditioned<T>(
        ZipArchive zip, string masterPath, string[] masterHeader, string conditionPath, string noun, string codeColumn,
        List<FlatCondition> flat,
        Func<long, string[], IReadOnlyList<IReadOnlyList<FlagCondition>>, T> create)
    {
        var masterText = TryReadEntry(zip, masterPath);
        if (masterText is null)
        {
            if (FindEntry(zip, conditionPath) is not null)
                throw new PackFormatException($"{conditionPath}가 있으려면 {masterPath}도 필요합니다.");
            return null;
        }

        var rows = new Dictionary<long, string[]>();
        var order = new List<long>();
        foreach (var r in Body(masterPath, masterText, masterHeader))
        {
            var code = ParseLong(masterPath, $"{noun} 코드", r[0]);
            if (!rows.TryAdd(code, r))
                throw new PackFormatException($"{masterPath}: {noun} 코드 '{code}'가 중복됩니다.");
            order.Add(code);
        }

        // 코드 → (그룹넘버 → 조건들)
        var groups = rows.Keys.ToDictionary(k => k, _ => new SortedDictionary<int, List<FlagCondition>>());
        var conditionText = TryReadEntry(zip, conditionPath);
        if (conditionText is not null)
        {
            var file = conditionPath;
            foreach (var r in Body(file, conditionText, codeColumn, "group_no", "flag_code", "condition_type", "condition_value"))
            {
                var code = ParseLong(file, $"{noun} 코드", r[0]);
                if (!groups.TryGetValue(code, out var byGroup))
                    throw new PackFormatException($"{file}: 알 수 없는 {noun} 코드 '{code}'.");
                if (!int.TryParse(r[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupNo))
                    throw new PackFormatException($"{file}: {noun} '{code}'의 그룹넘버 '{r[1]}'이(가) 정수가 아닙니다.");
                // 숫자("1")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
                if (!Enum.GetNames<ConditionType>().Contains(r[3], StringComparer.OrdinalIgnoreCase)
                    || !Enum.TryParse<ConditionType>(r[3], ignoreCase: true, out var type))
                    throw new PackFormatException(
                        $"{file}: {noun} '{code}'의 컨디션 타입 '{r[3]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<ConditionType>())})");
                // None은 조건 없음이므로 플래그 코드와 밸류를 비워 둘 수 있다.
                long flagCode = 0, value = 0;
                if (type != ConditionType.None)
                {
                    flagCode = ParseLong(file, $"{noun} '{code}'의 플래그 코드", r[2]);
                    value = ParseLong(file, $"{noun} '{code}'의 컨디션 밸류", r[4]);
                }

                if (!byGroup.TryGetValue(groupNo, out var list)) byGroup[groupNo] = list = [];
                var condition = new FlagCondition(flagCode, type, value);
                list.Add(condition);
                flat.Add(new FlatCondition(code, groupNo, condition));       // 파일 순서 그대로(MasterMemory 테이블용)
            }
        }

        return order
            .Select(code => create(
                code,
                rows[code],
                groups[code].Values.Select(l => (IReadOnlyList<FlagCondition>)l).ToList()))
            .ToList();
    }

    static List<AttributeEntry>? LoadAttributes(ZipArchive zip)
    {
        const string file = AttributeMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;

        var attributes = new List<AttributeEntry>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, "attribute_code", "attribute_name"))
        {
            var code = ParseLong(file, "속성 코드", r[0]);
            if (!seen.Add(code))
                throw new PackFormatException($"{file}: 속성 코드 '{code}'가 중복됩니다.");
            attributes.Add(new AttributeEntry(code, r[1]));
        }
        return attributes;
    }

    static List<StatusEffectEntry>? LoadStatusEffects(ZipArchive zip)
    {
        const string file = StatusEffectMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;

        var statusEffects = new List<StatusEffectEntry>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, "status_effect_code", "status_effect_name"))
        {
            var code = ParseLong(file, "상태이상 코드", r[0]);
            if (!seen.Add(code))
                throw new PackFormatException($"{file}: 상태이상 코드 '{code}'가 중복됩니다.");
            statusEffects.Add(new StatusEffectEntry(code, r[1]));
        }
        return statusEffects;
    }

    /// <summary>
    /// 상태이상 이펙트 마스터: 상태이상 코드는 상태이상 마스터에, 이펙트 코드는 임베디드 이펙트에 있어야 한다.
    /// 이펙트 밸류 1~3은 비워 두면 0이다.
    /// </summary>
    static List<StatusEffectEffect>? LoadStatusEffectEffects(
        ZipArchive zip, List<StatusEffectEntry>? statusEffects, EmbeddedEffectRegistry embeddedEffects)
    {
        const string file = StatusEffectEffectMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (statusEffects is null)
            throw new PackFormatException($"{file}가 있으려면 {StatusEffectMasterPath}도 필요합니다.");

        var statusCodes = statusEffects.Select(s => s.Code).ToHashSet();
        var rows = new List<StatusEffectEffect>();
        foreach (var r in Body(file, text, "status_effect_code", "trigger_type", "priority", "effect_code",
                     "effect_value_1", "effect_value_2", "effect_value_3"))
        {
            var statusCode = ParseLong(file, "상태이상 코드", r[0]);
            if (!statusCodes.Contains(statusCode))
                throw new PackFormatException($"{file}: 알 수 없는 상태이상 코드 '{statusCode}'.");
            var who = $"상태이상 '{statusCode}'의";

            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<TriggerType>().Contains(r[1], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<TriggerType>(r[1], ignoreCase: true, out var trigger))
                throw new PackFormatException(
                    $"{file}: {who} 트리거 타입 '{r[1]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<TriggerType>())})");

            var priority = ParseLong(file, $"{who} 우선도", r[2]);
            var effectCode = ParseLong(file, $"{who} 이펙트 코드", r[3]);
            if (!embeddedEffects.Contains(effectCode))
                throw new PackFormatException($"{file}: {who} 이펙트 코드 '{effectCode}'가 임베디드 이펙트에 없습니다.");

            rows.Add(new StatusEffectEffect(
                statusCode, trigger, priority, effectCode,
                OptionalLong(file, $"{who} 이펙트 밸류 1", r[4]),
                OptionalLong(file, $"{who} 이펙트 밸류 2", r[5]),
                OptionalLong(file, $"{who} 이펙트 밸류 3", r[6])));
        }
        return rows;
    }

    /// <summary>비어 있으면 0, 아니면 정수(long).</summary>
    static long OptionalLong(string file, string what, string value) =>
        value.Length == 0 ? 0 : ParseLong(file, what, value);

    static List<ItemEntry>? LoadItemInventory(ZipArchive zip) =>
        LoadLimitedInventory(zip, CommonItemInventoryMasterPath, "아이템", ["item_code", "item_name", "carry_limit"],
            (code, r, limit) => new ItemEntry(code, r[1], limit));

    /// <summary>캐릭터마다 유리속성과 불리속성이 있어야 하고, 둘 다 속성 마스터에 있어야 하며 서로 달라야 한다.</summary>
    static List<CharacterEntry>? LoadCharacterInventory(ZipArchive zip, List<AttributeEntry>? attributes)
    {
        const string file = CharacterInventoryMasterPath;
        var attributeCodes = attributes?.Select(a => a.Code).ToHashSet() ?? [];
        return LoadLimitedInventory(zip, file, "캐릭터",
            ["character_code", "character_name", "carry_limit", "advantage_attribute_code", "disadvantage_attribute_code"],
            (code, r, limit) =>
            {
                var advantage = ParseLong(file, $"캐릭터 '{code}'의 유리속성 코드", r[3]);
                var disadvantage = ParseLong(file, $"캐릭터 '{code}'의 불리속성 코드", r[4]);
                foreach (var (what, attribute) in new[] { ("유리속성", advantage), ("불리속성", disadvantage) })
                    if (!attributeCodes.Contains(attribute))
                        throw new PackFormatException($"{file}: 캐릭터 '{code}'의 {what} '{attribute}'가 속성 마스터에 없습니다.");
                if (advantage == disadvantage)
                    throw new PackFormatException($"{file}: 캐릭터 '{code}'의 유리속성과 불리속성이 같습니다 ({advantage}).");
                return new CharacterEntry(code, r[1], limit, advantage, disadvantage);
            });
    }

    /// <summary>
    /// "코드, 이름, carry_limit, ..." 인벤토리 마스터를 읽는다(열 이름과 추가 열은 마스터마다 다름). 파일이 없으면 null.
    /// 첫 열은 코드, 세 번째 열은 소지제한(1 이상)이고, 나머지 열의 해석은 create가 맡는다.
    /// </summary>
    static List<T>? LoadLimitedInventory<T>(
        ZipArchive zip, string path, string noun, string[] header, Func<long, string[], long, T> create)
    {
        var text = TryReadEntry(zip, path);
        if (text is null) return null;

        var items = new List<T>();
        var seen = new HashSet<long>();
        foreach (var r in Body(path, text, header))
        {
            var code = ParseLong(path, $"{noun} 코드", r[0]);
            if (!seen.Add(code))
                throw new PackFormatException($"{path}: {noun} 코드 '{code}'가 중복됩니다.");
            var limit = ParsePositive(path, $"{noun} '{code}'의 소지제한", r[2]);
            items.Add(create(code, r, limit));
        }
        return items;
    }

    /// <summary>같은 아이템 코드의 행들이 하나의 번치가 된다. 부품은 아이템 인벤토리의 아이템만 가리킬 수 있다.</summary>
    static List<BunchEntry>? LoadBunchInventory(ZipArchive zip, List<ItemEntry>? itemInventory)
    {
        const string file = BunchItemInventoryMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;

        var itemCodes = itemInventory?.Select(i => i.Code).ToHashSet() ?? [];
        var names = new Dictionary<long, string>();
        var parts = new Dictionary<long, List<BunchPart>>();
        var order = new List<long>();
        foreach (var r in Body(file, text, "item_code", "item_name", "part_item_code", "part_amount"))
        {
            var code = ParseLong(file, "번치 아이템 코드", r[0]);
            var partCode = ParseLong(file, $"번치 '{code}'의 파트 아이템 코드", r[2]);
            if (!itemCodes.Contains(partCode))
                throw new PackFormatException($"{file}: 번치 '{code}'의 파트 '{partCode}'가 아이템 인벤토리 마스터에 없습니다.");
            var amount = ParsePositive(file, $"번치 '{code}'의 파트 어마운트", r[3]);

            if (names.TryGetValue(code, out var name))
            {
                if (name != r[1])
                    throw new PackFormatException($"{file}: 번치 '{code}'의 아이템 이름이 행마다 다릅니다 ('{name}', '{r[1]}').");
            }
            else
            {
                names[code] = r[1];
                parts[code] = [];
                order.Add(code);
            }

            if (parts[code].Any(p => p.ItemCode == partCode))
                throw new PackFormatException($"{file}: 번치 '{code}'에 파트 '{partCode}'가 중복됩니다.");
            parts[code].Add(new BunchPart(partCode, amount));
        }
        return order.Select(code => new BunchEntry(code, names[code], parts[code])).ToList();
    }

    static void CheckItemCodesAreUnique(List<ItemEntry>? common, List<BunchEntry>? bunch)
    {
        if (common is null || bunch is null) return;
        var commonCodes = common.Select(i => i.Code).ToHashSet();
        foreach (var b in bunch)
            if (commonCodes.Contains(b.Code))
                throw new PackFormatException($"아이템 코드 '{b.Code}'가 아이템 인벤토리와 번치 인벤토리 마스터에 모두 있습니다.");
    }

    static List<ShopTrade>? LoadTrades(ZipArchive zip, List<FlatCondition> conditions) =>
        LoadConditioned(
            zip, ShopTradeMasterPath,
            ["code", "shop_code", "cost_item_code", "cost_amount", "receive_item_code", "receive_amount"],
            ShopTradeConditionMasterPath, "트레이드", "trade_code",
            conditions,
            (code, r, groups) =>
            {
                const string file = ShopTradeMasterPath;
                var who = $"트레이드 '{code}'의";
                return new ShopTrade(
                    code,
                    ParseLong(file, $"{who} 상점 코드", r[1]),
                    ParseLong(file, $"{who} 코스트 아이템 코드", r[2]),
                    ParsePositive(file, $"{who} 코스트 어마운트", r[3]),
                    ParseLong(file, $"{who} 리시브 아이템 코드", r[4]),
                    ParsePositive(file, $"{who} 리시브 어마운트", r[5]),
                    groups);
            });

    static void CheckTrades(List<ShopTrade>? trades, List<Shop>? shops, List<ItemEntry>? common, List<BunchEntry>? bunch)
    {
        if (trades is null) return;
        var shopCodes = shops?.Select(s => s.Code).ToHashSet() ?? [];
        var itemCodes = (common?.Select(i => i.Code) ?? []).Concat(bunch?.Select(b => b.Code) ?? []).ToHashSet();
        foreach (var t in trades)
        {
            if (!shopCodes.Contains(t.ShopCode))
                throw new PackFormatException($"{ShopTradeMasterPath}: 트레이드 '{t.Code}'의 상점 '{t.ShopCode}'가 상점 마스터에 없습니다.");
            if (!itemCodes.Contains(t.CostItemCode))
                throw new PackFormatException($"{ShopTradeMasterPath}: 트레이드 '{t.Code}'의 코스트 아이템 '{t.CostItemCode}'가 아이템/번치 인벤토리 마스터에 없습니다.");
            if (!itemCodes.Contains(t.ReceiveItemCode))
                throw new PackFormatException($"{ShopTradeMasterPath}: 트레이드 '{t.Code}'의 리시브 아이템 '{t.ReceiveItemCode}'가 아이템/번치 인벤토리 마스터에 없습니다.");
        }
    }

    // 일부 도구는 zip 안의 경로 구분자를 '\'로 저장하므로 둘 다 찾는다.
    static ZipArchiveEntry? FindEntry(ZipArchive zip, string name) =>
        zip.GetEntry(name) ?? zip.GetEntry(name.Replace('/', '\\'));

    static string? TryReadEntry(ZipArchive zip, string name)
    {
        var entry = FindEntry(zip, name);
        if (entry is null) return null;
        using var reader = new StreamReader(entry.Open(), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    static string ReadEntry(ZipArchive zip, string name) =>
        TryReadEntry(zip, name) ?? throw new PackFormatException($"게임팩에 '{name}'이 없습니다.");

    /// <summary>헤더를 검사하고 데이터 행만 반환한다. 열이 모자란 행은 빈 셀로 채운다.</summary>
    static IEnumerable<string[]> Body(string file, string tsv, params string[] header)
    {
        var rows = Tsv.Parse(tsv);
        if (rows.Count == 0 || !rows[0].Take(header.Length).SequenceEqual(header))
            throw new PackFormatException($"{file}: 헤더가 '{string.Join("\\t", header)}'이어야 합니다.");
        foreach (var raw in rows.Skip(1))
        {
            var r = raw;
            if (r.Length < header.Length) Array.Resize(ref r, header.Length);
            yield return r;
        }
    }
}

/// <summary>조건표의 한 행(파일 순서 그대로): 어느 코드(던전/상점/교환)의 몇 번 그룹에 어떤 조건이 있는가.</summary>
internal readonly record struct FlatCondition(long Code, int GroupNo, FlagCondition Condition);
