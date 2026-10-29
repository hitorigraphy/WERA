using Wera.Core;

namespace Wera.Core.Tests;

// memo 열은 사람이 보려고 적는 메모라 엔진은 읽지 않는다(마스터메모리에도 올리지 않는다).
public class MemoColumnTests
{
    static GamePack Load(params (string, string)[] files) =>
        GamePack.Load(CoreTests.PackZip([("pack.tsv", "id\tdemo\ntitle\t데모\n"), .. files]));

    [Fact]
    public void A_memo_column_after_the_last_column_is_ignored()
    {
        var pack = Load(("master/dungeon_master_v1.tsv", "code\tname\tmemo\n1001\t초원\t필수 아님\n1002\t동굴\t\n"));
        Assert.Equal([new Dungeon(1001, "초원", []), new Dungeon(1002, "동굴", [])], pack.Dungeons!.Select(d => d with { Groups = [] }));
    }

    [Fact]
    public void A_memo_column_may_sit_in_the_middle_and_rows_with_a_missing_memo_cell_still_load()
    {
        var pack = Load(("master/shop_master_v1.tsv", "code\tmemo\tname\n2001\t메모\t잡화점\n2002\t\t무기점\n"));
        Assert.Equal(["잡화점", "무기점"], pack.Shops!.Select(s => s.Name));

        var short_rows = Load(("master/shop_master_v1.tsv", "code\tname\tmemo\n2001\t잡화점\n"));
        Assert.Equal("잡화점", short_rows.Shops!.Single().Name);
    }

    [Fact]
    public void The_memo_header_is_matched_case_insensitively_and_other_headers_are_still_checked()
    {
        Assert.Single(Load(("master/shop_master_v1.tsv", "code\tname\tMEMO\n2001\t잡화점\t메모\n")).Shops!);
        Assert.Throws<PackFormatException>(() => Load(("master/shop_master_v1.tsv", "code\ttitle\tmemo\n2001\t잡화점\t메모\n")));
    }

    [Fact]
    public void A_memo_column_works_with_the_optional_trade_kind_columns()
    {
        const string header = "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\tcost_kind\treceive_kind\tmemo\n";
        var pack = Load(
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\tmemo\n11\t금화\t99999\t\n2\t약초\t99\t\n"),
            ("master/shop_master_v1.tsv", "code\tname\tmemo\n2001\t상점\t\n"),
            ("master/shop_trade_master_v1.tsv", header + "5001\t2001\t11\t1\t2\t1\t\t\t테스트용\n5002\t2001\t11\t2\t2\t2\tcommon_item\tcommon_item\t\n"));
        Assert.Equal(2, pack.Trades!.Count);
        Assert.All(pack.Trades, t => Assert.Equal(TradeKind.CommonItem, t.CostKind));

        // 종류 열 한 개만 있으면 메모가 있어도 여전히 거부한다.
        Assert.Throws<PackFormatException>(() => Load(
            ("master/shop_master_v1.tsv", "code\tname\n2001\t상점\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n11\t금화\t99\n"),
            ("master/shop_trade_master_v1.tsv",
                "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\tcost_kind\tmemo\n5001\t2001\t11\t1\t11\t1\t\t메모\n")));
    }

    [Fact]
    public void Memo_columns_work_on_the_condition_tables_too()
    {
        var pack = Load(
            ("master/dungeon_master_v1.tsv", "code\tname\tmemo\n1001\t초원\t\n"),
            ("master/dungeon_flag_master_v1.tsv",
                "dungeon_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\tmemo\n1001\t1\t1001\tGreaterOrEqual\t1\t튜토리얼을 마치면 열린다\n"));
        Assert.Single(pack.Masters.DungeonFlagRowTable.All);
    }

    // ---- 기본 게임팩 ----

    static GamePack Sample()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(zip);
    }

    [Fact]
    public void The_sample_pack_has_a_test_dungeon_and_a_test_shop_that_are_open_from_the_start()
    {
        var pack = Sample();
        Assert.Contains(pack.Dungeons!, d => d.Name == "테스트 던전");
        Assert.Contains(pack.Shops!, s => s.Name == "테스트 상점");

        var s = GameSession.NewGame(pack);
        Assert.Equal(["테스트 던전"], s.AvailableDungeons.Select(d => d.Name));
        Assert.Equal(["테스트 상점"], s.AvailableShops.Select(x => x.Name));
        Assert.Equal(["101", "102", "103", "104", "105", "110", "201", "202", "203", "200", "300"], s.Entries.Select(e => e.Code));
    }

    [Fact]
    public void The_test_shop_trades_cover_common_unique_and_character_kinds()
    {
        var s = GameSession.NewGame(Sample());
        s.InventoryItems[2] = new InventoryItem(200);
        s.Activate("102"); s.Activate("2999");
        Assert.Equal(
            ["금화 ×1 → 약초 ×1", "금화 ×10 → 철검 ×1", "금화 ×100 → 도적 ×1", "철검 ×1 → 금화 ×5"],
            s.CurrentShopTrades.Select(t => t.Text));

        Assert.Single(s.UniqueItemInstances);                             // 디버깅용으로 처음부터 가진 순례자의 은단검
        s.Activate("5992");                                               // 철검(유니크 아이템)을 받는다
        Assert.Equal(2, s.UniqueItemInstances.Count);
        s.Activate("5993");                                               // 도적(캐릭터)을 받는다
        Assert.True(s.Characters.ContainsKey(7004));
        s.Activate("5994");                                               // 철검을 내고 금화를 받는다
        Assert.Equal([8007L], s.UniqueItemInstances.Values.Select(i => i.ItemCode));      // 철검만 나가고 은단검은 그대로
    }

    [Fact]
    public void Only_the_required_rows_of_the_sample_pack_are_marked_required()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        var dir = Path.Combine(root, "samples", "sample-pack", "master");

        var required = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.tsv"))
        {
            var rows = Tsv.Parse(File.ReadAllText(file));
            var memo = Array.IndexOf(rows[0], "memo");
            Assert.True(memo >= 0, $"{Path.GetFileName(file)}에 memo 열이 없습니다.");
            foreach (var row in rows.Skip(1).Where(r => r.Length > memo && r[memo].StartsWith("필수")))
                required.Add($"{Path.GetFileName(file)}:{row[0]}");
        }

        // 엔진이 요구하는 행만 필수다: 예약 태그/스테이터스, 플레이어 캐릭터와 그 속성/태그, 캐릭터 스테이터스의 level/hp 행.
        Assert.Contains("tag_master_v1.tsv:1", required);
        Assert.Contains("tag_master_v1.tsv:2", required);
        Assert.Contains("status_master_v1.tsv:1", required);
        Assert.Contains("status_master_v1.tsv:2", required);
        Assert.Contains("character_inventory_master_v1.tsv:1", required);
        Assert.Contains("character_tag_master_v1.tsv:1", required);
        Assert.Contains("tag_master_v1.tsv:3", required);                    // 메인 통화 태그
        Assert.Contains("common_item_inventory_master_v1.tsv:2", required);  // 메인 통화 아이템
        Assert.Contains("common_item_tag_master_v1.tsv:2", required);
        Assert.DoesNotContain(required, r => r.StartsWith("dungeon_master") || r.StartsWith("shop_master") || r.StartsWith("shop_trade"));
        Assert.DoesNotContain("tag_master_v1.tsv:4", required);
        Assert.DoesNotContain("common_item_inventory_master_v1.tsv:3", required);
        Assert.DoesNotContain("status_master_v1.tsv:3", required);
    }
}
