using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Wera.Core;

/// <summary>
/// 합쳐 읽은 팩 하나(레이어): 중첩 팩 파일이거나 바깥 팩 자신. <paramref name="OrderNumber"/>는 pack_master의 order_number(바깥 팩 자신은 null, 가장 나중에 적용됨),
/// <paramref name="Id"/>와 <paramref name="Title"/>은 그 팩의 pack.tsv 값(없으면 null)이다. 표시용이며 바깥 팩의 값만 게임팩의 id와 제목이 된다.
/// </summary>
public sealed record PackLayer(string Name, long? OrderNumber, string? Id, string? Title);

// 모드: 게임팩 안에 다른 게임팩들을 넣을 수 있다. 바깥 팩의 master/pack_master_v1.tsv(order_number, pack_file)가 어느 파일(바깥 zip 안의 경로)을
// 어떤 순서로 쓸지 정한다. order_number가 작은 팩부터 차례로 얹고, 같은 마스터는 합치며, 같은 키의 행은 나중 팩이 덮어쓴다. 바깥 팩 자신의 파일이 가장 나중이다.
// 합치기는 로더보다 앞서 텍스트로 하고, 합친 결과를 메모리 zip으로 만들어 기존 로더에 그대로 넘긴다(그래서 모든 검증이 합친 결과에 적용된다).
// pack_master가 없으면 아무것도 하지 않고 지금까지와 똑같이 읽는다.
public sealed partial class GamePack
{
    public const string PackMasterPath = "master/pack_master_v1.tsv";

    /// <summary>한 바깥 팩이 담을 수 있는 중첩 팩의 수.</summary>
    public const int MaxNestedPacks = 32;

    /// <summary>중첩 팩 파일 하나의 최대 크기(바이트, 압축된 zip 그대로). 메모리로 읽어 열기 때문에 제한한다.</summary>
    public const long MaxNestedPackBytes = 64L * 1024 * 1024;

    /// <summary>합쳐 읽은 레이어들(적용 순서: 중첩 팩들을 order_number 순으로, 마지막이 바깥 팩 자신). pack_master가 없으면 바깥 팩 하나뿐이다.</summary>
    public IReadOnlyList<PackLayer> Layers { get; init; } = [];

    // 레이어 하나: 이름(표시), 열어 둔 zip(바깥 팩이나 중첩 팩).
    sealed record LayerZip(string Name, ZipArchive Zip);

    /// <summary>
    /// 바깥 팩에 pack_master가 있으면 중첩 팩들을 합친 메모리 zip(과 레이어 목록)을 돌려주고, 없으면 원래 스트림과 null을 돌려준다.
    /// 합친 zip의 pack.tsv는 바깥 팩의 것이다(중첩 팩의 pack.tsv는 읽지 않는다).
    /// </summary>
    static (Stream Stream, IReadOnlyList<PackLayer>? Layers) ResolveLayers(Stream zipStream)
    {
        using var outer = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        if (FindEntry(outer, PackMasterPath) is null) return (zipStream, null);

        var packMaster = TryReadEntry(outer, PackMasterPath)!;
        var nestedRows = new List<(long Order, string File)>();
        var orders = new HashSet<long>();
        foreach (var r in Body(PackMasterPath, packMaster, "order_number", "pack_file"))
        {
            var order = ParseLong(PackMasterPath, "order_number", r[0]);
            var file = r[1].Trim();
            if (!orders.Add(order))
                throw new PackFormatException($"{PackMasterPath}: order_number '{order}'가 두 번 있습니다.");
            if (file.Length == 0)
                throw new PackFormatException($"{PackMasterPath}: order_number '{order}'의 pack_file이 비어 있습니다.");
            if (file.StartsWith('/') || file.StartsWith('\\') || file.Split('/', '\\').Any(part => part is ".." or ""))
                throw new PackFormatException($"{PackMasterPath}: order_number '{order}'의 pack_file '{file}'는 바깥 게임팩 안의 상대 경로여야 합니다('..'나 맨 앞의 '/'를 쓸 수 없습니다).");
            nestedRows.Add((order, file));
        }
        if (nestedRows.Count > MaxNestedPacks)
            throw new PackFormatException($"{PackMasterPath}: 중첩 팩은 최대 {MaxNestedPacks}개입니다({nestedRows.Count}개).");
        nestedRows.Sort((a, b) => a.Order.CompareTo(b.Order));

        var opened = new List<LayerZip>();
        var layers = new List<PackLayer>();
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var (order, file) in nestedRows)
            {
                var entry = FindEntry(outer, file)
                    ?? throw new PackFormatException($"{PackMasterPath}: order_number '{order}'의 pack_file '{file}'가 게임팩에 없습니다(대소문자를 구분합니다).");
                if (entry.Length > MaxNestedPackBytes)
                    throw new PackFormatException($"{PackMasterPath}: pack_file '{file}'가 너무 큽니다({entry.Length} 바이트, 최대 {MaxNestedPackBytes} 바이트).");
                var bytes = new MemoryStream();
                using (var input = entry.Open()) input.CopyTo(bytes);
                bytes.Position = 0;
                ZipArchive nested;
                try { nested = new ZipArchive(bytes, ZipArchiveMode.Read); }
                catch (InvalidDataException)
                {
                    throw new PackFormatException($"{PackMasterPath}: pack_file '{file}'를 zip으로 읽을 수 없습니다.");
                }
                opened.Add(new LayerZip(file, nested));
                consumed.Add(entry.FullName.Replace('\\', '/'));
                if (FindEntry(nested, PackMasterPath) is not null)
                    throw new PackFormatException($"{PackMasterPath}: pack_file '{file}' 안에 {PackMasterPath}가 있습니다(중첩 팩 안에 다시 팩을 넣을 수 없습니다).");
                var (id, title) = PackIdAndTitle(nested);
                layers.Add(new PackLayer(file, order, id, title));
            }

