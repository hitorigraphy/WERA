using Wera.Core;

namespace Wera.Core.Tests;

public class ShopTests
{
    const string Header = "shop_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n";
    const string TwoShops = "code\tname\n2001\t잡화점\n2002\t무기점\n";
    const string TutorialOnly = Header + "2001\t1\t1001\tGreaterOrEqual\t1\n2002\t1\t1001\tGreaterOrEqual\t1\n2002\t1\t1002\tGreaterOrEqual\t3\n";

    static GamePack Pack(string? shops, string? conditions, string? dungeons = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
        };
        if (shops is not null) files.Add(("master/shop_master_v1.tsv", shops));
        if (conditions is not null) files.Add(("master/shop_condition_master_v1.tsv", conditions));
        if (dungeons is not null) files.Add(("master/dungeon_master_v1.tsv", dungeons));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    static Dictionary<long, long> F(params (long, long)[] kv) => kv.ToDictionary(x => x.Item1, x => x.Item2);

    [Fact]
    public void Shop_masters_are_optional_and_independent_of_dungeons()
    {
        Assert.Null(Pack(null, null).Shops);
        var pack = Pack(TwoShops, null);
        Assert.Equal([2001L, 2002L], pack.Shops!.Select(s => s.Code));
        Assert.Null(pack.Dungeons);
    }

    [Fact]
    public void Shop_without_conditions_is_always_open()
        => Assert.True(Pack(TwoShops, null).Shops![0].IsAvailable(F()));

    [Fact]
    public void Same_group_is_and_different_groups_are_or_and_none_is_always_met()
    {
        var pack = Pack(TwoShops, TutorialOnly + "2002\t2\t1003\tEqual\t1\n");
        var weapon = pack.Shops![1];
        Assert.False(weapon.IsAvailable(F((1001, 1))));                // 그룹1은 AND라 level(1002) 부족
        Assert.True(weapon.IsAvailable(F((1001, 1), (1002, 3))));
        Assert.True(weapon.IsAvailable(F((1003, 1))));                            // 그룹2

        var none = Pack(TwoShops, Header + "2001\t1\t\tNone\t\n");
        Assert.True(none.Shops![0].IsAvailable(F()));
    }

    [Theory]
    [InlineData("2001\t1\t9\tBogus\t1\n")]     // 모르는 컨디션 타입
    [InlineData("2001\t1\t9\t1\t1\n")]         // 숫자는 enum 이름이 아님
    [InlineData("9999\t1\t9\tEqual\t1\n")]     // 모르는 상점 코드
    [InlineData("2001\tx\t9\tEqual\t1\n")]     // 그룹넘버가 정수 아님
    [InlineData("2001\t1\t9\tEqual\tabc\n")]   // 밸류가 정수 아님
    [InlineData("2001\t1\t\tEqual\t1\n")]      // 플래그 코드 비어 있음
    public void Bad_condition_rows_are_rejected(string row)
        => Assert.Throws<PackFormatException>(() => Pack(TwoShops, Header + row));

    [Fact]
    public void Bad_shop_masters_are_rejected()
    {
        Assert.Throws<PackFormatException>(() => Pack("code\tname\n1\ta\n1\tb\n", null));   // 중복 코드
        Assert.Throws<PackFormatException>(() => Pack(null, Header));                      // 상점 마스터 없이 조건표만
        // 상점 조건표 헤더는 shop_code여야 한다 (던전 조건표 헤더와 혼동 금지).
        Assert.Throws<PackFormatException>(() =>
            Pack(TwoShops, "dungeon_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n"));
    }

    [Fact]
    public void Shop_button_102_is_disabled_until_a_shop_is_open()
    {
        var s = GameSession.NewGame(Pack(
            "code\tname\n2001\t잡화점\n",
            Header + "2001\t1\t1001\tGreaterOrEqual\t1\n"));
        Assert.False(s.Entries.Single(e => e.Code == "102").Enabled);
        Assert.NotNull(s.Activate("102"));
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Null(s.Message);

        s.PlayerFlags[1001] = 1;
        Assert.True(s.Entries.Single(e => e.Code == "102").Enabled);
        Assert.Contains(new ScreenEntry("102", "상점", Screen.ShopList), s.Entries);
        Assert.NotNull(s.Activate("102"));
        Assert.Equal(Screen.ShopList, s.Screen);
    }

    [Fact]
    public void Home_order_is_101_102_103_200_300()
    {
        var s = GameSession.NewGame(Pack(TwoShops, null, dungeons: "code\tname\n1001\t초원\n"));
        Assert.Equal(["101", "102", "103", "104", "105", "110", "201", "202", "203", "200", "300"], s.Entries.Select(e => e.Code));
    }

    [Fact]
    public void Shop_list_screen_lists_only_open_shops_and_999_goes_back()
    {
        var s = GameSession.NewGame(Pack(TwoShops, TutorialOnly));
        s.PlayerFlags[1001] = 1;                       // 잡화점만 열림 (무기점은 level(1002) 필요)
        Assert.NotNull(s.Activate(" 102 "));
        Assert.Equal(Screen.ShopList, s.Screen);
        Assert.Equal([2001L], s.AvailableShops.Select(x => x.Code));
        Assert.Equal(["2001", "999"], s.Entries.Select(e => e.Code));   // 열린 상점 + 돌아가기
        Assert.Null(s.Activate("2002"));                     // 닫힌 상점은 코드를 입력해도 들어갈 수 없다
        Assert.Null(s.Activate("102"));                      // 상점가 화면에는 102 항목이 없다
        Assert.NotNull(s.Activate("999"));
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Save_and_load_always_enter_home_even_from_shop_list()
    {
        var pack = Pack(TwoShops, null);
        var s = GameSession.NewGame(pack);
        s.Activate("102");
        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Contains("\"ShopList\"", s.ToSaveJson());
        Assert.Equal(Screen.Home, loaded.Screen);
    }
}
