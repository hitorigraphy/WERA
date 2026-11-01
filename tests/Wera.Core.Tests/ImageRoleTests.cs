using System.IO.Compression;
using System.Text;
using Wera.Core;

namespace Wera.Core.Tests;

// 이미지 역할(image_role): 같은 대상에 아이콘(정사각형 칸)과 포트레이트(300x600 상세 칸)를 따로 등록한다. 비우면 공용(기존과 같음).
public class ImageRoleTests
{
    const string Header = "image_code\tkind\ttarget_code\tvariation_number\tfile\timage_role\n";

    static GamePack PackWith(string imageMaster, params (string Path, byte[] Data)[] files)
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        var source = new MemoryStream();
        using (var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra")))
        using (var from = new ZipArchive(read))
        using (var to = new ZipArchive(source, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var e in from.Entries.Where(e => e.FullName != GamePack.ImageMasterPath && files.All(f => f.Path != e.FullName)))
            {
                using var input = e.Open();
                using var output = to.CreateEntry(e.FullName).Open();
                input.CopyTo(output);
            }
            using (var w = new StreamWriter(to.CreateEntry(GamePack.ImageMasterPath).Open(), new UTF8Encoding(false)))
                w.Write(imageMaster);
            foreach (var (path, data) in files)
            {
                using var output = to.CreateEntry(path).Open();
                output.Write(data);
            }
        }
        source.Position = 0;
        return GamePack.Load(source);
    }

    static string Error(string imageMaster, params (string Path, byte[] Data)[] files) =>
        Assert.Throws<PackFormatException>(() => PackWith(imageMaster, files)).Message;

    static byte[] Id(byte b) => [b];

    [Fact]
    public void Roles_are_read_by_name_ignoring_case_and_blank_means_shared()
    {
        var pack = PackWith(Header + "1\tCharacter\t1\t0\timage/s.png\t\n2\tCharacter\t1\t0\timage/i.png\ticon\n3\tCharacter\t1\t0\timage/p.png\tPORTRAIT\n",
            ("image/s.png", Id(1)), ("image/i.png", Id(2)), ("image/p.png", Id(3)));
        Assert.Equal([ImageRole.Shared, ImageRole.Icon, ImageRole.Portrait], pack.Images!.Select(i => i.Role));
        Assert.Equal([1], pack.ImageFor(ImageKind.Character, 1)!.Bytes);
        Assert.Equal([2], pack.ImageFor(ImageKind.Character, 1, 0, ImageRole.Icon)!.Bytes);
        Assert.Equal([3], pack.ImageFor(ImageKind.Character, 1, 0, ImageRole.Portrait)!.Bytes);
    }

    [Fact]
    public void A_pack_without_the_role_column_behaves_as_before()
    {
        var pack = PackWith("image_code\tkind\ttarget_code\tvariation_number\tfile\n1\tCharacter\t1\t0\timage/s.png\n", ("image/s.png", Id(1)));
        Assert.All(pack.Images!, i => Assert.Equal(ImageRole.Shared, i.Role));
        Assert.Equal([1], pack.ImageFor(ImageKind.Character, 1, 0, ImageRole.Icon)!.Bytes);       // 역할 이미지가 없으면 공용
        Assert.Equal([1], pack.ImageFor(ImageKind.Character, 1, 0, ImageRole.Portrait)!.Bytes);
    }

    [Fact]
    public void Lookup_tries_role_variation_then_role_0_then_shared_variation_then_shared_0()
    {
        // 대상 1: 공용 0, 공용 2, 아이콘 0, 아이콘 3. 요청한 변형은 2와 3, 5.
        var pack = PackWith(Header
            + "1\tCharacter\t1\t0\timage/s0.png\t\n2\tCharacter\t1\t2\timage/s2.png\t\n3\tCharacter\t1\t0\timage/i0.png\tIcon\n4\tCharacter\t1\t3\timage/i3.png\tIcon\n",
            ("image/s0.png", Id(10)), ("image/s2.png", Id(12)), ("image/i0.png", Id(20)), ("image/i3.png", Id(23)));
        byte B(long variation, ImageRole role) => pack.ImageFor(ImageKind.Character, 1, variation, role)!.Bytes[0];
        Assert.Equal(23, B(3, ImageRole.Icon));                                      // 그 역할의 그 변형
        Assert.Equal(20, B(5, ImageRole.Icon));                                      // 그 역할의 기본(0)이 공용의 그 변형보다 먼저
        Assert.Equal(20, B(2, ImageRole.Icon));                                      // 공용 변형 2가 있어도 아이콘 0이 먼저다
        Assert.Equal(12, B(2, ImageRole.Portrait));                                  // 포트레이트가 없으면 공용의 그 변형
        Assert.Equal(10, B(5, ImageRole.Portrait));                                  // 그것도 없으면 공용의 기본
        Assert.Equal(12, B(2, ImageRole.Shared));                                    // 역할 없이 찾으면 공용만 본다(아이콘 이미지는 안 쓴다)
        Assert.Equal(10, B(0, ImageRole.Shared));
        Assert.Null(pack.ImageFor(ImageKind.Character, 7001, 0, ImageRole.Icon));
    }

