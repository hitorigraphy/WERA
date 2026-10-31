using Wera.Core;

namespace Wera.Core.Tests;

// 던전 진행: [101] 던전 → 던전 선택 → 파티 선택(출격할 수 있는 파티만 나옴) → 던전 이동 화면(W A S D 키로 한 칸씩, Enter는 지금 칸의 상호작용), 닿은 칸의 타일 효과 실행.
// 한 걸음마다 던전 마스터의 인카운트률(%)로 던전 인카운트 마스터의 전투를 만날 수 있다.
// 예시 팩의 던전 1999 맵 0(도입 지점 x 1, y 1, 인카운트률 10%, 인카운트 전투 5001):
//   y0 ############
//   y1 #S..#...#.E#
//   y2 #.#.#.#.#.##
//   y3 #.#...#.B..#
//   y4 #.#####.##.#
//   y5 #>....A....#
//   y6 ############
// 맵 1(인카운트 없음): y0 #####, y1 #<..#, y2 #####. '>'(타일 8)는 맵 1의 (x 2, y 1)로, '<'(타일 9)는 맵 0의 (x 4, y 5)로 옮겨 간다.
public class DungeonFlowTests
{
    const string RunnableBattle = "code\tname\tcan_run\n5001\t테스트 전투\ttrue\n";

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

    /// <summary>파티-A에 당신(리더)을 놓고 던전 1999에 들어가 도입 지점에 선다. 난수는 99라 인카운트하지 않는다(인카운트률 10%).</summary>
    static GameSession Entered(GamePack? pack = null)
    {
        var s = GameSession.NewGame(pack ?? SamplePackWith());
        s.NextRandom = _ => 99;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        s.Activate("101");
        s.Activate("1999");
        s.Activate("11");
        return s;
    }

    /// <summary>U 위, D 아래, L 왼쪽, R 오른쪽으로 차례로 한 칸씩 간다(W S A D 키).</summary>
    static void Walk(GameSession s, string path)
    {
        foreach (var c in path) s.PressDungeonKey("wsad"["UDLR".IndexOf(c)]);
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
        Assert.Equal(["11"], parties.Select(e => e.Code));                                    // 리더가 있는 파티-A만 나온다(출격할 수 없는 파티는 표시하지 않는다)
        Assert.True(parties[0].Enabled);
        Assert.Equal(["11", "999"], s.Entries.Select(e => e.Code));
        Assert.Null(s.Activate("12"));                                                        // 나오지 않는 파티는 고를 수 없다
        Assert.Equal(Screen.DungeonPartySelect, s.Screen);
        s.Activate("11");
        Assert.Equal(Screen.DungeonMove, s.Screen);
        Assert.Equal(1999, s.CurrentDungeonCode);
        Assert.Equal((0L, 1L, 1L), At(s));
        Assert.Equal("테스트 던전 (맵 0)", s.DungeonTitle);
    }

    [Fact]
    public void Steps_are_wasd_keys_one_cell_at_a_time_and_blocked_by_impassable_cells()
    {
        var s = Entered();
        Assert.Empty(s.Entries);                                                              // 이동 화면에는 목록이 없다: [1]위 같은 표시도, 나가기도 없다
        s.PressDungeonKey('w');
        s.PressDungeonKey('A');
        Assert.Equal((0L, 1L, 1L), At(s));                                                    // 위(벽)와 왼쪽(벽)으로는 갈 수 없다
        s.PressDungeonKey('x');                                                               // 다른 키는 아무 일도 없다
        Assert.Equal((0L, 1L, 1L), At(s));
        Walk(s, "R");
        Assert.Equal((0L, 2L, 1L), At(s));                                                    // D(오른쪽), 대소문자 무시
        s.PressDungeonKey('S');
        Assert.Equal((0L, 2L, 1L), At(s));                                                    // 아래 (2,2)는 벽
        Walk(s, "LD");
        Assert.Equal((0L, 1L, 2L), At(s));
        Assert.Equal(Screen.DungeonMove, s.Screen);
    }

