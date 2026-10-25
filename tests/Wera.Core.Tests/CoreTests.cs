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

    /// <summary>
    /// 필수 마스터(<see cref="GamePack.RequiredMasterPaths"/>) 중 빠진 것을 기본값으로 채워서 zip을 만든다.
    /// 캐릭터 마스터가 있으면 거기 적힌 모든 캐릭터에게 level/hp 행(레벨 0)을 만들어 준다(HP는 1/1이라 새 캐릭터는 레벨 1, HP 1/1, 경험치 0/0).
    /// 직접 적은 캐릭터 마스터에는 플레이어(캐릭터 1)가 없으면 맨 끝에 붙인다. 나머지 이미 있는 마스터는 건드리지 않으므로,
    /// 필수 항목을 일부러 빼서 시험하는 테스트는 <see cref="Zip"/>으로 직접 만들어야 한다.
    /// </summary>
    internal static MemoryStream PackZip(params (string name, string content)[] files)
    {
        var all = files.ToList();
        string? Find(string name) => all.Where(f => f.name == name).Select(f => f.content).FirstOrDefault();
        void AddIfMissing(string name, string content) { if (Find(name) is null) all.Add((name, content)); }

        AddIfMissing(GamePack.AttributeMasterPath, "attribute_code\tattribute_name\n1\t불\n2\t물\n");
        AddIfMissing(GamePack.TagMasterPath, "tag_code\ttag_name\n1\t플레이어\n2\t리더\n");
        AddIfMissing(GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n3\txp\n");
        AddIfMissing(GamePack.CharacterInventoryMasterPath,
            "character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n");
        AddIfMissing(GamePack.CharacterTagMasterPath, "character_code\ttag_code\n1\t1\n");
        AddIfMissing(GamePack.TimeSlotMasterPath, "code\tname\n1\t아침\n");
        AddIfMissing(GamePack.TurnMasterPath, "turn_count\tyear\tmonth\tday\ttime_slot_code\n1\t1\t1\t1\t1\n");

        // 직접 적은 캐릭터 마스터에 플레이어(캐릭터 1)가 없으면 맨 끝에 붙인다(보유하지 않으면 목록에서 회색이라 대부분의 시험에 영향이 없다).
        var characterIndex = all.FindIndex(f => f.name == GamePack.CharacterInventoryMasterPath);
        var characterText = all[characterIndex].content;
        if (!Tsv.Parse(characterText).Skip(1).Any(r => r.Length > 0 && r[0] == "1"))
            all[characterIndex] = (GamePack.CharacterInventoryMasterPath, characterText + (characterText.EndsWith('\n') ? "" : "\n") + "1\t당신\t1\t1\t2\n");

        var characterCodes = Tsv.Parse(Find(GamePack.CharacterInventoryMasterPath)!)
            .Skip(1).Where(r => r.Length > 0 && r[0].Length > 0).Select(r => r[0]);
        AddIfMissing(GamePack.CharacterStatusMasterPath,
            "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n"
            + string.Concat(characterCodes.Select(c => $"{c}\t1\t0\t1\t1\t99\n{c}\t2\t0\t1\t0\t1\n")));
        return Zip(all.ToArray());
    }

    /// <summary><see cref="PackZip"/>에서 필수 마스터 하나(경로)만 뺀 zip. 그 마스터가 없을 때의 로드 오류를 시험한다.</summary>
    internal static MemoryStream PackZipWithout(string path, params (string name, string content)[] files)
    {
        using var source = new ZipArchive(PackZip(files), ZipArchiveMode.Read);
        var kept = source.Entries.Where(e => e.FullName != path).Select(e =>
        {
            using var reader = new StreamReader(e.Open(), new UTF8Encoding(false));
            return (e.FullName, reader.ReadToEnd());
        }).ToArray();
        return Zip(kept);
    }

    static MemoryStream SamplePack() => PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"));

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
        var pack = GamePack.Load(PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
            ("scenes.tsv", "id\ttext\na\tx\n"),
            ("choices.tsv", "scene\tlabel\tnext\n")));
        Assert.Equal("demo", pack.Id);
    }

    public static IEnumerable<object[]> RequiredMasters => GamePack.RequiredMasterPaths.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(RequiredMasters))]
    public void Every_master_that_the_engine_validates_must_exist_when_the_pack_is_loaded(string path)
    {
        var error = Assert.Throws<PackFormatException>(() =>
            GamePack.Load(PackZipWithout(path, ("pack.tsv", "id\tdemo\ntitle\t데모\n"))));
        Assert.Contains(path, error.Message);
        Assert.Contains("반드시 필요", error.Message);
    }

    [Fact]
    public void The_required_masters_are_the_validated_ones()
    {
        Assert.Equal(
            ["master/attribute_master_v1.tsv", "master/tag_master_v1.tsv", "master/status_master_v1.tsv",
             "master/character_inventory_master_v1.tsv", "master/character_tag_master_v1.tsv", "master/character_status_master_v1.tsv",
             "master/time_slot_master_v1.tsv", "master/turn_master_v1.tsv"],
            GamePack.RequiredMasterPaths);
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
