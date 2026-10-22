using Wera.Core;

namespace Wera.Core.Tests;

public class TradeTests
{
    const string Shops = "code\tname\n2001\t잡화점\n2002\t무기점\n";
    const string Common = "item_code\titem_name\n1\t금화\n2\t약초\n3\t철광석\n";
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
        if (common is not null) files.Add(("master/common_item_master_v1.tsv", common));
        if (bunch is not null) files.Add(("master/bunch_item_master_v1.tsv", bunch));
        if (trades is not null) files.Add(("master/shop_trade_master_v1.tsv", trades));
        if (tradeConditions is not null) files.Add(("master/shop_trade_condition_master_v1.tsv", tradeConditions));
        return GamePack.Load(CoreTests.Zip(files.ToArray()));
    }

    static Dictionary<string, int> F(params (string, int)[] kv) => kv.ToDictionary(x => x.Item1, x => x.Item2);

    [Fact]
    public void All_item_and_trade_masters_are_optional()
    {
        var pack = Pack(shops: null, common: null);
        Assert.Null(pack.CommonItems);
        Assert.Null(pack.BunchItems);
        Assert.Null(pack.Trades);
    }

    // ---- common_item_master ----

    [Fact]
    public void Common_items_load()
    {
        var items = Pack().CommonItems!;
        Assert.Equal([new CommonItem("1", "금화"), new CommonItem("2", "약초"), new CommonItem("3", "철광석")], items);
    }

    [Theory]
    [InlineData("item_code\titem_name\n1\ta\n1\tb\n")]   // 중복 코드
    [InlineData("item_code\titem_name\n\ta\n")]          // 빈 코드
    [InlineData("code\tname\n1\ta\n")]                   // 헤더 불일치
    public void Bad_common_items_are_rejected(string common)
        => Assert.Throws<PackFormatException>(() => Pack(common: common));

    // ---- bunch_item_master ----

    [Fact]
    public void Bunch_rows_with_same_code_form_one_item_with_parts_in_order()
    {
        var bunch = Assert.Single(Pack(bunch: Bunch).BunchItems!);
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
