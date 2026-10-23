using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Wera.Core;

public sealed class PackFormatException(string message) : Exception(message);

public sealed record Choice(string Label, string Next);

public sealed record Scene(string Id, string Text, IReadOnlyList<Choice> Choices);

/// <summary>
/// 게임팩: TSV 묶음 zip. 첫 줄은 헤더. 모든 코드와 수량은 long 정수, 그룹넘버(group_no)는 int.
///   pack.tsv                              key/value 행 (id, title, start)
///   scenes.tsv                            id, text
///   choices.tsv                           scene, label, next
///   master/dungeon_master_v1.tsv          (선택) code, name
///   master/dungeon_flag_master_v1.tsv     (선택) dungeon_code, group_no, flag_code, condition_type, condition_value
///   master/shop_master_v1.tsv             (선택) code, name
///   master/shop_condition_master_v1.tsv   (선택) shop_code, group_no, flag_code, condition_type, condition_value
///   master/item_inventory_master_v1.tsv      (선택) item_code, item_name, carry_limit
///   master/bunch_inventory_master_v1.tsv     (선택) item_code, item_name, part_item_code, part_amount
///   master/character_inventory_master_v1.tsv (선택) character_code, character_name, carry_limit
///   master/shop_trade_master_v1.tsv       (선택) code, shop_code, cost_item_code, cost_amount, receive_item_code, receive_amount
///   master/shop_trade_condition_master_v1.tsv (선택) trade_code, group_no, flag_code, condition_type, condition_value
/// </summary>
public sealed class GamePack
{
    public const string DungeonMasterPath = "master/dungeon_master_v1.tsv";
    public const string DungeonFlagMasterPath = "master/dungeon_flag_master_v1.tsv";
    public const string ShopMasterPath = "master/shop_master_v1.tsv";
    public const string ShopConditionMasterPath = "master/shop_condition_master_v1.tsv";
    public const string ItemInventoryMasterPath = "master/item_inventory_master_v1.tsv";
    public const string CharacterInventoryMasterPath = "master/character_inventory_master_v1.tsv";
    public const string BunchInventoryMasterPath = "master/bunch_inventory_master_v1.tsv";
    public const string ShopTradeMasterPath = "master/shop_trade_master_v1.tsv";
    public const string ShopTradeConditionMasterPath = "master/shop_trade_condition_master_v1.tsv";

    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string StartSceneId { get; init; }
    public required IReadOnlyDictionary<string, Scene> Scenes { get; init; }

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

    /// <summary>상점 교환 마스터가 없으면 null.</summary>
    public IReadOnlyList<ShopTrade>? Trades { get; init; }

    /// <summary>아이템/번치 인벤토리의 아이템 코드의 이름. 마스터에 없으면 코드를 그대로 문자열로 돌려준다.</summary>
    /// <remarks>캐릭터 인벤토리는 별도 마스터라 여기서 찾지 않는다.</remarks>
    public string ItemName(long itemCode) =>
        ItemInventory?.FirstOrDefault(i => i.Code == itemCode)?.Name
        ?? BunchInventory?.FirstOrDefault(b => b.Code == itemCode)?.Name
        ?? itemCode.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// 아이템 코드와 개수를 실제로 인벤토리에 오가는 아이템 인벤토리의 아이템들로 푼다.
    /// 번치는 부품 × 부품 어마운트 × 개수로, 일반 아이템은 그대로 돌려준다.
    /// </summary>
    /// <exception cref="OverflowException">곱이 long 범위를 넘을 때.</exception>
    public Dictionary<long, long> Expand(long itemCode, long amount)
    {
        var bunch = BunchInventory?.FirstOrDefault(b => b.Code == itemCode);
        return bunch is null
            ? new Dictionary<long, long> { [itemCode] = amount }
            : bunch.Parts.ToDictionary(p => p.ItemCode, p => checked(p.Amount * amount));
    }

    public static GamePack Load(Stream zipStream)
    {
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in Tsv.Parse(ReadEntry(zip, "pack.tsv")))
            if (r.Length >= 2) meta[r[0]] = r[1];

        string Need(string key) =>
            meta.TryGetValue(key, out var v) && v.Length > 0
                ? v
                : throw new PackFormatException($"pack.tsv에 '{key}' 항목이 없습니다.");

        var texts = new Dictionary<string, string>();
        foreach (var r in Body("scenes.tsv", ReadEntry(zip, "scenes.tsv"), "id", "text"))
        {
            if (!texts.TryAdd(r[0], r[1]))
                throw new PackFormatException($"scenes.tsv: 장면 id '{r[0]}'가 중복됩니다.");
        }

