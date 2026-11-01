using System.IO.Compression;
using System.Text;
using Wera.Core;

namespace Wera.Core.Tests;

// 이미지 마스터: kind/target_code/variation_number로 대상에 붙이는 게임팩 안의 그림. 샘플 게임팩을 바탕으로 마스터와 파일을 바꿔 가며 검증한다.
public class ImageTests
{
    const string Header = "image_code\tkind\ttarget_code\tvariation_number\tfile\n";
    static readonly byte[] Bytes = [1, 2, 3, 4];

    // 샘플 게임팩에서 이미지 마스터를 text로 바꾸고 files(경로 → 바이트)를 더해(같은 경로면 바꿔) 읽는다.
    static GamePack PackWith(string? imageMaster, params (string Path, byte[] Data)[] files)
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
            if (imageMaster is not null)
            {
                using var w = new StreamWriter(to.CreateEntry(GamePack.ImageMasterPath).Open(), new UTF8Encoding(false));
                w.Write(imageMaster);
            }
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

    [Fact]
    public void The_sample_pack_has_images_and_loads_them()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var stream = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(stream);
        Assert.Equal(6, pack.Images!.Count);
        var player = pack.ImageFor(ImageKind.Character, 1)!;
        Assert.Equal("image/png", player.Mime);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], player.Bytes.Take(4));
        Assert.NotNull(pack.ImageFor(ImageKind.CommonItem, 3));
        Assert.NotNull(pack.ImageFor(ImageKind.Attribute, 1));
        Assert.NotNull(pack.ImageFor(ImageKind.Facility, 2));
        Assert.Null(pack.ImageFor(ImageKind.Skill, 1001));                           // 이미지 마스터에 없는 대상
    }

    [Fact]
    public void Lookup_falls_back_from_a_variation_to_variation_0_and_then_to_null()
    {
        var pack = PackWith(Header + "1\tCharacter\t1\t0\timage/a.png\n2\tCharacter\t1\t2\timage/b.webp\n",
            ("image/a.png", [1]), ("image/b.webp", [2]));
        Assert.Equal([1], pack.ImageFor(ImageKind.Character, 1)!.Bytes);
        Assert.Equal([2], pack.ImageFor(ImageKind.Character, 1, 2)!.Bytes);
        Assert.Equal("image/webp", pack.ImageFor(ImageKind.Character, 1, 2)!.Mime);
        Assert.Equal([1], pack.ImageFor(ImageKind.Character, 1, 9)!.Bytes);          // 없는 변형은 기본 이미지
        Assert.Null(pack.ImageFor(ImageKind.Character, 7001));                       // 이미지가 없는 캐릭터
        Assert.Null(pack.ImageFor(ImageKind.Skill, 1));                              // 다른 종류의 같은 코드는 다른 대상
    }

    [Fact]
    public void A_variation_without_a_default_image_has_nothing_to_fall_back_to()
    {
        var pack = PackWith(Header + "1\tCharacter\t1\t3\timage/a.png\n", ("image/a.png", [1]));
        Assert.Equal([1], pack.ImageFor(ImageKind.Character, 1, 3)!.Bytes);
        Assert.Null(pack.ImageFor(ImageKind.Character, 1, 0));
        Assert.Null(pack.ImageFor(ImageKind.Character, 1, 4));
    }

    [Fact]
    public void Rows_pointing_at_the_same_file_share_one_set_of_bytes_and_a_pack_without_the_master_has_no_images()
    {
        var pack = PackWith(Header + "1\tCharacter\t1\t0\timage/a.png\n2\tCharacter\t7001\t0\timage/a.png\n", ("image/a.png", [1]));
        Assert.Same(pack.ImageFor(ImageKind.Character, 1), pack.ImageFor(ImageKind.Character, 7001));
        var none = PackWith(null);
        Assert.Null(none.Images);
        Assert.Null(none.ImageFor(ImageKind.Character, 1));
    }

    [Fact]
    public void Every_kind_is_checked_against_its_own_master()
    {
        var kinds = new (string Kind, long Target)[]
        {
            ("Character", 1), ("UniqueItem", 5001), ("CommonItem", 3), ("Skill", 1001), ("StatusEffect", 1), ("Attribute", 1), ("Facility", 2),
        };
        var rows = string.Concat(kinds.Select((k, i) => $"{i + 1}\t{k.Kind}\t{k.Target}\t0\timage/a.png\n"));
        var uniqueItem = PackWith(null).UniqueItems!.First().ItemCode;
        rows = rows.Replace("UniqueItem\t5001", $"UniqueItem\t{uniqueItem}");
        var pack = PackWith(Header + rows, ("image/a.png", [1]));
        Assert.Equal(7, pack.Images!.Count);
        foreach (var kind in Enum.GetValues<ImageKind>())
        {
            var message = Error(Header + $"1\t{kind}\t999999\t0\timage/a.png\n", ("image/a.png", [1]));
            Assert.Contains("999999", message);
            Assert.Contains("master/", message);
        }
    }

    [Theory]
    [InlineData("1\tCharcter\t1\t0\timage/a.png\n", "종류")]                          // 모르는 종류
    [InlineData("1\t0\t1\t0\timage/a.png\n", "종류")]                                // 숫자는 안 된다
    [InlineData("1\tCharacter\t424242\t0\timage/a.png\n", "424242")]                 // 없는 대상
    [InlineData("1\tCharacter\t1\t-1\timage/a.png\n", "variation_number")]           // 음수 변형
    [InlineData("1\tCharacter\t1\t0\timage/a.png\n1\tCharacter\t7001\t0\timage/a.png\n", "두 번")]   // 같은 이미지 코드
    [InlineData("1\tCharacter\t1\t0\timage/a.png\n2\tCharacter\t1\t0\timage/a.png\n", "두 번")]      // 같은 대상과 변형
    [InlineData("1\tCharacter\t1\t0\timage/missing.png\n", "게임팩에 없습니다")]     // 없는 파일
    [InlineData("1\tCharacter\t1\t0\timage/A.png\n", "게임팩에 없습니다")]           // 대소문자 구분
    [InlineData("1\tCharacter\t1\t0\timage/a.bmp\n", "확장자")]                      // 허용하지 않는 확장자
    [InlineData("1\tCharacter\t1\t0\timage/noext\n", "확장자")]
    [InlineData("1\tCharacter\t1\t0\t../a.png\n", "상대 경로")]                       // 경로 탈출
    [InlineData("1\tCharacter\t1\t0\timage/../a.png\n", "상대 경로")]
    [InlineData("1\tCharacter\t1\t0\t/image/a.png\n", "상대 경로")]
    [InlineData("1\tCharacter\t1\t0\timage\\a.png\n", "상대 경로")]
    [InlineData("1\tCharacter\t1\t0\t\n", "비어 있습니다")]
    public void Bad_rows_are_rejected_at_load(string rows, string expected) =>
        Assert.Contains(expected, Error(Header + rows, ("image/a.png", [1]), ("image/a.bmp", [1]), ("image/noext", [1]), ("a.png", [1])));

    [Fact]
    public void A_file_over_the_size_limit_is_rejected_but_the_limit_itself_is_allowed()
    {
        var atLimit = new byte[GamePack.MaxImageBytes];
        Assert.NotNull(PackWith(Header + "1\tCharacter\t1\t0\timage/big.png\n", ("image/big.png", atLimit)).ImageFor(ImageKind.Character, 1));
        Assert.Contains("2MB", Error(Header + "1\tCharacter\t1\t0\timage/big.png\n", ("image/big.png", new byte[GamePack.MaxImageBytes + 1])));
    }

    [Fact]
    public void The_header_is_checked_and_a_memo_column_is_ignored()
    {
        Assert.Contains("헤더", Error("code\tkind\ttarget_code\tvariation_number\tfile\n"));
        var pack = PackWith("image_code\tkind\ttarget_code\tvariation_number\tfile\tmemo\n1\tcharacter\t1\t0\timage/a.png\t메모\n", ("image/a.png", [1]));
        Assert.Equal(ImageKind.Character, pack.Images![0].Kind);                       // 종류 이름은 대소문자를 가리지 않는다
    }

    [Fact]
    public void Detail_views_and_hideout_rows_carry_the_target_codes_the_screens_need()
    {
        var s = GameSession.NewGame(PackWith(Header + "1\tCharacter\t1\t0\timage/a.png\n", ("image/a.png", Bytes)));
        Assert.Equal(1, s.PartySlotViews.FirstOrDefault(v => v.CharacterCode is not null)?.CharacterCode ?? s.PlayerSlot.CharacterCode);
        Assert.NotNull(s.Pack.ImageFor(ImageKind.Character, s.PlayerSlot.CharacterCode ?? 0));
        Assert.All(s.HideoutRows.Where(r => r.Floor is >= 0), r => Assert.Equal(r.HasFacility, r.BuiltFacilityCode is not null));
    }
}
