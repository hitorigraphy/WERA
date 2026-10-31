using Wera.Core;

namespace Wera.Core.Tests;

// 던전 맵 마스터: tile_master(code, tile_effect, value_1~3), dungeon_tile_master(dungeon_code, local_tile_text, tile_code),
// dungeon_map_master(dungeon_code, layer_number, pos_y, pos_x_0 ~ pos_x_29).
public class DungeonMapTests
{
    const string Dungeons = "code\tname\n1001\t초원\n1002\t동굴\n";
    // 맵이 있는 던전 1001은 도입 지점(레이어 0의 x 0, y 0)이 있어야 한다. 맵이 없는 던전 1002는 비운다.
    const string DungeonsWithEntry = "code\tname\tentry_layer_number\tentry_pos_x\tentry_pos_y\n1001\t초원\t0\t0\t0\n1002\t동굴\t\t\t\n";
    const string Battles = "code\tname\n5001\t전투\n";
    const string Advs = "code\tname\n4001\tADV\n";
    const string TileHeader = "code\ttile_effect\tvalue_1\tvalue_2\tvalue_3\n";
    const string DungeonTileHeader = "dungeon_code\tlocal_tile_text\ttile_code\n";
    static readonly string MapHeader = "dungeon_code\tlayer_number\tpos_y\t" + string.Join("\t", Enumerable.Range(0, 30).Select(i => $"pos_x_{i}")) + "\n";

    static string Row(long dungeon, long layer, long y, string cells) =>
        $"{dungeon}\t{layer}\t{y}\t" + string.Join("\t", cells.Select(c => c == ' ' ? "" : c.ToString()).Concat(Enumerable.Repeat("", 30 - cells.Length))) + "\n";

