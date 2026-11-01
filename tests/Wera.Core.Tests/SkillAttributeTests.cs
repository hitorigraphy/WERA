using System.IO.Compression;
using Wera.Core;

namespace Wera.Core.Tests;

// 스킬 속성: skill_master의 선택 열 attribute_code와 스킬 실행 서킷의 AttributeMultiplierToCache 이펙트(속성 배율을 캐시에 쓴다).
// 규칙: 시전자는 스킬 속성이 자신의 유리속성이면 ×유리 배율(1.5), 불리속성이면 ×불리 배율(0.5). 타겟은 유리속성이면 ×0.5, 불리속성이면 ×1.5. 두 보정은 곱한다.
public class SkillAttributeTests
{
    const string Head = "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
    const string Setting = "setting_type\tvalue_type\tlong_value\tdecimal_value\ttext_value\n";

    // 속성 1~4. 캐릭터: 1(당신) 유리 1 불리 2, 7001 유리 3 불리 4, 7002 유리 2 불리 1, 7003 유리 3 불리 4(중립).
    // 스킬 1은 속성 1(불), 스킬 2는 속성 없음. 스킬 1 이펙트: 메인 타겟 배율을 캐시 0, 서브 타겟 0번 배율을 캐시 1에 쓴다.
    static GamePack Pack(string skillMaster = "code\tname\tattribute_code\n1\t불 베기\t1\n2\t평타\t\n", string effects =
            "1\t1\tAttributeMultiplierToCache\t0\t0\t\n1\t2\tAttributeMultiplierToCache\t1\t1\t\n2\t1\tAttributeMultiplierToCache\t0\t0\t\n", string? settings = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            (GamePack.AttributeMasterPath, "attribute_code\tattribute_name\n1\t불\n2\t물\n3\t풀\n4\t빛\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
                "1\t당신\t1\t1\t2\n7001\t용사\t1\t3\t4\n7002\t마법사\t1\t2\t1\n7003\t전사\t1\t3\t4\n"),
            (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n"),
            (GamePack.CharacterStatusMasterPath,
                "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
                string.Concat(new[] { 1, 7001, 7002, 7003 }.Select(c => $"{c}\t1\t0\t1\t1\t99\n{c}\t2\t0\t100\t0\t100\n"))),
            (GamePack.SkillMasterPath, skillMaster),
            (GamePack.SkillExecuteEffectMasterPath, Head + effects),
        };
        if (settings is not null) files.Add(("master/game_setting_master_v1.tsv", Setting + settings));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    // 시전자와 메인 타겟(과 서브 타겟)으로 스킬을 실행한 서킷의 캐시.
    static List<decimal> Cache(GamePack pack, long skill, long caster, long main, long? sub = null)
    {
        var field = new CircuitField(pack, caster, main);
        if (sub is { } s && !field.Characters.ContainsKey(s)) field.Characters[s] = pack.NewCharacterInstance(s, $"c{s}");
        var circuit = new SkillExecuteCircuit(pack, field.Store, skill, field.Unit(caster), field.Unit(main), sub is { } t ? [field.Unit(t)] : null);
        circuit.Execute();
        return circuit.CacheValues;
    }

    [Fact]
    public void The_skill_master_reads_attribute_code_and_FindSkill_exposes_it()
    {
        var pack = Pack();
        Assert.Equal(1L, pack.FindSkill(1)!.AttributeCode);
        Assert.Null(pack.FindSkill(2)!.AttributeCode);
        Assert.Equal("불", pack.FindAttribute(pack.FindSkill(1)!.AttributeCode!.Value)!.Name);
    }

    [Fact]
    public void A_skill_attribute_must_exist_in_the_attribute_master_and_the_master_must_exist()
    {
        Assert.Contains("속성 '9'가 속성 마스터에 없습니다", Assert.Throws<PackFormatException>(() =>
            Pack("code\tname\tattribute_code\n1\t불 베기\t9\n2\t평타\t\n")).Message);
        Assert.Contains("정수", Assert.Throws<PackFormatException>(() =>
            Pack("code\tname\tattribute_code\n1\t불 베기\tabc\n2\t평타\t\n")).Message);

        // 속성 마스터는 필수 마스터라 없으면 팩을 읽지 못한다(속성이 있는 스킬도 마찬가지).
        Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZipWithout(GamePack.AttributeMasterPath,
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.SkillMasterPath, "code\tname\tattribute_code\n1\t불 베기\t1\n"))));
    }

    [Fact]
    public void Attribute_code_can_sit_among_the_other_optional_columns_in_any_order_and_may_be_blank()
    {
        var pack = Pack("code\tname\tattribute_code\tdescription\tmain_target_type\n1\t불 베기\t3\t설명\tEnemy\n2\t평타\t\t\t\n");
        Assert.Equal(3L, pack.FindSkill(1)!.AttributeCode);
        Assert.Equal("설명", pack.FindSkill(1)!.Description);
        Assert.Null(pack.FindSkill(2)!.AttributeCode);
    }

    [Fact]
    public void The_multiplier_is_neutral_when_neither_side_has_the_skill_attribute()
    {
        Assert.Equal(1m, Cache(Pack(), 1, 7001, 7003)[0]);                           // 불은 둘의 유리/불리가 아니다
    }

    [Fact]
    public void The_caster_gets_the_advantage_or_disadvantage_multiplier_for_its_own_attribute()
    {
        Assert.Equal(1.5m, Cache(Pack(), 1, 1, 7003)[0]);                            // 당신: 유리속성이 불
        Assert.Equal(0.5m, Cache(Pack(), 1, 7002, 7003)[0]);                         // 마법사: 불리속성이 불
    }

    [Fact]
    public void The_target_gets_the_reverse_multiplier_for_its_own_attribute()
    {
        Assert.Equal(0.5m, Cache(Pack(), 1, 7003, 1)[0]);                            // 타겟의 유리속성이 불이면 받는 쪽은 ×0.5
        Assert.Equal(1.5m, Cache(Pack(), 1, 7003, 7002)[0]);                         // 타겟의 불리속성이 불이면 ×1.5
    }

    [Fact]
    public void Both_corrections_multiply_up_to_2_25_and_cancel_to_0_25()
    {
        Assert.Equal(2.25m, Cache(Pack(), 1, 1, 7002)[0]);                           // 유리속성 시전자 × 불리속성 타겟 = 1.5 × 1.5
        Assert.Equal(0.25m, Cache(Pack(), 1, 7002, 1)[0]);                           // 불리속성 시전자 × 유리속성 타겟 = 0.5 × 0.5
        Assert.Equal(0.75m, Cache(Pack(), 1, 1, 1)[0]);                              // 1.5 × 0.5
    }

    [Fact]
    public void The_multipliers_come_from_the_game_settings()
    {
        var pack = Pack(settings: "AttributeAdvantageMultiplier\tDecimal\t\t2\t\nAttributeDisadvantageMultiplier\tDecimal\t\t0.25\t\n");
        Assert.Equal(2m, Cache(pack, 1, 1, 7003)[0]);
        Assert.Equal(0.25m, Cache(pack, 1, 7002, 7003)[0]);
        Assert.Equal(4m, Cache(pack, 1, 1, 7002)[0]);                                // 시전자 ×2, 타겟(불리속성) ×2
    }

    [Fact]
    public void A_skill_without_an_attribute_always_writes_1()
    {
        Assert.Equal(1m, Cache(Pack(), 2, 1, 7002)[0]);
    }

    [Fact]
    public void The_target_selector_picks_the_main_target_or_a_sub_target_and_a_missing_target_writes_1()
    {
        var cache = Cache(Pack(), 1, 7003, 1, sub: 7002);                            // 메인 타겟 당신(유리 불 → 0.5), 서브 타겟 0번 마법사(불리 불 → 1.5)
        Assert.Equal(0.5m, cache[0]);
        Assert.Equal(1.5m, cache[1]);

        var missing = Pack(effects: "1\t1\tAttributeMultiplierToCache\t0\t5\t\n");   // 서브 타겟 4번은 없다
        Assert.Equal(1m, Cache(missing, 1, 1, 7002)[0]);
    }

    [Fact]
    public void The_effect_arguments_are_validated_and_it_works_inside_a_submodule()
    {
        Assert.Contains("cache_index", Assert.Throws<PackFormatException>(() =>
            Pack(effects: "1\t1\tAttributeMultiplierToCache\t-1\t0\t\n")).Message);
        Assert.Contains("value_2", Assert.Throws<PackFormatException>(() =>
            Pack(effects: "1\t1\tAttributeMultiplierToCache\t0\t-1\t\n")).Message);

        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            (GamePack.AttributeMasterPath, "attribute_code\tattribute_name\n1\t불\n2\t물\n3\t풀\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7003\t전사\t1\t3\t2\n"),
            (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n"),
            (GamePack.CharacterStatusMasterPath,
                "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n1\t1\t0\t1\t1\t99\n1\t2\t0\t10\t0\t10\n7003\t1\t0\t1\t1\t99\n7003\t2\t0\t10\t0\t10\n"),
            (GamePack.SkillMasterPath, "code\tname\tattribute_code\n1\t불 베기\t1\n"),
            (GamePack.SkillExecuteEffectMasterPath, Head + "1\t1\tJumpToSubmodule\t7\t\t\n"),
            (GamePack.SkillExecuteSubmoduleMasterPath, "submodule_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n7\t1\tAttributeMultiplierToCache\t3\t0\t\n"),
        };
        var pack = GamePack.Load(CoreTests.PackZip(files.ToArray()));
        Assert.Equal(1.5m, Cache(pack, 1, 1, 7003)[3]);
    }

    [Fact]
    public void A_pack_circuit_multiplies_damage_by_the_attribute_multiplier_in_the_sample_pack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var stream = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(stream);

        Assert.Equal(4L, pack.FindSkill(1006)!.AttributeCode);                       // 샘플 스킬 1006 속성 베기: 속성 빛(4)
        var field = new CircuitField(pack, 7001, 7002);                              // 용사의 유리속성이 빛(4), 마법사는 빛과 무관
        var before = field.Characters[7002].Statuses[GamePack.HpStatusCode];
        new SkillExecuteCircuit(pack, field.Store, 1006, field.Unit(7001), field.Unit(7002)).Execute();
        Assert.Equal(before - 15, field.Characters[7002].Statuses[GamePack.HpStatusCode]);   // 피해 10 × 1.5
    }
}
