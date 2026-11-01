using Wera.Core;

namespace Wera.Core.Tests;

// 방어력: 오퍼레이터 슬롯마다 방어력 흐름(서킷)이 그 슬롯에 선 캐릭터의 최대 방어력을 계산하고, 시설은 시설 방어력 마스터의 값을 쓴다.
public class DefenseFlowTests
{
    static GameSession Session(string flows = "1\t1\tAddDefense\t5\t0\t0\n1\t2\tAddDefenseByStatus\t5\t50\t0\n", string facilityDefense = "2\t30\n")
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
            ("master/status_master_v1.tsv", "status_code\tstatus_name\tcharacter_display_order\n1\tlevel\t0\n2\thp\t0\n5\tatk\t1\n"),
            ("master/character_status_master_v1.tsv", "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n1\t1\t0\t1\t1\t99\n1\t2\t0\t10\t0\t10\n7001\t1\t0\t1\t1\t99\n7001\t2\t0\t10\t0\t10\n7001\t5\t0\t40\t0\t99\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t정비\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7001\t2\n7001\t4\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n"),
            ("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n2\t3\n"),
            ("master/facility_master_v1.tsv", "code\tname\n1\t정문\n2\t대장간\n"),
            ("master/operator_slot_master_v1.tsv", "code\tname\n1\t경비\n2\t정비\n"),
            ("master/operator_slot_defense_flow_master_v1.tsv", "operator_slot_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + flows),
            ("master/facility_defense_master_v1.tsv", "facility_code\tdefense\n" + facilityDefense)));
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);
        return s;
    }

    [Fact]
    public void The_operator_defense_is_what_the_slot_circuit_adds_up_from_fixed_values_and_the_operators_status()
    {
        var s = Session();
        Assert.Equal(5 + 20, s.OperatorDefenseOf(7001, 1));                    // 5 + 공격력 40의 50%
        Assert.Equal(0, s.OperatorDefenseOf(7001, 2));                         // 흐름이 없는 슬롯
        Assert.Equal(0, s.OperatorDefenseOf(9999, 1));                         // 보유하지 않은 캐릭터
    }

    [Fact]
    public void Jumps_and_finish_shape_the_defense_and_it_never_goes_below_zero()
    {
        var tagged = Session("1\t1\tJumpOperatorHaveTag\t4\t0\t4\n1\t2\tAddDefense\t100\t0\t0\n1\t3\tFinish\t0\t0\t0\n1\t4\tAddDefense\t7\t0\t0\n");
        Assert.Equal(7, tagged.OperatorDefenseOf(7001, 1));                    // 정비 태그가 있어 4번으로 점프
        var flagged = Session("1\t1\tJumpFlagGreaterOrEqual\t3\t1\t3\n1\t2\tAddDefense\t10\t0\t0\n1\t3\tAddDefense\t1\t0\t0\n");
        Assert.Equal(11, flagged.OperatorDefenseOf(7001, 1));                  // 플래그 3이 0이라 점프 안 함: 10 + 1
        flagged.PlayerFlags[3] = 1;
        Assert.Equal(1, flagged.OperatorDefenseOf(7001, 1));
        var random = Session("1\t1\tJumpRandom\t50\t100\t3\n1\t2\tAddDefense\t10\t0\t0\n1\t3\tAddDefense\t1\t0\t0\n");
        random.NextRandom = _ => 0;
        Assert.Equal(1, random.OperatorDefenseOf(7001, 1));
        random.NextRandom = _ => 99;
        Assert.Equal(11, random.OperatorDefenseOf(7001, 1));
        Assert.Equal(0, Session("1\t1\tAddDefense\t-50\t0\t0\n").OperatorDefenseOf(7001, 1));
    }

    [Fact]
    public void The_facility_defense_comes_from_its_master_and_is_zero_without_a_row()
    {
        var s = Session();
        Assert.Equal(30, s.Pack.FacilityDefenseOf(2));
        Assert.Equal(0, s.Pack.FacilityDefenseOf(1));
    }

    [Fact]
    public void The_defense_masters_are_validated()
    {
        PackFormatException Flow(string flows) => Assert.Throws<PackFormatException>(() => Session(flows));
        Assert.Contains("오퍼레이터 슬롯 '9'", Flow("9\t1\tFinish\t0\t0\t0\n").Message);
        Assert.Contains("알 수 없습니다", Flow("1\t1\tNope\t0\t0\t0\n").Message);
        Assert.Contains("두 번", Flow("1\t1\tFinish\t0\t0\t0\n1\t1\tFinish\t0\t0\t0\n").Message);
        Assert.Contains("스테이터스 코드 '9'", Flow("1\t1\tAddDefenseByStatus\t9\t50\t0\n").Message);
        Assert.Contains("태그 코드 '9'", Flow("1\t1\tJumpOperatorHaveTag\t9\t0\t2\n1\t2\tFinish\t0\t0\t0\n").Message);
        Assert.Contains("보다 커야", Flow("1\t1\tJump\t1\t0\t0\n").Message);
        Assert.Contains("같은 슬롯의 흐름에 없습니다", Flow("1\t1\tJump\t5\t0\t0\n").Message);
        Assert.Contains("모수", Flow("1\t1\tJumpRandom\t1\t0\t2\n1\t2\tFinish\t0\t0\t0\n").Message);
        Assert.Contains("시설 '9'", Assert.Throws<PackFormatException>(() => Session(facilityDefense: "9\t5\n")).Message);
        Assert.Contains("두 번", Assert.Throws<PackFormatException>(() => Session(facilityDefense: "2\t5\n2\t6\n")).Message);
        Assert.Contains("0 이상", Assert.Throws<PackFormatException>(() => Session(facilityDefense: "2\t-1\n")).Message);
    }
}
