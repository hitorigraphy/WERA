using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 타일의 효과 종류. TSV에는 이름(대소문자 무시)으로 적는다. 타일 하나에 효과를 여러 개 겹칠 수 있다(타일 마스터에서 같은 타일 코드에 다른 효과의 행을 더한다).
/// 숫자 값은 정해진 번호다: None 0, Wall 1, Exit 2, FixedBattle 3, Adv 4, Warp 5, Stairs 6, Impassable 7, Interact 8, DungeonEvent 9.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TileEffectType>))]
public enum TileEffectType
{
    /// <summary>아무 효과도 없다(그냥 지나갈 수 있는 바닥).</summary>
    None = 0,
    /// <summary>벽을 표시한다(보이기만 한다). 들어갈 수 없게 하려면 <see cref="Impassable"/>을 같이 겹친다.</summary>
    Wall = 1,
    /// <summary>던전을 나가는 칸: 던전에서 나가는 곳은 여기뿐이다(닿기만 해서는 나가지 않고, 그 칸에서 Enter를 눌러야 나간다). 인자는 쓰이지 않는다.</summary>
    Exit = 2,
    /// <summary>닿으면 정해진 전투가 벌어진다. value_1이 battle_code(전투 마스터)다.</summary>
    FixedBattle = 3,
    /// <summary>닿으면 ADV로 넘어간다. value_1이 adv_code(ADV 마스터)다.</summary>
    Adv = 4,
    /// <summary>
    /// 닿으면 같은 던전 안의 다른 칸으로 옮겨 간다(계단, 워프). value_1이 도착할 layer_number, value_2가 pos_x, value_3이 pos_y다.
    /// 다른 던전으로는 갈 수 없다(던전은 홈 화면에서 던전과 파티를 골라 들어가므로). 도착 칸은 그 던전의 맵에 있는 글자 칸이어야 한다(던전 맵 마스터 로드 검증).
    /// </summary>
    Warp = 5,
    /// <summary>계단을 표시한다(보이기만 한다). 층을 오가게 하려면 <see cref="Warp"/>를 같이 겹친다. 인자는 쓰이지 않는다.</summary>
    Stairs = 6,
    /// <summary>
    /// 이동불가: 이 효과가 있는 타일 칸으로는 들어갈 수 없다. 인자는 쓰이지 않는다. Wall과 겹치면 못 지나가는 벽이고, Wall만 있으면 지나갈 수 있는 벽이며,
    /// Warp와 Stairs만 있고 Wall이 없으면 옆 맵으로 걸어 넘어가는 듯한 이동이 된다.
    /// </summary>
    Impassable = 7,
    /// <summary>
    /// 상호작용: 이 효과가 있는 타일은 닿아도 FixedBattle, Adv, Warp가 바로 발동하지 않고, 그 칸에 선 채로 Enter를 눌러야 발동한다(출구 Exit는 늘 Enter로 동작한다).
    /// 인자는 쓰이지 않는다. 같이 겹친 효과들이 Enter를 누를 때 효과 번호 순서대로 실행된다(Warp의 도착 칸 효과는 실행하지 않는다).
    /// </summary>
    Interact = 8,
    /// <summary>
    /// 닿으면 던전 이벤트를 재생한다. value_1이 던전 이벤트 마스터의 코드다. 던전 이벤트의 흐름은 이벤트 흐름과 같은 종류(Adv, Battle, SetFlag, Jump*, GameOver 등)를 쓰고,
    /// 끝나면 이동 화면으로 돌아와 나머지 효과를 이어 실행한다. Enter로 발동시키려면 <see cref="Interact"/>를 같이 겹친다.
    /// </summary>
    DungeonEvent = 9,
}

/// <summary>타일 마스터의 한 행: 타일(TileCode)이 가진 효과(EffectType)와 그 인자. 키는 (TileCode, EffectType)이라 한 타일에 효과 종류별로 한 행씩 겹칠 수 있다.</summary>
public sealed record TileEffect(long TileCode, TileEffectType EffectType, long Value1, long Value2, long Value3);

