using Wera.Core;

namespace Wera.Core.Tests;

// 던전 진행: [101] 던전 → 던전 선택 → 파티 선택 → 던전 이동 화면(위·아래·왼쪽·오른쪽 한 칸씩), 닿은 칸의 타일 효과 실행.
// 예시 팩의 던전 1999 맵 0(도입 지점 x 1, y 1):
//   y0 ############
//   y1 #S..#...#.E#
//   y2 #.#.#.#.#.##
//   y3 #.#...#.B..#
//   y4 #.#####.##.#
//   y5 #>.R..A....#
//   y6 ############
// 맵 1: y0 #####, y1 #<..#, y2 #####. '>'(타일 8)는 맵 1의 (x 2, y 1)로, '<'(타일 9)는 맵 0의 (x 4, y 5)로 옮겨 간다.
public class DungeonFlowTests
{
    static GamePack SamplePackWith(params (string Path, string Text)[] extra)
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        var source = new MemoryStream();
        using (var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra")))
        using (var from = new System.IO.Compression.ZipArchive(read))
        using (var to = new System.IO.Compression.ZipArchive(source, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var e in from.Entries.Where(e => extra.All(x => x.Path != e.FullName)))
            {
                using var input = e.Open();
                using var output = to.CreateEntry(e.FullName).Open();
                input.CopyTo(output);
            }
            foreach (var (path, text) in extra)
            {
                using var w = new StreamWriter(to.CreateEntry(path).Open());
                w.Write(text);
            }
        }
        source.Position = 0;
        return GamePack.Load(source);
    }

    /// <summary>파티-A에 당신(리더)을 놓고 던전 1999에 들어가 도입 지점에 선다.</summary>
    static GameSession Entered(GamePack? pack = null)
    {
        var s = GameSession.NewGame(pack ?? SamplePackWith());
        s.NextRandom = _ => 0;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        s.Activate("101");
        s.Activate("1999");
        s.Activate("11");
        return s;
    }

    /// <summary>U 위, D 아래, L 왼쪽, R 오른쪽(코드 1~4)으로 차례로 한 칸씩 간다.</summary>
    static void Walk(GameSession s, string path)
    {
        foreach (var c in path) s.Activate("UDLR".IndexOf(c) + 1 + "");
    }

    static (long Layer, long X, long Y) At(GameSession s) => (s.DungeonLayer, s.DungeonX, s.DungeonY);

    [Fact]
    public void The_dungeon_entry_flows_through_dungeon_select_party_select_and_the_entry_point()
    {
        var s = GameSession.NewGame(SamplePackWith());
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        Assert.Equal(Screen.DungeonSelect, s.Activate("101")!.Target);
        var dungeons = s.Entries.Where(e => e.Action == EntryAction.SelectDungeon).ToList();
        Assert.Equal(["1999"], dungeons.Select(e => e.Code));                                 // 입장 조건을 만족하는 던전(1001, 1002는 플래그가 필요)
        Assert.True(dungeons[0].Enabled);
        s.Activate("1999");
        Assert.Equal(Screen.DungeonPartySelect, s.Screen);
        var parties = s.Entries.Where(e => e.Action == EntryAction.EnterDungeon).ToList();
        Assert.Equal(16, parties.Count);
        Assert.Equal("11", parties[0].Code);
        Assert.True(parties[0].Enabled);
        Assert.All(parties.Skip(1), e => Assert.False(e.Enabled));                            // 리더가 있는 파티-A만
        s.Activate("12");
        Assert.Equal(Screen.DungeonPartySelect, s.Screen);                                    // 회색 파티는 눌러도 반응이 없다
        s.Activate("11");
        Assert.Equal(Screen.DungeonMove, s.Screen);
        Assert.Equal(1999, s.CurrentDungeonCode);
        Assert.Equal((0L, 1L, 1L), At(s));
        Assert.Equal("테스트 던전 (맵 0)", s.DungeonTitle);
    }

    [Fact]
    public void Steps_are_one_cell_at_a_time_and_blocked_by_impassable_cells()
    {
        var s = Entered();
        Assert.Equal([false, true, false, true], s.Entries.Where(e => e.Action == EntryAction.DungeonStep).Select(e => e.Enabled));   // 위(벽)·왼쪽(벽)은 갈 수 없다
        s.Activate("1");
        Assert.Equal((0L, 1L, 1L), At(s));                                                    // 갈 수 없으면 아무 일도 없다
        Walk(s, "R");
        Assert.Equal((0L, 2L, 1L), At(s));
        Walk(s, "D");
        Assert.Equal((0L, 2L, 1L), At(s));                                                    // 아래 (2,2)는 벽
        Walk(s, "LD");
        Assert.Equal((0L, 1L, 2L), At(s));
        Assert.Equal(Screen.DungeonMove, s.Screen);
    }

