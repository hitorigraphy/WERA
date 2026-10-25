using Wera.Core;

namespace Wera.Core.Tests;

public class CharacterInventoryTests
{
    const string Header =
        "character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n";
    const string ItemHeader = "item_code\titem_name\tcarry_limit\n";
    const string Attributes = "attribute_code\tattribute_name\n1\t불\n2\t물\n3\t풀\n";

    static GamePack Pack(string? characters, string? items = null, string? attributes = Attributes)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        };
        if (characters is not null) files.Add(("master/character_inventory_master_v1.tsv", characters));
        if (items is not null) files.Add(("master/common_item_inventory_master_v1.tsv", items));
        if (attributes is not null) files.Add(("master/attribute_master_v1.tsv", attributes));
        return GamePack.Load(CoreTests.Zip(files.ToArray()));
    }

    [Fact]
    public void Character_inventory_is_optional()
        => Assert.Null(Pack(null).CharacterInventory);

    [Fact]
    public void Character_inventory_loads_code_name_carry_limit_and_attributes()
    {
        var pack = Pack(Header + "7001\t용사\t1\t1\t2\n7002\t마법사\t3\t3\t1\n");
        Assert.Equal(
            [new CharacterEntry(7001, "용사", 1, 1, 2), new CharacterEntry(7002, "마법사", 3, 3, 1)],
            pack.CharacterInventory);
    }

    [Theory]
    [InlineData("7001\ta\t1\t1\t2\n7001\tb\t1\t1\t2\n")]     // 중복 코드
    [InlineData("\ta\t1\t1\t2\n")]                           // 빈 코드
    [InlineData("abc\ta\t1\t1\t2\n")]                        // 정수 아닌 코드
    [InlineData("7001\ta\t0\t1\t2\n")]                       // 소지제한 0
    [InlineData("7001\ta\t-5\t1\t2\n")]                      // 음수
    [InlineData("7001\ta\tabc\t1\t2\n")]                     // 정수 아님
    [InlineData("7001\ta\t\t1\t2\n")]                        // 빈 소지제한
    [InlineData("7001\ta\t1\t\t2\n")]                        // 유리속성 비어 있음
    [InlineData("7001\ta\t1\t1\t\n")]                        // 불리속성 비어 있음
    [InlineData("7001\ta\t1\tx\t2\n")]                       // 유리속성이 정수 아님
    [InlineData("7001\ta\t1\t1\ty\n")]                       // 불리속성이 정수 아님
    [InlineData("7001\ta\t1\t99\t2\n")]                      // 속성 마스터에 없는 유리속성
    [InlineData("7001\ta\t1\t1\t99\n")]                      // 속성 마스터에 없는 불리속성
    [InlineData("7001\ta\t1\t2\t2\n")]                       // 유리속성과 불리속성이 같음
    public void Bad_character_rows_are_rejected(string rows)
        => Assert.Throws<PackFormatException>(() => Pack(Header + rows));

    [Theory]
    [InlineData("character_code\tcharacter_name\tcarry_limit\n7001\ta\t1\n")]                                // 속성 열 없음(옛 형식)
    [InlineData("character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\n7001\ta\t1\t1\n")]   // 불리속성 열 없음
    [InlineData("item_code\titem_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n7001\ta\t1\t1\t2\n")]
    [InlineData("code\tname\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n7001\ta\t1\t1\t2\n")]
    public void Character_inventory_header_is_checked(string tsv)
        => Assert.Throws<PackFormatException>(() => Pack(tsv));

    [Fact]
    public void Character_inventory_needs_the_attribute_master()
        => Assert.Throws<PackFormatException>(() => Pack(Header + "7001\ta\t1\t1\t2\n", attributes: null));

    [Fact]
    public void Item_inventory_still_uses_item_code_and_item_name()
    {
        Assert.NotNull(Pack(null, ItemHeader + "1\t금화\t99999\n").ItemInventory);
        Assert.Throws<PackFormatException>(() => Pack(null, Header + "1\t금화\t99999\t1\t2\n"));
    }

    [Fact]
    public void Character_and_item_inventories_are_separate_code_spaces()
    {
        // 같은 코드가 양쪽에 있어도 충돌하지 않고, 아이템 이름 조회는 캐릭터를 보지 않는다.
        var pack = Pack(Header + "1\t용사\t1\t1\t2\n", ItemHeader + "1\t금화\t99999\n");
        Assert.Equal("용사", pack.CharacterInventory![0].Name);
        Assert.Equal("금화", pack.ItemName(1));
        Assert.Equal("77", pack.ItemName(77));
    }

    [Fact]
    public void Character_inventory_does_not_affect_the_session_inventory()
    {
        var s = GameSession.NewGame(Pack(Header + "7001\t용사\t1\t1\t2\n"));
        Assert.Empty(s.InventoryItems);
        Assert.Equal(["103", "200", "300"], s.Entries.Select(e => e.Code));
    }
}