/// <summary>던전 타일 마스터의 한 행: 그 던전의 맵 TSV에서 한 글자(LocalTileText)가 가리키는 타일(TileCode). 키는 (DungeonCode, LocalTileText).</summary>
public sealed record DungeonTile(long DungeonCode, string LocalTileText, long TileCode);

/// <summary>던전 인카운트 마스터의 한 행: 그 던전의 LayerNumber번 맵에서 전투(BattleCode)를 만날 몫(Weight). 키는 (DungeonCode, LayerNumber, BattleCode).</summary>
public sealed record DungeonEncounter(long DungeonCode, long LayerNumber, long Weight, long BattleCode);

/// <summary>던전 맵 마스터의 한 줄: 그 던전의 LayerNumber번 맵(던전 안에 맵이 여러 개일 수 있다)의 위쪽에서 PosY번째 줄(0부터). Cells는 pos_x_0 ~ pos_x_29의 글자이고 빈 칸은 ""(맵 밖)이다.</summary>
public sealed record DungeonMapLine(long DungeonCode, long LayerNumber, long PosY, IReadOnlyList<string> Cells);

/// <summary>던전 층 마스터의 한 행: 그 던전의 LayerNumber번 맵의 이름(던전 이동 화면 오른쪽에 층 이름으로 나온다). 키는 (DungeonCode, LayerNumber).</summary>
public sealed record DungeonLayer(long DungeonCode, long LayerNumber, string Name);

// 타일 마스터, 던전 타일 마스터, 던전 맵 마스터, 던전 인카운트 마스터, 던전 층 마스터. 모두 선택이다.
// tile_master(code, tile_effect, value_1, value_2, value_3; 앞의 두 열이 키), dungeon_tile_master(dungeon_code, local_tile_text, tile_code; 앞의 두 열이 키),
// dungeon_map_master(dungeon_code, layer_number, pos_y, pos_x_0 ~ pos_x_29; 앞의 세 열이 키).
public sealed partial class GamePack
{
    public const string TileMasterPath = "master/tile_master_v1.tsv";
    public const string DungeonTileMasterPath = "master/dungeon_tile_master_v1.tsv";
    public const string DungeonMapMasterPath = "master/dungeon_map_master_v1.tsv";
    public const string DungeonEncounterMasterPath = "master/dungeon_encounter_master_v1.tsv";
    public const string DungeonLayerMasterPath = "master/dungeon_layer_master_v1.tsv";

    /// <summary>던전 맵의 가로 칸 수(pos_x_0 ~ pos_x_29).</summary>
    public const int DungeonMapWidth = 30;

    /// <summary>타일 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<TileEffect>? TileEffects { get; init; }

    /// <summary>던전 타일 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<DungeonTile>? DungeonTiles { get; init; }

    /// <summary>던전 맵 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<DungeonMapLine>? DungeonMaps { get; init; }

    /// <summary>던전 층 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<DungeonLayer>? DungeonLayers { get; init; }

    /// <summary>던전 인카운트 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<DungeonEncounter>? DungeonEncounters { get; init; }

