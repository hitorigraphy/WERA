using System.IO.Compression;
using System.Text;

namespace Wera.Core;

public sealed class PackFormatException(string message) : Exception(message);

public sealed record Choice(string Label, string Next);

public sealed record Scene(string Id, string Text, IReadOnlyList<Choice> Choices);

/// <summary>
/// 게임팩: TSV 묶음 zip. 첫 줄은 헤더.
///   pack.tsv                              key/value 행 (id, title, start)
///   scenes.tsv                            id, text
///   choices.tsv                           scene, label, next
///   master/dungeon_master_v1.tsv          (선택) code, name
///   master/dungeon_flag_master_v1.tsv     (선택) dungeon_code, group_no, flag_code, condition_type, condition_value
///   master/shop_master_v1.tsv             (선택) code, name
///   master/shop_condition_master_v1.tsv   (선택) shop_code, group_no, flag_code, condition_type, condition_value
///   master/common_item_master_v1.tsv      (선택) item_code, item_name
///   master/bunch_item_master_v1.tsv       (선택) item_code, item_name, part_item_code, part_amount
///   master/shop_trade_master_v1.tsv       (선택) code, shop_code, cost_item_code, cost_amount, receive_item_code, receive_amount
///   master/shop_trade_condition_master_v1.tsv (선택) trade_code, group_no, flag_code, condition_type, condition_value
/// </summary>
public sealed class GamePack
{
    public const string DungeonMasterPath = "master/dungeon_master_v1.tsv";
    public const string DungeonFlagMasterPath = "master/dungeon_flag_master_v1.tsv";
    public const string ShopMasterPath = "master/shop_master_v1.tsv";
    public const string ShopConditionMasterPath = "master/shop_condition_master_v1.tsv";
    public const string CommonItemMasterPath = "master/common_item_master_v1.tsv";
    public const string BunchItemMasterPath = "master/bunch_item_master_v1.tsv";
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

    /// <summary>일반 아이템 마스터가 없으면 null.</summary>
    public IReadOnlyList<CommonItem>? CommonItems { get; init; }

    /// <summary>번치 아이템 마스터가 없으면 null.</summary>
    public IReadOnlyList<BunchItem>? BunchItems { get; init; }

    /// <summary>상점 교환 마스터가 없으면 null.</summary>
    public IReadOnlyList<ShopTrade>? Trades { get; init; }

