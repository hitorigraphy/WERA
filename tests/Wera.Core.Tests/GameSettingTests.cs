using Wera.Core;

namespace Wera.Core.Tests;

// 게임 설정 마스터(setting_type, value_type, long_value, decimal_value, text_value).
public class GameSettingTests
{
    const string Head = "setting_type\tvalue_type\tlong_value\tdecimal_value\ttext_value\n";

    static GamePack Pack(string? rows)
    {
        var files = new List<(string, string)> { ("pack.tsv", "id\tdemo\ntitle\t데모\n") };
        if (rows is not null) files.Add((GamePack.GameSettingMasterPath, Head + rows));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    [Fact]
    public void The_game_setting_master_is_optional_and_missing_rows_fall_back()
    {
        var pack = Pack(null);
        Assert.Null(pack.GameSettings);
        Assert.Equal(7, pack.GameSettingLong(GameSettingType.None, 7));
        Assert.Equal(1.5m, pack.GameSettingDecimal(GameSettingType.None, 1.5m));
        Assert.Equal("x", pack.GameSettingText(GameSettingType.None, "x"));
    }

    [Theory]
    [InlineData("None\tLong\t20\t\t\n", GameSettingValueType.Long, 20L, 0, "")]
    [InlineData("none\tdecimal\t\t1.5\t\n", GameSettingValueType.Decimal, 0L, 1.5, "")]
    [InlineData("None\tTEXT\t\t\t안녕\n", GameSettingValueType.Text, 0L, 0, "안녕")]
    public void Only_the_column_of_the_value_type_is_read(string row, GameSettingValueType type, long l, double d, string t)
    {
        var s = Assert.Single(Pack(row).GameSettings!);
        Assert.Equal(new GameSettingEntry(GameSettingType.None, type, l, (decimal)d, t), s);
        Assert.Equal(type == GameSettingValueType.Long ? l : 9, Pack(row).GameSettingLong(GameSettingType.None, 9));   // 다른 종류로 읽으면 기본값
    }

    [Theory]
    [InlineData("Nope\tLong\t1\t\t\n")]                        // 모르는 설정 종류
    [InlineData("0\tLong\t1\t\t\n")]                           // 숫자는 이름이 아니다
    [InlineData("None\tFloat\t1\t\t\n")]                       // 모르는 값 종류
    [InlineData("None\tLong\t\t\t\n")]                         // Long인데 long_value가 비어 있음
    [InlineData("None\tLong\tabc\t\t\n")]                      // 정수가 아님
    [InlineData("None\tDecimal\t\tabc\t\n")]                   // 소수가 아님
    [InlineData("None\tText\t\t\t\n")]                         // Text인데 비어 있음
    [InlineData("None\tLong\t1\t\t\nNone\tLong\t2\t\t\n")]     // 같은 설정 두 번
    public void Bad_rows_are_rejected(string rows) => Assert.Throws<PackFormatException>(() => Pack(rows));

    [Fact]
    public void The_engine_settings_have_defaults_and_can_be_overridden()
    {
        var plain = Pack(null);
        Assert.Equal((1.5m, 0.5m, 20, 1L), (plain.AttributeAdvantageMultiplier, plain.AttributeDisadvantageMultiplier, plain.ListPageSize, plain.SynthesisSacrificeCount));
        var pack = Pack("AttributeAdvantageMultiplier\tDecimal\t\t2\t\nAttributeDisadvantageMultiplier\tDecimal\t\t0.25\t\nListPageSize\tLong\t5\t\t\nSynthesisSacrificeCount\tLong\t3\t\t\n");
        Assert.Equal((2m, 0.25m, 5, 3L), (pack.AttributeAdvantageMultiplier, pack.AttributeDisadvantageMultiplier, pack.ListPageSize, pack.SynthesisSacrificeCount));
    }

    [Theory]
    [InlineData("ListPageSize\tDecimal\t\t5\t\n")]                  // 값 종류가 정해져 있다
    [InlineData("ListPageSize\tLong\t0\t\t\n")]                     // 1 이상
    [InlineData("SynthesisSacrificeCount\tLong\t0\t\t\n")]
    [InlineData("AttributeAdvantageMultiplier\tLong\t2\t\t\n")]
    [InlineData("AttributeDisadvantageMultiplier\tDecimal\t\t-0.5\t\n")]
    public void Engine_settings_check_their_value_type_and_minimum(string rows) => Assert.Throws<PackFormatException>(() => Pack(rows));

    [Fact]
    public void The_list_page_size_setting_drives_the_paging()
    {
        var s = GameSession.NewGame(Pack("ListPageSize\tLong\t1\t\t\n"));
        Assert.Equal(1, s.ListPageSize);
        Assert.Equal(3, s.PageCountOf(3));
    }
}
