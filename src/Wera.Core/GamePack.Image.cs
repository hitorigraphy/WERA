using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 이미지가 붙는 대상의 종류. TSV에는 이름(대소문자 무시)으로 적는다. 화면에 이미지 칸이 있는 곳마다 하나씩 있고, target_code는 그 종류의 마스터 코드다.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ImageKind>))]
public enum ImageKind
{
    /// <summary>캐릭터(캐릭터 인벤토리 코드). 파티 슬롯, 상세 화면, ADV 화자 이미지. 같은 캐릭터의 인스턴스는 모두 이 이미지를 함께 쓴다.</summary>
    Character = 0,
    /// <summary>유니크 아이템(유니크 아이템 인벤토리의 item_code). 장비 상세 화면.</summary>
    UniqueItem = 1,
    /// <summary>커먼 아이템(커먼 아이템 인벤토리 코드). 아이템 상세 화면.</summary>
    CommonItem = 2,
    /// <summary>스킬(스킬 마스터 코드). 스킬 상세 화면.</summary>
    Skill = 3,
    /// <summary>상태이상(상태이상 마스터 코드). 슬롯의 상태이상 아이콘.</summary>
    StatusEffect = 4,
    /// <summary>속성(속성 마스터 코드). 슬롯의 속성 아이콘.</summary>
    Attribute = 5,
    /// <summary>시설(시설 마스터 코드). 아지트 화면의 시설 슬롯 이미지(지은 시설).</summary>
    Facility = 6,
}

/// <summary>
/// 이미지의 쓰임새(역할). TSV의 선택 열 image_role에 이름(대소문자 무시)으로 적고, 비우거나 열이 없으면 <see cref="Shared"/>(공용)다.
/// 같은 대상에 역할별 이미지를 따로 둘 수 있다: 슬롯, ADV 화자, 배지 같은 정사각형 칸은 <see cref="Icon"/>, 300x600 상세 칸은 <see cref="Portrait"/>를 먼저 찾는다.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ImageRole>))]
public enum ImageRole
{
    /// <summary>공용(역할을 비움). 역할 이미지가 없을 때 어느 칸이든 이것을 쓴다(기존 게임팩은 모두 이것이다).</summary>
    Shared = 0,
    /// <summary>아이콘: 슬롯(파티, 홈, 아지트, 전투), ADV 화자, 속성/상태이상 배지처럼 정사각형으로 그리는 칸.</summary>
    Icon = 1,
    /// <summary>포트레이트: 캐릭터, 장비, 개조, 아이템, 스킬 상세 화면의 300x600(1:2) 칸.</summary>
    Portrait = 2,
}

/// <summary>이미지 마스터의 한 행: image_code(키)의 이미지는 Kind의 TargetCode에 Variation번 변형으로, Role(역할)의 이미지로 붙고 File(게임팩 안 경로)이 그림이다.</summary>
public sealed record ImageEntry(long Code, ImageKind Kind, long TargetCode, long Variation, string File, ImageRole Role = ImageRole.Shared);

/// <summary>게임팩에서 읽은 이미지 파일 하나: 브라우저가 이해하는 MIME 종류와 원본 바이트.</summary>
public sealed record ImageData(string Mime, byte[] Bytes);

// image_master(image_code, kind, target_code, variation_number, file, 선택 열 image_role; 첫 열이 키). 선택이다.
// (kind, target_code, variation_number, 역할)도 한 번씩만 쓸 수 있다(역할을 비운 공용도 하나의 역할로 센다). variation_number 0이 기본 이미지다(변형이 없는 곳에서는 늘 0을 찾는다).
// file은 게임팩(zip) 안의 경로다(슬래시 구분, 대소문자 구분, 관례는 image/...). png, jpg, jpeg, webp, gif만 쓸 수 있고 한 파일은 2MB(MaxImageBytes)를 넘을 수 없다.
// 로드할 때 마스터가 가리키는 파일만(같은 파일은 한 번만) 바이트로 읽어 메모리에 둔다. 마스터가 가리키지 않는 파일은 무시한다.
public sealed partial class GamePack
{
    public const string ImageMasterPath = "master/image_master_v1.tsv";

