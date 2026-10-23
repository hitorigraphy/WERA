using System.IO.Compression;
using System.Text;
using Wera.Core;

namespace Wera.Core.Tests;

public class CoreTests
{
    internal static MemoryStream Zip(params (string name, string content)[] files)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, content) in files)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                w.Write(content);
            }
        ms.Position = 0;
        return ms;
    }

    static MemoryStream SamplePack() => Zip(("pack.tsv", "id\tdemo\ntitle\t데모\n"));

    [Fact]
    public void Tsv_handles_quotes_newlines_bom_and_crlf()
    {
        var rows = Tsv.Parse("﻿a\tb\r\n\"x\ty\"\t\"he said \"\"hi\"\"\"\r\n\r\nlast\t");
        Assert.Equal(3, rows.Count);
        Assert.Equal(["a", "b"], rows[0]);
        Assert.Equal(["x\ty", "he said \"hi\""], rows[1]);
        Assert.Equal(["last", ""], rows[2]);
    }

    [Fact]
    public void Pack_needs_only_pack_tsv_with_id_and_title()
    {
        var pack = GamePack.Load(SamplePack());
        Assert.Equal("demo", pack.Id);
        Assert.Equal("데모", pack.Title);
        Assert.Null(pack.Dungeons);
        Assert.Null(pack.Shops);
    }

    [Fact]
    public void Pack_rejects_missing_pack_tsv_or_missing_id_or_title()
    {
        Assert.Throws<PackFormatException>(() => GamePack.Load(Zip(("other.tsv", "a\tb\n"))));
        Assert.Throws<PackFormatException>(() => GamePack.Load(Zip(("pack.tsv", "title\tt\n"))));
        Assert.Throws<PackFormatException>(() => GamePack.Load(Zip(("pack.tsv", "id\tx\n"))));
    }

    [Fact]
    public void Leftover_scene_files_from_older_packs_are_ignored()
    {
        var pack = GamePack.Load(Zip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
            ("scenes.tsv", "id\ttext\na\tx\n"),
            ("choices.tsv", "scene\tlabel\tnext\n")));
        Assert.Equal("demo", pack.Id);
    }

    [Fact]
    public void Save_from_other_pack_or_garbage_is_rejected()
    {
        var pack = GamePack.Load(SamplePack());
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, "{\"Version\":1,\"PackId\":\"other\"}"));
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, "not json"));
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, "null"));
    }

    [Fact]
    public void Save_roundtrip_has_no_scene_fields_and_ignores_them_when_present()
    {
        var pack = GamePack.Load(SamplePack());
        var json = GameSession.NewGame(pack).ToSaveJson();
        Assert.DoesNotContain("SceneId", json);

        // 장면 방식을 쓰던 옛 세이브의 SceneId는 무시하고 그대로 불러온다.
        var loaded = GameSession.FromSaveJson(pack, "{\"Version\":1,\"PackId\":\"demo\",\"SceneId\":\"a\"}");
        Assert.Equal(Screen.Home, loaded.Screen);
    }
}