            var (outerId, outerTitle) = PackIdAndTitle(outer);
            layers.Add(new PackLayer("(바깥 팩)", null, outerId, outerTitle));
            opened.Add(new LayerZip("(바깥 팩)", outer));

            return (Merge(opened, consumed), layers);
        }
        finally
        {
            foreach (var layer in opened) if (!ReferenceEquals(layer.Zip, outer)) layer.Zip.Dispose();
        }
    }

    static (string? Id, string? Title) PackIdAndTitle(ZipArchive zip)
    {
        var text = TryReadEntry(zip, "pack.tsv");
        if (text is null) return (null, null);
        string? id = null, title = null;
        foreach (var r in Tsv.Parse(text))
        {
            if (r.Length < 2) continue;
            if (string.Equals(r[0], "id", StringComparison.OrdinalIgnoreCase)) id = r[1];
            else if (string.Equals(r[0], "title", StringComparison.OrdinalIgnoreCase)) title = r[1];
        }
        return (id, title);
    }

    /// <summary>
    /// 레이어들(적용 순서; 마지막이 바깥 팩)을 하나의 메모리 zip으로 합친다. pack.tsv는 바깥 팩의 것만 넣고, 바깥 팩이 쓰는 중첩 팩 파일(<paramref name="consumed"/>)은 넣지 않는다.
    /// 어느 레이어에든 한 곳에만 있는 파일은 그대로 복사하고, 같은 경로가 둘 이상이면 마스터 TSV는 키로 합치고 나머지는 나중 레이어의 것을 쓴다.
    /// </summary>
    static Stream Merge(List<LayerZip> layers, HashSet<string> consumed)
    {
        // 경로 → 그 경로를 가진 (레이어, 항목)들(레이어 순서). 경로는 첫 등장 순서를 지킨다.
        var paths = new Dictionary<string, List<(LayerZip Layer, ZipArchiveEntry Entry)>>(StringComparer.Ordinal);
        var outerLayer = layers[^1];
        foreach (var layer in layers)
            foreach (var entry in layer.Zip.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.Length == 0 || name.EndsWith('/')) continue;
                if (name == "pack.tsv" && !ReferenceEquals(layer, outerLayer)) continue;
                if (ReferenceEquals(layer, outerLayer) && consumed.Contains(name)) continue;
                if (!paths.TryGetValue(name, out var list)) paths[name] = list = [];
                list.Add((layer, entry));
            }

        var output = new MemoryStream();
        using (var merged = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, sources) in paths)
            {
                var target = merged.CreateEntry(path);
                using var writer = target.Open();
                if (sources.Count > 1 && path.StartsWith("master/", StringComparison.Ordinal) && path.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)
                    && PackMasterKeys.For(path) is { Kind: not MasterMergeKind.NotMerged } spec)
                {
                    var bytes = new UTF8Encoding(false).GetBytes(MergeTsv(path, spec, sources.Select(s => (s.Layer.Name, ReadText(s.Entry))).ToList()));
                    writer.Write(bytes);
                }
                else
                {
                    using var input = sources[^1].Entry.Open();      // 한 곳에만 있거나 TSV가 아니면 나중 레이어의 파일을 그대로 쓴다
                    input.CopyTo(writer);
                }
            }
        output.Position = 0;
        return output;
    }

    static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 같은 마스터의 TSV들(레이어 순서)을 하나로 합친 텍스트. 열은 헤더 이름으로 맞추고(없는 열은 빈 칸), 메모 열은 버린다.
    /// <see cref="MasterMergeKind.AppendOnly"/>면 행을 이어 붙이고, <see cref="MasterMergeKind.Keyed"/>면 나중 레이어의 행이 앞선 레이어의 같은 키 행의 자리를 통째로 차지한다
    /// (같은 레이어 안의 중복 키는 그대로 두어 로더가 오류로 알린다).
    /// </summary>
    internal static string MergeTsv(string path, MasterKeySpec spec, List<(string Layer, string Text)> layers)
    {
        var tables = layers.Select(l => (l.Layer, Rows: Tsv.WithoutMemo(Tsv.Parse(l.Text)))).Where(t => t.Rows.Count > 0).ToList();

        // 열: 앞 레이어의 순서를 지키고 새 열은 뒤에 붙인다. 열 순서가 정해진 마스터는 그 순서를 따른다.
        var columns = new List<string>();
        foreach (var (_, rows) in tables)
            foreach (var column in rows[0])
                if (!columns.Contains(column)) columns.Add(column);
        if (spec.ColumnOrder is { } canonical)
            columns = [.. canonical.Where(columns.Contains), .. columns.Where(c => !canonical.Contains(c))];

        var keyed = spec.Kind == MasterMergeKind.Keyed;
        if (keyed)
            foreach (var (layer, rows) in tables)
                foreach (var column in spec.Key.Concat(spec.Alternates.SelectMany(a => a)))
                    if (!rows[0].Contains(column) && spec.OptionalKey?.Contains(column) != true)
                        throw new PackFormatException($"{path}: 팩 '{layer}'의 헤더에 키 열 '{column}'이(가) 없어 합칠 수 없습니다.");

        var result = new List<Dictionary<string, string>?>();
        var owner = new List<int>();
        for (var layerIndex = 0; layerIndex < tables.Count; layerIndex++)
        {
            var rows = tables[layerIndex].Rows;
            var header = rows[0];

            // 앞 레이어의 행만 키로 찾는다(이 레이어가 이미 차지한 자리는 찾지 않는다).
            var byKey = new Dictionary<string, int>(StringComparer.Ordinal);
            var byAlternate = new List<Dictionary<string, int>>(spec.Alternates.Select(_ => new Dictionary<string, int>(StringComparer.Ordinal)));
            if (keyed)
                for (var i = 0; i < result.Count; i++)
                {
                    if (result[i] is not { } row || owner[i] >= layerIndex) continue;
                    byKey.TryAdd(KeyOf(spec.Key, row), i);
                    for (var a = 0; a < spec.Alternates.Length; a++) byAlternate[a].TryAdd(KeyOf(spec.Alternates[a], row), i);
                }

            foreach (var raw in rows.Skip(1))
            {
                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var column in columns) row[column] = "";
                for (var i = 0; i < header.Length && i < raw.Length; i++) row[header[i]] = raw[i];

                if (keyed)
                {
                    var hits = new SortedSet<int>();
                    if (byKey.TryGetValue(KeyOf(spec.Key, row), out var primary) && result[primary] is not null && owner[primary] < layerIndex) hits.Add(primary);
                    for (var a = 0; a < spec.Alternates.Length; a++)
                        if (byAlternate[a].TryGetValue(KeyOf(spec.Alternates[a], row), out var alt) && result[alt] is not null && owner[alt] < layerIndex) hits.Add(alt);
                    if (hits.Count > 0)
                    {
                        var first = hits.Min;
                        foreach (var extra in hits.Skip(1)) result[extra] = null;      // 다른 키로 겹친 행도 이 행이 대신한다
                        result[first] = row;
                        owner[first] = layerIndex;
                        continue;
                    }
                }
                result.Add(row);
                owner.Add(layerIndex);
            }
        }

        var text = new StringBuilder();
        text.Append(string.Join('\t', columns.Select(Quote))).Append('\n');
        foreach (var row in result)
            if (row is not null) text.Append(string.Join('\t', columns.Select(c => Quote(row[c])))).Append('\n');
        return text.ToString();
    }

    // 키 칸 비교: 정수는 쓴 모양(01, 1)과 상관없이 같고, 이름 칸은 대소문자를 가리지 않는다(로더가 가리지 않는 것과 같다). 던전 맵의 글자 칸은 대소문자를 구분한다.
    static string KeyOf(string[] columns, Dictionary<string, string> row) =>
        string.Join('\u001f', columns.Select(c =>
        {
            var cell = row.GetValueOrDefault(c, "").Trim();
            if (long.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n.ToString(CultureInfo.InvariantCulture);
            return c == "local_tile_text" ? row.GetValueOrDefault(c, "") : cell.ToLowerInvariant();
        }));

    static string Quote(string cell) =>
        cell.IndexOfAny(['\t', '\n', '\r', '"']) < 0 ? cell : "\"" + cell.Replace("\"", "\"\"") + "\"";
}
