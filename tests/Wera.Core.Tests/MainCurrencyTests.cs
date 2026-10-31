using System.IO.Compression;
using System.Text;
using Wera.Core;

namespace Wera.Core.Tests;

// 메인 통화: 커먼 아이템 인벤토리 코드 2는 반드시 있고(이름은 자유), 메인 통화 태그(3)를 가진다.
// 메인 통화 태그는 아이템 2만 가질 수 있어 메인 통화가 둘 이상 생길 수 없다.
public class MainCurrencyTests
{
    const string CommonPath = "master/common_item_inventory_master_v1.tsv";
    const string CommonTagPath = "master/common_item_tag_master_v1.tsv";

    /// <summary>필수 마스터를 기본값으로 채운 게임팩에서 파일의 내용을 바꾼(없으면 더한) zip. 바꿀 파일은 둘까지 쓴다.</summary>
    static MemoryStream With(string path, string content, params (string, string)[] more) =>
        With2(path, content, null, null, more);

    static MemoryStream With2(string path, string content, string? path2, string? content2, params (string, string)[] more)
    {
        using var source = new ZipArchive(CoreTests.PackZip([("pack.tsv", "id\tdemo\ntitle\t데모\n"), .. more]), ZipArchiveMode.Read);
        var entries = source.Entries.Select(e =>
        {
            using var r = new StreamReader(e.Open(), new UTF8Encoding(false));
            var text = r.ReadToEnd();
            return (e.FullName, e.FullName == path ? content : e.FullName == path2 ? content2! : text);
        }).ToList();
        if (entries.All(e => e.FullName != path)) entries.Add((path, content));
        if (path2 is not null && entries.All(e => e.FullName != path2)) entries.Add((path2, content2!));
        return CoreTests.Zip(entries.ToArray());
    }

    static string Message(MemoryStream zip) => Assert.Throws<PackFormatException>(() => GamePack.Load(zip)).Message;

    [Fact]
    public void The_reserved_codes_are_item_two_and_tag_three()
    {
        Assert.Equal(2, GamePack.MainCurrencyItemCode);
        Assert.Equal(3, GamePack.MainCurrencyTagCode);
    }

    [Fact]
    public void The_default_masters_have_item_two_with_the_main_currency_tag()
    {
        var pack = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Equal("금화", pack.FindItem(GamePack.MainCurrencyItemCode)!.Name);
        Assert.Equal([3L], pack.MasterTagsOfCommonItem(2));
        Assert.Equal("금화", pack.MainCurrency.Name);
    }

