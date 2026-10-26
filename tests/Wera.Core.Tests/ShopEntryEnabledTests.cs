using Wera.Core;

namespace Wera.Core.Tests;

// 상점 교환 목록의 ScreenEntry.Enabled / GameSession.CanTrade / TradeBlockedReason.
public class ShopEntryEnabledTests
{
    const string TradeHeader = "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\n";

    // 5001: 금화 100 → 약초 5, 5002: 약초 5 → 금화 100
    static GameSession Session()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
            ("master/shop_master_v1.tsv", "code\tname\n2001\t잡화점\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n11\t금화\t99999\n2\t약초\t99\n"),
            ("master/shop_trade_master_v1.tsv", TradeHeader + "5001\t2001\t11\t100\t2\t5\n5002\t2001\t2\t5\t11\t100\n")));
        var s = GameSession.NewGame(pack);
        s.Activate("102");
        s.Activate("2001");
        return s;
    }

    static ScreenEntry Entry(GameSession s, string code) => s.Entries.Single(e => e.Code == code);

    [Fact]
    public void Possible_trades_are_enabled_and_impossible_ones_are_not()
    {
        var s = Session();
        Assert.Equal(Screen.ShopTrades, s.Screen);
        Assert.False(Entry(s, "5001").Enabled);             // 금화가 없다
        Assert.False(Entry(s, "5002").Enabled);             // 약초가 없다

        s.AddInventory(11, 100);
        Assert.True(Entry(s, "5001").Enabled);
        Assert.False(Entry(s, "5002").Enabled);

        s.AddInventory(2, 5);
        Assert.True(Entry(s, "5001").Enabled);
        Assert.True(Entry(s, "5002").Enabled);
    }

    [Fact]
    public void Back_is_always_enabled()
    {
        var s = Session();
        Assert.True(Entry(s, "999").Enabled);
        s.AddInventory(11, 100);
        Assert.True(Entry(s, "999").Enabled);
    }

    [Fact]
    public void Activating_a_disabled_trade_returns_the_entry_but_changes_nothing()
    {
        var s = Session();
        s.AddInventory(2, 3);                               // 5002에는 약초 5개가 필요
        var before = s.ToSaveJson();

        var entry = s.Activate("5002");

        Assert.NotNull(entry);
        Assert.Equal("5002", entry!.Code);
        Assert.False(entry.Enabled);
        Assert.Null(s.Message);
        Assert.Equal(Screen.ShopTrades, s.Screen);
        Assert.Equal(before, s.ToSaveJson());
        Assert.Equal(3, s.InventoryItems[2].Amount);
        Assert.False(s.InventoryItems.ContainsKey(11));
        Assert.StartsWith("교환 실패 - 부족: 약초", s.TradeBlockedReason(5002));
    }

    [Fact]
    public void CanTrade_follows_the_inventory_and_blocked_reason_is_null_when_possible()
    {
        var s = Session();
        Assert.False(s.CanTrade(5001));
        Assert.StartsWith("교환 실패 - 부족: 금화", s.TradeBlockedReason(5001));

        s.AddInventory(11, 100);
        Assert.True(s.CanTrade(5001));
        Assert.Null(s.TradeBlockedReason(5001));
        Assert.Equal(100, s.InventoryItems[11].Amount);     // 확인만으로는 아무것도 바뀌지 않는다

        var entry = s.Activate("5001");                     // 실제로 교환해 금화를 다 쓴다
        Assert.True(entry!.Enabled);
        Assert.StartsWith("교환 완료", s.Message);
        Assert.False(s.CanTrade(5001));
        Assert.False(Entry(s, "5001").Enabled);
        Assert.NotNull(s.TradeBlockedReason(5001));
    }
}
