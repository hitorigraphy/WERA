using Wera.Core;

namespace Wera.Core.Tests;

// 캐릭터 1은 플레이어: 항상 플레이어 태그(1)를 가지고, 플레이어 태그는 캐릭터 1만 가질 수 있다. 이름은 게임팩 마음대로다.
public class PlayerCharacterTests
{
    const string Pack0 = "id\tdemo\ntitle\t데모\n";
    const string Attributes = "attribute_code\tattribute_name\n1\t불\n2\t물\n";
    const string CharacterHeader =
        "character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n";
    const string Tags = "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t검\n";

    static GamePack Load(
        string characters = CharacterHeader + "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n",
        string tags = Tags, string characterTags = "character_code\ttag_code\n1\t1\n",
        string? itemTags = null, string? commonItemTags = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", Pack0),
            ("master/attribute_master_v1.tsv", Attributes),
            ("master/character_inventory_master_v1.tsv", characters),
            ("master/unique_item_inventory_master_v1.tsv", "item_code\titem_name\n8001\t철검\n"),
            ("master/common_item_inventory_master_v1.tsv", "item_code\titem_name\tcarry_limit\n3\t뼈\t9\n"),
        };
        files.Add(("master/tag_master_v1.tsv", tags));
        files.Add(("master/character_tag_master_v1.tsv", characterTags));
        files.Add(("master/status_master_v1.tsv", "status_code\tstatus_name\n1\tlevel\n2\thp\n"));
        files.Add(("master/character_status_master_v1.tsv",
            "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
            "1\t1\t0\t1\t1\t99\n1\t2\t0\t1\t0\t1\n7001\t1\t0\t1\t1\t99\n7001\t2\t0\t1\t0\t1\n"));
        if (itemTags is not null) files.Add(("master/unique_item_tag_master_v1.tsv", itemTags));
        if (commonItemTags is not null) files.Add(("master/common_item_tag_master_v1.tsv", commonItemTags));
        return GamePack.Load(CoreTests.Zip(files.ToArray()));
    }

    // ---- 게임팩 ----

    [Fact]
    public void Character_one_with_the_player_tag_loads_under_any_name()
    {
        Assert.Equal(1, GamePack.PlayerCharacterCode);
        Assert.Equal("당신", Load().FindCharacter(1)!.Name);
        var renamed = Load(CharacterHeader + "1\tHero\t1\t1\t2\n7001\t용사\t1\t1\t2\n");
        Assert.Equal("Hero", renamed.FindCharacter(1)!.Name);
    }

    [Fact]
    public void Character_one_may_have_other_tags_too()
    {
        var pack = Load(characterTags: "character_code\ttag_code\n1\t1\n1\t2\n1\t3\n");
        Assert.Equal([1L, 2L, 3L], pack.MasterTagsOfCharacter(1));
    }

    [Theory]
    [InlineData(CharacterHeader + "7001\t용사\t1\t1\t2\n", "플레이어 캐릭터 1")]                          // 캐릭터 1이 없다
    [InlineData(CharacterHeader + "2\t누군가\t1\t1\t2\n7001\t용사\t1\t1\t2\n", "플레이어 캐릭터 1")]
    public void Character_one_must_exist_when_there_are_a_tag_master_and_a_character_master(string characters, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() =>
            Load(characters, characterTags: "character_code\ttag_code\n7001\t3\n")).Message);

    [Theory]
    [InlineData("character_code\ttag_code\n7001\t3\n")]                      // 캐릭터 1의 행이 없다
    [InlineData("character_code\ttag_code\n1\t3\n")]                         // 캐릭터 1에게 플레이어 태그가 없다
    [InlineData("character_code\ttag_code\n1\t2\n")]
    public void Character_one_must_carry_the_player_tag(string characterTags) =>
        Assert.Contains("플레이어 태그", Assert.Throws<PackFormatException>(() => Load(characterTags: characterTags)).Message);

    [Theory]
    [InlineData("character_code\ttag_code\n1\t1\n7001\t1\n")]                // 다른 캐릭터가 플레이어 태그를 가진다
    public void Only_character_one_may_have_the_player_tag_among_characters(string characterTags) =>
        Assert.Contains("캐릭터 1만", Assert.Throws<PackFormatException>(() => Load(characterTags: characterTags)).Message);

    [Fact]
    public void Items_cannot_carry_the_player_tag()
    {
        Assert.Contains("캐릭터 1만", Assert.Throws<PackFormatException>(() =>
            Load(itemTags: "item_code\ttag_code\n8001\t1\n")).Message);
        Assert.Contains("캐릭터 1만", Assert.Throws<PackFormatException>(() =>
            Load(commonItemTags: "item_code\ttag_code\n3\t1\n")).Message);
    }

    [Fact]
    public void The_shipped_sample_pack_has_character_one_named_you_with_the_player_tag()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.zip"));
        var pack = GamePack.Load(zip);
        Assert.Equal("당신", pack.FindCharacter(1)!.Name);
        Assert.Contains(GamePack.PlayerTagCode, pack.MasterTagsOfCharacter(1));
    }

    // ---- 게임 중 ----

    static GameSession Session()
    {
        var s = GameSession.NewGame(Load());
        s.AddCharacter(1);
        s.AddCharacter(7001);
        return s;
    }

    [Fact]
    public void A_new_player_character_copies_the_player_tag()
    {
        var s = Session();
        Assert.Equal([1L], s.CharacterTags(1));
        Assert.Empty(s.CharacterTags(7001));
    }

    [Fact]
    public void The_player_tag_cannot_be_given_to_others_or_taken_from_the_player()
    {
        var s = Session();
        Assert.False(s.AddCharacterTag(7001, 1));
        Assert.Empty(s.CharacterTags(7001));
        Assert.False(s.RemoveCharacterTag(1, 1));
        Assert.Equal([1L], s.CharacterTags(1));

        // 다른 태그는 자유롭게 바꿀 수 있다.
        Assert.True(s.AddCharacterTag(1, 3));
        Assert.True(s.RemoveCharacterTag(1, 3));
        Assert.True(s.AddCharacterTag(7001, 3));
    }

    [Fact]
    public void A_unique_item_instance_cannot_get_the_player_tag()
    {
        var s = Session();
        var id = s.AddUniqueItem(8001)!;
        Assert.False(s.AddUniqueItemTag(id, 1));
        Assert.DoesNotContain(1L, s.TagsOf(EquipRef.Unique(id)));
    }

    [Fact]
    public void The_player_cannot_be_removed()
    {
        var s = Session();
        Assert.False(s.RemoveCharacter(1));
        Assert.True(s.Characters.ContainsKey(1));
        Assert.True(s.RemoveCharacter(7001));
    }

    [Fact]
    public void The_player_cannot_be_paid_in_a_shop_trade()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", Pack0),
            ("master/attribute_master_v1.tsv", Attributes),
            ("master/common_item_inventory_master_v1.tsv", "item_code\titem_name\tcarry_limit\n3\t뼈\t999\n"),
            ("master/character_inventory_master_v1.tsv", CharacterHeader + "1\t당신\t1\t1\t2\n"),
            ("master/tag_master_v1.tsv", Tags),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n"),
            ("master/shop_master_v1.tsv", "code\tname\n2001\t상점\n"),
            ("master/shop_trade_master_v1.tsv",
                "code\tshop_code\tcost_item_code\tcost_amount\treceive_item_code\treceive_amount\tcost_kind\treceive_kind\n" +
                "5001\t2001\t1\t1\t3\t10\tcharacter\tcommon_item\n")));
        var s = GameSession.NewGame(pack);
        s.AddCharacter(1);

        s.Activate("102"); s.Activate("2001"); s.Activate("5001");
        Assert.Contains("플레이어는 낼 수 없습니다", s.Message);
        Assert.True(s.Characters.ContainsKey(1));
    }

    // ---- 세이브 ----

    const string Head = "{\"Version\":1,\"PackId\":\"demo\",";
    const string Chars =
        "\"Characters\":{\"1\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10},\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}}";

    [Fact]
    public void A_save_with_the_player_tag_in_place_loads_and_a_missing_one_is_copied_from_the_master()
    {
        var withTags = GameSession.FromSaveJson(Load(), Head + Chars + ",\"CharacterTags\":{\"1\":[1,3],\"7001\":[]}}");
        Assert.Equal([1L, 3L], withTags.CharacterTags(1));

        var copied = GameSession.FromSaveJson(Load(), Head + Chars + "}");
        Assert.Equal([1L], copied.CharacterTags(1));
    }

    [Theory]
    [InlineData(",\"CharacterTags\":{\"1\":[3]}")]                           // 캐릭터 1이 플레이어 태그를 잃었다
    [InlineData(",\"CharacterTags\":{\"1\":[1],\"7001\":[1]}")]              // 다른 캐릭터가 플레이어 태그를 가졌다
    [InlineData(",\"UniqueItems\":{\"01H00000000000000000000001\":{\"ItemCode\":8001,\"Tags\":[1]}}")]   // 유니크 아이템 인스턴스가 플레이어 태그를 가졌다
    public void A_save_breaking_the_player_tag_rule_is_rejected(string fragment) =>
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Load(), Head + Chars + fragment + "}"));
}
