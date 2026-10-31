using Wera.Core;

namespace Wera.Core.Tests;

public class DungeonTests
{
    const string Header = "dungeon_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n";

    static GamePack Pack(string? dungeons, string? flags, bool backslashPaths = false)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
        };
        var sep = backslashPaths ? '\\' : '/';
        if (dungeons is not null) files.Add(($"master{sep}dungeon_master_v1.tsv", dungeons));
        if (flags is not null) files.Add(($"master{sep}dungeon_flag_master_v1.tsv", flags));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    const string TwoDungeons = "code\tname\n1001\t초원\n1002\t동굴\n";

    static Dictionary<long, long> F(params (long, long)[] kv) => kv.ToDictionary(x => x.Item1, x => x.Item2);

    [Fact]
    public void Masters_are_optional()
    {
        Assert.Null(Pack(null, null).Dungeons);
        Assert.Equal([1001L, 1002L], Pack(TwoDungeons, null).Dungeons!.Select(d => d.Code));
    }

    [Fact]
    public void Dungeon_without_conditions_is_always_available()
    {
        var d = Pack(TwoDungeons, null).Dungeons![0];
        Assert.True(d.IsAvailable(F()));
    }

    [Fact]
    public void Same_group_is_and_different_groups_are_or()
    {
        var pack = Pack(TwoDungeons,
            Header +
            "1001\t1\t1001\tGreaterOrEqual\t1\n" +
            "1001\t1\t1002\tGreaterOrEqual\t5\n" +
            "1001\t2\t1003\tEqual\t1\n");
        var d = pack.Dungeons![0];

        Assert.False(d.IsAvailable(F()));
        Assert.False(d.IsAvailable(F((1001, 1))));              // 그룹1은 AND라 level 부족
        Assert.True(d.IsAvailable(F((1001, 1), (1002, 5)))); // 그룹1 만족
        Assert.True(d.IsAvailable(F((1003, 1))));                    // 그룹2 만족
    }

    [Theory]
    [InlineData("Equal", 3, 3, true)]
    [InlineData("equal", 3, 4, false)]
    [InlineData("NotEqual", 3, 4, true)]
    [InlineData("GreaterThan", 3, 3, false)]
    [InlineData("GreaterThan", 3, 4, true)]
    [InlineData("GreaterOrEqual", 3, 3, true)]
    [InlineData("LessThan", 3, 2, true)]
    [InlineData("LessThan", 3, 3, false)]
    [InlineData("LessOrEqual", 3, 3, true)]
    public void Condition_types(string type, int value, int actual, bool expected)
    {
        var pack = Pack(TwoDungeons, Header + $"1001\t1\t9\t{type}\t{value}\n");
        Assert.Equal(expected, pack.Dungeons![0].IsAvailable(F((9, actual))));
    }

    [Fact]
    public void None_is_always_met_and_may_leave_flag_and_value_empty()
    {
        var pack = Pack(TwoDungeons, Header + "1001\t1\t\tNone\t\n");
        Assert.True(pack.Dungeons![0].IsAvailable(F()));
        Assert.Equal(ConditionType.None, pack.Dungeons[0].Groups[0][0].Type);
        Assert.Equal(0, (int)ConditionType.None);
    }

    [Fact]
    public void None_in_one_group_makes_the_dungeon_available_regardless_of_other_groups()
    {
        var pack = Pack(TwoDungeons, Header + "1001\t1\t1003\tEqual\t1\n1001\t2\t\tNone\t\n");
        Assert.True(pack.Dungeons![0].IsAvailable(F()));
    }
    [Fact]
    public void Missing_flag_counts_as_zero()
    {
        var pack = Pack(TwoDungeons, Header + "1001\t1\t9\tEqual\t0\n");
        Assert.True(pack.Dungeons![0].IsAvailable(F()));
    }

    [Fact]
    public void Backslash_entry_paths_are_found()
        => Assert.NotNull(Pack(TwoDungeons, null, backslashPaths: true).Dungeons);

    [Theory]
    [InlineData("1001\t1\t9\tBogus\t1\n")]      // 모르는 컨디션 타입
    [InlineData("1001\t1\t9\t1\t1\n")]          // 숫자는 enum 이름이 아님
    [InlineData("9999\t1\t9\tEqual\t1\n")]      // 모르는 던전 코드
    [InlineData("abc\t1\t9\tEqual\t1\n")]       // 던전 코드가 정수 아님
    [InlineData("1001\tx\t9\tEqual\t1\n")]      // 그룹넘버가 정수 아님
    [InlineData("1001\t1.5\t9\tEqual\t1\n")]    // 그룹넘버는 int: 소수 불가
    [InlineData("1001\t1\t9\tEqual\tabc\n")]    // 밸류가 정수 아님
    [InlineData("1001\t1\t\tEqual\t1\n")]       // 플래그 코드 비어 있음
    [InlineData("1001\t1\tabc\tEqual\t1\n")]    // 플래그 코드가 정수 아님
    public void Bad_flag_rows_are_rejected(string row)
        => Assert.Throws<PackFormatException>(() => Pack(TwoDungeons, Header + row));

    [Fact]
    public void Bad_masters_are_rejected()
    {
        Assert.Throws<PackFormatException>(() => Pack("code\tname\n1\ta\n1\tb\n", null)); // 중복 코드
        Assert.Throws<PackFormatException>(() => Pack("wrong\theader\n", null));
        Assert.Throws<PackFormatException>(() => Pack(null, Header));                    // 던전 마스터 없이 플래그 마스터만
    }

    const string TutorialFlags = Header + "1001\t1\t1001\tGreaterOrEqual\t1\n";

    [Fact]
    public void Dungeon_button_disabled_until_a_dungeon_is_available()
    {
        var pack = Pack(TwoDungeons.Replace("1002\t동굴\n", ""), TutorialFlags);
        var s = GameSession.NewGame(pack);

        Assert.Equal(Screen.Home, s.Screen);
        Assert.False(s.Entries.Single(e => e.Code == "101").Enabled);
        var disabled = s.Activate("101");
        Assert.NotNull(disabled);
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Null(s.Message);

        s.PlayerFlags[1001] = 1;
        Assert.True(s.Entries.Single(e => e.Code == "101").Enabled);
        Assert.Contains(new ScreenEntry("101", "던전", Screen.DungeonSelect), s.Entries);
        Assert.NotNull(s.Activate("101"));
        Assert.Equal(Screen.DungeonSelect, s.Screen);
    }

    [Fact]
    public void One_available_dungeon_is_enough_to_show_the_button()
    {
        // 1001은 튜토리얼 필요, 1002는 조건 없음 → 처음부터 버튼이 보인다.
        var s = GameSession.NewGame(Pack(TwoDungeons, TutorialFlags));
        Assert.Contains(s.Entries, e => e.Code == "101");
        Assert.Equal([1002L], s.AvailableDungeons.Select(d => d.Code));
    }

    [Fact]
    public void Activating_101_opens_dungeon_select_and_999_goes_back()
    {
        var s = GameSession.NewGame(Pack(TwoDungeons, null));
        Assert.NotNull(s.Activate(" 101 "));
        Assert.Equal(Screen.DungeonSelect, s.Screen);
        Assert.Null(s.Activate("101"));   // 던전 선택 화면에는 101 항목이 없다
        Assert.NotNull(s.Activate("999"));
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void New_game_goes_straight_to_home_even_without_master()
    {
        var s = GameSession.NewGame(Pack(null, null));
        Assert.Equal(Screen.Home, s.Screen);
        Assert.False(s.Entries.Single(e => e.Code == "101").Enabled);
    }

    [Fact]
    public void Home_menu_is_in_code_order_with_the_always_present_entries()
    {
        // 던전이 없어도 이벤트(103), 교류(104), 파티 설정(105), 동료/장비/아이템 확인(201~203), 세이브(200), 로드(300), 디버그(400)는 항상 있고, 던전/상점(101, 102)도 항상 있고 열려 있지 않으면 비활성.
        Assert.Equal(["101", "102", "103", "104", "105", "201", "202", "203", "200", "300", "400"], GameSession.NewGame(Pack(null, null)).Entries.Select(e => e.Code));
        var s = GameSession.NewGame(Pack(TwoDungeons, null));
        Assert.Equal(["101", "102", "103", "104", "105", "201", "202", "203", "200", "300", "400"], s.Entries.Select(e => e.Code));
        Assert.Equal(["던전", "상점", "이벤트", "교류", "파티 설정", "동료 확인", "장비 확인", "아이템 확인", "세이브", "로드", "디버그"], s.Entries.Select(e => e.Label));
    }

    [Fact]
    public void Save_200_does_not_change_screen_and_reports_save_action()
    {
        var s = GameSession.NewGame(Pack(TwoDungeons, null));
        var entry = s.Activate("200");
        Assert.Equal(EntryAction.Save, entry!.Action);
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Load_300_opens_load_screen_and_999_goes_back()
    {
        var s = GameSession.NewGame(Pack(null, null));
        Assert.NotNull(s.Activate("300"));
        Assert.Equal(Screen.Load, s.Screen);
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));
        Assert.Null(s.Activate("200"));   // 로드 화면에는 세이브 항목이 없다
        Assert.NotNull(s.Activate("999"));
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Load_always_enters_home_and_keeps_flags()
    {
        var pack = Pack(TwoDungeons, TutorialFlags);
        var s = GameSession.NewGame(pack);
        s.PlayerFlags[1001] = 1;
        s.Activate("101");

        var json = s.ToSaveJson();
        Assert.Contains("\"DungeonSelect\"", json);

        var loaded = GameSession.FromSaveJson(pack, json);
        Assert.Equal(Screen.Home, loaded.Screen);   // 저장 당시 화면과 무관하게 홈
        Assert.Equal(1L, loaded.PlayerFlags[1001]);
    }

    [Fact]
    public void Minimal_save_without_player_state_loads_into_home()
    {
        var pack = Pack(null, null);
        var s = GameSession.FromSaveJson(pack, "{\"Version\":1,\"PackId\":\"demo\"}");
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Empty(s.PlayerFlags);
    }
}
