using Wera.Core;

namespace Wera.Core.Tests;

// 홈 메뉴의 101 던전 / 102 상점은 항상 있고, 열린 던전/상점이 없으면 비활성(Enabled == false)이다.
public class HomeMenuEnabledTests
{
    const string DungeonHeader = "dungeon_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n";
    const string ShopHeader = "shop_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n";

    static GamePack Pack(string? dungeons = null, string? dungeonFlags = null, string? shops = null, string? shopFlags = null)
    {
        var files = new List<(string, string)> { ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n") };
        if (dungeons is not null) files.Add(("master/dungeon_master_v1.tsv", dungeons));
        if (dungeonFlags is not null) files.Add(("master/dungeon_flag_master_v1.tsv", dungeonFlags));
        if (shops is not null) files.Add(("master/shop_master_v1.tsv", shops));
        if (shopFlags is not null) files.Add(("master/shop_condition_master_v1.tsv", shopFlags));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    static bool Enabled(GameSession s, string code) => s.Entries.Single(e => e.Code == code).Enabled;

    [Fact]
    public void Without_dungeon_and_shop_masters_only_101_and_102_are_disabled()
    {
        var s = GameSession.NewGame(Pack());
        Assert.False(Enabled(s, "101"));
        Assert.False(Enabled(s, "102"));
        foreach (var e in s.Entries.Where(e => e.Code is not ("101" or "102")))
            Assert.True(e.Enabled, e.Code);
        Assert.Equal(["101", "102", "103", "104", "105"], s.HomeMenu[0].Entries.Select(e => e.Code));
    }

    [Fact]
    public void An_available_dungeon_enables_101_only()
    {
        var s = GameSession.NewGame(Pack(dungeons: "code\tname\n1001\t초원\n"));
        Assert.True(Enabled(s, "101"));
        Assert.False(Enabled(s, "102"));
    }

    [Fact]
    public void An_open_shop_enables_102_only()
    {
        var s = GameSession.NewGame(Pack(shops: "code\tname\n2001\t잡화점\n"));
        Assert.False(Enabled(s, "101"));
        Assert.True(Enabled(s, "102"));
    }

    [Fact]
    public void Unmet_conditions_keep_them_disabled_until_met()
    {
        var s = GameSession.NewGame(Pack(
            "code\tname\n1001\t초원\n", DungeonHeader + "1001\t1\t1001\tGreaterOrEqual\t1\n",
            "code\tname\n2001\t잡화점\n", ShopHeader + "2001\t1\t1001\tGreaterOrEqual\t1\n"));
        Assert.False(Enabled(s, "101"));
        Assert.False(Enabled(s, "102"));
        Assert.Equal(Screen.Home, s.Screen);

        s.PlayerFlags[1001] = 1;
        Assert.True(Enabled(s, "101"));
        Assert.True(Enabled(s, "102"));
    }

    [Fact]
    public void Activating_a_disabled_entry_changes_nothing()
    {
        var s = GameSession.NewGame(Pack());
        foreach (var code in new[] { "101", "102" })
        {
            Assert.NotNull(s.Activate(code));
            Assert.Equal(Screen.Home, s.Screen);
            Assert.Null(s.Message);
        }
    }

    [Fact]
    public void The_sample_pack_has_both_enabled_on_a_new_game()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var s = GameSession.NewGame(GamePack.Load(zip));
        Assert.True(Enabled(s, "101"));
        Assert.True(Enabled(s, "102"));
        Assert.All(s.Entries, e => Assert.True(e.Enabled));
    }
}