    [Fact]
    public void The_main_currency_may_carry_any_name_and_other_tags_too()
    {
        var pack = GamePack.Load(With(CommonPath, "inventory_code\titem_name\tcarry_limit\n2\tCoins\t500\n"));
        Assert.Equal("Coins", pack.MainCurrency.Name);
        Assert.Equal(500, pack.MainCurrency.CarryLimit);

        var more = GamePack.Load(With(CommonTagPath, "item_code\ttag_code\n2\t3\n2\t4\n",
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t귀중품\n")));
        Assert.Equal([3L, 4L], more.MasterTagsOfCommonItem(2));
    }

    // ---- 검증 ----

    [Fact]
    public void Item_two_must_exist_in_the_common_item_inventory()
    {
        // 아이템 2가 없으면 그 태그 행도 있을 수 없으니 태그 마스터도 함께 바꾼다.
        var message = Message(With2(CommonPath, "inventory_code\titem_name\tcarry_limit\n3\t약초\t99\n", CommonTagPath, "item_code\ttag_code\n3\t4\n",
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t귀중품\n")));
        Assert.Contains("메인 통화 아이템 2", message);
    }

    [Theory]
    [InlineData("item_code\ttag_code\n")]                                      // 태그 행이 없다
    [InlineData("item_code\ttag_code\n3\t4\n")]                               // 아이템 2의 행이 없다
    [InlineData("item_code\ttag_code\n2\t4\n")]                               // 아이템 2에 메인 통화 태그가 없다
    public void Item_two_must_carry_the_main_currency_tag(string commonTags)
    {
        var message = Message(With(CommonTagPath, commonTags,
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t귀중품\n"),
            (CommonPath, "inventory_code\titem_name\tcarry_limit\n2\t금화\t9\n3\t약초\t9\n")));
        Assert.Contains("메인 통화 태그(3)", message);
    }

    [Fact]
    public void A_second_main_currency_is_rejected_on_another_common_item()
    {
        var message = Message(With(CommonTagPath, "item_code\ttag_code\n2\t3\n3\t3\n",
            (CommonPath, "inventory_code\titem_name\tcarry_limit\n2\t금화\t9\n3\t은화\t9\n")));
        Assert.Contains("메인 통화 태그(3)는 커먼 아이템 2만", message);
    }

    [Fact]
    public void The_main_currency_tag_cannot_sit_on_unique_items_or_characters()
    {
        var unique = With("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n8001\t3\n",
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"));
        Assert.Contains("메인 통화 태그(3)는 커먼 아이템 2만", Message(unique));

        var character = With("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n1\t3\n");
        Assert.Contains("메인 통화 태그(3)는 커먼 아이템 2만", Message(character));
    }

    [Fact]
    public void The_player_tag_still_belongs_to_character_one_only_and_nothing_else_is_exclusive()
    {
        Assert.True(GamePack.MayHoldTag(GamePack.PlayerTagCode, InventoryKind.Character, 1));
        Assert.False(GamePack.MayHoldTag(GamePack.PlayerTagCode, InventoryKind.Character, 7001));
        Assert.False(GamePack.MayHoldTag(GamePack.PlayerTagCode, InventoryKind.CommonItem, 1));
        Assert.True(GamePack.MayHoldTag(GamePack.MainCurrencyTagCode, InventoryKind.CommonItem, 2));
        Assert.False(GamePack.MayHoldTag(GamePack.MainCurrencyTagCode, InventoryKind.CommonItem, 3));
        Assert.False(GamePack.MayHoldTag(GamePack.MainCurrencyTagCode, InventoryKind.UniqueItem, 2));
        Assert.False(GamePack.MayHoldTag(GamePack.MainCurrencyTagCode, InventoryKind.Character, 2));
        Assert.True(GamePack.MayHoldTag(4, InventoryKind.UniqueItem, 8001));            // 그 밖의 태그는 누구나
        Assert.True(GamePack.MayHoldTag(GamePack.LeaderTagCode, InventoryKind.Character, 7001));
    }

    [Fact]
    public void The_two_common_item_masters_are_required_when_the_pack_is_loaded()
    {
        foreach (var path in new[] { CommonPath, CommonTagPath })
            Assert.Contains(path, Message(CoreTests.PackZipWithout(path, ("pack.tsv", "id\tdemo\ntitle\t데모\n"))));
    }

    // ---- 게임 중 ----

    static GameSession Session()
    {
        var s = GameSession.NewGame(GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t귀중품\n"))));
        s.AddCharacter(7001);
        return s;
    }

    [Fact]
    public void Characters_and_unique_item_instances_cannot_gain_the_main_currency_tag()
    {
        var s = Session();
        Assert.False(s.AddCharacterTag(7001, 3));
        Assert.False(s.AddCharacterTag(1, 3));
        Assert.True(s.AddCharacterTag(7001, 4));

        var id = s.AddUniqueItem(8001)!;
        Assert.False(s.AddUniqueItemTag(id, 3));
        Assert.DoesNotContain(3L, s.TagsOf(EquipRef.Unique(id)));
        Assert.True(s.AddUniqueItemTag(id, 4));
    }

    [Fact]
    public void A_save_giving_the_main_currency_tag_to_a_character_or_an_instance_is_rejected()
    {
        var pack = Session().Pack;
        const string head = "{\"Version\":1,\"PackId\":\"demo\",\"Characters\":{\"1\":{\"Level\":1,\"Hp\":1,\"MaxHp\":1,\"Xpp\":0,\"XppMax\":0},\"7001\":{\"Level\":1,\"Hp\":1,\"MaxHp\":1,\"Xpp\":0,\"XppMax\":0}}";
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, head + ",\"CharacterTags\":{\"1\":[1],\"7001\":[3]}}"));
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack,
            head + ",\"UniqueItems\":{\"01H00000000000000000000001\":{\"ItemCode\":8001,\"Tags\":[3]}}}"));
        GameSession.FromSaveJson(pack, head + ",\"CharacterTags\":{\"1\":[1],\"7001\":[4]}}");      // 다른 태그는 괜찮다
    }

    [Fact]
    public void The_default_pack_uses_gold_as_item_two_with_the_main_currency_tag()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(zip);

        Assert.Equal("금화", pack.MainCurrency.Name);
        Assert.Equal(2, pack.MainCurrency.Code);
        Assert.Contains(GamePack.MainCurrencyTagCode, pack.MasterTagsOfCommonItem(2));
        Assert.Equal("메인 통화", pack.Tags!.Single(t => t.Code == GamePack.MainCurrencyTagCode).Name);
        Assert.DoesNotContain(pack.CommonItemTags!, t => t.ItemCode != 2 && t.TagCode == GamePack.MainCurrencyTagCode);
    }
}
