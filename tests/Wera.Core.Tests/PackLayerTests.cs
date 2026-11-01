using System.IO.Compression;
using System.Reflection;
using System.Text;
using Wera.Core;

namespace Wera.Core.Tests;

// 모드(중첩 팩): 바깥 팩의 pack_master가 중첩 팩들의 순서를 정하고, 같은 마스터는 합쳐 읽으며 같은 키의 행은 나중 팩이 덮어쓴다. 바깥 팩 자신의 파일이 가장 나중이다.
public class PackLayerTests
{
    const string Items = GamePack.CommonItemInventoryMasterPath;
    const string ImageHeader = "image_code\tkind\ttarget_code\tvariation_number\tfile\n";

    static byte[] BytesOf(MemoryStream ms) => ms.ToArray();

    // 중첩 팩 하나(필요한 마스터가 빠진 부분 팩이어도 된다: 합친 결과만 검증된다).
    static byte[] Part(params (string name, string content)[] files) => BytesOf(CoreTests.Zip(files));

    // 바깥 팩: pack.tsv, pack_master(행들)와 파일 이름 → 바이트로 넣을 중첩 팩들. 항목은 주어진 순서로 zip에 들어간다.
    static MemoryStream Wrapper(string packMaster, (string path, byte[] data)[] nested, params (string name, string content)[] own)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Text(string name, string content)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                w.Write(content);
            }
            if (!own.Any(f => f.name == "pack.tsv")) Text("pack.tsv", "id\twrapper\ntitle\t묶음\n");
            Text(GamePack.PackMasterPath, packMaster);
            foreach (var (path, data) in nested)
            {
                using var o = zip.CreateEntry(path).Open();
                o.Write(data);
            }
            foreach (var (name, content) in own) Text(name, content);
        }
        ms.Position = 0;
        return ms;
    }

    static byte[] BasePack() => BytesOf(CoreTests.PackZip(
        ("pack.tsv", "id\tbase\ntitle\t기본 팩\n"),
        (Items, "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n10\t포션\t99\n"),
        (GamePack.ImageMasterPath, ImageHeader + "1\tCharacter\t1\t0\timage/hero.png\n"),
        ("image/hero.png", "BASE-IMAGE")));

    // 모드 A(order 2): 열 순서가 다른 태그 마스터, description 열이 더 있는 아이템(포션을 덮고 11을 더함), 이미지 키를 다른 image_code로 덮는다.
    static byte[] ModA() => Part(
        ("pack.tsv", "id\tmod-a\ntitle\t모드 A\n"),
        (Items, "inventory_code\titem_name\tcarry_limit\tdescription\n10\t고급 포션\t50\t회복약\n11\tA의 11\t10\t\n"),
        (GamePack.TagMasterPath, "tag_name\ttag_code\n새 태그\t20\n"),
        (GamePack.ImageMasterPath, ImageHeader + "9\tCharacter\t1\t0\timage/hero2.png\n"),
        ("image/hero2.png", "MOD-IMAGE"));

    // 모드 B(order 5): 11을 다시 덮고 12를 더한다.
    static byte[] ModB() => Part(
        ("pack.tsv", "id\tmod-b\ntitle\t모드 B\n"),
        (Items, "inventory_code\titem_name\tcarry_limit\n11\tB가 덮음\t7\n12\tB의 12\t3\n"));

    static GamePack LoadWrapper(string? ownItems = null)
    {
        (string name, string content)[] own = ownItems is null ? [] : [(Items, ownItems)];
        // 일부러 모드 B를 zip과 pack_master의 앞에 둔다(order_number가 큰 쪽이 나중에 적용돼야 한다).
        return GamePack.Load(Wrapper(
            "order_number\tpack_file\tmemo\n5\tpacks/mod_b.wra\t나중\n2\tpacks/mod_a.wra\t\n1\tpacks/base.wra\t먼저\n",
            [("packs/mod_b.wra", ModB()), ("packs/mod_a.wra", ModA()), ("packs/base.wra", BasePack())], own));
    }

    [Fact]
    public void Nested_packs_are_merged_in_order_number_with_later_packs_overriding_same_keys()
    {
        var pack = LoadWrapper();
        Assert.Equal("wrapper", pack.Id);                                            // pack.tsv는 바깥 팩의 것
        Assert.Equal("묶음", pack.Title);
        var items = pack.ItemInventory!.ToDictionary(i => i.Code);
        Assert.Equal("금화", items[2].Name);                                          // 기본 팩 그대로
        Assert.Equal("고급 포션", items[10].Name);                                    // 모드 A가 덮음(키 inventory_code)
        Assert.Equal(50, items[10].CarryLimit);
        Assert.Equal("회복약", items[10].Description);                                // 모드 A에만 있는 선택 열
        Assert.Equal("B가 덮음", items[11].Name);                                     // order 5가 order 2를 덮음(zip과 pack_master에서는 B가 앞이어도)
        Assert.Equal("B의 12", items[12].Name);
        Assert.Equal([2L, 10, 11, 12], pack.ItemInventory!.Select(i => i.Code));      // 덮은 행은 원래 자리, 새 행은 뒤에
    }

    [Fact]
    public void Columns_are_matched_by_name_and_new_keys_join_the_master()
    {
        var pack = LoadWrapper();
        var tags = pack.Tags!.ToDictionary(t => t.Code);
        Assert.Equal("새 태그", tags[20].Name);                                       // 열 순서가 달라도 이름으로 맞춘다
        Assert.Equal("플레이어", tags[1].Name);
    }

    [Fact]
    public void An_image_row_with_another_image_code_but_the_same_target_replaces_the_old_row_and_files_override_by_path()
    {
        var pack = LoadWrapper();
        Assert.Single(pack.Images!);
        Assert.Equal(9, pack.Images![0].Code);                                       // image_code 1(기본)이 (kind, target, variation)이 같은 image_code 9로 대체됨
        Assert.Equal("MOD-IMAGE", Encoding.UTF8.GetString(pack.ImageFor(ImageKind.Character, 1)!.Bytes));
    }

    [Fact]
    public void The_outer_packs_own_files_are_applied_last()
    {
        var pack = LoadWrapper("inventory_code\titem_name\tcarry_limit\n12\t바깥이 덮음\t1\n");
        Assert.Equal("바깥이 덮음", pack.ItemInventory!.Single(i => i.Code == 12).Name);
        Assert.Equal("B가 덮음", pack.ItemInventory!.Single(i => i.Code == 11).Name);
    }

    [Fact]
    public void Layers_list_the_applied_packs_in_order_ending_with_the_outer_pack()
    {
        var layers = LoadWrapper().Layers;
        Assert.Equal(["packs/base.wra", "packs/mod_a.wra", "packs/mod_b.wra", "(바깥 팩)"], layers.Select(l => l.Name));
        Assert.Equal([1L, 2, 5], layers.Take(3).Select(l => l.OrderNumber!.Value));
        Assert.Equal(["base", "mod-a", "mod-b", "wrapper"], layers.Select(l => l.Id!));
        Assert.Null(layers[^1].OrderNumber);
    }

    [Fact]
    public void A_pack_without_pack_master_loads_exactly_as_before()
    {
        var pack = GamePack.Load(new MemoryStream(BasePack()));
        Assert.Equal("base", pack.Id);
        Assert.Equal([new PackLayer("(바깥 팩)", null, "base", "기본 팩")], pack.Layers);
        Assert.Equal("포션", pack.ItemInventory!.Single(i => i.Code == 10).Name);
    }

    [Fact]
    public void Composite_key_masters_override_by_all_key_columns()
    {
        var mod = Part(("pack.tsv", "id\tm\ntitle\tm\n"),
            (GamePack.CharacterStatusMasterPath, "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n1\t2\t0\t50\t0\t100\n1\t2\t1\t60\t0\t100\n"));
        var pack = GamePack.Load(Wrapper("order_number\tpack_file\n1\tpacks/base.wra\n2\tpacks/mod.wra\n", [("packs/base.wra", BasePack()), ("packs/mod.wra", mod)]));
        Assert.Equal(100, pack.CharacterStatusAt(1, 2, 0)!.MaxValue);                // (1, 2, 0)은 덮음
        Assert.Equal(50, pack.CharacterStatusAt(1, 2, 0)!.InitialValue);
        Assert.Equal(99, pack.CharacterStatusAt(1, 1, 0)!.MaxValue);                 // (1, 1, 0)은 그대로
        Assert.Equal(60, pack.CharacterStatuses!.Single(s => s is { CharacterCode: 1, StatusCode: 2, Level: 1 }).InitialValue);   // 새 레벨 행은 더해짐
    }

    [Fact]
    public void A_duplicate_key_inside_one_layer_is_still_a_load_error()
    {
        var mod = Part(("pack.tsv", "id\tm\ntitle\tm\n"), (Items, "inventory_code\titem_name\tcarry_limit\n30\t가\t1\n30\t나\t1\n"));
        var error = Assert.Throws<PackFormatException>(() =>
            GamePack.Load(Wrapper("order_number\tpack_file\n1\tpacks/base.wra\n2\tpacks/mod.wra\n", [("packs/base.wra", BasePack()), ("packs/mod.wra", mod)])));
        Assert.Contains("중복", error.Message);
    }

    [Fact]
    public void Masters_without_a_key_are_appended_and_the_loader_reports_what_it_always_did()
    {
        var legacy = (GamePack.AdvFlowConditionMasterPath, "adv_code\tx\n1\t1\n");
        var error = Assert.Throws<PackFormatException>(() =>
            GamePack.Load(Wrapper("order_number\tpack_file\n1\tpacks/base.wra\n2\tpacks/mod.wra\n3\tpacks/mod2.wra\n",
                [("packs/base.wra", BasePack()), ("packs/mod.wra", Part(("pack.tsv", "id\ta\ntitle\ta\n"), legacy)), ("packs/mod2.wra", Part(("pack.tsv", "id\tb\ntitle\tb\n"), legacy))])));
        Assert.Contains("adv_flow_condition", error.Message);

        var appended = GamePack.MergeTsv("master/unknown_master_v1.tsv", PackMasterKeys.For("master/unknown_master_v1.tsv"), [("a", "k\tv\n1\tx\n"), ("b", "v\tk\ny\t1\n")]);
        Assert.Equal("k\tv\n1\tx\n1\ty\n", appended);                                 // 등록되지 않은 마스터는 이어 붙인다(같은 키여도 덮지 않는다)
    }

    [Fact]
    public void A_single_layer_file_is_copied_untouched_so_memo_columns_still_work()
    {
        var mod = Part(("pack.tsv", "id\tm\ntitle\tm\n"), (GamePack.EquipmentSlotMasterPath, "equipment_slot_code\tequipment_slot_name\tmemo\n1\t무기\t메모\n"));
        var pack = GamePack.Load(Wrapper("order_number\tpack_file\n1\tpacks/mod.wra\n2\tpacks/base.wra\n", [("packs/mod.wra", mod), ("packs/base.wra", BasePack())]));
        Assert.Equal("무기", pack.EquipmentSlots!.Single().Name);
    }

    [Fact]
    public void Dungeon_columns_are_ordered_so_the_loader_can_read_entry_columns_added_by_a_mod()
    {
        var merged = GamePack.MergeTsv(GamePack.DungeonMasterPath, PackMasterKeys.For(GamePack.DungeonMasterPath),
            [("base", "code\tname\tencounter_rate\n1\t던전\t5\n"), ("mod", "code\tname\tentry_layer_number\tentry_pos_x\tentry_pos_y\n1\t던전\t0\t1\t2\n")]);
        Assert.Equal("code\tname\tentry_layer_number\tentry_pos_x\tentry_pos_y\tencounter_rate\n1\t던전\t0\t1\t2\t\n", merged);
    }

    [Fact]
    public void Key_cells_compare_numbers_by_value_and_names_ignoring_case()
    {
        var merged = GamePack.MergeTsv(GamePack.TileMasterPath, PackMasterKeys.For(GamePack.TileMasterPath),
            [("a", "code\ttile_effect\tvalue_1\n1\tExit\t0\n2\tWall\t0\n"), ("b", "code\ttile_effect\tvalue_1\n01\texit\t9\n")]);
        Assert.Equal("code\ttile_effect\tvalue_1\n01\texit\t9\n2\tWall\t0\n", merged);
    }

    [Fact]
    public void A_mod_may_not_use_a_different_key_header()
    {
        var error = Assert.Throws<PackFormatException>(() => GamePack.MergeTsv(GamePack.TagMasterPath, PackMasterKeys.For(GamePack.TagMasterPath),
            [("a", "tag_code\ttag_name\n1\t가\n"), ("mod", "code\ttag_name\n1\t나\n")]));
        Assert.Contains("키 열 'tag_code'", error.Message);
    }

    [Fact]
    public void Pack_master_errors_are_reported_in_korean()
    {
        static MemoryStream With(string packMaster, params (string, byte[])[] nested) => Wrapper(packMaster, nested);
        var good = ("packs/base.wra", BasePack());

        var missing = Assert.Throws<PackFormatException>(() => GamePack.Load(With("order_number\tpack_file\n1\tpacks/none.wra\n", good)));
        Assert.Contains("packs/none.wra", missing.Message);
        Assert.Contains("게임팩에 없습니다", missing.Message);

        var notZip = Assert.Throws<PackFormatException>(() => GamePack.Load(With("order_number\tpack_file\n1\tpacks/bad.wra\n", ("packs/bad.wra", "hello"u8.ToArray()))));
        Assert.Contains("zip으로 읽을 수 없습니다", notZip.Message);

        var duplicate = Assert.Throws<PackFormatException>(() => GamePack.Load(With("order_number\tpack_file\n1\tpacks/base.wra\n1\tpacks/base.wra\n", good)));
        Assert.Contains("order_number '1'가 두 번", duplicate.Message);

        var nestedMaster = Part(("pack.tsv", "id\tx\ntitle\tx\n"), (GamePack.PackMasterPath, "order_number\tpack_file\n"));
        var recursion = Assert.Throws<PackFormatException>(() => GamePack.Load(With("order_number\tpack_file\n1\tpacks/deep.wra\n", ("packs/deep.wra", nestedMaster))));
        Assert.Contains("다시 팩을 넣을 수 없습니다", recursion.Message);

        var header = Assert.Throws<PackFormatException>(() => GamePack.Load(With("a\tb\n1\tpacks/base.wra\n", good)));
        Assert.Contains("헤더", header.Message);

        var notNumber = Assert.Throws<PackFormatException>(() => GamePack.Load(With("order_number\tpack_file\nx\tpacks/base.wra\n", good)));
        Assert.Contains("order_number", notNumber.Message);

        var escape = Assert.Throws<PackFormatException>(() => GamePack.Load(With("order_number\tpack_file\n1\t../base.wra\n", good)));
        Assert.Contains("상대 경로", escape.Message);

        var empty = Assert.Throws<PackFormatException>(() => GamePack.Load(With("order_number\tpack_file\n1\t\n", good)));
        Assert.Contains("비어 있습니다", empty.Message);
    }

    [Fact]
    public void The_merged_result_is_validated_like_any_pack()
    {
        // 모드가 없애지 못한 필수 마스터가 없으면 그대로 로드 오류다(여기서는 바깥 팩에 아무 마스터도 없는 빈 묶음).
        var error = Assert.Throws<PackFormatException>(() => GamePack.Load(Wrapper("order_number\tpack_file\n", [])));
        Assert.Contains("반드시 필요", error.Message);
    }

    [Fact]
    public void An_empty_pack_master_is_a_plain_pack_with_the_outers_own_files()
    {
        var ms = BasePack();
        using var source = new ZipArchive(new MemoryStream(ms));
        var own = source.Entries.Where(e => e.FullName != "pack.tsv").Select(e =>
        {
            using var r = new StreamReader(e.Open(), new UTF8Encoding(false));
            return (e.FullName, r.ReadToEnd());
        }).Append(("pack.tsv", "id\tplain\ntitle\t순수\n")).ToArray();
        var pack = GamePack.Load(Wrapper("order_number\tpack_file\n", [], own));
        Assert.Equal("plain", pack.Id);
        Assert.Equal([new PackLayer("(바깥 팩)", null, "plain", "순수")], pack.Layers);
    }

    [Fact]
    public void The_shipped_sample_pack_is_unchanged_by_the_layer_machinery()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        var bytes = File.ReadAllBytes(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(new MemoryStream(bytes));
        Assert.Equal("sample", pack.Id);
        Assert.Single(pack.Layers);

        // 샘플을 통째로 중첩해도(마스터가 한 레이어에만 있으니 그대로 복사) 같은 게임팩이 된다.
        var wrapped = GamePack.Load(Wrapper("order_number\tpack_file\n1\tpacks/sample.wra\n", [("packs/sample.wra", bytes)],
            ("pack.tsv", "id\tsample\ntitle\t샘플 게임\n")));
        Assert.Equal(pack.CharacterInventory!.Count, wrapped.CharacterInventory!.Count);
        Assert.Equal(pack.ItemInventory!.Select(i => i.Name), wrapped.ItemInventory!.Select(i => i.Name));
        Assert.Equal(pack.Images!.Count, wrapped.Images!.Count);
    }

    // ---- 키 등록 ----

    static IEnumerable<string> AllMasterPathConstants() =>
        typeof(GamePack).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.EndsWith("MasterPath", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void Every_master_path_constant_has_a_registered_merge_rule()
    {
        var missing = AllMasterPathConstants().Where(p => !PackMasterKeys.All.ContainsKey(p)).ToList();
        Assert.True(missing.Count == 0, "병합 키가 등록되지 않은 마스터(PackMasterKeys.All에 키를 등록하세요): " + string.Join(", ", missing));
        var unknown = PackMasterKeys.All.Keys.Where(p => !AllMasterPathConstants().Contains(p)).ToList();
        Assert.True(unknown.Count == 0, "마스터 경로 상수가 없는 등록: " + string.Join(", ", unknown));
    }

    [Fact]
    public void Every_master_file_of_the_sample_pack_is_registered_and_its_key_columns_exist_in_its_header()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        var problems = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "samples", "sample-pack", "master"), "*.tsv"))
        {
            var path = "master/" + Path.GetFileName(file);
            if (!PackMasterKeys.All.TryGetValue(path, out var spec)) { problems.Add($"{path}: 등록 안 됨"); continue; }
            var header = Tsv.Parse(File.ReadAllText(file))[0];
            foreach (var column in spec.Key.Concat(spec.Alternates.SelectMany(a => a)))
                if (!header.Contains(column) && spec.OptionalKey?.Contains(column) != true) problems.Add($"{path}: 키 열 '{column}'이(가) 헤더에 없음");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Keyed_masters_name_at_least_one_key_column_and_the_registry_has_only_known_kinds()
    {
        foreach (var (path, spec) in PackMasterKeys.All)
            if (spec.Kind == MasterMergeKind.Keyed) Assert.True(spec.Key.Length > 0, path);
            else Assert.True(spec.Key.Length == 0 && spec.Alternates.Length == 0, path);
    }
}
