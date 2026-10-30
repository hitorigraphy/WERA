using Wera.Core;

namespace Wera.Core.Tests;

// 스킬 서킷: 이펙트를 실행 번호 순서로 실행하고, CacheToTargetModValue/SetValue가 캐시 값을 타겟의 스테이터스에 쓴다.
public class SkillCircuitTests
{
    const long Hp = GamePack.HpStatusCode;
    const long Mana = 4;

    static GamePack Pack(string effects) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/character_inventory_master_v1.tsv",
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n4\tmana\n"),
        (GamePack.CharacterStatusMasterPath,
            "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
            "1\t1\t0\t1\t1\t99\n1\t2\t0\t40\t0\t40\n" +
            "7001\t1\t0\t1\t1\t99\n7001\t2\t0\t40\t0\t40\n7001\t4\t0\t10\t0\t20\n"),
        (GamePack.SkillMasterPath, "code\tname\n1\t테스트\n"),
        (GamePack.SkillExecuteEffectMasterPath, "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n" + effects)));

    static SkillCircuit Circuit(GamePack pack) =>
        new(pack, 1, BattleUnit.Create(pack, 1), BattleUnit.Create(pack, 7001));

    [Fact]
    public void A_unit_gets_every_status_the_master_lists_for_its_character()
    {
        var pack = Pack("");
        var unit = BattleUnit.Create(pack, 7001);
        Assert.Equal((1m, 40m, 10m), (unit.ValueOf(1)!.Value, unit.ValueOf(Hp)!.Value, unit.ValueOf(Mana)!.Value));
        Assert.Null(BattleUnit.Create(pack, 1).ValueOf(Mana));         // 당신에게는 mana 행이 없다
    }

    [Fact]
    public void Mod_adds_the_cached_value_and_set_replaces_with_it()
    {
        var pack = Pack("1\t1\tCacheToTargetModValue\t2\t0\t\n1\t2\tCacheToTargetSetValue\t4\t1\t\n");
        var c = Circuit(pack);
        c.CacheValues.AddRange([-15.5m, 7m]);
        c.Execute();
        Assert.Equal(24.5m, c.Target.ValueOf(Hp));
        Assert.Equal(7m, c.Target.ValueOf(Mana));
        Assert.Equal(40m, c.Caster.ValueOf(Hp));                        // 시전자는 건드리지 않는다
    }

    [Fact]
    public void Effects_run_in_execute_number_order()
    {
        var pack = Pack("2\tCacheToTargetSetValue\t2\t0\t\n".Insert(0, "1\t") + "1\t1\tCacheToTargetModValue\t2\t1\t\n");
        var c = Circuit(pack);
        c.CacheValues.AddRange([30m, -5m]);
        c.Execute();                                                    // 1번: -5 → 35, 2번: 30으로 덮어씀
        Assert.Equal(30m, c.Target.ValueOf(Hp));
    }

    [Fact]
    public void The_result_is_clamped_to_the_status_range()
    {
        var pack = Pack("1\t1\tCacheToTargetModValue\t2\t0\t\n1\t2\tCacheToTargetSetValue\t4\t1\t\n");
        var c = Circuit(pack);
        c.CacheValues.AddRange([-100m, 99m]);
        c.Execute();
        Assert.Equal(0m, c.Target.ValueOf(Hp));
        Assert.Equal(20m, c.Target.ValueOf(Mana));
    }

    [Fact]
    public void A_missing_cache_value_or_a_status_the_target_lacks_does_nothing()
    {
        var pack = Pack("1\t1\tCacheToTargetModValue\t2\t5\t\n1\t2\tCacheToTargetSetValue\t4\t0\t\n");
        var c = new SkillCircuit(pack, 1, BattleUnit.Create(pack, 7001), BattleUnit.Create(pack, 1));    // 타겟은 mana가 없는 캐릭터
        c.CacheValues.Add(9m);
        c.Execute();                                                    // 번호 5의 캐시는 없고, 타겟에게 mana는 없다
        Assert.Equal(40m, c.Target.ValueOf(Hp));
        Assert.Null(c.Target.ValueOf(Mana));
    }

    [Fact]
    public void The_loader_checks_the_status_code_and_the_cache_index()
    {
        Assert.Contains("value_1(status_code) '9'", Assert.Throws<PackFormatException>(() => Pack("1\t1\tCacheToTargetSetValue\t9\t0\t\n")).Message);
        Assert.Contains("value_2(cache_index) '-1'", Assert.Throws<PackFormatException>(() => Pack("1\t1\tCacheToTargetModValue\t2\t-1\t\n")).Message);
        Assert.NotNull(Pack("1\t1\tcachetotargetsetvalue\t2\t0\t\n"));  // 이름은 대소문자를 가리지 않는다
    }

    // 공백으로 구분해 쓴 이펙트 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    [Fact]
    public void Cache_to_caster_changes_the_caster_and_leaves_the_target()
    {
        var pack = Pack(Rows("1 1 CacheToCasterModValue 4 0 ", "1 2 CacheToCasterSetValue 2 1 "));
        var c = new SkillCircuit(pack, 1, BattleUnit.Create(pack, 7001), BattleUnit.Create(pack, 7001));
        c.CacheValues.AddRange([-3m, 12m]);
        c.Execute();
        Assert.Equal((7m, 12m), (c.Caster.ValueOf(Mana)!.Value, c.Caster.ValueOf(Hp)!.Value));
        Assert.Equal((10m, 40m), (c.Target.ValueOf(Mana)!.Value, c.Target.ValueOf(Hp)!.Value));
    }

    [Fact]
    public void Value_to_cache_copies_the_status_into_the_cache_and_pads_with_zeros()
    {
        var pack = Pack(Rows("1 1 TargetValueToCache 4 2 ", "1 2 CasterValueToCache 2 0 "));
        var c = new SkillCircuit(pack, 1, BattleUnit.Create(pack, 1), BattleUnit.Create(pack, 7001));
        c.Execute();
        Assert.Equal([40m, 0m, 10m], c.CacheValues);                  // 시전자 hp는 0번, 타겟 mana는 2번(1번은 0으로 채움)
    }

    [Fact]
    public void Value_to_cache_overwrites_and_skips_a_status_the_unit_lacks()
    {
        var pack = Pack(Rows("1 1 CasterValueToCache 4 0 ", "1 2 TargetValueToCache 2 0 "));
        var c = new SkillCircuit(pack, 1, BattleUnit.Create(pack, 1), BattleUnit.Create(pack, 7001));
        c.CacheValues.Add(99m);
        c.Execute();                                                   // 당신에게 mana는 없어 99가 남고, 그다음 타겟 hp 40으로 덮어쓴다
        Assert.Equal([40m], c.CacheValues);
    }

    [Fact]
    public void A_value_can_travel_from_one_unit_to_the_other_through_the_cache()
    {
        var pack = Pack(Rows("1 1 CasterValueToCache 2 0 ", "1 2 CacheToTargetSetValue 2 0 "));
        var caster = BattleUnit.Create(pack, 7001);
        caster.SetValue(Hp, 5);
        var c = new SkillCircuit(pack, 1, caster, BattleUnit.Create(pack, 1));
        c.Execute();
        Assert.Equal(5m, c.Target.ValueOf(Hp));
    }

    [Fact]
    public void The_loader_checks_every_cache_effect_and_bounds_the_cache_index()
    {
        foreach (var type in new[] { "CacheToCasterModValue", "CacheToCasterSetValue", "CasterValueToCache", "TargetValueToCache" })
        {
            Assert.Contains("value_1(status_code) '9'", Assert.Throws<PackFormatException>(() => Pack(Rows($"1 1 {type} 9 0 "))).Message);
            Assert.Contains("value_2(cache_index) '256'", Assert.Throws<PackFormatException>(() => Pack(Rows($"1 1 {type} 2 256 "))).Message);
            Assert.NotNull(Pack(Rows($"1 1 {type} 2 255 ")));
        }
    }

    static List<decimal> Calc(string type, decimal left, decimal right, int into = 2)
    {
        var pack = Pack(Rows($"1 1 {type} {into} 0 1"));
        var c = Circuit(pack);
        c.CacheValues.AddRange([left, right]);
        c.Execute();
        return c.CacheValues;
    }

    [Fact]
    public void The_four_operations_write_cache_value_2_op_value_3_into_value_1()
    {
        Assert.Equal([7m, 2m, 9m], Calc("CacheAdd", 7m, 2m));
        Assert.Equal([7m, 2m, 5m], Calc("CacheSubtract", 7m, 2m));
        Assert.Equal([7m, 2m, 14m], Calc("CacheMultiply", 7m, 2m));
        Assert.Equal([7m, 2m, 3.5m], Calc("CacheDivide", 7m, 2m));
    }

    [Fact]
    public void The_result_can_overwrite_an_operand_and_a_far_index_pads_with_zeros()
    {
        Assert.Equal([7m, 9m], Calc("CacheAdd", 7m, 2m, into: 1));          // 오른쪽 칸을 덮어쓴다
        Assert.Equal([9m, 2m], Calc("CacheAdd", 7m, 2m, into: 0));          // 왼쪽 칸을 덮어쓴다
        Assert.Equal([7m, 2m, 0m, 9m], Calc("CacheAdd", 7m, 2m, into: 3));
    }

    [Fact]
    public void Division_does_not_round()
    {
        Assert.Equal(1m / 3m, Calc("CacheDivide", 1m, 3m)[2]);
    }

    [Fact]
    public void Missing_operands_division_by_zero_and_overflow_do_nothing()
    {
        Assert.Equal([7m, 0m], Calc("CacheDivide", 7m, 0m));
        Assert.Equal([decimal.MaxValue, 2m], Calc("CacheMultiply", decimal.MaxValue, 2m));
        Assert.Equal([decimal.MinValue, 1m], Calc("CacheSubtract", decimal.MinValue, 1m));

        var pack = Pack(Rows("1 1 CacheAdd 2 0 5"));                        // 5번 칸이 없다
        var c = Circuit(pack);
        c.CacheValues.AddRange([1m, 2m]);
        c.Execute();
        Assert.Equal([1m, 2m], c.CacheValues);
    }

    [Fact]
    public void Operations_chain_through_the_cache_with_unit_values()
    {
        // 타겟 hp를 (시전자 hp / 2)만큼 깎는다: [0]=시전자 hp, [1]=2, [2]=[0]/[1], [3]=0-[2]
        var pack = Pack(Rows("1 1 CasterValueToCache 2 0", "1 2 CacheDivide 2 0 1", "1 3 CacheSubtract 3 4 2", "1 4 CacheToTargetModValue 2 3"));
        var c = Circuit(pack);
        c.CacheValues.AddRange([0m, 2m, 0m, 0m, 0m]);
        c.Execute();
        Assert.Equal([40m, 2m, 20m, -20m, 0m], c.CacheValues);
        Assert.Equal(20m, c.Target.ValueOf(Hp));
    }

    [Fact]
    public void The_loader_treats_all_three_arguments_of_a_calculation_as_cache_indexes()
    {
        foreach (var type in new[] { "CacheAdd", "CacheSubtract", "CacheMultiply", "CacheDivide" })
        {
            Assert.NotNull(Pack(Rows($"1 1 {type} 255 0 255")));                          // 상태 코드 검사는 하지 않는다
            Assert.Contains("value_1(cache_index) '256'", Assert.Throws<PackFormatException>(() => Pack(Rows($"1 1 {type} 256 0 0"))).Message);
            Assert.Contains("value_2(cache_index) '-1'", Assert.Throws<PackFormatException>(() => Pack(Rows($"1 1 {type} 0 -1 0"))).Message);
            Assert.Contains("value_3(cache_index) '300'", Assert.Throws<PackFormatException>(() => Pack(Rows($"1 1 {type} 0 0 300"))).Message);
        }
    }

    static SkillCircuit Run(params string[] rows)
    {
        var c = Circuit(Pack(Rows(rows)));
        c.Execute();
        return c;
    }

    [Fact]
    public void SetCacheValue_writes_a_constant_with_decimal_places_and_pads()
    {
        Assert.Equal([0m, 0m, 0.15m], Run("1 1 SetCacheValue 2 15 2").CacheValues);
        Assert.Equal([-3m], Run("1 1 SetCacheValue 0 -3 0").CacheValues);
        Assert.Equal([0.0000000000000000000000000001m], Run("1 1 SetCacheValue 0 1 28").CacheValues);
    }

    [Fact]
    public void Constants_make_a_percentage_skill_possible_with_masters_only()
    {
        // 타겟 hp를 시전자 hp의 15%만큼 깎는다
        var c = Run("1 1 CasterValueToCache 2 0", "1 2 SetCacheValue 1 15 2", "1 3 CacheMultiply 2 0 1",
                    "1 4 SetCacheValue 3 -1 0", "1 5 CacheMultiply 2 2 3", "1 6 CacheToTargetModValue 2 2");
        Assert.Equal(34m, c.Target.ValueOf(Hp));                       // 40 - 6
    }

    [Fact]
    public void Jump_skips_forward_and_Finish_stops()
    {
        Assert.Equal([1m, 3m], Run("1 1 SetCacheValue 0 1 0", "1 2 Jump 4", "1 3 SetCacheValue 1 2 0", "1 4 SetCacheValue 1 3 0").CacheValues);
        Assert.Equal([1m], Run("1 1 SetCacheValue 0 1 0", "1 2 Finish", "1 3 SetCacheValue 1 2 0").CacheValues);
    }

    [Theory]
    [InlineData("Equal", 5, 5, true)]
    [InlineData("Equal", 5, 6, false)]
    [InlineData("NotEqual", 5, 6, true)]
    [InlineData("NotEqual", 5, 5, false)]
    [InlineData("GreaterThan", 6, 5, true)]
    [InlineData("GreaterThan", 5, 5, false)]
    [InlineData("GreaterOrEqual", 5, 5, true)]
    [InlineData("GreaterOrEqual", 4, 5, false)]
    [InlineData("LessThan", 4, 5, true)]
    [InlineData("LessThan", 5, 5, false)]
    [InlineData("LessOrEqual", 5, 5, true)]
    [InlineData("LessOrEqual", 6, 5, false)]
    public void The_conditional_jumps_compare_two_cache_values_and_jump_only_when_true(string comparison, int left, int right, bool jumps)
    {
        var c = Run($"1 1 SetCacheValue 0 {left} 0", $"1 2 SetCacheValue 1 {right} 0", $"1 3 JumpCache{comparison} 0 1 5",
                    "1 4 SetCacheValue 2 1 0", "1 5 SetCacheValue 3 1 0");
        Assert.Equal(jumps ? [(decimal)left, right, 0m, 1m] : [(decimal)left, right, 1m, 1m], c.CacheValues);
    }

    [Fact]
    public void A_conditional_jump_with_a_missing_operand_does_not_jump()
    {
        var c = Run("1 1 SetCacheValue 0 1 0", "1 2 JumpCacheEqual 0 9 4", "1 3 SetCacheValue 1 7 0", "1 4 SetCacheValue 2 8 0");
        Assert.Equal([1m, 7m, 8m], c.CacheValues);
    }

    [Fact]
    public void A_branch_picks_a_different_effect_depending_on_a_unit_value()
    {
        // 타겟 hp가 시전자 hp보다 적으면 타겟을 1로 만든다, 아니면 그대로(2번에서 3번 건너뜀)
        var rows = new[] { "1 1 CasterValueToCache 2 0", "1 2 TargetValueToCache 2 1", "1 3 JumpCacheGreaterOrEqual 1 0 6",
                           "1 4 SetCacheValue 2 1 0", "1 5 CacheToTargetSetValue 2 2", "1 6 Finish" };
        var weak = Circuit(Pack(Rows(rows)));
        weak.Target.SetValue(Hp, 10);
        weak.Execute();
        Assert.Equal(1m, weak.Target.ValueOf(Hp));
        var strong = Circuit(Pack(Rows(rows)));
        strong.Execute();                                              // 40 >= 40 이라 점프한다
        Assert.Equal(40m, strong.Target.ValueOf(Hp));
    }

    [Fact]
    public void The_loader_checks_the_new_effect_arguments_and_jump_targets()
    {
        Assert.Contains("value_1(cache_index) '256'", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 SetCacheValue 256 1 0"))).Message);
        Assert.Contains("value_3(소수 자릿수) '29'", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 SetCacheValue 0 1 29"))).Message);
        Assert.Contains("value_2(cache_index) '256'", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 JumpCacheEqual 0 256 2", "1 2 Finish"))).Message);
        Assert.Contains("자신의 실행 번호보다 커야", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 Jump 1"))).Message);
        Assert.Contains("자신의 실행 번호보다 커야", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 Finish", "1 2 JumpCacheEqual 0 0 1"))).Message);
        Assert.Contains("같은 스킬에 없습니다", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 Jump 5", "1 2 Finish"))).Message);
        Assert.NotNull(Pack(Rows("1 1 Jump 3", "1 3 Finish")));        // 사이의 번호가 비어 있어도 된다
    }
}
