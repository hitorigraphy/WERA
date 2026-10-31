using Wera.Core;

namespace Wera.Core.Tests;

// after_hit_effect_master와 피격 후 서킷: 스킬 실행 이펙트와 같은 구성으로 시전자와 타겟을 조작한다.
public class AfterHitCircuitTests
{
    const string Head = "execute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
    const long Hp = GamePack.HpStatusCode;
    const long Mana = 4;

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(string effects) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/character_inventory_master_v1.tsv",
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n4\tmana\n"),
        (GamePack.CharacterStatusMasterPath, "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
            Rows("1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 40 0 40", "7001 4 0 10 0 20")),
        (GamePack.AfterHitEffectMasterPath, Head + effects)));

    static AfterHitCircuit Circuit(GamePack pack) =>
        new(pack, BattleUnit.Create(pack, 1), BattleUnit.Create(pack, 7001));

    [Fact]
    public void Rows_are_read_in_order_and_the_master_is_optional()
    {
        var pack = Pack(Rows("20 None 1 2 3", "10 none"));
        Assert.Equal([10L, 20L], pack.AfterHitEffectsInOrder().Select(e => e.ExecuteNumber));
        Assert.Equal(new AfterHitEffect(20, AfterHitEffectType.None, 1, 2, 3), pack.AfterHitEffectsInOrder()[1]);
        var none = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Null(none.AfterHitEffects);
        Assert.Empty(none.AfterHitEffectsInOrder());
    }

    [Fact]
    public void The_caster_pays_hp_into_the_cache_and_the_target_takes_it_clamped()
    {
        // 시전자 hp(40)를 캐시 0에, -15를 캐시 1에 쓰고 타겟 hp에 더하고(25), 시전자 mana는 없어서 건너뛰며, 타겟 mana에는 캐시 0(40)을 더한다(최댓값 20).
        var pack = Pack(Rows("1 CasterValueToCache 2 0", "2 SetCacheValue 1 -15", "3 CacheToTargetModValue 2 1", "4 CacheToCasterModValue 4 1",
            "5 CacheToTargetModValue 4 0"));
        var c = Circuit(pack);
        c.Execute();
        Assert.Equal(25m, c.Target.ValueOf(Hp));
        Assert.Equal(20m, c.Target.ValueOf(Mana));
        Assert.Null(c.Caster.ValueOf(Mana));
        Assert.Equal(40m, c.Caster.ValueOf(Hp));
    }

    [Fact]
    public void Caster_set_and_target_value_to_cache_and_arithmetic_work()
    {
        var pack = Pack(Rows("1 TargetValueToCache 4 0", "2 SetCacheValue 1 5", "3 CacheMultiply 2 0 1", "4 CacheToCasterSetValue 2 2"));
        var c = Circuit(pack);
        c.Execute();
        Assert.Equal([10m, 5m, 50m], c.CacheValues);
        Assert.Equal(40m, c.Caster.ValueOf(Hp));                   // 50은 최댓값 40으로 맞춘다
    }

    [Fact]
    public void Jump_and_finish_follow_the_skill_effect_rules()
    {
        var pack = Pack(Rows("1 SetCacheValue 0 3", "2 SetCacheValue 1 3", "3 JumpCacheEqual 0 1 5",
            "4 CacheToTargetSetValue 4 0", "5 Jump 7", "6 CacheToTargetSetValue 2 0", "7 Finish", "8 CacheToTargetSetValue 2 0"));
        var c = Circuit(pack);
        c.Execute();
        Assert.Equal(10m, c.Target.ValueOf(Mana));
        Assert.Equal(40m, c.Target.ValueOf(Hp));
    }

    [Fact]
    public void Invalid_rows_are_rejected_at_load()
    {
        string Err(string rows) => Assert.Throws<PackFormatException>(() => Pack(rows)).Message;
        Assert.Contains("스테이터스 마스터에 없습니다", Err(Rows("1 TargetValueToCache 99 0")));
        Assert.Contains("cache_index", Err(Rows("1 CacheToTargetModValue 2 256")));
        Assert.Contains("value_3(소수 자릿수)", Err(Rows("1 SetCacheValue 0 1 29")));
        Assert.Contains("자신의 실행 번호보다 커야", Err(Rows("5 Jump 5")));
        Assert.Contains("마스터에 없습니다", Err(Rows("1 Jump 9")));
        Assert.Contains("두 번", Err(Rows("1 None", "1 None")));
        Assert.Contains("'Explode'", Err(Rows("1 Explode")));
    }
}
