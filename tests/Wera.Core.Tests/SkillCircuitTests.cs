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
        (GamePack.SkillEffectMasterPath, "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n" + effects)));

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
}
