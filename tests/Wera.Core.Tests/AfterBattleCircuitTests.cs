using Wera.Core;

namespace Wera.Core.Tests;

// after_battle_effect_master와 전투 후 서킷: 스킬 실행 이펙트와 같은 구성으로 아군 캐릭터 하나를 조작한다.
public class AfterBattleCircuitTests
{
    const string Head = "execute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
    const long Hp = GamePack.HpStatusCode;
    const long Mana = 6;

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(string effects) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/character_inventory_master_v1.tsv",
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n6\tmana\n"),
        (GamePack.CharacterStatusMasterPath, "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
            Rows("1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 40 0 40", "7001 6 0 10 0 20")),
        (GamePack.AfterBattleEffectMasterPath, Head + effects)));

    static CharacterInstance Run(GamePack pack, long character = 7001) =>
        new AfterBattleCircuit(pack, pack.NewCharacterInstance(character)).Execute();

    [Fact]
    public void Rows_are_read_in_order_and_the_master_is_optional()
    {
        var pack = Pack(Rows("20 None 1 2 3", "10 none"));
        Assert.Equal([10L, 20L], pack.AfterBattleEffectsInOrder().Select(e => e.ExecuteNumber));
        Assert.Equal(new AfterBattleEffect(20, AfterBattleEffectType.None, 1, 2, 3), pack.AfterBattleEffectsInOrder()[1]);
        var none = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Null(none.AfterBattleEffects);
        Assert.Empty(none.AfterBattleEffectsInOrder());
        var c = none.NewCharacterInstance(1);
        Assert.Equal(c, new AfterBattleCircuit(none, c).Execute());     // 이펙트가 없으면 그대로
    }

    [Fact]
    public void Effects_move_values_between_the_character_and_the_cache_and_clamp()
    {
        // mana(10)를 캐시 0에 읽고, 캐시 1에 5를 쓰고, 둘을 더해 캐시 2(15)에, 그 값을 mana에 더한다(25 → 최댓값 20).
        var pack = Pack(Rows("1 CasterValueToCache 6 0", "2 SetCacheValue 1 5", "3 CacheAdd 2 0 1", "4 CacheToTargetModValue 6 2", "5 CacheToCasterSetValue 2 1"));
        var c = Run(pack);
        Assert.Equal(20, c.Statuses[Mana]);
        Assert.Equal(5, c.Hp);
    }

    [Fact]
    public void Decimals_are_truncated_and_missing_statuses_do_nothing()
    {
        var pack = Pack(Rows("1 SetCacheValue 0 25 1", "2 CacheToCasterSetValue 6 0", "3 CacheToCasterSetValue 6 5"));
        var c = Run(pack);
        Assert.Equal(2, c.Statuses[Mana]);                       // 2.5 → 2, 3번은 캐시 5가 없어서 건너뜀
        Assert.False(Run(pack, 1).Statuses.ContainsKey(Mana));   // 당신에게는 mana가 없다
    }

    [Fact]
    public void Jump_and_finish_follow_the_skill_effect_rules_and_are_checked_at_load()
    {
        var pack = Pack(Rows("1 SetCacheValue 0 3", "2 SetCacheValue 1 3", "3 JumpCacheEqual 0 1 5",
            "4 CacheToCasterSetValue 6 0", "5 Jump 7", "6 CacheToCasterSetValue 2 0", "7 Finish", "8 CacheToCasterSetValue 2 0"));
        var c = Run(pack);
        Assert.Equal((10, 40), (c.Statuses[Mana], c.Hp));

        string Err(string rows) => Assert.Throws<PackFormatException>(() => Pack(rows)).Message;
        Assert.Contains("스테이터스 마스터에 없습니다", Err(Rows("1 CasterValueToCache 99 0")));
        Assert.Contains("cache_index", Err(Rows("1 CacheToTargetModValue 2 256")));
        Assert.Contains("자신의 실행 번호보다 커야", Err(Rows("5 Jump 5")));
        Assert.Contains("두 번", Err(Rows("1 None", "1 None")));
        Assert.Contains("'Explode'", Err(Rows("1 Explode")));
    }
}