        var choices = texts.Keys.ToDictionary(k => k, _ => new List<Choice>());
        foreach (var r in Body("choices.tsv", ReadEntry(zip, "choices.tsv"), "scene", "label", "next"))
        {
            if (!choices.TryGetValue(r[0], out var list))
                throw new PackFormatException($"choices.tsv: 알 수 없는 장면 '{r[0]}'.");
            if (!texts.ContainsKey(r[2]))
                throw new PackFormatException($"choices.tsv: '{r[0]}'의 이동 대상 '{r[2]}'가 없습니다.");
            list.Add(new Choice(r[1], r[2]));
        }

        var start = Need("start");
        if (!texts.ContainsKey(start))
            throw new PackFormatException($"시작 장면 '{start}'가 scenes.tsv에 없습니다.");

        var dungeons = LoadConditioned(zip, DungeonMasterPath, ["code", "name"], DungeonFlagMasterPath, "던전", "dungeon_code",
            (code, r, groups) => new Dungeon(code, r[1], groups));
        var shops = LoadConditioned(zip, ShopMasterPath, ["code", "name"], ShopConditionMasterPath, "상점", "shop_code",
            (code, r, groups) => new Shop(code, r[1], groups));
        var itemInventory = LoadItemInventory(zip);
        var bunchInventory = LoadBunchInventory(zip, itemInventory);
        var characterInventory = LoadLimitedInventory(zip, CharacterInventoryMasterPath, "캐릭터", "character_code", "character_name",
            (code, name, limit) => new CharacterEntry(code, name, limit));
        CheckItemCodesAreUnique(itemInventory, bunchInventory);
        var trades = LoadTrades(zip);
        CheckTrades(trades, shops, itemInventory, bunchInventory);

        return new GamePack
        {
            Id = Need("id"),
            Title = Need("title"),
            StartSceneId = start,
            Scenes = texts.ToDictionary(kv => kv.Key, kv => new Scene(kv.Key, kv.Value, choices[kv.Key])),
            Dungeons = dungeons,
            Shops = shops,
            ItemInventory = itemInventory,
            BunchInventory = bunchInventory,
            CharacterInventory = characterInventory,
            Trades = trades,
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
                list.Add(new FlagCondition(flagCode, type, value));
            }
        }

        return order
            .Select(code => create(
                code,
                rows[code],
                groups[code].Values.Select(l => (IReadOnlyList<FlagCondition>)l).ToList()))
            .ToList();
    }

    static List<ItemEntry>? LoadItemInventory(ZipArchive zip) =>
        LoadLimitedInventory(zip, ItemInventoryMasterPath, "아이템", "item_code", "item_name",
            (code, name, limit) => new ItemEntry(code, name, limit));

    /// <summary>"코드, 이름, carry_limit" 3열 인벤토리 마스터를 읽는다(열 이름은 마스터마다 다름). 파일이 없으면 null.</summary>
    static List<T>? LoadLimitedInventory<T>(
        ZipArchive zip, string path, string noun, string codeColumn, string nameColumn, Func<long, string, long, T> create)
    {
        var text = TryReadEntry(zip, path);
        if (text is null) return null;

        var items = new List<T>();
        var seen = new HashSet<long>();
        foreach (var r in Body(path, text, codeColumn, nameColumn, "carry_limit"))
        {
            var code = ParseLong(path, $"{noun} 코드", r[0]);
            if (!seen.Add(code))
                throw new PackFormatException($"{path}: {noun} 코드 '{code}'가 중복됩니다.");
            var limit = ParsePositive(path, $"{noun} '{code}'의 소지제한", r[2]);
            items.Add(create(code, r[1], limit));
        }
        return items;
    }

    /// <summary>같은 아이템 코드의 행들이 하나의 번치가 된다. 부품은 아이템 인벤토리의 아이템만 가리킬 수 있다.</summary>
    static List<BunchEntry>? LoadBunchInventory(ZipArchive zip, List<ItemEntry>? itemInventory)
    {
        const string file = BunchInventoryMasterPath;
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

    static List<ShopTrade>? LoadTrades(ZipArchive zip) =>
        LoadConditioned(
            zip, ShopTradeMasterPath,
            ["code", "shop_code", "cost_item_code", "cost_amount", "receive_item_code", "receive_amount"],
            ShopTradeConditionMasterPath, "트레이드", "trade_code",
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
