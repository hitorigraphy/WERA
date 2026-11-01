using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Wera.Core;

/// <summary>
/// 게임 설정의 종류. TSV에는 이름(대소문자 무시)으로 적고, 종류마다 엔진이 어떤 값(<see cref="GameSettingValueType"/>)을 어디에 쓰는지 여기에 정한다.
/// 설정 마스터에 행이 없으면 엔진의 기본값을 쓴다.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GameSettingType>))]
public enum GameSettingType
{
    /// <summary>아무 설정도 아니다(자리 표시). 엔진은 이 값을 읽지 않는다.</summary>
    None = 0,
    /// <summary>유리속성으로 때릴 때의 배율(Decimal, 0 이상, 기본 1.5). 유리와 불리가 겹치면 두 배율을 곱한다.</summary>
    AttributeAdvantageMultiplier = 1,
    /// <summary>불리속성으로 때릴 때의 배율(Decimal, 0 이상, 기본 0.5).</summary>
    AttributeDisadvantageMultiplier = 2,
    /// <summary>목록 화면 한 페이지에 보이는 항목 수(Long, 1 이상, 기본 20).</summary>
    ListPageSize = 3,
    /// <summary>유니크 아이템 1개를 강화하려면 제물로 바쳐야 하는 같은 유니크 아이템의 수(Long, 1 이상, 기본 1).</summary>
    SynthesisSacrificeCount = 4,
    /// <summary>유니크 아이템 강화 레벨의 상한(Long, 0 이상, 행이 없으면 상한 없음). 합성이 끝나면 강화 레벨(강화 횟수)이 이 값을 넘지 못하게 자르고,
    /// 유니크 아이템 강화 스테이터스 마스터에 행이 있는 (아이템, 스테이터스)는 강화 레벨 1~이 값이 모두 적혀 있어야 로드된다.</summary>
    EnhanceLevelMax = 5,
    /// <summary>제련석으로 쓰는 커먼 아이템의 인벤토리 코드(Long, 1 이상, 커먼 아이템 인벤토리 마스터에 있어야 한다). 행이 없으면 제련석이 없어 제련을 할 수 없다.</summary>
    SmeltStoneItemCode = 6,
}

/// <summary>게임 설정 한 행이 쓰는 값의 종류. 종류에 맞는 열(long_value, decimal_value, text_value)이 그 설정의 값이다.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GameSettingValueType>))]
public enum GameSettingValueType
{
    /// <summary>정수(long_value).</summary>
    Long = 0,
    /// <summary>소수(decimal_value).</summary>
    Decimal = 1,
    /// <summary>글(text_value).</summary>
    Text = 2,
}

/// <summary>게임 설정 마스터의 한 행. 값은 ValueType에 맞는 칸만 의미가 있고 나머지는 0/빈 글이다.</summary>
public sealed record GameSettingEntry(GameSettingType SettingType, GameSettingValueType ValueType, long LongValue, decimal DecimalValue, string TextValue);

// game_setting_master(setting_type, value_type, long_value, decimal_value, text_value; 첫 열이 키). 선택이다.
public sealed partial class GamePack
{
    public const string GameSettingMasterPath = "master/game_setting_master_v1.tsv";

    /// <summary>게임 설정 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<GameSettingEntry>? GameSettings { get; init; }

    public const decimal DefaultAttributeAdvantageMultiplier = 1.5m;
    public const decimal DefaultAttributeDisadvantageMultiplier = 0.5m;
    public const long DefaultListPageSize = 20;
    public const long DefaultSynthesisSacrificeCount = 1;

    /// <summary>유리속성 배율(설정 AttributeAdvantageMultiplier, 기본 1.5).</summary>
    public decimal AttributeAdvantageMultiplier => GameSettingDecimal(GameSettingType.AttributeAdvantageMultiplier, DefaultAttributeAdvantageMultiplier);

    /// <summary>불리속성 배율(설정 AttributeDisadvantageMultiplier, 기본 0.5).</summary>
    public decimal AttributeDisadvantageMultiplier => GameSettingDecimal(GameSettingType.AttributeDisadvantageMultiplier, DefaultAttributeDisadvantageMultiplier);

    /// <summary>목록 한 페이지의 항목 수(설정 ListPageSize, 기본 20).</summary>
    public int ListPageSize => (int)Math.Min(GameSettingLong(GameSettingType.ListPageSize, DefaultListPageSize), int.MaxValue);

    /// <summary>합성 제물 수(설정 SynthesisSacrificeCount, 기본 1).</summary>
    public long SynthesisSacrificeCount => GameSettingLong(GameSettingType.SynthesisSacrificeCount, DefaultSynthesisSacrificeCount);

    /// <summary>강화 레벨 상한(설정 EnhanceLevelMax). 설정이 없으면 null(상한 없음).</summary>
    public long? EnhanceLevelMax => FindGameSetting(GameSettingType.EnhanceLevelMax) is { ValueType: GameSettingValueType.Long } s ? s.LongValue : null;