    /// <summary>
    /// 타일 마스터를 읽는다. 키 (타일 코드, 효과)를 두 번 쓸 수 없다. 효과는 이름으로 적고, 인자(value_1~3)는 정수(long, 비우면 0)다.
    /// 효과별 인자 검증: Warp는 도착 layer_number와 pos_y가 0 이상, pos_x가 0~29(도착 칸이 맵에 있는지는 던전 맵 마스터를 읽을 때 본다), FixedBattle의 value_1은 전투 마스터의 코드, Adv의 value_1은 ADV 마스터의 코드여야 한다
    /// (전투나 ADV 효과가 있으려면 그 마스터가 있어야 한다). DungeonEvent의 value_1은 던전 이벤트 마스터의 코드여야 한다. None, Wall, Exit, Stairs, Impassable, Interact는 인자를 보지 않는다.
    /// </summary>
    static List<TileEffect>? LoadTileEffects(ZipArchive zip, List<AdvEntry>? advs, List<BattleEntry>? battles, List<DungeonEventEntry>? dungeonEvents)
    {
        const string file = TileMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;

        var advCodes = advs?.Select(a => a.Code).ToHashSet();
        var battleCodes = battles?.Select(b => b.Code).ToHashSet();
        var dungeonEventCodes = dungeonEvents?.Select(e => e.Code).ToHashSet();
        var rows = new List<TileEffect>();
        var seen = new HashSet<(long, TileEffectType)>();
        foreach (var r in Body(file, text, "code", "tile_effect", "value_1", "value_2", "value_3"))
        {
            var code = ParseLong(file, "타일 코드", r[0]);
            var where = $"타일 '{code}'의";
            // 숫자("1")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<TileEffectType>().Contains(r[1], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<TileEffectType>(r[1], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 효과 '{r[1]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<TileEffectType>())})");
            var v1 = OptionalLong(file, $"{where} {type}의 value_1", r[2]);
            var v2 = OptionalLong(file, $"{where} {type}의 value_2", r[3]);
            var v3 = OptionalLong(file, $"{where} {type}의 value_3", r[4]);
            if (!seen.Add((code, type)))
                throw new PackFormatException($"{file}: 같은 키(타일 {code}, 효과 {type})가 두 번 있습니다.");

            void CheckBattle(long battle)
            {
                if (battleCodes is null) throw new PackFormatException($"{file}: {where} {type}이 있으려면 {BattleMasterPath}도 필요합니다.");
                if (!battleCodes.Contains(battle)) throw new PackFormatException($"{file}: {where} {type}의 전투 '{battle}'가 전투 마스터에 없습니다.");
            }

            switch (type)
            {
                case TileEffectType.FixedBattle:
                    CheckBattle(v1);
                    break;
                case TileEffectType.Warp when v1 < 0 || v2 < 0 || v2 >= DungeonMapWidth || v3 < 0:
                    throw new PackFormatException($"{file}: {where} Warp의 도착(layer_number {v1}, pos_x {v2}, pos_y {v3})은 layer_number와 pos_y가 0 이상, pos_x가 0 이상 {DungeonMapWidth} 미만이어야 합니다.");
                case TileEffectType.DungeonEvent:
                    if (dungeonEventCodes is null) throw new PackFormatException($"{file}: {where} DungeonEvent가 있으려면 {DungeonEventMasterPath}도 필요합니다.");
                    if (!dungeonEventCodes.Contains(v1)) throw new PackFormatException($"{file}: {where} DungeonEvent의 던전 이벤트 '{v1}'가 던전 이벤트 마스터에 없습니다.");
                    break;
                case TileEffectType.Adv:
                    if (advCodes is null) throw new PackFormatException($"{file}: {where} Adv가 있으려면 {AdvMasterPath}도 필요합니다.");
                    if (!advCodes.Contains(v1)) throw new PackFormatException($"{file}: {where} Adv의 ADV '{v1}'가 ADV 마스터에 없습니다.");
                    break;
            }
            rows.Add(new TileEffect(code, type, v1, v2, v3));
        }
        return rows;
    }

