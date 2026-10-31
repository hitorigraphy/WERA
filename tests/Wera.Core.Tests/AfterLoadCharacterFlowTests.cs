using Wera.Core;

namespace Wera.Core.Tests;

// after_load_character_flow_master와 로드 후 캐릭터 서킷.
public class AfterLoadCharacterFlowTests
{
    const string Head = "execute_number\tflow_type\tvalue_1\tvalue_2\tvalue_3\n";

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(string? flows) => GamePack.Load(flows is null
        ? CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"))
        : CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.AfterLoadCharacterFlowMasterPath, flows)));

    [Fact]
    public void Rows_are_read_in_execute_number_order_with_empty_values_as_zero()
    {
        var pack = Pack(Head + Rows("20 None 1 2 3", "10 none"));
        Assert.Equal(2, pack.AfterLoadCharacterFlows!.Count);
        Assert.Equal([10L, 20L], pack.AfterLoadCharacterFlowsInOrder().Select(f => f.ExecuteNumber));
        Assert.Equal(new LoadCharacterFlow(20, LoadCharacterFlowType.None, 1, 2, 3), pack.AfterLoadCharacterFlowsInOrder()[1]);
        Assert.Equal(new LoadCharacterFlow(10, LoadCharacterFlowType.None, 0, 0, 0), pack.AfterLoadCharacterFlowsInOrder()[0]);
    }

    [Fact]
    public void The_master_is_optional()
    {
        var pack = Pack(null);
        Assert.Null(pack.AfterLoadCharacterFlows);
        Assert.Empty(pack.AfterLoadCharacterFlowsInOrder());
    }

    [Fact]
    public void Duplicate_numbers_and_unknown_types_are_rejected()
    {
        Assert.Contains("두 번", Assert.Throws<PackFormatException>(() => Pack(Head + Rows("1 None", "1 None"))).Message);
        Assert.Contains("'Explode'", Assert.Throws<PackFormatException>(() => Pack(Head + Rows("1 Explode"))).Message);
        Assert.Contains("'0'", Assert.Throws<PackFormatException>(() => Pack(Head + Rows("1 0"))).Message);
    }

    [Fact]
    public void The_circuit_runs_on_every_character_after_a_load_and_none_changes_nothing()
    {
        var pack = Pack(Head + Rows("1 None"));
        var s = GameSession.NewGame(pack);
        var before = s.Characters[GamePack.PlayerCharacterCode];
        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(before, loaded.Characters[GamePack.PlayerCharacterCode]);

        var circuit = new AfterLoadCharacterCircuit(pack, before);
        Assert.Equal(before, circuit.Execute());
        Assert.Empty(circuit.CacheValues);
    }

    // ---- 동작: 스킬 실행 이펙트와 같은 구성 ----

    const long Hp = GamePack.HpStatusCode;
    const long Mana = 6;

    static GamePack FlowPack(string flows) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/character_inventory_master_v1.tsv",
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n6\tmana\n7\tghost\n"),
        (GamePack.CharacterStatusMasterPath, "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
            Rows("1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 40 0 40", "7001 6 0 10 0 20")),
        (GamePack.AfterLoadCharacterFlowMasterPath, Head + flows)));

    static CharacterInstance Run(GamePack pack, long character = 7001) =>
        new AfterLoadCharacterCircuit(pack, pack.NewCharacterInstance(character)).Execute();

    [Fact]
    public void Flows_move_values_between_the_character_and_the_cache_and_clamp_to_the_master_range()
    {
        // mana를 캐시 0에 읽고, 캐시 1에 5를 쓰고, 둘을 더해 캐시 2에, 그 값을 mana에 더한다(10+15=25 → 최댓값 20으로 맞춤).
        var pack = FlowPack(Rows("1 CasterValueToCache 6 0", "2 SetCacheValue 1 5", "3 CacheAdd 2 0 1", "4 SetCacheValue 3 10", "5 CacheAdd 2 2 3",
            "6 CacheToTargetModValue 6 2", "7 CacheToCasterSetValue 2 1"));
        var c = Run(pack);
        Assert.Equal(20, c.Statuses[Mana]);
        Assert.Equal(5, c.Hp);                            // hp를 캐시 1(5)로 바꿨다
    }

    [Fact]
    public void Decimals_are_truncated_when_written_back_and_missing_statuses_do_nothing()
    {
        var pack = FlowPack(Rows("1 SetCacheValue 0 25 1", "2 CacheToCasterSetValue 6 0", "3 CacheToCasterSetValue 7 0", "4 CasterValueToCache 7 1"));
        var c = Run(pack);                                // 2.5 → 2
        Assert.Equal(2, c.Statuses[Mana]);
        Assert.False(c.Statuses.ContainsKey(7));
        var circuit = new AfterLoadCharacterCircuit(pack, pack.NewCharacterInstance(7001));
        circuit.Execute();
        Assert.Equal([2.5m], circuit.CacheValues);        // 없는 스테이터스는 캐시를 건드리지 않는다
    }

    [Fact]
    public void Jump_and_finish_follow_the_skill_effect_rules()
    {
        var pack = FlowPack(Rows("1 SetCacheValue 0 3", "2 SetCacheValue 1 3", "3 JumpCacheEqual 0 1 5",
            "4 CacheToCasterSetValue 6 0", "5 Jump 7", "6 CacheToCasterSetValue 2 0", "7 Finish", "8 CacheToCasterSetValue 2 0"));
        var c = Run(pack);
        Assert.Equal(10, c.Statuses[Mana]);               // 4번과 6번은 건너뛰었다
        Assert.Equal(40, c.Hp);                           // Finish 뒤의 8번은 실행하지 않는다
    }

    [Fact]
    public void Row_arguments_and_jump_targets_are_checked_at_load()
    {
        string Err(string rows) => Assert.Throws<PackFormatException>(() => FlowPack(rows)).Message;
        Assert.Contains("스테이터스 마스터에 없습니다", Err(Rows("1 CasterValueToCache 99 0")));
        Assert.Contains("cache_index", Err(Rows("1 CasterValueToCache 6 256")));
        Assert.Contains("value_3(소수 자릿수)", Err(Rows("1 SetCacheValue 0 1 29")));
        Assert.Contains("cache_index", Err(Rows("1 CacheAdd 0 1 300")));
        Assert.Contains("cache_index", Err(Rows("1 JumpCacheEqual 0 300 2", "2 None")));
        Assert.Contains("자신의 실행 번호보다 커야", Err(Rows("5 Jump 5")));
        Assert.Contains("마스터에 없습니다", Err(Rows("1 Jump 9")));
    }

    [Fact]
    public void A_save_load_runs_the_flows_on_every_character()
    {
        var pack = FlowPack(Rows("1 SetCacheValue 0 7", "2 CacheToCasterSetValue 6 0"));
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);
        Assert.Equal(10, s.Characters[7001].Statuses[Mana]);
        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(7, loaded.Characters[7001].Statuses[Mana]);
        Assert.False(loaded.Characters[GamePack.PlayerCharacterCode].Statuses.ContainsKey(Mana));    // 당신에게는 mana가 없다
    }
}
