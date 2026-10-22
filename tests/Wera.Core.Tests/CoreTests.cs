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

    static MemoryStream SamplePack() => Zip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
        ("scenes.tsv", "id\ttext\na\t\"첫째 줄\n둘째 줄\"\nb\t끝\n"),
        ("choices.tsv", "scene\tlabel\tnext\na\t간다\tb\n"));

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
    public void Pack_loads_scenes_and_choices()
    {
        var pack = GamePack.Load(SamplePack());
        Assert.Equal("데모", pack.Title);
        Assert.Equal("첫째 줄\n둘째 줄", pack.Scenes["a"].Text);
        Assert.Equal("b", pack.Scenes["a"].Choices[0].Next);
        Assert.Empty(pack.Scenes["b"].Choices);
    }

    [Fact]
    public void Pack_rejects_dangling_choice_target()
    {
        var zip = Zip(
            ("pack.tsv", "id\td\ntitle\tt\nstart\ta\n"),
            ("scenes.tsv", "id\ttext\na\tx\n"),
            ("choices.tsv", "scene\tlabel\tnext\na\tgo\tzzz\n"));
        Assert.Throws<PackFormatException>(() => GamePack.Load(zip));
    }

    [Fact]
    public void Pack_rejects_missing_file()
        => Assert.Throws<PackFormatException>(() => GamePack.Load(Zip(("pack.tsv", "id\tx\n"))));

    [Fact]
    public void Session_save_roundtrip()
    {
        var pack = GamePack.Load(SamplePack());
        var s = GameSession.NewGame(pack);
        s.Choose(0);
        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal("b", loaded.Current.Id);
    }

    [Fact]
    public void Save_from_other_pack_or_garbage_is_rejected()
    {
        var pack = GamePack.Load(SamplePack());
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, "{\"Version\":1,\"PackId\":\"other\",\"SceneId\":\"a\"}"));
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, "not json"));
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, "{\"Version\":1,\"PackId\":\"demo\",\"SceneId\":\"nope\"}"));
    }
}