    [Fact]
    public void An_icon_only_target_has_no_shared_image()
    {
        var pack = PackWith(Header + "1\tCharacter\t1\t0\timage/i.png\tIcon\n", ("image/i.png", Id(2)));
        Assert.Equal([2], pack.ImageFor(ImageKind.Character, 1, 0, ImageRole.Icon)!.Bytes);
        Assert.Null(pack.ImageFor(ImageKind.Character, 1, 0, ImageRole.Portrait));   // 포트레이트 칸은 No Image
        Assert.Null(pack.ImageFor(ImageKind.Character, 1));
    }

    [Fact]
    public void ADV_speaker_variation_selection_works_with_the_icon_role()
    {
        var pack = PackWith(Header + "1\tCharacter\t1\t0\timage/s0.png\t\n2\tCharacter\t1\t1\timage/i1.png\tIcon\n",
            ("image/s0.png", Id(10)), ("image/i1.png", Id(21)));
        Assert.Equal(21, pack.ImageFor(ImageKind.Character, 1, 1, ImageRole.Icon)!.Bytes[0]);   // variation_number 1을 아이콘 역할로
        Assert.Equal(10, pack.ImageFor(ImageKind.Character, 1, 4, ImageRole.Icon)!.Bytes[0]);   // 없는 변형은 공용 기본으로
    }

    [Fact]
    public void A_bad_role_and_duplicate_keys_including_the_role_are_load_errors()
    {
        Assert.Contains("역할 'Face'", Error(Header + "1\tCharacter\t1\t0\timage/a.png\tFace\n", ("image/a.png", Id(1))));
        Assert.Contains("역할 '1'", Error(Header + "1\tCharacter\t1\t0\timage/a.png\t1\n", ("image/a.png", Id(1))));      // 숫자는 안 된다
        Assert.Contains("공용", Error(Header + "1\tCharacter\t1\t0\timage/a.png\t\n2\tCharacter\t1\t0\timage/a.png\t\n", ("image/a.png", Id(1))));
        Assert.Contains("Icon", Error(Header + "1\tCharacter\t1\t0\timage/a.png\tIcon\n2\tCharacter\t1\t0\timage/a.png\ticon\n", ("image/a.png", Id(1))));

        // 역할이 다르면 같은 대상·변형이어도 된다(공용 + 아이콘 + 포트레이트).
        var pack = PackWith(Header + "1\tCharacter\t1\t0\timage/a.png\t\n2\tCharacter\t1\t0\timage/a.png\tIcon\n3\tCharacter\t1\t0\timage/a.png\tPortrait\n", ("image/a.png", Id(1)));
        Assert.Equal(3, pack.Images!.Count);
    }

    [Fact]
    public void The_sample_pack_has_a_portrait_for_the_player_and_slots_use_the_shared_image()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var stream = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(stream);
        var portrait = pack.ImageFor(ImageKind.Character, 1, 0, ImageRole.Portrait)!;
        var shared = pack.ImageFor(ImageKind.Character, 1)!;
        Assert.NotSame(portrait, shared);
        Assert.NotEqual(portrait.Bytes, shared.Bytes);
        Assert.Same(shared, pack.ImageFor(ImageKind.Character, 1, 0, ImageRole.Icon));           // 아이콘 이미지가 없으니 공용
        Assert.Same(pack.ImageFor(ImageKind.Character, 7001), pack.ImageFor(ImageKind.Character, 7001, 0, ImageRole.Portrait));   // 7001은 공용 하나뿐
    }

    [Fact]
    public void Layer_merge_overrides_by_the_role_key_and_keeps_other_roles()
    {
        var spec = PackMasterKeys.For(GamePack.ImageMasterPath);
        Assert.Equal(["image_role"], spec.OptionalKey);
        var merged = GamePack.MergeTsv(GamePack.ImageMasterPath, spec, [
            ("base", Header + "1\tCharacter\t1\t0\timage/s.png\t\n2\tCharacter\t1\t0\timage/i.png\tIcon\n"),
            ("mod", Header + "9\tCharacter\t1\t0\timage/i2.png\ticon\n"),                         // 같은 (kind, target, variation, 역할)이라 2번 행을 대신한다
        ]);
        var rows = Tsv.Parse(merged).Skip(1).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(["1", "9"], rows.Select(r => r[0]));
        Assert.Equal("image/i2.png", rows[1][4]);

        // 한쪽 레이어에 역할 열이 없어도 합친다(없는 열은 공용으로 본다): 공용 행끼리 덮어쓴다.
        var withoutColumn = GamePack.MergeTsv(GamePack.ImageMasterPath, spec, [
            ("base", "image_code\tkind\ttarget_code\tvariation_number\tfile\n1\tCharacter\t1\t0\timage/s.png\n"),
            ("mod", Header + "5\tCharacter\t1\t0\timage/s2.png\t\n6\tCharacter\t1\t0\timage/p.png\tPortrait\n"),
        ]);
        var rows2 = Tsv.Parse(withoutColumn).Skip(1).ToList();
        Assert.Equal(2, rows2.Count);
        Assert.Equal(["5", "6"], rows2.Select(r => r[0]));
    }
}
