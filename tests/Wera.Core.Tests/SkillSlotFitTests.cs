using Wera.Core;

namespace Wera.Core.Tests;

// 스킬 슬롯 태그 규칙(장비 슬롯 태그와 같다: 그룹 안은 AND, 그룹끼리는 OR, is_positive가 거짓이면 가지면 안 됨)과 행동 순서.
public class SkillSlotFitTests
{
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
        "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n";
    const string Tags = "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t검\n5\t마법\n6\t금지\n7\t전사\n";

    static GamePack Pack(string slotTags) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/character_inventory_master_v1.tsv", Characters),
        (GamePack.TagMasterPath, Tags),
        (GamePack.CharacterTagMasterPath, "character_code\ttag_code\n1\t1\n7001\t7\n"),
        (GamePack.SkillSlotMasterPath, "code\tname\n1\t슬롯1\n2\t슬롯2\n3\t슬롯3\n"),
        (GamePack.SkillMasterPath, "code\tname\n1\t베기\n2\t불꽃\n3\t금지된 검\n4\t무태그\n"),
        (GamePack.SkillTagMasterPath, "skill_code\ttag_code\n1\t4\n2\t5\n3\t4\n3\t6\n"),
        (GamePack.CharacterSkillSlotTagMasterPath, "character_code\tskill_slot_code\tgroup_number\ttag_code\tis_positive\n" + slotTags)));

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    [Fact]
    public void A_positive_tag_must_be_on_the_skill_and_a_negative_tag_must_not_be()
    {
        // 슬롯 1: 검 태그가 있고 금지 태그는 없는 스킬
        var pack = Pack(Rows("7001 1 1 4 true", "7001 1 1 6 false"));
        Assert.True(pack.CanSlotSkill(7001, 1, 1));
        Assert.False(pack.CanSlotSkill(7001, 1, 2));
        Assert.False(pack.CanSlotSkill(7001, 1, 3));            // 검이지만 금지 태그가 있다
        Assert.False(pack.CanSlotSkill(7001, 1, 4));
        Assert.Equal([1L], pack.SlottableSkills(7001, 1).Select(s => s.Code));
    }

    [Fact]
    public void Groups_are_ored_together_and_a_slot_without_rules_takes_nothing()
    {
        // 슬롯 1: 그룹 1(검) 또는 그룹 2(마법)
        var pack = Pack(Rows("7001 1 1 4 true", "7001 1 2 5 true"));
        Assert.Equal([1L, 2L, 3L], pack.SlottableSkills(7001, 1).Select(s => s.Code));
        Assert.Empty(pack.SlottableSkills(7001, 2));            // 슬롯 2에는 규칙이 없다
        Assert.Empty(pack.SlottableSkills(1, 1));               // 당신에게는 규칙이 없다
    }

    [Fact]
    public void The_characters_own_tags_count_too()
    {
        // 슬롯 3: 용사의 마스터 태그(전사)가 있어야 하고 스킬 태그(마법)도 있어야 한다
        var pack = Pack(Rows("7001 3 1 7 true", "7001 3 1 5 true"));
        Assert.Equal([2L], pack.SlottableSkills(7001, 3).Select(s => s.Code));
    }

    [Fact]
    public void The_rule_rows_carry_group_number_and_is_positive_and_the_lookups_use_them()
    {
        var pack = Pack(Rows("7001 1 1 4 true", "7001 1 1 6 false", "7001 1 2 5 1"));
        var all = pack.SkillSlotTagsOf(7001);
        Assert.Equal([(1, 4L, true), (1, 6L, false), (2, 5L, true)], all.Select(t => (t.GroupNumber, t.TagCode, t.IsPositive)));
        Assert.Equal([4L, 6L], pack.SkillSlotTagsOf(7001, 1, 1).Select(t => t.TagCode));
        Assert.Equal([false], pack.SkillSlotTagsOf(7001, 1, 1).Where(t => t.TagCode == 6).Select(t => t.IsPositive));
    }

    [Fact]
    public void The_matching_function_is_the_same_as_the_equipment_one()
    {
        var rules = new[]
        {
            new CharacterSkillSlotTag(1, 1, 1, 4, true), new CharacterSkillSlotTag(1, 1, 1, 6, false),
            new CharacterSkillSlotTag(1, 1, 2, 5, true),
        };
        Assert.True(SkillSlotRules.Matches(rules, new HashSet<long> { 4 }));
        Assert.False(SkillSlotRules.Matches(rules, new HashSet<long> { 4, 6 }));
        Assert.True(SkillSlotRules.Matches(rules, new HashSet<long> { 4, 6, 5 }));         // 그룹 2가 맞는다
        Assert.False(SkillSlotRules.Matches([], new HashSet<long> { 4 }));
    }

    // ---- 행동 순서 ----

    [Fact]
    public void A_bigger_order_point_acts_first()
    {
        var order = ActionOrder.Sort(
        [
            new BattleActor(BattleSide.Ally, 0, 5m), new BattleActor(BattleSide.Enemy, 1, 9m),
            new BattleActor(BattleSide.Ally, 1, 20m), new BattleActor(BattleSide.Enemy, 0, -3m),
        ]);
        Assert.Equal([(BattleSide.Ally, 1), (BattleSide.Enemy, 1), (BattleSide.Ally, 0), (BattleSide.Enemy, 0)], order.Select(a => (a.Side, a.Slot)));
    }

    [Fact]
    public void Equal_order_points_put_the_enemy_first_and_then_the_lower_slot()
    {
        var order = ActionOrder.Sort(
        [
            new BattleActor(BattleSide.Ally, 0, 7m), new BattleActor(BattleSide.Enemy, 4, 7m),
            new BattleActor(BattleSide.Ally, 2, 7m), new BattleActor(BattleSide.Enemy, 1, 7m),
        ]);
        Assert.Equal([(BattleSide.Enemy, 1), (BattleSide.Enemy, 4), (BattleSide.Ally, 0), (BattleSide.Ally, 2)], order.Select(a => (a.Side, a.Slot)));
    }
}