    [Fact]
    public void The_keys_do_nothing_outside_the_dungeon_move_screen()
    {
        var s = GameSession.NewGame(SamplePackWith());
        s.PressDungeonKey('w');
        s.InteractDungeon();
        Assert.Equal(Screen.Home, s.Screen);
        Assert.False(s.CanInteractDungeon);
    }

    [Fact]
    public void A_wall_without_impassable_can_be_walked_through_and_impassable_without_wall_blocks()
    {
        const string header = "code\ttile_effect\tvalue_1\tvalue_2\tvalue_3\n";
        // 벽(#, 타일 2)은 보이기만 하는 Wall뿐이라 지나갈 수 있고, 바닥(., 타일 1)에 이동불가를 겹치면 못 간다.
        var s = Entered(SamplePackWith((GamePack.TileMasterPath, header
            + "1\tImpassable\t0\t0\t0\n2\tWall\t0\t0\t0\n3\tNone\t0\t0\t0\n4\tExit\t0\t0\t0\n5\tNone\t0\t0\t0\n"
            + "7\tNone\t0\t0\t0\n8\tNone\t0\t0\t0\n9\tNone\t0\t0\t0\n")));
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
        var s = Entered(SamplePackWith(("master/battle_master_v1.tsv", RunnableBattle)));
        Walk(s, "RRDDRRUURRDD");
        s.NextRandom = _ => 0;
        Walk(s, "R");                                                                         // (8,3) 고정 전투
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Equal(5001, s.CurrentBattleCode);
        Assert.Equal((0L, 8L, 3L), At(s));
        s.Activate("106");                                                                    // 도망
        s.Activate("999");
        Assert.Equal(Screen.DungeonMove, s.Screen);                                           // 도망은 이동 화면으로 돌아와 계속한다
        Assert.Equal((0L, 8L, 3L), At(s));
    }

    /// <summary>고정 전투(8,3)로 가서 우리 파티(리더 체력 1)가 지는 전투를 치른다.</summary>
    static void LoseTheFixedBattle(GameSession s, long hero)
    {
        s.NextRandom = _ => 99;
        Walk(s, "RRDDRRUURRDD");
        s.NextRandom = _ => 1;                                                                // 적 파티 2(전사 하나)
        Walk(s, "R");
        Assert.Equal(Screen.Battle, s.Screen);
        FinishLosing(s, hero);
    }

    /// <summary>전투 화면에서 적 체력을 높이고 리더 체력을 1로 해 한 턴 만에 지게 한다.</summary>
    static void FinishLosing(GameSession s, long hero)
    {
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
        var s = Entered(SamplePackWith(("master/battle_master_v1.tsv", RunnableBattle)));
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
        var s = GameSession.NewGame(SamplePackWith(("master/battle_master_v1.tsv", RunnableBattle)));
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        s.AddCharacter(7001);
        s.Parties[1].Units[0] = s.Parties[0].Units[0]! with { CharacterCode = 7001 };       // 파티-B: 리더 용사 혼자
        s.Activate("101"); s.Activate("1999");
        Assert.Equal(["11", "12", "999"], s.Entries.Select(e => e.Code));                     // 파견할 수 있는 두 파티
        s.Activate("12");
        Assert.Equal(Screen.DungeonMove, s.Screen);
        LoseTheFixedBattle(s, 7001);
        s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Null(s.CurrentDungeonCode);
    }

    [Fact]
    public void The_event_game_over_flow_ends_the_event_and_goes_to_the_game_over_screen()
    {
        const string flows = "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n"
            + "3002\t1\tBattle\t5001\t2001\t0\n3002\t2\tJumpFlagEqual\t2001\t3\t4\n3002\t3\tFinish\t0\t0\t0\n3002\t4\tGameOver\t0\t0\t0\n";
        var s = GameSession.NewGame(SamplePackWith(("master/event_flow_master_v1.tsv", flows)));
        s.NextRandom = _ => 1;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        s.Activate("103"); s.Activate("3002");
        Assert.Equal(Screen.Battle, s.Screen);
        FinishLosing(s, 1);
        s.Activate("999");                                                                    // 패배(플래그 3) → GameOver 칸
        Assert.Equal(Screen.GameOver, s.Screen);
        Assert.Null(s.CurrentEvent);
        Assert.Equal(Screen.Load, s.Activate("999")!.Target);
        Assert.Empty(s.Entries);
    }

