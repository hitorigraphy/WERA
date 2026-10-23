using Wera.Core;

namespace Wera.Core.Tests;

public class PlayerStateTests
{
    static GamePack Pack() => GamePack.Load(CoreTests.Zip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
        ("master/dungeon_master_v1.tsv", "code\tname\n1001\t초원\n"),
        ("master/dungeon_flag_master_v1.tsv",
            "dungeon_code\tgroup_no\tflag_code\tcondition_type\tcondition_value\n1001\t1\t1004\tGreaterOrEqual\t5000000000\n")));

    [Fact]
    public void New_game_starts_with_empty_player_flags_and_inventory()
    {
        var s = GameSession.NewGame(Pack());
        Assert.Empty(s.PlayerFlags);
        Assert.Empty(s.InventoryItems);
    }

    [Fact]
    public void Inventory_item_is_a_struct_keyed_by_item_code()
    {
        Assert.True(typeof(InventoryItem).IsValueType);
        var s = GameSession.NewGame(Pack());
        s.InventoryItems[1] = new InventoryItem(100);
        Assert.Equal(100, s.InventoryItems[1].Amount);
    }

    [Fact]
    public void Player_flags_are_long_values_and_conditions_compare_them()
    {
        var pack = Pack();
        var s = GameSession.NewGame(pack);
        Assert.Empty(s.AvailableDungeons);

        s.PlayerFlags[1004] = int.MaxValue;                // int 범위로는 부족
        Assert.Empty(s.AvailableDungeons);

        s.PlayerFlags[1004] = 5_000_000_000L;              // int를 넘는 long 값
        Assert.Single(s.AvailableDungeons);
        Assert.Equal(5_000_000_000L, pack.Dungeons![0].Groups[0][0].Value);
    }

    [Fact]
    public void Save_roundtrip_keeps_player_flags_and_inventory()
    {
        var pack = Pack();
        var s = GameSession.NewGame(pack);
        s.PlayerFlags[1004] = 9_000_000_000L;
        s.PlayerFlags[1005] = -3;
        s.InventoryItems[1] = new InventoryItem(100);
        s.InventoryItems[9001] = new InventoryItem(2);

        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(9_000_000_000L, loaded.PlayerFlags[1004]);
        Assert.Equal(-3L, loaded.PlayerFlags[1005]);
        Assert.Equal(new InventoryItem(100), loaded.InventoryItems[1]);
        Assert.Equal(new InventoryItem(2), loaded.InventoryItems[9001]);
    }

    [Fact]
    public void Save_json_uses_player_flags_and_inventory_names()
    {
        var s = GameSession.NewGame(Pack());
        s.PlayerFlags[1001] = 1;
        s.InventoryItems[1] = new InventoryItem(7);
        var json = s.ToSaveJson();

        Assert.Contains("\"Version\": 1", json);
        Assert.Contains("\"PlayerFlags\"", json);
        Assert.Contains("\"InventoryItems\"", json);
        Assert.Contains("\"Amount\": 7", json);
    }

    [Fact]
    public void Save_version_stays_1_until_told_otherwise()
    {
        Assert.Equal(1, GameSession.SaveVersion);
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(),
            "{\"Version\":2,\"PackId\":\"demo\"}"));
    }

    [Fact]
    public void Save_without_player_state_loads_as_empty()
    {
        var s = GameSession.FromSaveJson(Pack(), "{\"Version\":1,\"PackId\":\"demo\"}");
        Assert.Empty(s.PlayerFlags);
        Assert.Empty(s.InventoryItems);
    }

    [Fact]
    public void Negative_inventory_amount_in_save_is_rejected()
        => Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(),
            "{\"Version\":1,\"PackId\":\"demo\",\"InventoryItems\":{\"1\":{\"Amount\":-1}}}"));

    [Fact]
    public void Wrong_value_types_in_save_are_rejected_as_bad_json()
    {
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(),
            "{\"Version\":1,\"PackId\":\"demo\",\"PlayerFlags\":{\"1\":true}}"));
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(),
            "{\"Version\":1,\"PackId\":\"demo\",\"InventoryItems\":{\"1\":5}}"));
    }

    [Fact]
    public void Non_numeric_flag_or_item_keys_in_a_save_are_rejected()
    {
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(),
            "{\"Version\":1,\"PackId\":\"demo\",\"PlayerFlags\":{\"tutorial_clear\":1}}"));
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(),
            "{\"Version\":1,\"PackId\":\"demo\",\"InventoryItems\":{\"gold\":{\"Amount\":1}}}"));
    }

    [Fact]
    public void Codes_and_amounts_beyond_int_range_roundtrip()
    {
        var pack = Pack();
        var s = GameSession.NewGame(pack);
        s.PlayerFlags[5_000_000_000L] = long.MaxValue;
        s.InventoryItems[6_000_000_000L] = new InventoryItem(9_000_000_000_000L);

        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(long.MaxValue, loaded.PlayerFlags[5_000_000_000L]);
        Assert.Equal(9_000_000_000_000L, loaded.InventoryItems[6_000_000_000L].Amount);
    }

    [Theory]
    [InlineData(ConditionType.Equal, 3, 3, true)]
    [InlineData(ConditionType.GreaterThan, 3, 4, true)]
    [InlineData(ConditionType.LessThan, 3, 3, false)]
    [InlineData(ConditionType.NotEqual, 3, 3, false)]
    public void Condition_compares_a_long_value_directly(ConditionType type, long value, long actual, bool expected)
        => Assert.Equal(expected, new FlagCondition(9, type, value).IsMet(actual));
}