    /// <summary>일반/번치 아이템 코드의 이름. 마스터에 없으면 코드를 그대로 돌려준다.</summary>
    public string ItemName(string itemCode) =>
        CommonItems?.FirstOrDefault(i => i.Code == itemCode)?.Name
        ?? BunchItems?.FirstOrDefault(b => b.Code == itemCode)?.Name
        ?? itemCode;

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
            (r, groups) => new Dungeon(r[0], r[1], groups));
        var shops = LoadConditioned(zip, ShopMasterPath, ["code", "name"], ShopConditionMasterPath, "상점", "shop_code",
            (r, groups) => new Shop(r[0], r[1], groups));
        var commonItems = LoadCommonItems(zip);
        var bunchItems = LoadBunchItems(zip, commonItems);
        CheckItemCodesAreUnique(commonItems, bunchItems);
        var trades = LoadTrades(zip);
        CheckTrades(trades, shops, commonItems, bunchItems);

        return new GamePack
        {
            Id = Need("id"),
            Title = Need("title"),
            StartSceneId = start,
            Scenes = texts.ToDictionary(kv => kv.Key, kv => new Scene(kv.Key, kv.Value, choices[kv.Key])),
            Dungeons = dungeons,
            Shops = shops,
            CommonItems = commonItems,
            BunchItems = bunchItems,
            Trades = trades,
        };
    }

    /// <summary>
    /// 마스터(첫 열이 고유코드)와 조건표(같은 형식의 5열)를 읽는다. 마스터 파일이 없으면 null.
    /// 조건표가 없으면 모든 항목이 조건 없이 열린다. 행 값 검증은 create가 맡는다.
    /// </summary>
    static List<T>? LoadConditioned<T>(
        ZipArchive zip, string masterPath, string[] masterHeader, string conditionPath, string noun, string codeColumn,
        Func<string[], IReadOnlyList<IReadOnlyList<FlagCondition>>, T> create)
    {
        var masterText = TryReadEntry(zip, masterPath);
        if (masterText is null)
        {
            if (FindEntry(zip, conditionPath) is not null)
                throw new PackFormatException($"{conditionPath}가 있으려면 {masterPath}도 필요합니다.");
            return null;
        }

        var rows = new Dictionary<string, string[]>();
        var order = new List<string>();
        foreach (var r in Body(masterPath, masterText, masterHeader))
        {
            if (r[0].Length == 0) throw new PackFormatException($"{masterPath}: 고유코드가 비어 있습니다.");
            if (!rows.TryAdd(r[0], r))
                throw new PackFormatException($"{masterPath}: {noun} 코드 '{r[0]}'가 중복됩니다.");
            order.Add(r[0]);
        }

        // 코드 → (그룹넘버 → 조건들)
        var groups = rows.Keys.ToDictionary(k => k, _ => new SortedDictionary<int, List<FlagCondition>>());
        var conditionText = TryReadEntry(zip, conditionPath);
        if (conditionText is not null)
        {
            var file = conditionPath;
            foreach (var r in Body(file, conditionText, codeColumn, "group_no", "flag_code", "condition_type", "condition_value"))
            {
                if (!groups.TryGetValue(r[0], out var byGroup))
                    throw new PackFormatException($"{file}: 알 수 없는 {noun} 코드 '{r[0]}'.");
                if (!int.TryParse(r[1], out var groupNo))
                    throw new PackFormatException($"{file}: {noun} '{r[0]}'의 그룹넘버 '{r[1]}'가 정수가 아닙니다.");
                // 숫자("1")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
                if (!Enum.GetNames<ConditionType>().Contains(r[3], StringComparer.OrdinalIgnoreCase)
                    || !Enum.TryParse<ConditionType>(r[3], ignoreCase: true, out var type))
                    throw new PackFormatException(
                        $"{file}: {noun} '{r[0]}'의 컨디션 타입 '{r[3]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<ConditionType>())})");
                // None은 조건 없음이므로 플래그 코드와 밸류를 비워 둘 수 있다.
                long value = 0;
                if (type != ConditionType.None)
                {
                    if (r[2].Length == 0)
                        throw new PackFormatException($"{file}: {noun} '{r[0]}'의 플래그 코드가 비어 있습니다.");
                    if (!long.TryParse(r[4], out value))
                        throw new PackFormatException($"{file}: {noun} '{r[0]}'의 컨디션 밸류 '{r[4]}'가 정수가 아닙니다.");
                }

                if (!byGroup.TryGetValue(groupNo, out var list)) byGroup[groupNo] = list = [];
                list.Add(new FlagCondition(r[2], type, value));
            }
        }

        return order
            .Select(code => create(
                rows[code],
                groups[code].Values.Select(l => (IReadOnlyList<FlagCondition>)l).ToList()))
            .ToList();
    }

    static List<CommonItem>? LoadCommonItems(ZipArchive zip)
    {
        var text = TryReadEntry(zip, CommonItemMasterPath);
        if (text is null) return null;

        var items = new List<CommonItem>();
        var seen = new HashSet<string>();
        foreach (var r in Body(CommonItemMasterPath, text, "item_code", "item_name"))
        {
            if (r[0].Length == 0) throw new PackFormatException($"{CommonItemMasterPath}: 아이템 코드가 비어 있습니다.");
            if (!seen.Add(r[0]))
                throw new PackFormatException($"{CommonItemMasterPath}: 아이템 코드 '{r[0]}'가 중복됩니다.");
            items.Add(new CommonItem(r[0], r[1]));
        }
        return items;
    }

    /// <summary>같은 아이템 코드의 행들이 하나의 번치 아이템이 된다. 부품은 일반 아이템만 가리킬 수 있다.</summary>
    static List<BunchItem>? LoadBunchItems(ZipArchive zip, List<CommonItem>? commonItems)
    {
        const string file = BunchItemMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;

        var commonCodes = commonItems?.Select(i => i.Code).ToHashSet() ?? [];
        var names = new Dictionary<string, string>();
        var parts = new Dictionary<string, List<BunchPart>>();
        var order = new List<string>();
        foreach (var r in Body(file, text, "item_code", "item_name", "part_item_code", "part_amount"))
        {
            if (r[0].Length == 0) throw new PackFormatException($"{file}: 아이템 코드가 비어 있습니다.");
            if (r[2].Length == 0) throw new PackFormatException($"{file}: 번치 '{r[0]}'의 파트 아이템 코드가 비어 있습니다.");
            if (!commonCodes.Contains(r[2]))
                throw new PackFormatException($"{file}: 번치 '{r[0]}'의 파트 '{r[2]}'가 일반 아이템 마스터에 없습니다.");
            if (!int.TryParse(r[3], out var amount) || amount <= 0)
                throw new PackFormatException($"{file}: 번치 '{r[0]}'의 파트 어마운트 '{r[3]}'가 1 이상의 정수가 아닙니다.");

            if (names.TryGetValue(r[0], out var name))
            {
                if (name != r[1])
                    throw new PackFormatException($"{file}: 번치 '{r[0]}'의 아이템 이름이 행마다 다릅니다 ('{name}', '{r[1]}').");
            }
            else
            {
                names[r[0]] = r[1];
                parts[r[0]] = [];
                order.Add(r[0]);
            }

            if (parts[r[0]].Any(p => p.ItemCode == r[2]))
                throw new PackFormatException($"{file}: 번치 '{r[0]}'에 파트 '{r[2]}'가 중복됩니다.");
            parts[r[0]].Add(new BunchPart(r[2], amount));
        }
        return order.Select(code => new BunchItem(code, names[code], parts[code])).ToList();
    }

    static void CheckItemCodesAreUnique(List<CommonItem>? common, List<BunchItem>? bunch)
    {
        if (common is null || bunch is null) return;
        var commonCodes = common.Select(i => i.Code).ToHashSet();
        foreach (var b in bunch)
            if (commonCodes.Contains(b.Code))
                throw new PackFormatException($"아이템 코드 '{b.Code}'가 일반 아이템과 번치 아이템 마스터에 모두 있습니다.");
    }

    static List<ShopTrade>? LoadTrades(ZipArchive zip) =>
        LoadConditioned(
            zip, ShopTradeMasterPath,
            ["code", "shop_code", "cost_item_code", "cost_amount", "receive_item_code", "receive_amount"],
            ShopTradeConditionMasterPath, "트레이드", "trade_code",
            (r, groups) =>
            {
                const string file = ShopTradeMasterPath;
                foreach (var (index, column) in new[] { (1, "shop_code"), (2, "cost_item_code"), (4, "receive_item_code") })
                    if (r[index].Length == 0)
                        throw new PackFormatException($"{file}: 트레이드 '{r[0]}'의 {column}가 비어 있습니다.");
                if (!int.TryParse(r[3], out var cost) || cost <= 0)
                    throw new PackFormatException($"{file}: 트레이드 '{r[0]}'의 코스트 어마운트 '{r[3]}'가 1 이상의 정수가 아닙니다.");
                if (!int.TryParse(r[5], out var receive) || receive <= 0)
                    throw new PackFormatException($"{file}: 트레이드 '{r[0]}'의 리시브 어마운트 '{r[5]}'가 1 이상의 정수가 아닙니다.");
                return new ShopTrade(r[0], r[1], r[2], cost, r[4], receive, groups);
            });

    static void CheckTrades(List<ShopTrade>? trades, List<Shop>? shops, List<CommonItem>? common, List<BunchItem>? bunch)
    {
        if (trades is null) return;
        var shopCodes = shops?.Select(s => s.Code).ToHashSet() ?? [];
        var itemCodes = (common?.Select(i => i.Code) ?? []).Concat(bunch?.Select(b => b.Code) ?? []).ToHashSet();
        foreach (var t in trades)
        {
            if (!shopCodes.Contains(t.ShopCode))
                throw new PackFormatException($"{ShopTradeMasterPath}: 트레이드 '{t.Code}'의 상점 '{t.ShopCode}'가 상점 마스터에 없습니다.");
            if (!itemCodes.Contains(t.CostItemCode))
                throw new PackFormatException($"{ShopTradeMasterPath}: 트레이드 '{t.Code}'의 코스트 아이템 '{t.CostItemCode}'가 아이템 마스터에 없습니다.");
            if (!itemCodes.Contains(t.ReceiveItemCode))
                throw new PackFormatException($"{ShopTradeMasterPath}: 트레이드 '{t.Code}'의 리시브 아이템 '{t.ReceiveItemCode}'가 아이템 마스터에 없습니다.");
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