    [Fact]
    public void The_sample_event_3002_ends_in_a_game_over_when_the_battle_is_lost()
    {
        var s = GameSession.NewGame(SamplePackWith());
        s.NextRandom = _ => 1;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        s.Activate("103"); s.Activate("3002");
        FinishLosing(s, 1);
        s.Activate("999");
        Assert.Equal(Screen.GameOver, s.Screen);
    }

    [Fact]
    public void Each_step_checks_the_encounter_rate_and_fights_a_battle_of_the_map()
    {
        var s = Entered();
        s.NextRandom = _ => 99;                                                               // 99% 이상: 인카운트률 10%에 걸리지 않는다
        Walk(s, "R");
        Assert.Equal(Screen.DungeonMove, s.Screen);
        s.NextRandom = n => n == 100 ? 9 : 0;                                                // 9 < 10: 인카운트
        Walk(s, "R");
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Equal(5001, s.CurrentBattleCode);
        Assert.Equal((0L, 3L, 1L), At(s));                                                    // 걸음은 이미 옮겼다
    }

    [Fact]
    public void A_map_without_encounter_battles_never_encounters()
    {
        var s = Entered();
        Walk(s, "DDD");
        s.NextRandom = _ => 0;
        Walk(s, "D");                                                                         // 계단 '>': 맵 1(설정된 전투가 없는 맵)로
        Assert.Equal((1L, 2L, 1L), At(s));
        Assert.Equal(Screen.DungeonMove, s.Screen);                                           // 난수 0이어도 인카운트하지 않는다
        Walk(s, "R");
        Assert.Equal((1L, 3L, 1L), At(s));
        Assert.Equal(Screen.DungeonMove, s.Screen);
    }

    [Fact]
    public void The_encounter_battle_is_chosen_by_weight_among_the_map_battles()
    {
        const string battles = "code\tname\tcan_run\n5001\t전투 하나\ttrue\n5002\t전투 둘\ttrue\n";
        const string encounters = "dungeon_code\tlayer_number\tweight\tbattle_code\n1999\t0\t1\t5001\n1999\t0\t3\t5002\n";
        var pack = SamplePackWith(("master/battle_master_v1.tsv", battles), (GamePack.DungeonEncounterMasterPath, encounters));
        foreach (var (roll, expected) in new[] { (0L, 5001L), (1L, 5002L), (3L, 5002L) })
        {
            var s = Entered(pack);
            s.NextRandom = n => n == 100 ? 0 : roll;                                          // 인카운트 판정은 통과, 전투 고르는 난수는 합 4 안의 roll
            Walk(s, "R");
            Assert.Equal(Screen.Battle, s.Screen);
            Assert.Equal(expected, s.CurrentBattleCode);
        }
    }

    [Fact]
    public void After_an_encounter_battle_the_dungeon_continues_and_the_check_is_not_repeated()
    {
        var s = Entered(SamplePackWith(("master/battle_master_v1.tsv", RunnableBattle)));
        s.NextRandom = _ => 0;
        Walk(s, "R");
        Assert.Equal(Screen.Battle, s.Screen);
        s.Activate("106");
        s.Activate("999");
        Assert.Equal(Screen.DungeonMove, s.Screen);                                           // 같은 걸음에서 다시 인카운트하지 않는다
        Assert.Equal((0L, 2L, 1L), At(s));
    }

