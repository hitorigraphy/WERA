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
    public void The_sample_pack_synthesis_raises_the_enhance_count_up_to_the_status_maximum()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(read);
        var item = new UniqueItemInstance("", 8001, 0, null, pack.MasterStatusesOfUniqueItem(8001));
        var once = new SynthesisFlowCircuit(pack, item).Execute();
        Assert.Equal(1, once.Statuses![GamePack.EnhanceCountStatusCode]);
        var capped = once with { Statuses = new Dictionary<long, long>(once.Statuses) { [GamePack.EnhanceCountStatusCode] = 99 } };
        Assert.Equal(99, new SynthesisFlowCircuit(pack, capped).Execute().Statuses![GamePack.EnhanceCountStatusCode]);   // 최댓값 99
    }
}
