using System.IO.Compression;
using System.Text;

namespace Wera.Core;

public sealed class PackFormatException(string message) : Exception(message);

public sealed record Choice(string Label, string Next);

public sealed record Scene(string Id, string Text, IReadOnlyList<Choice> Choices);

/// <summary>
/// 게임팩: TSV 묶음 zip.
///   pack.tsv    key/value 행 (id, title, start)
///   scenes.tsv  헤더: id, text
///   choices.tsv 헤더: scene, label, next
/// </summary>
public sealed class GamePack
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string StartSceneId { get; init; }
    public required IReadOnlyDictionary<string, Scene> Scenes { get; init; }

    public static GamePack Load(Stream zipStream)
    {
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in Tsv.Parse(ReadEntry(zip, "pack.tsv")))
            if (r.Length >= 2) meta[r[0]] = r[1];

        string Need(string key) =>
            meta.TryGetValue(key, out var v) && v.Length > 0
                ? v
                : throw new PackFormatException($"pack.tsv에 '{key}' 항목이 없습니다.");

        var texts = new Dictionary<string, string>();
        foreach (var r in Body(ReadEntry(zip, "scenes.tsv"), "id", "text"))
        {
            if (!texts.TryAdd(r[0], r[1]))
                throw new PackFormatException($"scenes.tsv: 장면 id '{r[0]}'가 중복됩니다.");
        }

        var choices = texts.Keys.ToDictionary(k => k, _ => new List<Choice>());
        foreach (var r in Body(ReadEntry(zip, "choices.tsv"), "scene", "label", "next"))
        {
            if (!choices.TryGetValue(r[0], out var list))
                throw new PackFormatException($"choices.tsv: 알 수 없는 장면 '{r[0]}'.");
            if (!texts.ContainsKey(r[2]))
                throw new PackFormatException($"choices.tsv: '{r[0]}'의 이동 대상 '{r[2]}'가 없습니다.");
            list.Add(new Choice(r[1], r[2]));
        }

        var start = Need("start");
        if (!texts.ContainsKey(start))
            throw new PackFormatException($"시작 장면 '{start}'가 scenes.tsv에 없습니다.");

        return new GamePack
        {
            Id = Need("id"),
            Title = Need("title"),
            StartSceneId = start,
            Scenes = texts.ToDictionary(kv => kv.Key, kv => new Scene(kv.Key, kv.Value, choices[kv.Key])),
        };
    }

    static string ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new PackFormatException($"게임팩에 '{name}'이 없습니다.");
        using var reader = new StreamReader(entry.Open(), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>헤더를 검사하고 데이터 행만 반환한다. 열이 모자란 행은 빈 셀로 채운다.</summary>
    static IEnumerable<string[]> Body(string tsv, params string[] header)
    {
        var rows = Tsv.Parse(tsv);
        if (rows.Count == 0 || !rows[0].Take(header.Length).SequenceEqual(header))
            throw new PackFormatException($"헤더가 '{string.Join("\t", header)}'이어야 합니다.");
        foreach (var raw in rows.Skip(1))
        {
            var r = raw;
            if (r.Length < header.Length) Array.Resize(ref r, header.Length);
            yield return r.Select(c => c ?? "").ToArray();
        }
    }
}