    /// <summary>
    /// 던전 타일 마스터를 읽는다. 던전은 던전 마스터에, 타일은 타일 마스터에 있어야 하고, 키 (던전, 글자)를 두 번 쓸 수 없다.
    /// local_tile_text는 정확히 한 글자(보이는 문자 하나)여야 하며 공백은 쓸 수 없다(맵 TSV에서 칸이 보이도록 `.`, `#` 같은 글자를 쓴다).
    /// </summary>
    static List<DungeonTile>? LoadDungeonTiles(ZipArchive zip, List<Dungeon>? dungeons, List<TileEffect>? tiles)
    {
        const string file = DungeonTileMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (dungeons is null) throw new PackFormatException($"{file}가 있으려면 {DungeonMasterPath}도 필요합니다.");
        if (tiles is null) throw new PackFormatException($"{file}가 있으려면 {TileMasterPath}도 필요합니다.");

        var dungeonCodes = dungeons.Select(d => d.Code).ToHashSet();
        var tileCodes = tiles.Select(t => t.TileCode).ToHashSet();
        var rows = new List<DungeonTile>();
        var seen = new HashSet<(long, string)>();
        foreach (var r in Body(file, text, "dungeon_code", "local_tile_text", "tile_code"))
        {
            var dungeon = ParseLong(file, "던전 코드", r[0]);
            var where = $"던전 '{dungeon}'의 글자 '{r[1]}'의";
            var tile = ParseLong(file, $"{where} 타일 코드", r[2]);
            if (!dungeonCodes.Contains(dungeon))
                throw new PackFormatException($"{file}: 던전 코드 '{dungeon}'가 던전 마스터에 없습니다.");
            if (!IsSingleVisibleChar(r[1]))
                throw new PackFormatException($"{file}: 던전 '{dungeon}'의 local_tile_text '{r[1]}'는 공백이 아닌 한 글자여야 합니다.");
            if (!tileCodes.Contains(tile))
                throw new PackFormatException($"{file}: {where} 타일 코드 '{tile}'가 타일 마스터에 없습니다.");
            if (!seen.Add((dungeon, r[1])))
                throw new PackFormatException($"{file}: 같은 키(던전 {dungeon}, 글자 '{r[1]}')가 두 번 있습니다.");
            rows.Add(new DungeonTile(dungeon, r[1], tile));
        }
        return rows;
    }

    /// <summary>
    /// 던전 맵 마스터를 읽는다. 던전은 던전 마스터에 있어야 하고, 키 (던전, layer_number, pos_y)를 두 번 쓸 수 없으며 layer_number와 pos_y는 0 이상이다.
    /// pos_x_0 ~ pos_x_29의 칸은 비어 있거나(맵 밖) 그 던전의 던전 타일 마스터에 있는 글자여야 한다.
    /// </summary>
    static List<DungeonMapLine>? LoadDungeonMaps(ZipArchive zip, List<Dungeon>? dungeons, List<DungeonTile>? dungeonTiles, List<TileEffect>? tiles)
    {
        const string file = DungeonMapMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null)
        {
            if (dungeons?.FirstOrDefault(d => d.Entry is not null) is { } withEntry)
                throw new PackFormatException($"{DungeonMasterPath}: 던전 '{withEntry.Code}'에 도입 지점이 있으려면 {file}도 필요합니다.");
            return null;
        }
        if (dungeons is null) throw new PackFormatException($"{file}가 있으려면 {DungeonMasterPath}도 필요합니다.");

        var dungeonCodes = dungeons.Select(d => d.Code).ToHashSet();
        var known = (dungeonTiles ?? []).Select(t => (t.DungeonCode, t.LocalTileText)).ToHashSet();
        string[] header = ["dungeon_code", "layer_number", "pos_y", .. Enumerable.Range(0, DungeonMapWidth).Select(x => $"pos_x_{x.ToString(CultureInfo.InvariantCulture)}")];
        var rows = new List<DungeonMapLine>();
        var seen = new HashSet<(long, long, long)>();
        foreach (var r in Body(file, text, header))
        {
            var dungeon = ParseLong(file, "던전 코드", r[0]);
            var layer = ParseLong(file, $"던전 '{dungeon}'의 layer_number", r[1]);
            var posY = ParseLong(file, $"던전 '{dungeon}'의 레이어 {layer}의 pos_y", r[2]);
            if (!dungeonCodes.Contains(dungeon))
                throw new PackFormatException($"{file}: 던전 코드 '{dungeon}'가 던전 마스터에 없습니다.");
            if (layer < 0)
                throw new PackFormatException($"{file}: 던전 '{dungeon}'의 layer_number '{layer}'는 0 이상이어야 합니다.");
            if (posY < 0)
                throw new PackFormatException($"{file}: 던전 '{dungeon}'의 레이어 {layer}의 pos_y '{posY}'는 0 이상이어야 합니다.");
            if (!seen.Add((dungeon, layer, posY)))
                throw new PackFormatException($"{file}: 같은 키(던전 {dungeon}, layer_number {layer}, pos_y {posY})가 두 번 있습니다.");
            var cells = r.Skip(3).ToList();
            for (var x = 0; x < cells.Count; x++)
                if (cells[x] != "" && !known.Contains((dungeon, cells[x])))
                    throw new PackFormatException(
                        $"{file}: 던전 '{dungeon}'의 레이어 {layer}, pos_y {posY}, pos_x_{x}의 글자 '{cells[x]}'가 {DungeonTileMasterPath}에 없습니다.");
            rows.Add(new DungeonMapLine(dungeon, layer, posY, cells));
        }

