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
            rows.Add(new GameSettingEntry(type, valueType, longValue, decimalValue, textValue));
        }
        return rows;
    }
}