    static (string, string)[] Base() =>
        [("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.DungeonMasterPath, Dungeons), (GamePack.BattleMasterPath, Battles), (GamePack.AdvMasterPath, Advs),
         (GamePack.TileMasterPath, TileHeader + "1\tNone\t0\t0\t0\n2\tWall\t0\t0\t0\n3\tExit\t0\t0\t0\n"),
         (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t.\t1\n1001\t#\t2\n1001\tS\t3\n")];

    static GamePack Load(params (string, string)[] extra)
    {
        var files = extra.ToList();
        if (files.Any(e => e.Item1 == GamePack.DungeonMapMasterPath) && files.All(e => e.Item1 != GamePack.DungeonMasterPath))
            files.Add((GamePack.DungeonMasterPath, DungeonsWithEntry));      // 맵이 있으면 던전 마스터에 도입 지점이 있어야 한다
        return GamePack.Load(CoreTests.PackZip(Base().Where(b => files.All(e => e.Item1 != b.Item1)).Concat(files).ToArray()));
    }

    static void Rejects(string message, params (string, string)[] files) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(files)).Message);

    static void RejectsWithout(string missing, string message, params (string, string)[] files) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            Base().Where(b => b.Item1 != missing && files.All(e => e.Item1 != b.Item1)).Concat(files).ToArray()))).Message);

    [Fact]
    public void The_masters_load_and_are_looked_up_by_tile_dungeon_and_character()
    {
        var pack = Load(
            (GamePack.TileMasterPath, TileHeader + "1\tNone\t0\t0\t0\n5\tFixedBattle\t5001\t0\t0\n5\tWall\t0\t0\t0\n7\tAdv\t4001\t0\t0\n"),
            (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t.\t1\n1001\tB\t5\n"),
            (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 1, ".B.") + Row(1001, 0, 0, "B . ") + Row(1001, 2, 0, "B") + Row(1001, 1, 5, ".")));
        Assert.Equal([TileEffectType.Wall, TileEffectType.FixedBattle], pack.TileEffectsOf(5).Select(e => e.EffectType));      // 같은 타일에 효과가 겹치고 효과 번호 순
        Assert.Equal(5, pack.DungeonTileCodeOf(1001, "B"));
        Assert.Null(pack.DungeonTileCodeOf(1001, "x"));
        Assert.Null(pack.DungeonTileCodeOf(1002, "B"));
        var map = pack.DungeonMapOf(1001);
        Assert.Equal([(0L, 0L), (0L, 1L), (1L, 5L), (2L, 0L)], map.Select(m => (m.LayerNumber, m.PosY)));                   // layer_number 순, 같은 맵 안에서는 pos_y 순
        Assert.Equal([0L, 1L, 2L], pack.DungeonLayersOf(1001));
        Assert.Equal([0L, 1L], pack.DungeonMapOf(1001, 0).Select(m => m.PosY));
        Assert.Equal("B", pack.DungeonMapOf(1001, 2).Single().Cells[0]);
        Assert.Empty(pack.DungeonMapOf(1001, 3));
        Assert.Equal(30, map[0].Cells.Count);
        Assert.Equal("B", map[0].Cells[0]);
        Assert.Equal("", map[0].Cells[1]);                                                                                  // 비운 칸은 맵 밖
        Assert.Equal(".", map[0].Cells[2]);
        Assert.Empty(pack.DungeonMapOf(1002));
    }

    [Fact]
    public void Without_the_masters_everything_is_null_and_empty()
    {
        var pack = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Null(pack.TileEffects);
        Assert.Null(pack.DungeonTiles);
        Assert.Null(pack.DungeonMaps);
        Assert.Empty(pack.TileEffectsOf(1));
        Assert.Empty(pack.DungeonMapOf(1001));
    }

    const string EncounterHeader = "dungeon_code\tlayer_number\tweight\tbattle_code\n";
    const string DungeonsWithRate = "code\tname\tentry_layer_number\tentry_pos_x\tentry_pos_y\tencounter_rate\n1001\t초원\t0\t0\t0\t25\n1002\t동굴\t\t\t\t\n";

    static (string, string)[] WithMap() =>
        [(GamePack.DungeonMasterPath, DungeonsWithRate), (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, ".") + Row(1001, 1, 0, "."))];

    [Fact]
    public void The_dungeon_encounter_master_gives_each_map_its_weighted_battles_and_the_dungeon_its_rate()
    {
        var pack = Load([.. WithMap(), (GamePack.DungeonEncounterMasterPath, EncounterHeader + "1001\t0\t3\t5001\n1001\t0\t1\t5002\n1001\t1\t1\t5001\n"),
            (GamePack.BattleMasterPath, "code\tname\n5001\t전투\n5002\t전투 2\n")]);
        Assert.Equal(25, pack.Dungeons!.Single(d => d.Code == 1001).EncounterRate);
        Assert.Equal(0, pack.Dungeons!.Single(d => d.Code == 1002).EncounterRate);
        Assert.Equal([(5001L, 3L), (5002L, 1L)], pack.DungeonEncountersOf(1001, 0).Select(e => (e.BattleCode, e.Weight)));
        Assert.Equal([5001L], pack.DungeonEncountersOf(1001, 1).Select(e => e.BattleCode));
        Assert.Empty(pack.DungeonEncountersOf(1001, 2));                                      // 설정된 전투가 없는 맵은 인카운트하지 않는다
        Assert.Equal(0, Load().Dungeons!.First().EncounterRate);                              // 열이 없으면 0
    }

    [Fact]
    public void Bad_encounter_masters_and_rates_are_rejected()
    {
        (string, string)[] With(string rows) =>
            [.. WithMap(), (GamePack.DungeonEncounterMasterPath, EncounterHeader + rows)];
        Rejects("던전 코드 '9'", With("9\t0\t1\t5001\n"));
        Rejects("layer_number '3'의 맵이 없습니다", With("1001\t3\t1\t5001\n"));
        Rejects("weight '0'", With("1001\t0\t0\t5001\n"));
        Rejects("전투 '9'", With("1001\t0\t1\t9\n"));
        Rejects("두 번", With("1001\t0\t1\t5001\n1001\t0\t2\t5001\n"));
        Rejects("encounter_rate '101'", (GamePack.DungeonMasterPath, "code\tname\tencounter_rate\n1001\t초원\t101\n"));
        Rejects("encounter_rate '-1'", (GamePack.DungeonMasterPath, "code\tname\tencounter_rate\n1001\t초원\t-1\n"));
        Rejects("정수가 아닙니다", (GamePack.DungeonMasterPath, "code\tname\tencounter_rate\n1001\t초원\tx\n"));
        Assert.Equal(40, Load((GamePack.DungeonMasterPath, "code\tname\tencounter_rate\n1001\t초원\t40\n1002\t동굴\t\n")).Dungeons![0].EncounterRate);   // 도입 지점 열 없이도 된다
        RejectsWithout(GamePack.BattleMasterPath, "battle_master_v1.tsv도 필요", With("1001\t0\t1\t5001\n"));
    }

    [Fact]
    public void The_dungeon_event_masters_load_and_the_tile_effect_is_validated()
    {
        const string flows = "dungeon_event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n6001\t1\tAdv\t4001\t0\t0\n6001\t2\tJump\t3\t0\t0\n6001\t3\tFinish\t0\t0\t0\n";
        var pack = Load((GamePack.DungeonEventMasterPath, "code\tname\n6001\t상자\n"), (GamePack.DungeonEventFlowMasterPath, flows),
            (GamePack.TileMasterPath, TileHeader + "1\tNone\t0\t0\t0\n2\tWall\t0\t0\t0\n3\tExit\t0\t0\t0\n4\tDungeonEvent\t6001\t0\t0\n"));
        Assert.Equal("상자", pack.FindDungeonEvent(6001)!.Name);
        Assert.Equal([1L, 2L, 3L], pack.DungeonEventFlowsOf(6001).Select(f => f.FlowNumber));
        Assert.Equal(new TileEffect(4, TileEffectType.DungeonEvent, 6001, 0, 0), pack.TileEffectsOf(4).Single());
        Assert.Null(pack.FindDungeonEvent(1));
        Assert.Empty(pack.DungeonEventFlowsOf(1));

        Rejects("던전 이벤트 '9'가 던전 이벤트 마스터에 없습니다", (GamePack.DungeonEventMasterPath, "code\tname\n6001\t상자\n"),
            (GamePack.DungeonEventFlowMasterPath, "dungeon_event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n9\t1\tFinish\t0\t0\t0\n"));
        Rejects("ADV '9'", (GamePack.DungeonEventMasterPath, "code\tname\n6001\t상자\n"),
            (GamePack.DungeonEventFlowMasterPath, "dungeon_event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n6001\t1\tAdv\t9\t0\t0\n"));
        Rejects("같은 던전 이벤트의 흐름에 없습니다", (GamePack.DungeonEventMasterPath, "code\tname\n6001\t상자\n"),
            (GamePack.DungeonEventFlowMasterPath, "dungeon_event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n6001\t1\tJump\t5\t0\t0\n"));
        Rejects("던전 이벤트 '9'", (GamePack.TileMasterPath, TileHeader + "1\tDungeonEvent\t9\t0\t0\n"),
            (GamePack.DungeonEventMasterPath, "code\tname\n6001\t상자\n"));
        RejectsWithout(GamePack.DungeonEventMasterPath, "dungeon_event_master_v1.tsv도 필요", (GamePack.TileMasterPath, TileHeader + "1\tDungeonEvent\t6001\t0\t0\n"));
        Rejects("dungeon_event_master_v1.tsv도 필요", (GamePack.DungeonEventFlowMasterPath, "dungeon_event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n6001\t1\tFinish\t0\t0\t0\n"));
    }

    [Fact]
    public void Bad_tile_masters_are_rejected()
    {
        Rejects("알 수 없습니다", (GamePack.TileMasterPath, TileHeader + "1\tLava\t0\t0\t0\n"));
        Rejects("알 수 없습니다", (GamePack.TileMasterPath, TileHeader + "1\t1\t0\t0\t0\n"));                                // 숫자는 안 된다
        Rejects("두 번", (GamePack.TileMasterPath, TileHeader + "1\tWall\t0\t0\t0\n1\tWall\t0\t0\t0\n"));
        Rejects("전투 '9'", (GamePack.TileMasterPath, TileHeader + "1\tFixedBattle\t9\t0\t0\n"));
        Rejects("ADV '9'", (GamePack.TileMasterPath, TileHeader + "1\tAdv\t9\t0\t0\n"));
        RejectsWithout(GamePack.BattleMasterPath, "battle_master_v1.tsv도 필요", (GamePack.TileMasterPath, TileHeader + "1\tFixedBattle\t5001\t0\t0\n"));
        Rejects("헤더", (GamePack.TileMasterPath, "code\ttile_effect\n1\tWall\n"));
    }

    [Fact]
    public void Bad_dungeon_tile_masters_are_rejected()
    {
        Rejects("던전 코드 '9'", (GamePack.DungeonTileMasterPath, DungeonTileHeader + "9\t.\t1\n"));
        Rejects("한 글자", (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t..\t1\n"));
        Rejects("한 글자", (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t \t1\n"));
        Rejects("한 글자", (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t\t1\n"));
        Rejects("타일 코드 '9'", (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t.\t9\n"));
        Rejects("두 번", (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t.\t1\n1001\t.\t2\n"));
        RejectsWithout(GamePack.TileMasterPath, "tile_master_v1.tsv도 필요", (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t.\t1\n"));
        Assert.Equal(2L, Load((GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t가\t1\n1002\t가\t2\n")).DungeonTileCodeOf(1002, "가"));   // 던전마다 같은 글자를 따로 쓴다
    }

    [Fact]
    public void Bad_dungeon_map_masters_are_rejected()
    {
        Rejects("던전 코드 '9'", (GamePack.DungeonMapMasterPath, MapHeader + Row(9, 0, 0, "#")));
        Rejects("pos_y '-1'", (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, -1, "#")));
        Rejects("layer_number '-1'", (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, -1, 0, "#")));
        Rejects("두 번", (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, "#") + Row(1001, 0, 0, ".")));
        Assert.Equal([0L, 1L], Load((GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, "#") + Row(1001, 1, 0, "."))).DungeonLayersOf(1001));   // 같은 pos_y라도 맵이 다르면 따로
        Rejects("글자 'x'", (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, "#x")));
        Rejects("글자 '#'", (GamePack.DungeonMapMasterPath, MapHeader + Row(1002, 0, 0, "#")));                                // 다른 던전의 글자는 쓸 수 없다
        Rejects("헤더", (GamePack.DungeonMapMasterPath, "dungeon_code\tpos_y\tpos_x_0\n1001\t0\t#\n"));
    }

    [Fact]
    public void The_sample_pack_has_a_test_dungeon_map_with_every_tile_kind()
    {
        var pack = GamePack.Load(File.OpenRead(Path.Combine(FindRoot(), "src", "Wera.Web", "wwwroot", "sample-pack.wra")));
        var map = pack.DungeonMapOf(1999);
        Assert.Equal(10, map.Count);
        Assert.Equal([0L, 1L], pack.DungeonLayersOf(1999));
        Assert.Equal(7, pack.DungeonMapOf(1999, 0).Count);
        Assert.Equal(3, pack.DungeonMapOf(1999, 1).Count);
        var kinds = map.SelectMany(m => m.Cells).Where(c => c != "").Select(c => pack.DungeonTileCodeOf(1999, c)!.Value).Distinct()
            .SelectMany(t => pack.TileEffectsOf(t)).Select(e => e.EffectType).ToHashSet();
        Assert.Superset(new HashSet<TileEffectType>
        {
            TileEffectType.None, TileEffectType.Wall, TileEffectType.Exit,
            TileEffectType.FixedBattle, TileEffectType.Adv, TileEffectType.Warp, TileEffectType.Stairs, TileEffectType.Impassable,
        }, kinds);
    }

    [Fact]
    public void Warp_moves_within_the_same_dungeon_and_its_destination_must_be_a_map_cell()
    {
        var tiles = TileHeader + "1\tNone\t0\t0\t0\n2\tWarp\t1\t2\t3\n";
        var dt = DungeonTileHeader + "1001\t.\t1\n1001\t>\t2\n";
        // 레이어 0의 > 는 레이어 1의 (x 2, y 3)으로
        var ok = Load((GamePack.TileMasterPath, tiles), (GamePack.DungeonTileMasterPath, dt),
            (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, ">") + Row(1001, 1, 3, "...")));
        Assert.Equal(new TileEffect(2, TileEffectType.Warp, 1, 2, 3), ok.TileEffectsOf(2).Single());
        Rejects("같은 던전의 맵에 있는 칸이 아닙니다", (GamePack.TileMasterPath, tiles), (GamePack.DungeonTileMasterPath, dt),
            (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, ">") + Row(1001, 1, 3, ".. ")));                          // 도착 칸이 빈 칸
        Rejects("같은 던전의 맵에 있는 칸이 아닙니다", (GamePack.TileMasterPath, tiles), (GamePack.DungeonTileMasterPath, dt),
            (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, ">") + Row(1001, 1, 2, "...")));                          // 그 줄이 없다
        Rejects("같은 던전의 맵에 있는 칸이 아닙니다", (GamePack.TileMasterPath, tiles), (GamePack.DungeonTileMasterPath, dt),
            (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, ">")));                                                  // 그 맵이 없다
        Rejects("pos_x 30", (GamePack.TileMasterPath, TileHeader + "2\tWarp\t0\t30\t0\n"));
        Rejects("Warp의 도착", (GamePack.TileMasterPath, TileHeader + "2\tWarp\t-1\t0\t0\n"));
        Assert.Equal(4, (int)TileEffectType.Adv);
        Assert.Equal(5, (int)TileEffectType.Warp);
        Assert.Equal(6, (int)TileEffectType.Stairs);
        Assert.Equal(7, (int)TileEffectType.Impassable);
        Assert.Equal(8, (int)TileEffectType.Interact);
        Assert.Equal(9, (int)TileEffectType.DungeonEvent);
        Assert.DoesNotContain("Start", Enum.GetNames<TileEffectType>());
        Assert.DoesNotContain("RandomBattle", Enum.GetNames<TileEffectType>());      // 전투는 던전 인카운트 마스터로 정한다
    }

    [Fact]
    public void The_dungeon_master_entry_point_is_validated_against_the_dungeon_map()
    {
        var pack = Load((GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, "#")));
        Assert.Equal(new DungeonEntry(0, 0, 0), pack.Dungeons!.Single(d => d.Code == 1001).Entry);
        Assert.Null(pack.Dungeons!.Single(d => d.Code == 1002).Entry);                                  // 맵이 없는 던전은 도입 지점이 없다
        const string head = "code\tname\tentry_layer_number\tentry_pos_x\tentry_pos_y\n";
        var map = (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 0, "#.") + Row(1001, 1, 2, "  #"));
        Assert.Equal(new DungeonEntry(1, 2, 2), Load((GamePack.DungeonMasterPath, head + "1001\t초원\t1\t2\t2\n1002\t동굴\t\t\t\n"), map)
            .Dungeons![0].Entry);
        Rejects("도입 지점(레이어 0, pos_x 5, pos_y 0)이 던전 맵에 있는 칸이 아닙니다",
            (GamePack.DungeonMasterPath, head + "1001\t초원\t0\t5\t0\n"), map);                         // 빈 칸
        Rejects("도입 지점(레이어 0, pos_x 0, pos_y 3)이 던전 맵에 있는 칸이 아닙니다",
            (GamePack.DungeonMasterPath, head + "1001\t초원\t0\t0\t3\n"), map);                         // 그 줄이 없다
        Rejects("도입 지점(layer_number 0, pos_x 30, pos_y 0)은", (GamePack.DungeonMasterPath, head + "1001\t초원\t0\t30\t0\n"));
        Rejects("entry_pos_x이(가) 비어 있습니다", (GamePack.DungeonMasterPath, head + "1001\t초원\t0\t\t0\n"));               // 일부만 적으면 오류
        Rejects("맵이 있어 도입 지점", (GamePack.DungeonMasterPath, "code\tname\n1001\t초원\n"), map);               // 맵이 있는데 도입 지점이 없다
        Rejects("도입 지점이 있으려면", (GamePack.DungeonMasterPath, head + "1001\t초원\t0\t0\t0\n"));         // 도입 지점이 있는데 맵이 없다
    }

    static string FindRoot()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        return root;
    }
}
