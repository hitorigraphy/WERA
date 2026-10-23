using Wera.Core;

namespace Wera.Core.Tests;

public class CharacterInventoryTests
{
    const string Header = "character_code\tcharacter_name\tcarry_limit\n";
    const string ItemHeader = "item_code\titem_name\tcarry_limit\n";

    static GamePack Pack(string? characters, string? items = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
            ("scenes.tsv", "id\ttext\na\tx\n"),
            ("choices.tsv", "scene\tlabel\tnext\n"),
        };
        if (characters is not null) files.Add(("master/character_inventory_master_v1.tsv", characters));
        if (items is not null) files.Add(("master/item_inventory_master_v1.tsv", items));
        return GamePack.Load(CoreTests.Zip(files.ToArray()));
    }

    [Fact]
    public void Character_inventory_is_optional()
        => Assert.Null(Pack(null).CharacterInventory);

    [Fact]
    public void Character_inventory_loads_code_name_and_carry_limit()
    {
        var pack = Pack(Header + "7001\t용사\t1\n7002\t마법사\t3\n");
        Assert.Equal([new CharacterEntry(7001, "용사", 1), new CharacterEntry(7002, "마법사", 3)], pack.CharacterInventory);
    }

    [Theory]
    [InlineData("7001\ta\t1\n7001\tb\t1\n")]     // 중복 코드
    [InlineData("\ta\t1\n")]                      // 빈 코드
    [InlineData("7001\ta\t0\n")]                  // 소지제한 0
    [InlineData("7001\ta\t-5\n")]                 // 음수
    [InlineData("7001\ta\tabc\n")]                // 정수 아님
    [InlineData("7001\ta\t\n")]                   // 빈 소지제한
    public void Bad_character_rows_are_rejected(string rows)
        => Assert.Throws<PackFormatException>(() => Pack(Header + rows));

    [Theory]
    [InlineData("character_code\tcharacter_name\n7001\ta\n")]                // carry_limit 열 없음
    [InlineData("item_code\titem_name\tcarry_limit\n7001\ta\t1\n")]          // 옛 열 이름은 더 이상 받지 않는다
    [InlineData("code\tname\tcarry_limit\n7001\ta\t1\n")]
    public void Character_inventory_header_is_checked(string tsv)
        => Assert.Throws<PackFormatException>(() => Pack(tsv));

    [Fact]
    public void Item_inventory_still_uses_item_code_and_item_name()
    {
        Assert.NotNull(Pack(null, ItemHeader + "1\t금화\t99999\n").ItemInventory);
        Assert.Throws<PackFormatException>(() => Pack(null, Header + "1\t금화\t99999\n"));
    }

    [Fact]
    public void Character_and_item_inventories_are_separate_code_spaces()
    {
        // 같은 코드가 양쪽에 있어도 충돌하지 않고, 아이템 이름 조회는 캐릭터를 보지 않는다.
        var pack = Pack(Header + "1\t용사\t1\n", ItemHeader + "1\t금화\t99999\n");
        Assert.Equal("용사", pack.CharacterInventory![0].Name);
        Assert.Equal("금화", pack.ItemName(1));
        Assert.Equal("77", pack.ItemName(77));
    }

    [Fact]
    public void Character_inventory_does_not_affect_the_session_yet()
    {
        var s = GameSession.NewGame(Pack(Header + "7001\t용사\t1\n"));
        Assert.Empty(s.InventoryItems);
        Assert.Equal(["103", "200", "300"], s.Entries.Select(e => e.Code));
    }
}