    [Fact]
    public void A_wall_without_impassable_can_be_walked_through_and_impassable_without_wall_blocks()
    {
        const string header = "code\ttile_effect\tvalue_1\tvalue_2\tvalue_3\n";
        // 벽(#, 타일 2)은 보이기만 하는 Wall뿐이라 지나갈 수 있고, 바닥(., 타일 1)에 이동불가를 겹치면 못 간다.
        var s = Entered(SamplePackWith((GamePack.TileMasterPath, header
            + "1\tImpassable\t0\t0\t0\n2\tWall\t0\t0\t0\n3\tNone\t0\t0\t0\n4\tExit\t0\t0\t0\n5\tNone\t0\t0\t0\n"
            + "6\tNone\t0\t0\t0\n7\tNone\t0\t0\t0\n8\tNone\t0\t0\t0\n9\tNone\t0\t0\t0\n")));
        Walk(s, "U");
        Assert.Equal((0L, 1L, 0L), At(s));                                                    // 지나갈 수 있는 벽 위에 섰다
        Walk(s, "R");
        Assert.Equal((0L, 2L, 0L), At(s));
        Walk(s, "D");
        Assert.Equal((0L, 2L, 0L), At(s));                                                    // 아래 (2,1)은 Wall 없이 이동불가만 있는 바닥
        Assert.Equal(DungeonCellLook.Impassable, s.DungeonMapRows[1][2].Look);
    }

    [Fact]
    public void Stairs_warp_within_the_dungeon_and_the_destination_effects_do_not_run()
    {
        var s = Entered();
        Walk(s, "DDDD");
        Assert.Equal((1L, 2L, 1L), At(s));                                                    // '>' → 맵 1의 (2,1)(도착 칸의 효과는 실행하지 않는다)
        Assert.Equal("테스트 던전 (맵 1)", s.DungeonTitle);
        Assert.True(s.DungeonMapRows[1][2].IsPlayer);
        Assert.True(s.DungeonMapRows[1][1].Look.HasFlag(DungeonCellLook.Stairs));
        Walk(s, "L");
        Assert.Equal((0L, 4L, 5L), At(s));                                                    // '<' → 맵 0의 (4,5)
        Walk(s, "R");
        Assert.Equal((0L, 5L, 5L), At(s));
    }

    [Fact]
    public void The_map_rows_mark_the_player_and_the_looks_of_walls_and_stairs()
    {
        var s = Entered();
        var rows = s.DungeonMapRows;
        Assert.Equal(7, rows.Count);
        Assert.Equal(12, rows[0].Count);                                                      // 오른쪽 끝의 빈 열은 잘린다
        Assert.True(rows[0][0].Look.HasFlag(DungeonCellLook.Wall) && rows[0][0].Look.HasFlag(DungeonCellLook.Impassable));
        Assert.True(rows[1][1].IsPlayer);
        Assert.Equal("S", rows[1][1].Text);
        Assert.True(rows[5][1].Look.HasFlag(DungeonCellLook.Stairs) && rows[5][1].Look.HasFlag(DungeonCellLook.Warp));
        Assert.True(rows[3][8].Look.HasFlag(DungeonCellLook.Battle));
        Assert.True(rows[1][10].Look.HasFlag(DungeonCellLook.Exit));
        Assert.Equal(DungeonCellLook.None, rows[1][2].Look);
    }

    [Fact]
    public void A_fixed_battle_tile_starts_the_battle_with_the_party_and_returns_to_the_dungeon()
    {
        var s = Entered(SamplePackWith(("master/battle_master_v1.tsv", "code\tname\tcan_run\n5001\t테스트 전투\ttrue\n")));
        Walk(s, "RRDDRRUURRDDR");                                                            // (8,3) 고정 전투
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Equal(5001, s.CurrentBattleCode);
        Assert.Equal((0L, 8L, 3L), At(s));
        s.Activate("106");                                                                    // 도망
        s.Activate("999");
        Assert.Equal(Screen.DungeonMove, s.Screen);
        Assert.Equal((0L, 8L, 3L), At(s));
    }

