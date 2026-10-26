using Wera.Core;

namespace Wera.Core.Tests;

// 태그 마스터: 플레이어(1)와 리더(2)는 엔진이 예약한 코드라 반드시 있어야 한다(이름은 자유).
public class ReservedTagTests
{
    static GamePack Load(string tags) =>
        GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/tag_master_v1.tsv", tags)));

    [Fact]
    public void The_reserved_codes_are_one_two_and_three()
    {
        Assert.Equal(1, GamePack.PlayerTagCode);
        Assert.Equal(2, GamePack.LeaderTagCode);
        Assert.Equal(3, GamePack.MainCurrencyTagCode);
    }

    [Fact]
    public void A_tag_master_with_player_and_leader_loads()
    {
        var pack = Load("tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t검\n");
        Assert.Equal([1L, 2L, 3L, 4L], pack.Tags!.Select(t => t.Code));
    }

    [Fact]
    public void The_reserved_tags_may_carry_any_name()
    {
        var pack = Load("tag_code\ttag_name\n3\tmain currency\n2\t대장\n1\tplayer\n");
        Assert.Equal("player", pack.Tags!.Single(t => t.Code == 1).Name);
    }

    [Theory]
    [InlineData("tag_code\ttag_name\n2\t리더\n3\t메인 통화\n4\t검\n", "1(플레이어)")]          // 플레이어가 없다
    [InlineData("tag_code\ttag_name\n1\t플레이어\n3\t메인 통화\n4\t검\n", "2(리더)")]           // 리더가 없다
    [InlineData("tag_code\ttag_name\n1\t플레이어\n2\t리더\n4\t검\n", "3(메인 통화)")]          // 메인 통화가 없다
    [InlineData("tag_code\ttag_name\n4\t검\n5\t갑옷\n", "예약")]                                // 모두 없다
    [InlineData("tag_code\ttag_name\n", "예약")]                                   // 비어 있다
    public void A_tag_master_missing_a_reserved_code_is_rejected(string tags, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(tags)).Message);

    [Fact]
    public void The_tag_master_itself_is_required_when_the_pack_is_loaded()
    {
        var error = Assert.Throws<PackFormatException>(() =>
            GamePack.Load(CoreTests.PackZipWithout(GamePack.TagMasterPath, ("pack.tsv", "id\tdemo\ntitle\t데모\n"))));
        Assert.Contains("tag_master", error.Message);
    }

    [Fact]
    public void The_shipped_sample_pack_defines_the_reserved_tags()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(zip);
        Assert.Equal("플레이어", pack.Tags!.Single(t => t.Code == GamePack.PlayerTagCode).Name);
        Assert.Equal("리더", pack.Tags!.Single(t => t.Code == GamePack.LeaderTagCode).Name);
    }
}