        // 맵이 있는 던전은 도입 지점이 있어야 하고(맵이 없는 던전에는 없어야 하며), 도입 지점은 그 던전의 맵에 있는 글자 칸이어야 한다.
        foreach (var dungeon in dungeons)
        {
            var hasMap = rows.Any(l => l.DungeonCode == dungeon.Code);
            if (dungeon.Entry is not { } entry)
            {
                if (hasMap)
                    throw new PackFormatException($"{DungeonMasterPath}: 던전 '{dungeon.Code}'는 {file}에 맵이 있어 도입 지점(entry_layer_number, entry_pos_x, entry_pos_y)이 필요합니다.");
                continue;
            }
            if (rows.FirstOrDefault(l => l.DungeonCode == dungeon.Code && l.LayerNumber == entry.LayerNumber && l.PosY == entry.PosY) is not { } at || at.Cells[(int)entry.PosX] == "")
                throw new PackFormatException(
                    $"{DungeonMasterPath}: 던전 '{dungeon.Code}'의 도입 지점(레이어 {entry.LayerNumber}, pos_x {entry.PosX}, pos_y {entry.PosY})이 던전 맵에 있는 칸이 아닙니다.");
        }

        // Warp 타일이 그 던전의 맵 안(글자가 있는 칸)으로 옮겨 가는지 본다.
        var tileOf = (dungeonTiles ?? []).ToDictionary(t => (t.DungeonCode, t.LocalTileText), t => t.TileCode);
        var warps = (tiles ?? []).Where(t => t.EffectType == TileEffectType.Warp).ToDictionary(t => t.TileCode);
        var lineAt = rows.ToDictionary(l => (l.DungeonCode, l.LayerNumber, l.PosY));
        foreach (var line in rows)
            for (var x = 0; x < line.Cells.Count; x++)
                if (line.Cells[x] != "" && tileOf.TryGetValue((line.DungeonCode, line.Cells[x]), out var tile) && warps.TryGetValue(tile, out var warp))
                {
                    var from = $"던전 '{line.DungeonCode}'의 레이어 {line.LayerNumber}, pos_y {line.PosY}, pos_x_{x}의 글자 '{line.Cells[x]}'(타일 {tile}) Warp의";
                    if (warp.Value2 < 0 || warp.Value2 >= DungeonMapWidth)
                        throw new PackFormatException($"{file}: {from} 도착 pos_x '{warp.Value2}'는 0 이상 {DungeonMapWidth} 미만이어야 합니다.");
                    if (!lineAt.TryGetValue((line.DungeonCode, warp.Value1, warp.Value3), out var target) || target.Cells[(int)warp.Value2] == "")
                        throw new PackFormatException(
                            $"{file}: {from} 도착(레이어 {warp.Value1}, pos_x {warp.Value2}, pos_y {warp.Value3})이 같은 던전의 맵에 있는 칸이 아닙니다.");
                }
        return rows;
    }

    /// <summary>
    /// 던전 인카운트 마스터를 읽는다(dungeon_code, layer_number, weight, battle_code). 던전은 던전 마스터에, 그 던전의 맵(layer_number)은 던전 맵 마스터에, 전투는 전투 마스터에 있어야 하고,
    /// 가중치는 1 이상이며 키 (던전, layer_number, battle_code)를 두 번 쓸 수 없다. 한 맵에 전투가 하나도 없으면 그 맵에서는 인카운트하지 않는다.
    /// </summary>
    static List<DungeonEncounter>? LoadDungeonEncounters(ZipArchive zip, List<Dungeon>? dungeons, List<DungeonMapLine>? maps, List<BattleEntry>? battles)
    {
        const string file = DungeonEncounterMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (dungeons is null) throw new PackFormatException($"{file}가 있으려면 {DungeonMasterPath}도 필요합니다.");
        if (maps is null) throw new PackFormatException($"{file}가 있으려면 {DungeonMapMasterPath}도 필요합니다.");
        if (battles is null) throw new PackFormatException($"{file}가 있으려면 {BattleMasterPath}도 필요합니다.");

        var dungeonCodes = dungeons.Select(d => d.Code).ToHashSet();
        var layers = maps.Select(m => (m.DungeonCode, m.LayerNumber)).ToHashSet();
        var battleCodes = battles.Select(b => b.Code).ToHashSet();
        var rows = new List<DungeonEncounter>();
        var seen = new HashSet<(long, long, long)>();
        foreach (var r in Body(file, text, "dungeon_code", "layer_number", "weight", "battle_code"))
        {
            var dungeon = ParseLong(file, "던전 코드", r[0]);
            var layer = ParseLong(file, $"던전 '{dungeon}'의 layer_number", r[1]);
            var weight = ParseLong(file, $"던전 '{dungeon}'의 레이어 {layer}의 weight", r[2]);
            var battle = ParseLong(file, $"던전 '{dungeon}'의 레이어 {layer}의 battle_code", r[3]);
            if (!dungeonCodes.Contains(dungeon))
                throw new PackFormatException($"{file}: 던전 코드 '{dungeon}'가 던전 마스터에 없습니다.");
            if (!layers.Contains((dungeon, layer)))
                throw new PackFormatException($"{file}: 던전 '{dungeon}'에 layer_number '{layer}'의 맵이 없습니다.");
            if (weight < 1)
                throw new PackFormatException($"{file}: 던전 '{dungeon}'의 레이어 {layer}의 weight '{weight}'는 1 이상이어야 합니다.");
            if (!battleCodes.Contains(battle))
                throw new PackFormatException($"{file}: 던전 '{dungeon}'의 레이어 {layer}의 전투 '{battle}'가 전투 마스터에 없습니다.");
            if (!seen.Add((dungeon, layer, battle)))
                throw new PackFormatException($"{file}: 같은 키(던전 {dungeon}, layer_number {layer}, 전투 {battle})가 두 번 있습니다.");
            rows.Add(new DungeonEncounter(dungeon, layer, weight, battle));
        }
        return rows;
    }

    /// <summary>
    /// 던전 층 마스터를 읽는다(dungeon_code, layer_number, name). 던전은 던전 마스터에, 그 던전의 맵(layer_number)은 던전 맵 마스터에 있어야 하고,
    /// 이름은 비울 수 없으며 키 (던전, layer_number)를 두 번 쓸 수 없다. 이름이 없는 맵은 "맵 N"으로 보인다.
    /// </summary>
    static List<DungeonLayer>? LoadDungeonLayers(ZipArchive zip, List<Dungeon>? dungeons, List<DungeonMapLine>? maps)
    {
        const string file = DungeonLayerMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (dungeons is null) throw new PackFormatException($"{file}가 있으려면 {DungeonMasterPath}도 필요합니다.");
        if (maps is null) throw new PackFormatException($"{file}가 있으려면 {DungeonMapMasterPath}도 필요합니다.");

        var dungeonCodes = dungeons.Select(d => d.Code).ToHashSet();
        var layers = maps.Select(m => (m.DungeonCode, m.LayerNumber)).ToHashSet();
        var rows = new List<DungeonLayer>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "dungeon_code", "layer_number", "name"))
        {
            var dungeon = ParseLong(file, "던전 코드", r[0]);
            var layer = ParseLong(file, $"던전 '{dungeon}'의 layer_number", r[1]);
            if (!dungeonCodes.Contains(dungeon))
                throw new PackFormatException($"{file}: 던전 코드 '{dungeon}'가 던전 마스터에 없습니다.");
            if (!layers.Contains((dungeon, layer)))
                throw new PackFormatException($"{file}: 던전 '{dungeon}'에 layer_number '{layer}'의 맵이 없습니다.");
            if (r[2].Trim() == "")
                throw new PackFormatException($"{file}: 던전 '{dungeon}'의 레이어 {layer}의 이름이 비어 있습니다.");
            if (!seen.Add((dungeon, layer)))
                throw new PackFormatException($"{file}: 같은 키(던전 {dungeon}, layer_number {layer})가 두 번 있습니다.");
            rows.Add(new DungeonLayer(dungeon, layer, r[2]));
        }
        return rows;
    }

    /// <summary>던전의 맵(layer_number)의 이름. 층 마스터에 없으면 null.</summary>
    public string? DungeonLayerNameOf(long dungeonCode, long layerNumber) =>
        DungeonLayers?.FirstOrDefault(l => l.DungeonCode == dungeonCode && l.LayerNumber == layerNumber)?.Name;

    /// <summary>한 던전의 한 맵(layer_number)에서 만날 수 있는 전투들(전투 코드 순). 없으면 빈 목록(그 맵에서는 인카운트하지 않는다).</summary>
    public IReadOnlyList<DungeonEncounter> DungeonEncountersOf(long dungeonCode, long layerNumber) =>
        Masters.DungeonEncounterRowTable.FindByDungeonCode(dungeonCode)
            .Where(r => r.LayerNumber == layerNumber)
            .OrderBy(r => r.BattleCode)
            .Select(r => new DungeonEncounter(r.DungeonCode, r.LayerNumber, r.Weight, r.BattleCode)).ToList();

    /// <summary>공백이 아닌 보이는 글자 하나인가(조합 문자나 이모지는 하나로 센다).</summary>
    static bool IsSingleVisibleChar(string s) =>
        s.Length > 0 && !s.Any(char.IsWhiteSpace) && new StringInfo(s).LengthInTextElements == 1;

    /// <summary>한 타일의 효과들(효과 번호 순).</summary>
    public IReadOnlyList<TileEffect> TileEffectsOf(long tileCode) =>
        Masters.TileEffectRowTable.FindByTileCode(tileCode)
            .OrderBy(r => (int)r.EffectType)
            .Select(r => new TileEffect(r.TileCode, r.EffectType, r.Value1, r.Value2, r.Value3)).ToList();

    /// <summary>그 던전의 맵에서 글자가 가리키는 타일 코드. 없으면 null.</summary>
    public long? DungeonTileCodeOf(long dungeonCode, string localTileText) =>
        Masters.DungeonTileRowTable.TryFindByDungeonCodeAndLocalTileText((dungeonCode, localTileText), out var r) ? r.TileCode : null;

    /// <summary>던전의 모든 맵 줄들(layer_number 순, 같은 맵 안에서는 pos_y 순).</summary>
    public IReadOnlyList<DungeonMapLine> DungeonMapOf(long dungeonCode) =>
        Masters.DungeonMapRowTable.FindByDungeonCode(dungeonCode)
            .OrderBy(r => r.LayerNumber).ThenBy(r => r.PosY)
            .Select(r => new DungeonMapLine(r.DungeonCode, r.LayerNumber, r.PosY, r.Cells)).ToList();

    /// <summary>던전의 맵 하나(layer_number번)의 줄들(pos_y 순). 없으면 빈 목록.</summary>
    public IReadOnlyList<DungeonMapLine> DungeonMapOf(long dungeonCode, long layerNumber) =>
        DungeonMapOf(dungeonCode).Where(l => l.LayerNumber == layerNumber).ToList();

    /// <summary>던전에 있는 맵들의 layer_number(오름차순).</summary>
    public IReadOnlyList<long> DungeonLayersOf(long dungeonCode) =>
        DungeonMapOf(dungeonCode).Select(l => l.LayerNumber).Distinct().ToList();
}