    /// <summary>고정 전투(8,3)로 가서 우리 파티(리더 체력 1)가 지는 전투를 치른다.</summary>
    static void LoseTheFixedBattle(GameSession s, long hero)
    {
        Walk(s, "RRDDRRUURRDDR");
        Assert.Equal(Screen.Battle, s.Screen);
        s.NextRandom = _ => 0;
        foreach (var enemy in s.EnemyInstances.Values)
        {
            enemy.Statuses[GamePack.HpStatusCode] = 100;
            enemy.Statuses[GamePack.MaxHpStatusCode] = 100;
        }
        s.Characters[hero].Statuses[GamePack.HpStatusCode] = 1;
        s.Activate("102"); s.Activate("101"); s.Activate((s.EnemyInstances.Keys.First() + 1).ToString());
        s.Activate("107");
        Assert.Equal(GameSession.BattleOutcomeKind.Lose, s.BattleOutcome);
    }

    [Fact]
    public void Losing_a_dungeon_battle_with_the_player_in_the_party_is_a_game_over()
    {
        var s = Entered(SamplePackWith(("master/battle_master_v1.tsv", "code\tname\tcan_run\n5001\t테스트 전투\ttrue\n")));
        s.NextRandom = _ => 1;
        LoseTheFixedBattle(s, 1);
        s.Activate("999");
        Assert.Equal(Screen.GameOver, s.Screen);
        Assert.Null(s.CurrentDungeonCode);
        Assert.Equal(Screen.Load, s.Activate("999")!.Target);
        Assert.Equal(Screen.Load, s.Screen);
        Assert.Empty(s.Entries);                                                              // 게임 오버 뒤의 로드 화면에는 돌아가기가 없다
        Assert.Null(s.Activate("999"));
        Assert.Equal(Screen.Load, s.Screen);
    }

    [Fact]
    public void Losing_a_dungeon_battle_without_the_player_in_the_party_returns_home()
    {
        var s = GameSession.NewGame(SamplePackWith(("master/battle_master_v1.tsv", "code\tname\tcan_run\n5001\t테스트 전투\ttrue\n")));
        s.NextRandom = _ => 1;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        s.AddCharacter(7001);
        s.Parties[1].Units[0] = s.Parties[0].Units[0]! with { CharacterCode = 7001 };       // 파티-B: 리더 용사 혼자
        s.Activate("101"); s.Activate("1999"); s.Activate("12");
        Assert.Equal(Screen.DungeonMove, s.Screen);
        LoseTheFixedBattle(s, 7001);
        s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Null(s.CurrentDungeonCode);
    }

    [Fact]
    public void A_random_battle_tile_fights_only_when_the_roll_is_below_the_chance()
    {
        var s = Entered();
        Walk(s, "DDDDL");                                                                     // 맵 1을 거쳐 (4,5)에서 왼쪽의 R(3,5)로
        s.NextRandom = _ => 99;                                                               // 30/100: 99는 빗나간다
        Walk(s, "L");
        Assert.Equal(Screen.DungeonMove, s.Screen);
        Assert.Equal((0L, 3L, 5L), At(s));
        Walk(s, "R");
        s.NextRandom = _ => 0;
        Walk(s, "L");
        Assert.Equal(Screen.Battle, s.Screen);
    }

    [Fact]
    public void An_adv_tile_plays_the_adv_and_returns_to_the_dungeon()
    {
        var s = Entered(SamplePackWith(("master/tile_master_v1.tsv", File.ReadAllText(TileMaster()).Replace("7\tAdv\t4001", "7\tAdv\t4002"))));
        Walk(s, "DDDDL");                                                                     // (4,5)
        s.NextRandom = _ => 99;                                                               // R(3,5)를 피해 오른쪽으로만 간다
        Walk(s, "RR");                                                                        // (6,5) 'A'
        Assert.Equal(Screen.EventPlay, s.Screen);
        Assert.Equal("전투에서 승리했습니다.", s.CurrentAdvStep!.Value.Text);
        Assert.Equal("ADV: 승리 ADV", s.CurrentStepText);
        s.Activate("1");
        Assert.Equal(Screen.DungeonMove, s.Screen);
        Assert.Equal((0L, 6L, 5L), At(s));
        Assert.Null(s.CurrentAdv);
    }

    [Fact]
    public void An_exit_tile_and_the_leave_command_return_to_the_home_screen()
    {
        var s = Entered(SamplePackWith(("master/tile_master_v1.tsv", File.ReadAllText(TileMaster()).Replace("1\tNone\t0\t0\t0", "1\tExit\t0\t0\t0"))));
        Walk(s, "R");                                                                         // 바닥(타일 1)을 출구로 바꿨다
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Null(s.CurrentDungeonCode);

        var t = Entered();
        Assert.Equal(Screen.Home, t.Activate("999")!.Target);                                 // 나가기
        Assert.Equal(Screen.Home, t.Screen);
        Assert.Null(t.CurrentDungeonCode);
    }

    static string TileMaster()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        return Path.Combine(root, "samples", "sample-pack", "master", "tile_master_v1.tsv");
    }
}
