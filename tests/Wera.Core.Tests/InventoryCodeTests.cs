using Wera.Core;

namespace Wera.Core.Tests;

// 인벤토리 마스터(커먼, 유니크, 번치, 캐릭터)의 코드 열은 inventory_code이고, 게임팩을 읽을 때 네 마스터를 코드로 찾는
// 하나의 사전(GamePack.Inventory)으로 합친다. 코드는 네 마스터를 통틀어 겹치지 않는다.
public class InventoryCodeTests
{
    const string Common = "inventory_code\titem_name\tcarry_limit\n11\t금화\t99999\n12\t약초\t99\n";
    const string Unique = "inventory_code\titem_name\n8001\t철검\n";
    const string Bunch = "inventory_code\titem_name\tpart_item_code\tpart_amount\n9001\t초보자 세트\t12\t3\n9001\t초보자 세트\t11\t1\n";
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n";

    static GamePack Load(
        string common = Common, string unique = Unique, string? bunch = Bunch, string characters = Characters, string? trades = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/common_item_inventory_master_v1.tsv", common),
            ("master/unique_item_inventory_master_v1.tsv", unique),
            ("master/character_inventory_master_v1.tsv", characters),
        };
        if (bunch is not null) files.Add(("master/bunch_item_inventory_master_v1.tsv", bunch));
        if (trades is not null)
        {
            files.Add(("master/shop_master_v1.tsv", "code\tname\n2001\t상점\n"));
            files.Add(("master/shop_trade_master_v1.tsv", trades));
        }
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    // ---- 사전 ----

    [Fact]
    public void The_inventory_dictionary_holds_every_inventory_master_by_code()
    {
        var inventory = Load().Inventory;
        Assert.Equal(new InventoryEntry(11, InventoryKind.CommonItem, "금화"), inventory[11]);
        Assert.Equal(new InventoryEntry(8001, InventoryKind.UniqueItem, "철검"), inventory[8001]);
        Assert.Equal(new InventoryEntry(9001, InventoryKind.BunchItem, "초보자 세트"), inventory[9001]);
        Assert.Equal(new InventoryEntry(1, InventoryKind.Character, "당신"), inventory[1]);
        Assert.Equal(new InventoryEntry(7001, InventoryKind.Character, "용사"), inventory[7001]);
        Assert.Equal(7, inventory.Count);                                           // 번치는 부품 행이 아니라 번치 하나, 메인 통화(아이템 2)는 시험 도우미가 더한다
    }

    [Fact]
    public void Lookups_by_inventory_code_find_the_kind_or_null()
    {
        var pack = Load();
        Assert.Equal(InventoryKind.Character, pack.FindInventory(1)!.Kind);
        Assert.Equal(InventoryKind.BunchItem, pack.FindInventory(9001)!.Kind);
        Assert.Null(pack.FindInventory(123456));
    }

    [Fact]
    public void A_pack_without_the_optional_inventory_masters_still_holds_the_others()
    {
        var inventory = Load(bunch: null).Inventory;
        Assert.DoesNotContain(inventory.Values, e => e.Kind == InventoryKind.BunchItem);
        Assert.Equal(6, inventory.Count);
    }

    // ---- 겹치지 않는다 ----

    [Theory]
    [InlineData("1\t다른 금화\t9\n", null, null, "common_item_inventory_master", "character_inventory_master")]       // 커먼 ↔ 캐릭터(플레이어 코드 1)
    [InlineData(null, "11\t철검\n", null, "common_item_inventory_master", "unique_item_inventory_master")]                  // 커먼 ↔ 유니크
    [InlineData(null, null, "12\t세트\t11\t1\n", "common_item_inventory_master", "bunch_item_inventory_master")]            // 커먼 ↔ 번치
    [InlineData(null, "7001\t철검\n", null, "unique_item_inventory_master", "character_inventory_master")]                  // 유니크 ↔ 캐릭터
    [InlineData(null, "9001\t철검\n", null, "unique_item_inventory_master", "bunch_item_inventory_master")]                 // 유니크 ↔ 번치
    [InlineData(null, null, "1\t세트\t11\t1\n", "bunch_item_inventory_master", "character_inventory_master")]               // 번치 ↔ 캐릭터
    public void The_same_inventory_code_in_two_inventory_masters_is_rejected(
        string? commonRows, string? uniqueRows, string? bunchRows, string first, string second)
    {
        var error = Assert.Throws<PackFormatException>(() => Load(
            common: Common + (commonRows ?? ""),
            unique: Unique + (uniqueRows ?? ""),
            bunch: Bunch + (bunchRows ?? "")));
        Assert.Contains("인벤토리 코드", error.Message);
        Assert.Contains(first, error.Message);
        Assert.Contains(second, error.Message);
    }

