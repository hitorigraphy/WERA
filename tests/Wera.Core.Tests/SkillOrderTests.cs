using Wera.Core;

namespace Wera.Core.Tests;

// 스킬 순서 이펙트 마스터(skill_order_effect_master)와 순서 서킷: 이펙트를 돌린 뒤의 Result가 OrderPoint다.
public class SkillOrderTests
{
    const long Hp = GamePack.HpStatusCode;
    const long Mana = 20;

    // 공백으로 구분해 쓴 이펙트 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    const string Head = "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";

    static GamePack Pack(string orderEffects, string? executeEffects = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
            (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n20\tmana\n"),
            (GamePack.CharacterStatusMasterPath,
                "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
                "1\t1\t0\t1\t1\t99\n1\t2\t0\t40\t0\t40\n1\t20\t0\t6\t0\t20\n" +
                "7001\t1\t0\t1\t1\t99\n7001\t2\t0\t30\t0\t40\n7001\t20\t0\t10\t0\t20\n"),
            (GamePack.SkillMasterPath, "code\tname\n1\t테스트\n"),
            (GamePack.SkillOrderEffectMasterPath, Head + orderEffects),
        };
        if (executeEffects is not null) files.Add((GamePack.SkillExecuteEffectMasterPath, Head + executeEffects));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    static SkillOrderCircuit Run(params string[] rows)
    {
        var pack = Pack(Rows(rows));
        var field = new CircuitField(pack, 1, 7001);
        var c = new SkillOrderCircuit(pack, field.Store, 1, field.Unit(7001), field.Unit(1));    // 시전자 용사, 메인 타겟 당신
        c.Execute();
        return c;
    }

    [Fact]
    public void The_execute_and_order_masters_are_separate_and_both_optional()
    {
        var pack = Pack(Rows("1 1 CacheToResult 0"), Rows("1 1 None"));
        Assert.Single(pack.SkillOrderEffectsOf(1));
        Assert.Single(pack.SkillExecuteEffectsOf(1));
        Assert.Equal(OrderEffectType.CacheToResult, pack.SkillOrderEffects![0].EffectType);

        var none = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Null(none.SkillOrderEffects);
        Assert.Empty(none.SkillOrderEffectsOf(1));
    }

    [Fact]
    public void The_result_starts_at_zero_and_only_CacheToResult_changes_it()
    {
        Assert.Equal(0m, Run("1 1 SetCacheValue 0 9 0").Result);
        Assert.Equal(9m, Run("1 1 SetCacheValue 0 9 0", "1 2 CacheToResult 0").Result);
        Assert.Equal(0m, Run("1 1 CacheToResult 5").Result);                // 없는 칸이면 그대로
    }

    [Fact]
    public void The_order_point_is_computed_from_the_casters_and_the_targets_values()
    {
        // OrderPoint = 시전자 mana(10) + 타겟 mana(6) * 0.5
        var c = Run("1 1 FocusCasterCharacter", "1 2 FocusedCharacterValueToCache 20 0", "1 3 FocusMainTargetCharacter", "1 4 FocusedCharacterValueToCache 20 1",
                    "1 5 SetCacheValue 2 5 1", "1 6 CacheMultiply 1 1 2", "1 7 CacheAdd 0 0 1", "1 8 CacheToResult 0");
        Assert.Equal(13m, c.Result);
        Assert.Equal(13m, c.CacheValues[0]);
    }

    [Fact]
    public void Order_effects_never_change_unit_statuses()
    {
        var pack = Pack(Rows("1 1 FocusCasterCharacter", "1 2 FocusedCharacterValueToCache 2 0", "1 3 CacheToResult 0"));
        var field = new CircuitField(pack, 1, 7001);
        var before = (field.Characters[1], field.Characters[7001]);
        var c = new SkillOrderCircuit(pack, field.Store, 1, field.Unit(7001), field.Unit(1));
        c.Execute();
        Assert.Equal(30, c.FocusedCharacterInstance!.Hp);
        Assert.Equal(30m, c.Result);
        Assert.Equal(before, (field.Characters[1], field.Characters[7001]));
    }

    [Fact]
    public void Branches_and_finish_work_like_the_execute_circuit()
    {
        // 시전자 hp가 타겟 hp보다 크면 1000, 아니면 1(여기서는 30 < 40이라 1)
        var c = Run("1 1 FocusCasterCharacter", "1 2 FocusedCharacterValueToCache 2 0", "1 3 FocusMainTargetCharacter", "1 4 FocusedCharacterValueToCache 2 1",
                    "1 5 JumpCacheGreaterThan 0 1 8", "1 6 SetCacheValue 2 1 0", "1 7 Jump 9", "1 8 SetCacheValue 2 1000 0", "1 9 CacheToResult 2",
                    "1 10 Finish", "1 11 SetCacheValue 2 5 0");
        Assert.Equal(1m, c.Result);

        var finished = Run("1 1 SetCacheValue 0 2 0", "1 2 CacheToResult 0", "1 3 Finish", "1 4 SetCacheValue 0 7 0", "1 5 CacheToResult 0");
        Assert.Equal(2m, finished.Result);
    }

    [Fact]
    public void Division_by_zero_and_missing_cells_do_nothing()
    {
        Assert.Equal(4m, Run("1 1 SetCacheValue 0 4 0", "1 2 SetCacheValue 1 0 0", "1 3 CacheDivide 0 0 1", "1 4 CacheToResult 0").Result);
        Assert.Equal(4m, Run("1 1 SetCacheValue 0 4 0", "1 2 CacheAdd 0 0 9", "1 3 CacheToResult 0").Result);
    }

    [Fact]
    public void The_loader_checks_the_arguments_per_type_and_the_jump_targets()
    {
        Assert.Contains("스킬 코드 '9'가 스킬 마스터에 없습니다", Assert.Throws<PackFormatException>(() => Pack(Rows("9 1 None"))).Message);
        Assert.Contains("이펙트 종류 'Fly'을(를) 알 수 없습니다", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 Fly"))).Message);
        Assert.Contains("이펙트 종류 'CacheToFocusedCharacterSetValue'을(를) 알 수 없습니다",
            Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 CacheToFocusedCharacterSetValue 2 0"))).Message);                    // 실행 이펙트의 종류는 못 쓴다
        Assert.Contains("실행 번호 '1'가 두 번", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 None", "1 1 None"))).Message);
        Assert.Contains("value_1(status_code) '9'", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 FocusedCharacterValueToCache 9 0"))).Message);
        Assert.Contains("value_2(cache_index) '256'", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 FocusedCharacterValueToCache 2 256"))).Message);
        Assert.Contains("value_3(소수 자릿수) '29'", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 SetCacheValue 0 1 29"))).Message);
        Assert.Contains("value_1(cache_index) '256'", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 CacheToResult 256"))).Message);
        Assert.Contains("value_3(cache_index) '300'", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 CacheAdd 0 0 300"))).Message);
        Assert.Contains("value_2(cache_index) '-1'", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 JumpCacheEqual 0 -1 2", "1 2 Finish"))).Message);
        Assert.Contains("자신의 실행 번호보다 커야", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 Jump 1"))).Message);
        Assert.Contains("같은 스킬에 없습니다", Assert.Throws<PackFormatException>(() => Pack(Rows("1 1 Jump 5", "1 2 Finish"))).Message);
        Assert.NotNull(Pack(Rows("1 1 Jump 3", "1 3 Finish")));
    }

    [Fact]
    public void The_order_master_needs_the_skill_master()
    {
        var noSkills = CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.SkillOrderEffectMasterPath, Head));
        Assert.Contains(GamePack.SkillMasterPath, Assert.Throws<PackFormatException>(() => GamePack.Load(noSkills)).Message);
    }
}
