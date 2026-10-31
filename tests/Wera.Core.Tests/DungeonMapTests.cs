using Wera.Core;

namespace Wera.Core.Tests;

// 던전 맵 마스터: tile_master(code, tile_effect, value_1~3), dungeon_tile_master(dungeon_code, local_tile_text, tile_code),
// dungeon_map_master(dungeon_code, layer_number, pos_y, pos_x_0 ~ pos_x_29).
public class DungeonMapTests
{
    const string Dungeons = "code\tname\n1001\t초원\n1002\t동굴\n";
    const string Battles = "code\tname\n5001\t전투\n";
    const string Advs = "code\tname\n4001\tADV\n";
    const string TileHeader = "code\ttile_effect\tvalue_1\tvalue_2\tvalue_3\n";
    const string DungeonTileHeader = "dungeon_code\tlocal_tile_text\ttile_code\n";
    static readonly string MapHeader = "dungeon_code\tlayer_number\tpos_y\t" + string.Join("\t", Enumerable.Range(0, 30).Select(i => $"pos_x_{i}")) + "\n";

    static string Row(long dungeon, long layer, long y, string cells) =>
        $"{dungeon}\t{layer}\t{y}\t" + string.Join("\t", cells.Select(c => c == ' ' ? "" : c.ToString()).Concat(Enumerable.Repeat("", 30 - cells.Length))) + "\n";

    static (string, string)[] Base() =>
        [("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.DungeonMasterPath, Dungeons), (GamePack.BattleMasterPath, Battles), (GamePack.AdvMasterPath, Advs),
         (GamePack.TileMasterPath, TileHeader + "1\tNone\t0\t0\t0\n2\tWall\t0\t0\t0\n3\tStart\t0\t0\t0\n"),
         (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t.\t1\n1001\t#\t2\n1001\tS\t3\n")];

    static GamePack Load(params (string, string)[] extra) => GamePack.Load(CoreTests.PackZip(
        Base().Where(b => extra.All(e => e.Item1 != b.Item1)).Concat(extra).ToArray()));

    static void Rejects(string message, params (string, string)[] files) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(files)).Message);

    static void RejectsWithout(string missing, string message, params (string, string)[] files) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            Base().Where(b => b.Item1 != missing && files.All(e => e.Item1 != b.Item1)).Concat(files).ToArray()))).Message);

    [Fact]
    public void The_masters_load_and_are_looked_up_by_tile_dungeon_and_character()
    {
        var pack = Load(
            (GamePack.TileMasterPath, TileHeader + "1\tNone\t0\t0\t0\n5\tFixedBattle\t5001\t0\t0\n5\tWall\t0\t0\t0\n6\tRandomBattle\t30\t100\t5001\n7\tAdv\t4001\t0\t0\n"),
            (GamePack.DungeonTileMasterPath, DungeonTileHeader + "1001\t.\t1\n1001\tB\t5\n"),
            (GamePack.DungeonMapMasterPath, MapHeader + Row(1001, 0, 1, ".B.") + Row(1001, 0, 0, "B . ") + Row(1001, 2, 0, "B") + Row(1001, 1, 5, ".")));
        Assert.Equal([TileEffectType.Wall, TileEffectType.FixedBattle], pack.TileEffectsOf(5).Select(e => e.EffectType));      // 같은 타일에 효과가 겹치고 효과 번호 순
        Assert.Equal(new TileEffect(6, TileEffectType.RandomBattle, 30, 100, 5001), pack.TileEffectsOf(6).Single());
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

    [Fact]
    public void Bad_tile_masters_are_rejected()
    {
        Rejects("알 수 없습니다", (GamePack.TileMasterPath, TileHeader + "1\tLava\t0\t0\t0\n"));
        Rejects("알 수 없습니다", (GamePack.TileMasterPath, TileHeader + "1\t1\t0\t0\t0\n"));                                // 숫자는 안 된다
        Rejects("두 번", (GamePack.TileMasterPath, TileHeader + "1\tWall\t0\t0\t0\n1\tWall\t0\t0\t0\n"));
        Rejects("전투 '9'", (GamePack.TileMasterPath, TileHeader + "1\tFixedBattle\t9\t0\t0\n"));
        Rejects("모수", (GamePack.TileMasterPath, TileHeader + "1\tRandomBattle\t30\t0\t5001\n"));
        Rejects("전투 '9'", (GamePack.TileMasterPath, TileHeader + "1\tRandomBattle\t30\t100\t9\n"));
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
        Assert.Equal(7, map.Count);
        Assert.Equal([0L], pack.DungeonLayersOf(1999));
        var kinds = map.SelectMany(m => m.Cells).Where(c => c != "").Select(c => pack.DungeonTileCodeOf(1999, c)!.Value).Distinct()
            .SelectMany(t => pack.TileEffectsOf(t)).Select(e => e.EffectType).ToHashSet();
        Assert.Superset(new HashSet<TileEffectType>
        {
            TileEffectType.None, TileEffectType.Wall, TileEffectType.Start, TileEffectType.Exit,
            TileEffectType.FixedBattle, TileEffectType.RandomBattle, TileEffectType.Adv,
        }, kinds);
    }

    static string FindRoot()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        return root;
    }
}