    /// <summary>제련석 아이템 코드(설정 SmeltStoneItemCode). 설정이 없으면 null.</summary>
    public long? SmeltStoneItemCode => FindGameSetting(GameSettingType.SmeltStoneItemCode) is { ValueType: GameSettingValueType.Long } s ? s.LongValue : null;

    // 로드 중(속성이 아직 없을 때)에 설정 목록에서 강화 레벨 상한을 읽는다.
    static long? GameSettingLevelMax(List<GameSettingEntry>? settings) =>
        settings?.FirstOrDefault(s => s.SettingType == GameSettingType.EnhanceLevelMax) is { ValueType: GameSettingValueType.Long } s ? s.LongValue : null;

    /// <summary>설정 종류마다 정해진 값 종류와 최솟값. None은 아무 종류나 허용한다.</summary>
    static readonly Dictionary<GameSettingType, (GameSettingValueType ValueType, decimal Min)> SettingRules = new()
    {
        [GameSettingType.AttributeAdvantageMultiplier] = (GameSettingValueType.Decimal, 0),
        [GameSettingType.AttributeDisadvantageMultiplier] = (GameSettingValueType.Decimal, 0),
        [GameSettingType.ListPageSize] = (GameSettingValueType.Long, 1),
        [GameSettingType.SynthesisSacrificeCount] = (GameSettingValueType.Long, 1),
        [GameSettingType.EnhanceLevelMax] = (GameSettingValueType.Long, 0),
        [GameSettingType.SmeltStoneItemCode] = (GameSettingValueType.Long, 1),
    };

    public GameSettingEntry? FindGameSetting(GameSettingType type) => GameSettings?.FirstOrDefault(s => s.SettingType == type);

    /// <summary>정수 설정. 행이 없거나 값의 종류가 Long이 아니면 기본값.</summary>
    public long GameSettingLong(GameSettingType type, long fallback) =>
        FindGameSetting(type) is { ValueType: GameSettingValueType.Long } s ? s.LongValue : fallback;

    /// <summary>소수 설정. 행이 없거나 값의 종류가 Decimal이 아니면 기본값.</summary>
    public decimal GameSettingDecimal(GameSettingType type, decimal fallback) =>
        FindGameSetting(type) is { ValueType: GameSettingValueType.Decimal } s ? s.DecimalValue : fallback;

    /// <summary>글 설정. 행이 없거나 값의 종류가 Text가 아니면 기본값.</summary>
    public string GameSettingText(GameSettingType type, string fallback) =>
        FindGameSetting(type) is { ValueType: GameSettingValueType.Text } s ? s.TextValue : fallback;

    /// <summary>
    /// 게임 설정 마스터: setting_type과 value_type은 이름(대소문자 무시)으로 적고 setting_type은 두 번 쓸 수 없다.
    /// value_type에 맞는 열은 비울 수 없고(Long은 정수, Decimal은 소수, Text는 글) 나머지 두 열은 읽지 않는다.
    /// </summary>
    static List<GameSettingEntry>? LoadGameSettings(ZipArchive zip)
    {
        const string file = GameSettingMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        var rows = new List<GameSettingEntry>();
        var seen = new HashSet<GameSettingType>();
        foreach (var r in Body(file, text, "setting_type", "value_type", "long_value", "decimal_value", "text_value"))
        {
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<GameSettingType>().Contains(r[0], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<GameSettingType>(r[0], ignoreCase: true, out var type))
                throw new PackFormatException($"{file}: 설정 종류 '{r[0]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<GameSettingType>())})");
            var who = $"설정 '{type}'의";
            if (!Enum.GetNames<GameSettingValueType>().Contains(r[1], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<GameSettingValueType>(r[1], ignoreCase: true, out var valueType))
                throw new PackFormatException($"{file}: {who} 값 종류 '{r[1]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<GameSettingValueType>())})");
            if (!seen.Add(type))
                throw new PackFormatException($"{file}: 설정 종류 '{type}'가 두 번 적혀 있습니다.");
            long longValue = 0;
            decimal decimalValue = 0;
            var textValue = "";
            switch (valueType)
            {
                case GameSettingValueType.Long:
                    longValue = ParseLong(file, $"{who} long_value", r[2]);
                    break;
                case GameSettingValueType.Decimal:
                    if (!decimal.TryParse(r[3].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimalValue))
                        throw new PackFormatException($"{file}: {who} decimal_value '{r[3]}'은(는) 소수가 아닙니다.");
                    break;
                default:
                    if (r[4].Trim() == "")
                        throw new PackFormatException($"{file}: {who} text_value가 비어 있습니다.");
                    textValue = r[4];
                    break;
            }
            if (SettingRules.TryGetValue(type, out var rule))
            {
                if (valueType != rule.ValueType)
                    throw new PackFormatException($"{file}: {who} 값 종류는 {rule.ValueType}여야 합니다 ('{r[1]}').");
                var value = valueType == GameSettingValueType.Long ? longValue : decimalValue;
                if (value < rule.Min)
                    throw new PackFormatException($"{file}: {who} 값 '{value}'은(는) {rule.Min} 이상이어야 합니다.");
            }
            rows.Add(new GameSettingEntry(type, valueType, longValue, decimalValue, textValue));
        }
        return rows;
    }
}
