using Wera.Core;

namespace Wera.Core.Tests;

public class AttributeTests
{
    const string Header = "attribute_code\tattribute_name\n";

    static GamePack Pack(string? attributes)
    {
        var files = new List<(string, string)> { ("pack.tsv", "id\tdemo\ntitle\t데모\n") };
        if (attributes is not null) files.Add(("master/attribute_master_v1.tsv", attributes));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    [Fact]
    public void Attribute_master_is_required_because_characters_point_at_it()
        => Assert.Contains("attribute_master", Assert.Throws<PackFormatException>(() =>
            GamePack.Load(CoreTests.PackZipWithout(GamePack.AttributeMasterPath, ("pack.tsv", "id\tdemo\ntitle\t데모\n")))).Message);

    [Fact]
    public void Attribute_master_loads_code_and_name_in_order()
    {
        var pack = Pack(Header + "1\t불\n2\t물\n5000000000\t큰 속성\n");
        Assert.Equal([new AttributeEntry(1, "불"), new AttributeEntry(2, "물"), new AttributeEntry(5000000000, "큰 속성")], pack.Attributes);
    }

    [Theory]
    [InlineData("1\ta\n1\tb\n")]       // 중복 코드
    [InlineData("\ta\n")]               // 빈 코드
    [InlineData("abc\ta\n")]            // 정수 아님
    [InlineData("1.5\ta\n")]            // 소수
    public void Bad_attribute_rows_are_rejected(string rows)
        => Assert.Throws<PackFormatException>(() => Pack(Header + rows));

    [Theory]
    [InlineData("code\tname\n1\ta\n")]
    [InlineData("attribute_code\n1\n")]
    public void Attribute_master_header_is_checked(string tsv)
        => Assert.Throws<PackFormatException>(() => Pack(tsv));
}
