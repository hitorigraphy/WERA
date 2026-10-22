using Wera.Core;

namespace Wera.Core.Tests;

public class TradeTests
{
    const string Shops = "code\tname\n2001\t잡화점\n2002\t무기점\n";
    const string Common = "item_code\titem_name\tcarry_limit\n1\t금화\t99999\n2\t약초\t99\n3\t철광석\t99\n";
    const string Bunch = "item_code\titem_name\tpart_item_code\tpart_amount\n9001\t초보자 세트\t2\t3\n9001\t초보자 세트\t3\t1\n";
    const string TradeHeader = "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\n";
    const string CondHeader = "trade_code\tgroup_no\tflag_code\tcondition_type\tcondition_value\n";

    static GamePack Pack(
        string? shops = Shops, string? common = Common, string? bunch = null,
        string? trades = null, string? tradeConditions = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
            ("scenes.tsv", "id\ttext\na\tx\n"),
            ("choices.tsv", "scene\tlabel\tnext\n"),
        };
        if (shops is not null) files.Add(("master/shop_master_v1.tsv", shops));
        if (common is not null) files.Add(("master/item_inventory_master_v1.tsv", common));
        if (bunch is not null) files.Add(("master/bunch_inventory_master_v1.tsv", bunch));
        if (trades is not null) files.Add(("master/shop_trade_master_v1.tsv", trades));
        if (tradeConditions is not null) files.Add(("master/shop_trade_condition_master_v1.tsv", tradeConditions));
        return GamePack.Load(CoreTests.Zip(files.ToArray()));
    }

    static Dictionary<string, long> F(params (string, int)[] kv) => kv.ToDictionary(x => x.Item1, x => (long)x.Item2);

    [Fact]
    public void All_item_and_trade_masters_are_optional()
    {
        var pack = Pack(shops: null, common: null);
        Assert.Null(pack.ItemInventory);
        Assert.Null(pack.BunchInventory);
        Assert.Null(pack.Trades);
    }

    // ---- item_inventory_master ----

    [Fact]
    public void Item_inventory_loads_with_carry_limits()
    {
        var items = Pack().ItemInventory!;
        Assert.Equal(
            [new ItemEntry("1", "금화", 99999), new ItemEntry("2", "약초", 99), new ItemEntry("3", "철광석", 99)],
            items);
    }

    [Theory]
    [InlineData("item_code\titem_name\tcarry_limit\n1\ta\t5\n1\tb\t5\n")]   // 중복 코드
    [InlineData("item_code\titem_name\tcarry_limit\n\ta\t5\n")]             // 빈 코드
    [InlineData("item_code\titem_name\n1\ta\n")]                            // carry_limit 열 없음
    [InlineData("code\tname\tcarry_limit\n1\ta\t5\n")]                      // 헤더 불일치
    [InlineData("item_code\titem_name\tcarry_limit\n1\ta\t0\n")]            // 소지제한 0
    [InlineData("item_code\titem_name\tcarry_limit\n1\ta\t-1\n")]           // 음수
    [InlineData("item_code\titem_name\tcarry_limit\n1\ta\tabc\n")]          // 정수 아님
    [InlineData("item_code\titem_name\tcarry_limit\n1\ta\t\n")]             // 빈 소지제한
    [InlineData("item_code\titem_name\tcarry_limit\n1\ta\t3000000000\n")]   // int 범위 초과
    public void Bad_item_inventory_is_rejected(string common)
        => Assert.Throws<PackFormatException>(() => Pack(common: common));

    // ---- bunch_inventory_master (소지제한 열 없음) ----


    [Fact]
    public void Bunch_rows_with_same_code_form_one_item_with_parts_in_order()
    {
        var bunch = Assert.Single(Pack(bunch: Bunch).BunchInventory!);
        Assert.Equal("9001", bunch.Code);
        Assert.Equal("초보자 세트", bunch.Name);
        Assert.Equal([new BunchPart("2", 3), new BunchPart("3", 1)], bunch.Parts);
    }

    [Theory]
    [InlineData("9001\tA\t2\t1\n9001\tB\t3\t1\n")]   // 같은 번치의 이름이 행마다 다름
    [InlineData("9001\tA\t99\t1\n")]                  // 일반 아이템에 없는 파트
    [InlineData("9001\tA\t\t1\n")]                    // 빈 파트 코드
    [InlineData("9001\tA\t2\t0\n")]                   // 어마운트 0
    [InlineData("9001\tA\t2\t-1\n")]                  // 음수
    [InlineData("9001\tA\t2\tx\n")]                   // 정수 아님
    [InlineData("9001\tA\t2\t1\n9001\tA\t2\t2\n")]    // 같은 파트 중복
    [InlineData("\tA\t2\t1\n")]                       // 빈 번치 코드
    [InlineData("1\tA\t2\t1\n")]                      // 일반 아이템 코드와 충돌
    public void Bad_bunch_rows_are_rejected(string row)
        => Assert.Throws<PackFormatException>(() =>
            Pack(bunch: "item_code\titem_name\tpart_item_code\tpart_amount\n" + row));

    [Fact]
    public void Bunch_parts_cannot_reference_other_bunch_items()
        => Assert.Throws<PackFormatException>(() => Pack(bunch:
            "item_code\titem_name\tpart_item_code\tpart_amount\n9001\tA\t2\t1\n9002\tB\t9001\t1\n"));

    [Fact]
    public void Bunch_without_common_item_master_is_rejected()
        => Assert.Throws<PackFormatException>(() => Pack(common: null, bunch: Bunch));

    // ---- shop_trade_master ----

    [Fact]
    public void Trades_load_all_six_columns()
    {
        var pack = Pack(trades: TradeHeader + "5001\t2001\t1\t100\t2\t5\n");
        var t = Assert.Single(pack.Trades!);
        Assert.Equal(new ShopTrade("5001", "2001", "1", 100, "2", 5, []), t with { Groups = [] });
    }

    [Fact]
    public void Trades_may_use_bunch_items_as_cost_or_receive()
    {
        var pack = Pack(bunch: Bunch, trades: TradeHeader + "5001\t2001\t1\t100\t9001\t1\n5002\t2001\t9001\t1\t1\t50\n");
        Assert.Equal(2, pack.Trades!.Count);
    }

    [Theory]
    [InlineData("5001\t9999\t1\t1\t2\t1\n")]   // 모르는 상점
    [InlineData("5001\t2001\t99\t1\t2\t1\n")]  // 모르는 코스트 아이템
    [InlineData("5001\t2001\t1\t1\t99\t1\n")]  // 모르는 리시브 아이템
    [InlineData("5001\t2001\t1\t0\t2\t1\n")]   // 코스트 어마운트 0
    [InlineData("5001\t2001\t1\t1\t2\t0\n")]   // 리시브 어마운트 0
    [InlineData("5001\t2001\t1\tx\t2\t1\n")]   // 정수 아님
    [InlineData("5001\t\t1\t1\t2\t1\n")]       // 빈 상점 코드
    [InlineData("5001\t2001\t\t1\t2\t1\n")]    // 빈 코스트 아이템
    [InlineData("5001\t2001\t1\t1\t\t1\n")]    // 빈 리시브 아이템
    [InlineData("\t2001\t1\t1\t2\t1\n")]       // 빈 고유코드
    [InlineData("5001\t2001\t1\t1\t2\t1\n5001\t2002\t1\t1\t2\t1\n")]   // 고유코드 중복
    public void Bad_trade_rows_are_rejected(string rows)
        => Assert.Throws<PackFormatException>(() => Pack(trades: TradeHeader + rows));

    [Fact]
    public void Trades_need_shop_and_item_masters()
    {
        const string row = TradeHeader + "5001\t2001\t1\t1\t2\t1\n";
        Assert.Throws<PackFormatException>(() => Pack(shops: null, trades: row));
        Assert.Throws<PackFormatException>(() => Pack(common: null, trades: row));
    }

    [Fact]
    public void Trade_master_header_is_checked()
        => Assert.Throws<PackFormatException>(() => Pack(trades: "code\tshop_code\n5001\t2001\n"));

    // ---- 교환 목록 화면 ----

    static GameSession Session()
    {
        var pack = Pack(
            bunch: Bunch,
            trades: TradeHeader +
                "5001\t2001\t1\t100\t2\t5\n" +
                "5002\t2001\t2\t10\t1\t30\n" +
                "5003\t2002\t3\t3\t9001\t1\n",
            tradeConditions: CondHeader + "5002\t1\ttutorial_clear\tGreaterOrEqual\t1\n");
        var s = GameSession.NewGame(pack);
        s.Activate("102");
        return s;
    }

    [Fact]
    public void Picking_a_shop_opens_its_trade_list_with_item_names()
    {
        var s = Session();
        Assert.Equal(["2001", "2002", "999"], s.Entries.Select(e => e.Code));

        Assert.NotNull(s.Activate("2002"));
        Assert.Equal(Screen.ShopTrades, s.Screen);
        Assert.Equal("2002", s.CurrentShopCode);
        Assert.Equal("무기점", s.CurrentShop!.Name);
        // 번치 아이템도 이름으로 보인다.
        Assert.Equal([new TradeLine("5003", "철광석", 3, "초보자 세트", 1)], s.CurrentShopTrades);
    }

    [Fact]
    public void Trade_list_shows_only_trades_whose_conditions_are_met()
    {
        var s = Session();
        s.Activate("2001");
        Assert.Equal(["5001"], s.CurrentShopTrades.Select(t => t.Code));    // 5002는 튜토리얼 필요

        s.PlayerFlags["tutorial_clear"] = 1;
        Assert.Equal(["5001", "5002"], s.CurrentShopTrades.Select(t => t.Code));
        Assert.Equal(new TradeLine("5001", "금화", 100, "약초", 5), s.CurrentShopTrades[0]);
    }

    [Fact]
    public void Trade_list_back_goes_to_shop_list_then_home()
    {
        var s = Session();
        s.Activate("2001");
        Assert.Equal(["5001", "999"], s.Entries.Select(e => e.Code));   // 교환 + 돌아가기

        Assert.NotNull(s.Activate("999"));
        Assert.Equal(Screen.ShopList, s.Screen);
        Assert.Null(s.CurrentShopCode);
        Assert.Empty(s.CurrentShopTrades);

        Assert.NotNull(s.Activate("999"));
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Shop_with_no_trades_shows_an_empty_list()
    {
        var pack = Pack(trades: TradeHeader + "5001\t2001\t1\t1\t2\t1\n");
        var s = GameSession.NewGame(pack);
        s.Activate("102");
        s.Activate("2002");                                   // 2002에는 교환이 없다
        Assert.Equal(Screen.ShopTrades, s.Screen);
        Assert.Empty(s.CurrentShopTrades);
    }

    // ---- 교환 실행 ----

    static int Have(GameSession s, string item) => s.InventoryItems.GetValueOrDefault(item).Amount;

    static GameSession InShop(string shopCode, params (string item, int amount)[] inventory)
    {
        var s = Session();                      // 이미 상점가 화면
        foreach (var (item, amount) in inventory) s.InventoryItems[item] = new InventoryItem(amount);
        s.Activate(shopCode);
        return s;
    }

    [Fact]
    public void Trade_entries_are_the_available_trades_plus_back()
    {
        var s = InShop("2001");
        Assert.Equal(["5001", "999"], s.Entries.Select(e => e.Code));
        Assert.Equal("금화 ×100 → 약초 ×5", s.Entries[0].Label);
        Assert.Equal(EntryAction.Trade, s.Entries[0].Action);

        s.PlayerFlags["tutorial_clear"] = 1;                 // 5002가 열린다
        Assert.Equal(["5001", "5002", "999"], s.Entries.Select(e => e.Code));
    }

    [Fact]
    public void Trade_pays_cost_and_gives_receive_and_stays_on_the_screen()
    {
        var s = InShop("2001", ("1", 150));
        var entry = s.Activate("5001");

        Assert.Equal(EntryAction.Trade, entry!.Action);
        Assert.Equal(Screen.ShopTrades, s.Screen);
        Assert.Equal("2001", s.CurrentShopCode);
        Assert.Equal(50, Have(s, "1"));
        Assert.Equal(5, Have(s, "2"));
        Assert.Equal("교환 완료: 금화 ×100 → 약초 ×5", s.Message);
    }

    [Fact]
    public void Trade_can_be_repeated_and_removes_an_item_that_reaches_zero()
    {
        var s = InShop("2001", ("1", 200));
        s.Activate("5001");
        s.Activate("5001");
        Assert.False(s.InventoryItems.ContainsKey("1"));     // 0개가 되면 항목을 지운다
        Assert.Equal(10, Have(s, "2"));

        s.Activate("5001");                                  // 이제 금화가 없다
        Assert.Contains("교환 실패", s.Message);
        Assert.Equal(10, Have(s, "2"));
    }

    [Fact]
    public void Trade_fails_without_changing_anything_when_cost_is_short()
    {
        var s = InShop("2001", ("1", 99), ("2", 4));
        s.Activate("5001");

        Assert.Contains("부족: 금화", s.Message);
        Assert.Contains("보유 99 / 필요 100", s.Message);
        Assert.Equal(99, Have(s, "1"));
        Assert.Equal(4, Have(s, "2"));
    }

    [Fact]
    public void Trade_fails_when_the_cost_item_is_not_owned_at_all()
    {
        var s = InShop("2001");
        s.Activate("5001");
        Assert.Contains("보유 0 / 필요 100", s.Message);
        Assert.Empty(s.InventoryItems);
    }

    [Fact]
    public void Trade_with_unmet_conditions_cannot_be_run_even_by_typing_its_code()
    {
        var s = InShop("2001", ("2", 50));
        Assert.Null(s.Activate("5002"));
        Assert.Null(s.Message);
        Assert.Equal(50, Have(s, "2"));

        s.PlayerFlags["tutorial_clear"] = 1;
        Assert.NotNull(s.Activate("5002"));                  // 약초 ×10 → 금화 ×30
        Assert.Equal(40, Have(s, "2"));
        Assert.Equal(30, Have(s, "1"));
    }

    [Fact]
    public void Receiving_a_bunch_item_gives_its_parts()
    {
        var s = InShop("2002", ("3", 5));                    // 철광석 ×3 → 초보자 세트 ×1 (약초 ×3, 철광석 ×1)
        s.Activate("5003");

        Assert.Equal("교환 완료: 철광석 ×3 → 초보자 세트 ×1", s.Message);
        Assert.Equal(3, Have(s, "3"));                       // 5 - 3 + 1
        Assert.Equal(3, Have(s, "2"));
        Assert.False(s.InventoryItems.ContainsKey("9001"));  // 번치 코드 자체는 인벤토리에 들어가지 않는다
    }

    [Fact]
    public void Paying_with_a_bunch_item_takes_its_parts()
    {
        var pack = Pack(bunch: Bunch, trades: TradeHeader + "5101\t2001\t9001\t2\t1\t50\n");
        var s = GameSession.NewGame(pack);
        s.Activate("102");
        s.Activate("2001");
        s.InventoryItems["2"] = new InventoryItem(6);        // 필요: 약초 6, 철광석 2
        s.InventoryItems["3"] = new InventoryItem(1);

        s.Activate("5101");
        Assert.Contains("부족: 철광석 (보유 1 / 필요 2)", s.Message);
        Assert.Equal(6, Have(s, "2"));

        s.InventoryItems["3"] = new InventoryItem(2);
        s.Activate("5101");
        Assert.False(s.InventoryItems.ContainsKey("2"));
        Assert.False(s.InventoryItems.ContainsKey("3"));
        Assert.Equal(50, Have(s, "1"));
    }

    [Fact]
    public void Cost_is_checked_before_receiving_when_the_same_item_is_on_both_sides()
    {
        var pack = Pack(trades: TradeHeader + "5201\t2001\t1\t100\t1\t30\n");   // 금화 100 → 금화 30
        var s = GameSession.NewGame(pack);
        s.Activate("102");
        s.Activate("2001");

        s.InventoryItems["1"] = new InventoryItem(99);
        s.Activate("5201");
        Assert.Contains("교환 실패", s.Message);
        Assert.Equal(99, Have(s, "1"));

        s.InventoryItems["1"] = new InventoryItem(100);
        s.Activate("5201");
        Assert.Equal(30, Have(s, "1"));
    }

    [Fact]
    public void Trade_that_would_exceed_the_carry_limit_is_refused()
    {
        var s = InShop("2001", ("1", 100), ("2", 95));       // 약초 소지제한 99, +5 → 100
        s.Activate("5001");

        Assert.Contains("소지 한도 초과: 약초 (한도 99)", s.Message);
        Assert.Equal(100, Have(s, "1"));                     // 아무것도 바뀌지 않는다
        Assert.Equal(95, Have(s, "2"));
    }

    [Fact]
    public void Trade_may_reach_exactly_the_carry_limit()
    {
        var s = InShop("2001", ("1", 100), ("2", 94));       // 94 + 5 = 99
        s.Activate("5001");

        Assert.StartsWith("교환 완료", s.Message);
        Assert.Equal(99, Have(s, "2"));
    }

    [Fact]
    public void Carry_limit_applies_to_the_parts_of_a_received_bunch_item()
    {
        // 초보자 세트(약초 ×3, 철광석 ×1)를 받는데 약초가 97개면 100개가 되어 거절된다.
        var s = InShop("2002", ("3", 5), ("2", 97));
        s.Activate("5003");

        Assert.Contains("소지 한도 초과: 약초", s.Message);
        Assert.Equal(5, Have(s, "3"));
        Assert.Equal(97, Have(s, "2"));
    }

    [Fact]
    public void An_item_that_only_shrinks_is_never_blocked_by_the_limit()
    {
        // 이미 한도를 넘게 들고 있어도(예: 마스터를 나중에 낮춘 경우) 줄이는 교환은 막지 않는다.
        var s = InShop("2001", ("1", 100), ("2", 150));
        s.Activate("5002");                                  // 5002는 튜토리얼 필요 → 코드로는 실행되지 않음
        Assert.Null(s.Message);

        s.PlayerFlags["tutorial_clear"] = 1;
        s.Activate("5002");                                  // 약초 ×10 → 금화 ×30: 약초는 줄기만 한다
        Assert.Equal(140, Have(s, "2"));
        Assert.Equal(130, Have(s, "1"));
    }

    [Fact]
    public void Message_is_cleared_by_the_next_activation()
    {
        var s = InShop("2001", ("1", 100));
        s.Activate("5001");
        Assert.NotNull(s.Message);
        s.Activate("999");
        Assert.Null(s.Message);
        Assert.Null(s.Activate("nonsense"));
        Assert.Null(s.Message);
    }

    [Fact]
    public void Inventory_after_trades_survives_save_and_load()
    {
        var s = InShop("2001", ("1", 150));
        s.Activate("5001");
        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal(50, Have(loaded, "1"));
        Assert.Equal(5, Have(loaded, "2"));
    }

    [Fact]
    public void Expand_resolves_bunch_items_into_parts()
    {
        var pack = Pack(bunch: Bunch);
        Assert.Equal(new Dictionary<string, long> { ["2"] = 6, ["3"] = 2 }, pack.Expand("9001", 2));
        Assert.Equal(new Dictionary<string, long> { ["1"] = 5 }, pack.Expand("1", 5));
    }

    [Fact]
    public void Item_name_falls_back_to_code_when_unknown()
        => Assert.Equal("77", Pack().ItemName("77"));

    // ---- shop_trade_condition_master ----

    [Fact]
    public void Trade_without_conditions_is_always_available()
    {
        var pack = Pack(trades: TradeHeader + "5001\t2001\t1\t1\t2\t1\n");
        Assert.True(pack.Trades![0].IsAvailable(F()));
    }

    [Fact]
    public void Trade_conditions_use_the_same_group_rules()
    {
        var pack = Pack(
            trades: TradeHeader + "5001\t2001\t1\t1\t2\t1\n5002\t2001\t1\t1\t2\t1\n",
            tradeConditions: CondHeader +
                "5001\t1\ttutorial_clear\tGreaterOrEqual\t1\n" +
                "5001\t1\tlevel\tGreaterOrEqual\t3\n" +
                "5001\t2\tvip\tEqual\t1\n" +
                "5002\t1\t\tNone\t\n");
        var t1 = pack.Trades![0];
        Assert.False(t1.IsAvailable(F(("tutorial_clear", 1))));                 // 그룹1 AND 미충족
        Assert.True(t1.IsAvailable(F(("tutorial_clear", 1), ("level", 3))));
        Assert.True(t1.IsAvailable(F(("vip", 1))));                             // 그룹2 OR
        Assert.True(pack.Trades[1].IsAvailable(F()));                           // None
    }

    [Theory]
    [InlineData("9999\t1\tf\tEqual\t1\n")]     // 모르는 트레이드 코드
    [InlineData("5001\t1\tf\tBogus\t1\n")]     // 모르는 컨디션 타입
    [InlineData("5001\tx\tf\tEqual\t1\n")]     // 그룹넘버가 정수 아님
    public void Bad_trade_conditions_are_rejected(string row)
        => Assert.Throws<PackFormatException>(() =>
            Pack(trades: TradeHeader + "5001\t2001\t1\t1\t2\t1\n", tradeConditions: CondHeader + row));

    [Fact]
    public void Trade_condition_header_must_be_trade_code()
        => Assert.Throws<PackFormatException>(() => Pack(
            trades: TradeHeader + "5001\t2001\t1\t1\t2\t1\n",
            tradeConditions: "shop_code\tgroup_no\tflag_code\tcondition_type\tcondition_value\n"));

    [Fact]
    public void Trade_conditions_without_trade_master_are_rejected()
        => Assert.Throws<PackFormatException>(() => Pack(tradeConditions: CondHeader));
}
