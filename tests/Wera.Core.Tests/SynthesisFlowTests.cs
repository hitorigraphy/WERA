using Wera.Core;

namespace Wera.Core.Tests;

// synthesis_flow_master와 합성 흐름 서킷.
public class SynthesisFlowTests
{
    const string Head = "execute_number\tflow_type\tvalue_1\tvalue_2\tvalue_3\n";
    const long Durability = 21;
    const long Power = 22;

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(string flows) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n21\tdurability\n22\tpower\n9\tghost\n"),
        (GamePack.UniqueItemInventoryMasterPath, "inventory_code\titem_name\n8001\t철검\n8002\t대검\n"),
        (GamePack.UniqueItemStatusMasterPath, "item_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n" +
            Rows("8001 21 100 0 100", "8001 22 5 0 50")),
        (GamePack.SynthesisFlowMasterPath, Head + flows)));

    static GamePack PackWithout() => GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));

    static UniqueItemInstance Run(GamePack pack, long item = 8001) =>
        new SynthesisFlowCircuit(pack, new UniqueItemInstance("", item, 0, null, pack.MasterStatusesOfUniqueItem(item))).Execute();

    [Fact]
    public void Rows_are_read_in_execute_number_order_and_the_master_is_optional()
    {
        var pack = Pack(Rows("20 None 1 2 3", "10 none"));
        Assert.Equal([10L, 20L], pack.SynthesisFlowsInOrder().Select(f => f.ExecuteNumber));
        Assert.Equal(new SynthesisFlow(20, SynthesisFlowType.None, 1, 2, 3), pack.SynthesisFlowsInOrder()[1]);
        Assert.Null(PackWithout().SynthesisFlows);
        Assert.Empty(PackWithout().SynthesisFlowsInOrder());
    }

    [Fact]
    public void Flows_change_item_statuses_within_the_item_status_master_range()
    {
        // durability(100)를 캐시 0에 읽고, power를 캐시 0의 두 배(200 → 최댓값 50)로, durability를 -30만큼 바꾼다(70).
        var pack = Pack(Rows("1 FocusedItemValueToCache 21 0", "2 SetCacheValue 1 2", "3 CacheMultiply 2 0 1", "4 CacheToFocusedItemSetValue 22 2",
            "5 SetCacheValue 3 -30", "6 CacheToFocusedItemModValue 21 3"));
        var item = Run(pack);
        Assert.Equal(50, item.Statuses![Power]);
        Assert.Equal(70, item.Statuses[Durability]);
    }

    [Fact]
    public void Decimals_are_truncated_and_statuses_the_item_lacks_do_nothing()
    {
        var pack = Pack(Rows("1 SetCacheValue 0 255 1", "2 CacheToFocusedItemSetValue 21 0", "3 CacheToFocusedItemSetValue 9 0"));
        var item = Run(pack);                             // 25.5 → 25
        Assert.Equal(25, item.Statuses![Durability]);
        Assert.False(item.Statuses.ContainsKey(9));
        Assert.Empty(Run(pack, 8002).Statuses!);           // 행이 없는 아이템은 빈 Statuses 그대로
    }

    [Fact]
    public void Jump_and_finish_follow_the_skill_effect_rules_and_are_checked_at_load()
    {
        var pack = Pack(Rows("1 SetCacheValue 0 1", "2 SetCacheValue 1 2", "3 JumpCacheLessThan 0 1 5",
            "4 CacheToFocusedItemSetValue 21 0", "5 Finish", "6 CacheToFocusedItemSetValue 22 0"));
        var item = Run(pack);
        Assert.Equal((100, 5), (item.Statuses![Durability], item.Statuses[Power]));

        string Err(string rows) => Assert.Throws<PackFormatException>(() => Pack(rows)).Message;
        Assert.Contains("스테이터스 마스터에 없습니다", Err(Rows("1 FocusedItemValueToCache 99 0")));
        Assert.Contains("자신의 실행 번호보다 커야", Err(Rows("5 Jump 5")));
        Assert.Contains("두 번", Err(Rows("1 None", "1 None")));
        Assert.Contains("'Explode'", Err(Rows("1 Explode")));
    }

    [Fact]
    public void The_sample_pack_synthesis_raises_the_enhance_count_up_to_the_enhance_level_max()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(read);
        var item = new UniqueItemInstance("", 8001, 0, null, pack.MasterStatusesOfUniqueItem(8001));
        var once = new SynthesisFlowCircuit(pack, item).Execute();
        Assert.Equal(1, once.Statuses![GamePack.EnhanceCountStatusCode]);
        var capped = once with { Statuses = new Dictionary<long, long>(once.Statuses) { [GamePack.EnhanceCountStatusCode] = 99 } };
        Assert.Equal(20, new SynthesisFlowCircuit(pack, capped).Execute().Statuses![GamePack.EnhanceCountStatusCode]);   // 강화 레벨 상한 20(샘플 팩의 EnhanceLevelMax)
    }

    static UniqueItemInstance Item(GamePack pack, string id, long code = 8001) =>
        new(id, code, 0, null, pack.MasterStatusesOfUniqueItem(code));

    [Fact]
    public void Sacrifices_are_deleted_only_by_the_flow_and_the_target_is_changed()
    {
        // 타겟 포커스 → 캐시 0에 -30 → 내구도 -30(70) → 제물 1번만 없앰
        var pack = Pack(Rows("1 FocusSynthesisTarget", "2 SetCacheValue 0 -30", "3 CacheToFocusedItemModValue 21 0", "4 DeleteSacrifice 1"));
        var target = Item(pack, "T");
        var a = Item(pack, "A");
        var b = Item(pack, "B");
        var circuit = new SynthesisFlowCircuit(pack, target, [a, b]);
        var result = circuit.Execute();
        Assert.Equal(70, result.Statuses![Durability]);
        Assert.Equal([b], circuit.DeletedSacrifices);
        Assert.Equal([a, b], circuit.Sacrifices);

        var none = new SynthesisFlowCircuit(PackWithout(), Item(PackWithout(), "T"), [Item(PackWithout(), "A")]);
        none.Execute();
        Assert.Empty(none.DeletedSacrifices);                                          // 흐름이 없으면 제물은 그대로
    }

    [Fact]
    public void Delete_all_removes_the_sacrifices_from_the_store_but_not_the_target_and_a_deleted_sacrifice_is_not_written_back()
    {
        var pack = Pack(Rows("1 FocusSacrifice 0", "2 SetCacheValue 0 -10", "3 CacheToFocusedItemModValue 21 0", "4 DeleteAllSacrifices", "5 FocusSacrifice 0",
            "6 CacheToFocusedItemModValue 21 0", "7 FocusSynthesisTarget", "8 CacheToFocusedItemModValue 22 0"));
        var items = new Dictionary<string, UniqueItemInstance>();
        foreach (var id in new[] { "T", "A", "B" }) items[id] = Item(pack, id);
        var circuit = new SynthesisFlowCircuit(pack, new UnitStore([], items), items["T"], [items["A"], items["B"]]);
        var target = circuit.Execute();
        Assert.Equal(["T"], items.Keys);                                                 // 제물 둘이 모두 지워졌다
        Assert.Equal(0, target.Statuses![Power]);                                        // 타겟의 power: 5 + (-10) → 최솟값 0
        Assert.Equal(2, circuit.DeletedSacrifices.Count);
    }

    [Fact]
    public void Missing_sacrifices_are_ignored_and_negative_sacrifice_numbers_are_rejected_at_load()
    {
        var pack = Pack(Rows("1 FocusSacrifice 5", "2 SetCacheValue 0 1", "3 CacheToFocusedItemModValue 21 0", "4 DeleteSacrifice 9"));
        var circuit = new SynthesisFlowCircuit(pack, Item(pack, "T"), []);
        Assert.Equal(100, circuit.Execute().Statuses![Durability]);                      // 제물이 없으면 포커스가 비어 아무 일도 없다
        Assert.Empty(circuit.DeletedSacrifices);
        Assert.Contains("0 이상", Assert.Throws<PackFormatException>(() => Pack(Rows("1 FocusSacrifice -1"))).Message);
        Assert.Contains("0 이상", Assert.Throws<PackFormatException>(() => Pack(Rows("1 DeleteSacrifice -2"))).Message);
    }

    [Fact]
    public void The_sample_pack_flow_adds_one_enhance_and_deletes_the_sacrifices_in_a_session()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var s = GameSession.NewGame(GamePack.Load(read));
        var target = s.UniqueItemInstances[s.AddUniqueItem(8001)!];
        var sacrifice = s.UniqueItemInstances[s.AddUniqueItem(8001)!];
        var store = new UnitStore([], s.UniqueItemInstances);
        var result = new SynthesisFlowCircuit(s.Pack, store, target, [sacrifice]).Execute();
        Assert.Equal(1, result.Statuses![GamePack.EnhanceCountStatusCode]);
        Assert.Equal(1, s.UniqueItemInstances[target.InstanceId].Statuses![GamePack.EnhanceCountStatusCode]);
        Assert.False(s.UniqueItemInstances.ContainsKey(sacrifice.InstanceId));
    }
}