    static GamePack WithInteract(int tile) =>
        SamplePackWith(("master/battle_master_v1.tsv", RunnableBattle),
            ("master/tile_master_v1.tsv", File.ReadAllText(TileMaster()).TrimEnd() + $"\n{tile}\tInteract\t0\t0\t0\t\n"));

    [Fact]
    public void An_interact_tile_does_not_fire_on_touch_and_fires_with_enter()
    {
        var s = Entered(WithInteract(8));                                                     // 계단 '>'에 Interact
        Walk(s, "DDDD");
        Assert.Equal((0L, 1L, 5L), At(s));                                                    // 닿아도 옮겨 가지 않는다
        Assert.True(s.CanInteractDungeon);
        s.InteractDungeon();                                                                  // Enter
        Assert.Equal((1L, 2L, 1L), At(s));                                                    // 워프가 발동했다(도착 칸의 효과는 실행하지 않는다)
        Assert.Equal(Screen.DungeonMove, s.Screen);
        Assert.False(s.CanInteractDungeon);                                                   // 도착한 '<'(Interact 없음)는 Enter로 할 일이 없다
    }

    [Fact]
    public void An_interact_battle_tile_starts_the_battle_only_with_enter()
    {
        var s = Entered(WithInteract(5));                                                     // 고정 전투 'B'에 Interact
        Walk(s, "RRDDRRUURRDDR");
        Assert.Equal(Screen.DungeonMove, s.Screen);
        Assert.Equal((0L, 8L, 3L), At(s));
        s.NextRandom = _ => 0;
        s.InteractDungeon();
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Equal(5001, s.CurrentBattleCode);
        s.Activate("106");
        s.Activate("999");
        Assert.Equal(Screen.DungeonMove, s.Screen);                                           // 이동 화면으로 돌아와 계속한다(Enter의 전투 뒤에 인카운트 판정은 없다)
        Assert.Equal((0L, 8L, 3L), At(s));
    }

    [Fact]
    public void Enter_does_nothing_on_a_tile_that_fires_on_touch()
    {
        var s = Entered(WithInteract(8));
        Walk(s, "R");
        Assert.False(s.CanInteractDungeon);
        s.InteractDungeon();
        Assert.Equal((0L, 2L, 1L), At(s));
    }

    [Fact]
    public void An_adv_tile_plays_the_adv_and_returns_to_the_dungeon()
    {
        var s = Entered(SamplePackWith(("master/tile_master_v1.tsv", File.ReadAllText(TileMaster()).Replace("7\tAdv\t4001", "7\tAdv\t4002"))));
        Walk(s, "DDDDL");                                                                     // (4,5)
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
    public void Only_an_exit_tile_lets_the_party_leave_and_only_with_enter()
    {
        var s = Entered(SamplePackWith(("master/tile_master_v1.tsv", File.ReadAllText(TileMaster()).Replace("1\tNone\t0\t0\t0", "1\tExit\t0\t0\t0"))));
        Assert.False(s.CanInteractDungeon);                                                   // 시작 칸(S, 타일 3)은 출구가 아니다
        Walk(s, "R");                                                                         // 바닥(타일 1)을 출구로 바꿨다
        Assert.Equal(Screen.DungeonMove, s.Screen);                                           // 닿기만 해서는 나가지 않는다
        Assert.True(s.CanInteractDungeon);
        s.InteractDungeon();                                                                  // Enter
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Null(s.CurrentDungeonCode);

        var t = Entered();
        Assert.False(t.CanInteractDungeon);                                                   // 출구가 아닌 칸에서는 Enter가 아무 일도 하지 않는다
        t.InteractDungeon();
        Assert.Equal(Screen.DungeonMove, t.Screen);
        Assert.Null(t.Activate("999"));                                                       // 던전에서 나가는 [999]는 없다
        Assert.Equal(Screen.DungeonMove, t.Screen);
        Assert.NotNull(t.CurrentDungeonCode);
    }

    static string TileMaster()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        return Path.Combine(root, "samples", "sample-pack", "master", "tile_master_v1.tsv");
    }
}