    /// <summary>이미지 파일 한 개의 최대 크기(바이트, 압축을 푼 크기). 넘으면 로드 오류다.</summary>
    public const int MaxImageBytes = 2 * 1024 * 1024;

    static readonly Dictionary<string, string> ImageMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp", [".gif"] = "image/gif",
    };

    /// <summary>이미지 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<ImageEntry>? Images { get; init; }

    // 파일 경로 → 읽어 둔 이미지. 같은 파일을 가리키는 행이 여럿이어도 바이트는 하나다.
    IReadOnlyDictionary<string, ImageData> ImageFiles { get; init; } = new Dictionary<string, ImageData>();

    /// <summary>
    /// 대상의 이미지를 아래 순서로 처음 찾은 것으로 돌려준다(없으면 null이고 화면은 No Image를 그린다). 이미지 마스터가 없는 게임팩은 늘 null이다.
    /// 1) 그 역할(role)의 그 변형(variation), 2) 그 역할의 기본(variation 0), 3) 공용(역할 비움)의 그 변형, 4) 공용의 기본(variation 0).
    /// role이 <see cref="ImageRole.Shared"/>(기본)면 1, 2는 건너뛰어 3, 4만 본다(역할 열이 없던 때와 같다). variation이 0이면 2, 4는 1, 3과 같아 건너뛴다.
    /// </summary>
    public ImageData? ImageFor(ImageKind kind, long targetCode, long variation = 0, ImageRole role = ImageRole.Shared)
    {
        if (Images is null) return null;
        ImageEntry? Find(ImageRole r, long v) => Images.FirstOrDefault(i => i.Kind == kind && i.TargetCode == targetCode && i.Variation == v && i.Role == r);
        var row = (role == ImageRole.Shared ? null : Find(role, variation))
            ?? (role == ImageRole.Shared || variation == 0 ? null : Find(role, 0))
            ?? Find(ImageRole.Shared, variation)
            ?? (variation == 0 ? null : Find(ImageRole.Shared, 0));
        return row is not null && ImageFiles.TryGetValue(row.File, out var data) ? data : null;
    }

    /// <summary>
    /// 이미지 마스터: kind는 이름으로 적는다(숫자는 안 된다). target_code는 그 종류의 마스터에 있어야 하고(마스터가 없으면 그 종류는 쓸 수 없다),
    /// variation_number는 0 이상, file은 게임팩 안에 있는 png/jpg/jpeg/webp/gif(경로에 ..나 빈 칸, 역슬래시, 맨 앞 슬래시가 있으면 안 된다)여야 한다.
    /// 선택 열 image_role(file 바로 뒤, 메모 열 앞)은 <see cref="ImageRole"/> 이름(Icon, Portrait; 대소문자 무시, 숫자는 안 된다)이고 비우거나 열이 없으면 공용이다.
    /// </summary>
    static (List<ImageEntry>? Rows, Dictionary<string, ImageData> Files) LoadImages(
        ZipArchive zip,
        IEnumerable<long>? characters, IEnumerable<long>? uniqueItems, IEnumerable<long>? commonItems, IEnumerable<long>? skills,
        IEnumerable<long>? statusEffects, IEnumerable<long>? attributes, IEnumerable<long>? facilities)
    {
        const string file = ImageMasterPath;
        var files = new Dictionary<string, ImageData>();
        var text = TryReadEntry(zip, file);
        if (text is null) return (null, files);

        var targets = new Dictionary<ImageKind, (string Label, string Master, HashSet<long>? Codes)>
        {
            [ImageKind.Character] = ("캐릭터", CharacterInventoryMasterPath, characters?.ToHashSet()),
            [ImageKind.UniqueItem] = ("유니크 아이템", UniqueItemInventoryMasterPath, uniqueItems?.ToHashSet()),
            [ImageKind.CommonItem] = ("커먼 아이템", CommonItemInventoryMasterPath, commonItems?.ToHashSet()),
            [ImageKind.Skill] = ("스킬", SkillMasterPath, skills?.ToHashSet()),
            [ImageKind.StatusEffect] = ("상태이상", StatusEffectMasterPath, statusEffects?.ToHashSet()),
            [ImageKind.Attribute] = ("속성", AttributeMasterPath, attributes?.ToHashSet()),
            [ImageKind.Facility] = ("시설", FacilityMasterPath, facilities?.ToHashSet()),
        };

        var rows = new List<ImageEntry>();
        var codes = new HashSet<long>();
        var keys = new HashSet<(ImageKind, long, long, ImageRole)>();
        var table = Tsv.WithoutMemo(Tsv.Parse(text));
        string[] required = ["image_code", "kind", "target_code", "variation_number", "file"];
        var hasRole = table.Count > 0 && table[0].Length > required.Length && table[0][required.Length] == "image_role";
        foreach (var r in BodyRows(file, table, hasRole ? [.. required, "image_role"] : required))
        {
            var code = ParseLong(file, "이미지 코드", r[0]);
            var where = $"이미지 '{code}'의";
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<ImageKind>().Contains(r[1], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<ImageKind>(r[1], ignoreCase: true, out var kind))
                throw new PackFormatException($"{file}: {where} 종류 '{r[1]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<ImageKind>())})");
            var target = ParseLong(file, $"{where} target_code", r[2]);
            var variation = ParseLong(file, $"{where} variation_number", r[3]);
            var path = r[4].Trim();
            var role = ImageRole.Shared;
            if (hasRole && r[5].Trim() is { Length: > 0 } roleName)
            {
                if (!Enum.GetNames<ImageRole>().Contains(roleName, StringComparer.OrdinalIgnoreCase)
                    || !Enum.TryParse(roleName, ignoreCase: true, out role))
                    throw new PackFormatException($"{file}: {where} 역할 '{roleName}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<ImageRole>().Skip(1))}; 비우면 공용)");
            }

            if (!codes.Add(code)) throw new PackFormatException($"{file}: 이미지 코드 '{code}'가 두 번 적혀 있습니다.");
            if (variation < 0) throw new PackFormatException($"{file}: {where} variation_number '{variation}'는 0 이상이어야 합니다.");
            if (!keys.Add((kind, target, variation, role)))
                throw new PackFormatException($"{file}: 같은 대상({kind} {target}, variation {variation}, 역할 {(role == ImageRole.Shared ? "공용" : role.ToString())})의 이미지가 두 번 있습니다.");

            var (label, master, known) = targets[kind];
            if (known is null) throw new PackFormatException($"{file}: {where} {kind}이 있으려면 {master}도 필요합니다.");
            if (!known.Contains(target)) throw new PackFormatException($"{file}: {where} {label} '{target}'가 {master}에 없습니다.");

            if (!files.ContainsKey(path)) files[path] = ReadImageFile(zip, file, where, path);
            rows.Add(new ImageEntry(code, kind, target, variation, path, role));
        }
        return (rows, files);
    }

    static ImageData ReadImageFile(ZipArchive zip, string file, string where, string path)
    {
        if (path.Length == 0) throw new PackFormatException($"{file}: {where} file이 비어 있습니다.");
        if (path.StartsWith('/') || path.Contains('\\') || path.Split('/').Any(part => part is "" or "." or ".."))
            throw new PackFormatException($"{file}: {where} file '{path}'는 게임팩 안의 상대 경로(슬래시 구분, ..나 빈 칸 없음)여야 합니다.");
        if (!ImageMimes.TryGetValue(Path.GetExtension(path), out var mime))
            throw new PackFormatException($"{file}: {where} file '{path}'의 확장자는 {string.Join(", ", ImageMimes.Keys)} 중 하나여야 합니다.");
        var entry = zip.GetEntry(path) ?? throw new PackFormatException($"{file}: {where} file '{path}'이(가) 게임팩에 없습니다(대소문자를 구분합니다).");
        if (entry.Length > MaxImageBytes)
            throw new PackFormatException($"{file}: {where} file '{path}'가 {MaxImageBytes / 1024 / 1024}MB를 넘습니다({entry.Length}바이트).");
        using var input = entry.Open();
        using var buffer = new MemoryStream((int)entry.Length);
        input.CopyTo(buffer);
        return new ImageData(mime, buffer.ToArray());
    }
}
