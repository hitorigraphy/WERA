using Wera.Core;

namespace Wera.Core.Tests;

// after_attack_effect_master와 공격 후 서킷: 스킬 실행 이펙트와 같은 구성으로, 포커스한 시전자나 메인 타겟의 캐릭터를 조작한다.
public class AfterAttackCircuitTests
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
        (GamePack.AfterAttackEffectMasterPath, Head + effects)));

    // 시전자 당신(1), 메인 타겟 용사(7001)
    static (AfterAttackCircuit Circuit, CircuitField Field) Start(GamePack pack)
    {
        var field = new CircuitField(pack, 1, 7001);
        return (new AfterAttackCircuit(pack, field.Store, field.Unit(1), field.Unit(7001)), field);
    }

    [Fact]
    public void Rows_are_read_in_order_and_the_master_is_optional()
    {
        var pack = Pack(Rows("20 None 1 2 3", "10 none"));
        Assert.Equal([10L, 20L], pack.AfterAttackEffectsInOrder().Select(e => e.ExecuteNumber));
        Assert.Equal(new AfterAttackEffect(20, AfterAttackEffectType.None, 1, 2, 3), pack.AfterAttackEffectsInOrder()[1]);
        var none = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Null(none.AfterAttackEffects);
        Assert.Empty(none.AfterAttackEffectsInOrder());
    }

    [Fact]
    public void The_caster_pays_hp_into_the_cache_and_the_target_takes_it_clamped()
    {
        // 시전자 hp(40)를 캐시 0에, -15를 캐시 1에 쓰고 타겟 hp에 더하고(25), 시전자 mana는 없어서 건너뛰며, 타겟 mana에는 캐시 0(40)을 더한다(최댓값 20).
        var pack = Pack(Rows("1 FocusCasterCharacter", "2 FocusedCharacterValueToCache 2 0", "3 SetCacheValue 1 -15", "4 FocusMainTargetCharacter",
            "5 CacheToFocusedCharacterModValue 2 1", "6 CacheToFocusedCharacterModValue 6 0", "7 FocusCasterCharacter", "8 CacheToFocusedCharacterModValue 6 1"));
        var (c, field) = Start(pack);
        c.Execute();
        Assert.Equal((25, 20), (field.Characters[7001].Hp, field.Characters[7001].Statuses[Mana]));
        Assert.False(field.Characters[1].Statuses.ContainsKey(Mana));
        Assert.Equal(40, field.Characters[1].Hp);
    }

    [Fact]
    public void Value_to_cache_and_arithmetic_work_and_the_value_is_clamped_on_the_way_back()
    {
        var pack = Pack(Rows("1 FocusMainTargetCharacter", "2 FocusedCharacterValueToCache 6 0", "3 SetCacheValue 1 5", "4 CacheMultiply 2 0 1",
            "5 FocusCasterCharacter", "6 CacheToFocusedCharacterSetValue 2 2"));
        var (c, field) = Start(pack);
        c.Execute();
        Assert.Equal([10m, 5m, 50m], c.CacheValues);
        Assert.Equal(40, field.Characters[1].Hp);                   // 50은 최댓값 40으로 맞춘다
    }

    [Fact]
    public void Jump_and_finish_follow_the_skill_effect_rules()
    {
        var pack = Pack(Rows("1 FocusMainTargetCharacter", "2 SetCacheValue 0 3", "3 SetCacheValue 1 3", "4 JumpCacheEqual 0 1 6",
            "5 CacheToFocusedCharacterSetValue 6 0", "6 Jump 8", "7 CacheToFocusedCharacterSetValue 2 0", "8 Finish", "9 CacheToFocusedCharacterSetValue 2 0"));
        var (c, field) = Start(pack);
        c.Execute();
        Assert.Equal((10, 40), (field.Characters[7001].Statuses[Mana], field.Characters[7001].Hp));
    }

    [Fact]
    public void Invalid_rows_are_rejected_at_load()
    {
        string Err(string rows) => Assert.Throws<PackFormatException>(() => Pack(rows)).Message;
        Assert.Contains("스테이터스 마스터에 없습니다", Err(Rows("1 FocusedCharacterValueToCache 99 0")));
        Assert.Contains("cache_index", Err(Rows("1 CacheToFocusedCharacterModValue 2 256")));
        Assert.Contains("value_3(소수 자릿수)", Err(Rows("1 SetCacheValue 0 1 29")));
        Assert.Contains("자신의 실행 번호보다 커야", Err(Rows("5 Jump 5")));
        Assert.Contains("마스터에 없습니다", Err(Rows("1 Jump 9")));
        Assert.Contains("두 번", Err(Rows("1 None", "1 None")));
        Assert.Contains("'Explode'", Err(Rows("1 Explode")));
    }
}