    [Fact]
    public void The_same_code_twice_inside_one_master_is_still_rejected_by_that_master()
    {
        Assert.Throws<PackFormatException>(() => Load(common: Common + "11\t또 금화\t9\n"));
        Assert.Throws<PackFormatException>(() => Load(unique: Unique + "8001\t또 철검\n"));
        Assert.Throws<PackFormatException>(() => Load(characters: Characters + "7001\t또 용사\t1\t1\t2\n"));
    }

    // ---- 열 이름 ----

    [Theory]
    [InlineData("item_code\titem_name\tcarry_limit\n11\t금화\t9\n")]
    public void The_old_code_column_name_is_rejected_for_common_items(string common) =>
        Assert.Throws<PackFormatException>(() => Load(common: common));

    [Fact]
    public void The_old_code_column_names_are_rejected_for_the_other_inventory_masters()
    {
        Assert.Throws<PackFormatException>(() => Load(unique: "item_code\titem_name\n8001\t철검\n"));
        Assert.Throws<PackFormatException>(() => Load(bunch: "item_code\titem_name\tpart_item_code\tpart_amount\n9001\tA\t12\t1\n"));
        Assert.Throws<PackFormatException>(() => Load(characters:
            "character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n"));
    }

    // ---- 교환의 종류는 코드로 알아낸다 ----

    const string TradeHeader = "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\tcost_kind\treceive_kind\n";

    [Fact]
    public void Trade_kinds_are_inferred_from_the_inventory_code_when_the_cells_are_empty()
    {
        var pack = Load(trades: TradeHeader +
            "5001\t2001\t11\t5\t8001\t1\t\t\n" +             // 커먼 → 유니크
            "5002\t2001\t8001\t1\t7001\t1\t\t\n" +           // 유니크 → 캐릭터
            "5003\t2001\t7001\t1\t9001\t1\t\t\n");           // 캐릭터 → 번치(커먼 취급)
        var kinds = pack.Trades!.Select(t => (t.CostKind, t.ReceiveKind)).ToList();
        Assert.Equal([(TradeKind.CommonItem, TradeKind.UniqueItem), (TradeKind.UniqueItem, TradeKind.Character), (TradeKind.Character, TradeKind.CommonItem)], kinds);
    }

    [Fact]
    public void Trade_kind_columns_may_be_left_out_entirely()
    {
        var pack = Load(trades: "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\n5001\t2001\t11\t5\t8001\t1\n");
        Assert.Equal(TradeKind.UniqueItem, pack.Trades!.Single().ReceiveKind);
    }

    [Theory]
    [InlineData("5001\t2001\t8001\t1\t11\t1\tcommon_item\t\n", "코스트 코드 '8001'")]      // 유니크 코드를 커먼이라고 적었다
    [InlineData("5001\t2001\t11\t1\t7001\t1\t\tunique_item\n", "리시브 코드 '7001'")]      // 캐릭터 코드를 유니크라고 적었다
    [InlineData("5001\t2001\t11\t1\t9001\t1\t\tcharacter\n", "리시브 코드 '9001'")]        // 번치 코드를 캐릭터라고 적었다
    public void A_written_kind_that_disagrees_with_the_inventory_code_is_rejected(string row, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(trades: TradeHeader + row)).Message);

    [Fact]
    public void An_unknown_code_is_reported_as_missing_from_the_masters()
    {
        var error = Assert.Throws<PackFormatException>(() => Load(trades: TradeHeader + "5001\t2001\t99999\t1\t11\t1\t\t\n"));
        Assert.Contains("코스트 '99999'", error.Message);
    }

    // ---- 기본 게임팩 ----

    [Fact]
    public void The_default_pack_keeps_every_inventory_code_unique_with_the_player_at_one()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.zip"));
        var pack = GamePack.Load(zip);

        Assert.Equal(InventoryKind.Character, pack.FindInventory(GamePack.PlayerCharacterCode)!.Kind);
        Assert.Equal("금화", pack.FindInventory(GamePack.MainCurrencyItemCode)!.Name);
        Assert.Equal(InventoryKind.UniqueItem, pack.FindInventory(8001)!.Kind);
        Assert.Equal(InventoryKind.BunchItem, pack.FindInventory(9001)!.Kind);
        var total = (pack.ItemInventory?.Count ?? 0) + (pack.UniqueItems?.Count ?? 0) + (pack.BunchInventory?.Count ?? 0) + (pack.CharacterInventory?.Count ?? 0);
        Assert.Equal(total, pack.Inventory.Count);                                   // 겹친 코드가 없다
    }
}
