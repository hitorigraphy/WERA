using Wera.Core;

namespace Wera.Core.Tests;

// 방어전 적 파티 마스터: 프리셋 파티가 적, 위협도(플레이어 플래그, 코드는 게임 설정 ThreatFlagCode)가 min_threat 이상이면 후보,
// priority가 작은 순서로 chance(%)를 굴려 처음 통과한 적이 등장하고 consume_threat만큼 위협도가 줄어든다.
public class DefenseEnemyTests
{
    const string Enemies = "code\tpreset_party_code\tmin_threat\tconsume_threat\tpriority\tchance\n";

    static GameSession Session(string enemies = "1\t1\t10\t10\t2\t100\n2\t2\t50\t30\t1\t100\n", string settings = "ThreatFlagCode\tLong\t9\t\t\n")
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv", "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n"),
            ("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n2\t3\n"),
            ("master/preset_unit_master_v1.tsv", "code\tcharacter_code\n1\t7001\n"),
            ("master/preset_party_master_v1.tsv", "code\tname\tfront_left_unit_code\tfront_center_unit_code\tfront_right_unit_code\tmiddle_left_unit_code\tmiddle_center_unit_code\tmiddle_right_unit_code\tback_left_unit_code\tback_center_unit_code\tback_right_unit_code\n1\t졸개\t1\t\t\t\t\t\t\t\t\n2\t대장\t1\t\t\t\t\t\t\t\t\n"),
            ("master/game_setting_master_v1.tsv", "setting_type\tvalue_type\tlong_value\tdecimal_value\ttext_value\n" + settings),
            ("master/hideout_defense_enemy_master_v1.tsv", Enemies + enemies)));
        return GameSession.NewGame(pack);
    }

    static GameSession WithThreat(long threat, string enemies = "1\t1\t10\t10\t2\t100\n2\t2\t50\t30\t1\t100\n")
    {
        var s = Session(enemies);
        s.PlayerFlags[9] = threat;
        s.NextRandom = _ => 0;                                   // 항상 가장 낮은 값: chance가 0보다 크면 늘 통과
        return s;
    }

    [Fact]
    public void The_threat_is_the_player_flag_named_by_the_game_setting()
    {
        var s = WithThreat(42);
        Assert.Equal(9, s.Pack.ThreatFlagCode);
        Assert.Equal(42, s.Threat);
    }

    [Fact]
    public void An_enemy_needs_the_minimum_threat_and_the_one_with_the_smaller_priority_is_checked_first()
    {
        Assert.Null(WithThreat(9).RollDefenseEnemy());                                   // 어느 적도 최소 위협도에 못 미친다
        Assert.Equal(1, WithThreat(10).RollDefenseEnemy()!.Code);                        // 1번만 후보
        Assert.Equal(2, WithThreat(60).RollDefenseEnemy()!.Code);                        // 둘 다 후보: priority 1인 2번이 먼저
    }

    [Fact]
    public void An_enemy_that_appears_lowers_the_threat_by_its_consumption_but_never_below_zero()
    {
        var s = WithThreat(60);
        Assert.Equal(2, s.RollDefenseEnemy()!.Code);
        Assert.Equal(30, s.Threat);
        Assert.Equal(1, s.RollDefenseEnemy()!.Code);                                     // 30은 2번 최소(50)에 못 미친다
        Assert.Equal(20, s.Threat);
        var low = WithThreat(5, "1\t1\t0\t10\t1\t100\n");
        Assert.NotNull(low.RollDefenseEnemy());
        Assert.Equal(0, low.Threat);
    }

    [Fact]
    public void A_failed_chance_roll_skips_that_enemy_and_a_peaceful_day_keeps_the_threat()
    {
        var s = WithThreat(60, "1\t1\t10\t10\t2\t100\n2\t2\t50\t30\t1\t30\n");
        var rolls = new Queue<long>([30, 99]);                                            // 2번: 30 < 30 아니다(탈락), 1번: 99 < 100 통과
        s.NextRandom = _ => rolls.Dequeue();
        Assert.Equal(1, s.RollDefenseEnemy()!.Code);
        Assert.Equal(50, s.Threat);

        var peaceful = WithThreat(500, "1\t1\t10\t10\t1\t0\n");                          // chance 0: 위협도가 높아도 나오지 않는다
        Assert.Null(peaceful.RollDefenseEnemy());
        Assert.Equal(500, peaceful.Threat);
    }

    [Fact]
    public void Without_the_setting_there_is_no_threat_and_the_master_is_validated()
    {
        Assert.Contains("ThreatFlagCode", Assert.Throws<PackFormatException>(() => Session(settings: "ListPageSize\tLong\t5\t\t\n")).Message);
        Assert.Throws<PackFormatException>(() => Session(settings: "ThreatFlagCode\tLong\t0\t\t\n"));
        Assert.Contains("프리셋 파티 '9'", Assert.Throws<PackFormatException>(() => Session("1\t9\t0\t0\t1\t50\n")).Message);
        Assert.Contains("min_threat", Assert.Throws<PackFormatException>(() => Session("1\t1\t-1\t0\t1\t50\n")).Message);
        Assert.Contains("consume_threat", Assert.Throws<PackFormatException>(() => Session("1\t1\t0\t-1\t1\t50\n")).Message);
        Assert.Contains("chance", Assert.Throws<PackFormatException>(() => Session("1\t1\t0\t0\t1\t101\n")).Message);
        Assert.Contains("priority", Assert.Throws<PackFormatException>(() => Session("1\t1\t0\t0\t1\t50\n2\t2\t0\t0\t1\t50\n")).Message);
        Assert.Contains("코드 '1'", Assert.Throws<PackFormatException>(() => Session("1\t1\t0\t0\t1\t50\n1\t2\t0\t0\t2\t50\n")).Message);
    }
}
