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
}
