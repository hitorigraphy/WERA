using Wera.Core;

namespace Wera.Core.Tests;

// 예약 태그(플레이어·리더·메인 통화, [구금] 태그 둘)의 코드는 게임 설정으로 게임팩이 정한다. 설정이 없으면 1, 2, 3(구금 태그는 없음).
public class ReservedTagSettingTests
{
    const string Setting = "setting_type\tvalue_type\tlong_value\tdecimal_value\ttext_value\n";

    static GamePack Pack(string settings = "", string tags = "1\t플레이어\n2\t리더\n3\t메인 통화\n", string characterTags = "1\t1\n", string commonItemTags = "2\t3\n")
    {
        var entries = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv", "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n" + tags),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n" + characterTags),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n"),
            ("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n" + commonItemTags),
        };
        if (settings != "") entries.Add(("master/game_setting_master_v1.tsv", Setting + settings));
        return GamePack.Load(CoreTests.PackZip(entries.ToArray()));
    }

    [Fact]
    public void Without_settings_the_codes_are_the_old_reserved_ones_and_the_captured_tags_do_not_exist()
    {
        var pack = Pack();
        Assert.Equal([1, 2, 3], new[] { pack.PlayerTagCode, pack.LeaderTagCode, pack.MainCurrencyTagCode });
        Assert.Null(pack.CapturedEnemyTagCode);
        Assert.Null(pack.CapturedAllyTagCode);
    }

    [Fact]
    public void The_game_pack_chooses_every_reserved_tag_code()
    {
        var pack = Pack("PlayerTagCode\tLong\t10\t\t\nLeaderTagCode\tLong\t11\t\t\nMainCurrencyTagCode\tLong\t12\t\t\nCapturedEnemyTagCode\tLong\t13\t\t\nCapturedAllyTagCode\tLong\t14\t\t\n",
            tags: "10\t플레이어\n11\t리더\n12\t메인 통화\n13\t구금\n14\t구금\n", characterTags: "1\t10\n", commonItemTags: "2\t12\n");
        Assert.Equal([10, 11, 12], new[] { pack.PlayerTagCode, pack.LeaderTagCode, pack.MainCurrencyTagCode });
        Assert.Equal(13, pack.CapturedEnemyTagCode);
        Assert.Equal(14, pack.CapturedAllyTagCode);
        Assert.True(pack.MayHoldTag(10, InventoryKind.Character, 1));
        Assert.False(pack.MayHoldTag(10, InventoryKind.Character, 7001));              // 새 플레이어 태그도 캐릭터 1만
        Assert.False(pack.MayHoldTag(12, InventoryKind.UniqueItem, 2));
        Assert.True(pack.MayHoldTag(1, InventoryKind.UniqueItem, 5));                  // 옛 코드 1은 이제 예약이 아니다
    }

    [Fact]
    public void The_set_tags_must_exist_and_be_distinct()
    {
        Assert.Contains("예약된 태그 코드 10(플레이어)", Assert.Throws<PackFormatException>(() => Pack("PlayerTagCode\tLong\t10\t\t\n")).Message);
        Assert.Contains("구금된 적 태그 코드 13", Assert.Throws<PackFormatException>(() => Pack("CapturedEnemyTagCode\tLong\t13\t\t\n")).Message);
        Assert.Contains("구금된 아군 태그 코드 13", Assert.Throws<PackFormatException>(() => Pack("CapturedAllyTagCode\tLong\t13\t\t\n")).Message);
        Assert.Contains("같은 태그 코드", Assert.Throws<PackFormatException>(() =>
            Pack("CapturedEnemyTagCode\tLong\t2\t\t\n", tags: "1\t플레이어\n2\t리더\n3\t메인 통화\n")).Message);
        Assert.Throws<PackFormatException>(() => Pack("LeaderTagCode\tLong\t0\t\t\n"));
        Assert.Throws<PackFormatException>(() => Pack("PlayerTagCode\tDecimal\t\t1\t\n"));
    }
}
