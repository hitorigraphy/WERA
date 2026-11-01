using Wera.Core;

namespace Wera.Core.Tests;

// 제련: 제련 흐름 마스터와 서킷(제련석 소모, 확률, 대실패/실패/성공), 제련 화면, 제련석 설정.
public class SmeltTests
{
    static GamePack SamplePack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(read);
    }

    static UniqueItemInstance Sword(GamePack pack, long enhance = 0) =>
        new("x", 8001, 0, null, new Dictionary<long, long>(pack.MasterStatusesOfUniqueItem(8001)) { [GamePack.EnhanceCountStatusCode] = enhance });

    [Fact]
    public void The_sample_flow_is_ten_percent_great_failure_sixty_percent_failure_thirty_percent_success()
    {
        var pack = SamplePack();
        var items = new Dictionary<string, UniqueItemInstance> { ["x"] = Sword(pack) };

        // 0~9: 대실패(타겟을 없애고 제련석 소모)
        var great = new SmeltFlowCircuit(pack, new UnitStore([], items), items["x"], _ => 9);
        Assert.Null(great.Execute());
        Assert.Equal((1L, true), (great.StonesConsumed, great.TargetDeleted));
        Assert.Empty(items);

        // 10~69: 실패(제련석만 소모)
        var fail = new SmeltFlowCircuit(pack, Sword(pack), _ => 10);
        Assert.Equal(0, fail.Execute()!.Statuses![GamePack.EnhanceCountStatusCode]);
        Assert.Equal((1L, false), (fail.StonesConsumed, fail.TargetDeleted));
        Assert.Equal(0, new SmeltFlowCircuit(pack, Sword(pack), _ => 69).Execute()!.Statuses![GamePack.EnhanceCountStatusCode]);

        // 70~99: 성공(강화 +1, 제련석 소모)
        var win = new SmeltFlowCircuit(pack, Sword(pack, 4), _ => 70);
        var item = win.Execute()!;
        Assert.Equal(5, item.Statuses![GamePack.EnhanceCountStatusCode]);
        Assert.Equal(10 + pack.UniqueItemEnhanceStatuses!.First(r => r.ItemCode == 8001 && r.EnhanceLevel == 5).Value, item.Statuses[40]);   // 강화 레벨 5의 값이 반영
        Assert.Equal(1, win.StonesConsumed);
    }

    [Fact]
    public void A_smelt_at_the_enhance_level_max_stays_at_the_max()
    {
        var pack = SamplePack();
        var item = new SmeltFlowCircuit(pack, Sword(pack, 20), _ => 99).Execute()!;
        Assert.Equal(20, item.Statuses![GamePack.EnhanceCountStatusCode]);
    }

    static GamePack FlowPack(string flows) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n"),
        (GamePack.UniqueItemInventoryMasterPath, "inventory_code\titem_name\n8001\t철검\n"),
        (GamePack.SmeltFlowMasterPath, "execute_number\tflow_type\tvalue_1\tvalue_2\tvalue_3\n" + flows)));

    [Fact]
    public void Smelt_only_flow_types_are_checked_at_load_and_the_flows_run_in_order()
    {
        Assert.Contains("1 이상", Assert.Throws<PackFormatException>(() => FlowPack("1\tRandomToCache\t0\t0\n")).Message);
        Assert.Contains("cache_index", Assert.Throws<PackFormatException>(() => FlowPack("1\tRandomToCache\t-1\t5\n")).Message);
        Assert.Contains("Explode", Assert.Throws<PackFormatException>(() => FlowPack("1\tExplode\n")).Message);
        var pack = FlowPack("2\tConsumeSmeltStone\n1\tConsumeSmeltStone\n3\tConsumeSmeltStone\n");
        Assert.Equal([1L, 2L, 3L], pack.SmeltFlowsInOrder().Select(f => f.ExecuteNumber));
        var circuit = new SmeltFlowCircuit(pack, new UniqueItemInstance("x", 8001));
        circuit.Execute();
        Assert.Equal(3, circuit.StonesConsumed);                                              // 흐름이 소모하는 만큼만(여러 번이면 여러 개)
        Assert.Null(GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"))).SmeltFlows);   // 마스터는 선택
    }

    static GameSession SmeltScreen(long stones, long roll, long swordEnhance = 0)
    {
        var s = GameSession.NewGame(SamplePack());
        var id = s.AddUniqueItem(8001)!;
        s.UniqueItemInstances[id] = s.UniqueItemInstances[id] with
        {
            Statuses = new Dictionary<long, long>(s.UniqueItemInstances[id].Statuses!) { [GamePack.EnhanceCountStatusCode] = swordEnhance },
        };
        s.AddInventory(5, stones);
        s.NextRandom = _ => roll;
        s.Activate(GameSession.EquipmentEnhanceEntryCode);
        s.Activate("902");                                                                   // 제련
        return s;
    }

    static ScreenEntry Row(GameSession s) => s.Entries.First(e => e.Label.StartsWith("철검"));

    [Fact]
    public void Smelt_rows_need_a_stone_and_picking_a_target_shows_execute_and_cancel()
    {
        var none = SmeltScreen(stones: 0, roll: 99);
        Assert.False(Row(none).Enabled);                                                     // 제련석이 없으면 회색

        var s = SmeltScreen(stones: 2, roll: 99);
        Assert.True(Row(s).Enabled);
        s.Activate(Row(s).Code);
        Assert.Equal(Screen.EquipmentEnhance, s.Screen);
        Assert.Contains(s.Entries, e => e.Label == "철검 (대상)");
        Assert.All(s.Entries.Where(e => e.Action == EntryAction.SelectSacrifice), e => Assert.False(e.Enabled));
        Assert.Equal(["실행", "취소"], s.Entries.Where(e => e.Code is "901" or "902").Select(e => e.Label));
        Assert.True(s.Entries.First(e => e.Code == "901").Enabled);                          // 제물이 없어 바로 실행할 수 있다
        s.Activate("902");                                                                   // 취소
        Assert.Null(s.EnhanceTargetId);
        Assert.Equal(2, s.CountInventory(5));                                                // 제련석은 그대로
    }

    [Fact]
    public void Executing_a_smelt_spends_a_stone_and_reports_success_failure_or_great_failure()
    {
        var win = SmeltScreen(stones: 3, roll: 99, swordEnhance: 2);
        win.Activate(Row(win).Code);
        win.Activate("901");
        Assert.Equal("제련 성공: 철검 +3", win.Message);
        Assert.Equal(2, win.CountInventory(5));
        Assert.Equal(3, win.UniqueItemInstances.Values.Single(i => i.ItemCode == 8001).Statuses![GamePack.EnhanceCountStatusCode]);
        Assert.Null(win.EnhanceTargetId);
        Assert.Equal(EnhanceMode.Smelt, win.EnhanceMode);

        var fail = SmeltScreen(stones: 3, roll: 30, swordEnhance: 2);
        fail.Activate(Row(fail).Code);
        fail.Activate("901");
        Assert.Equal("제련 실패: 철검", fail.Message);
        Assert.Equal(2, fail.CountInventory(5));
        Assert.Equal(2, fail.UniqueItemInstances.Values.Single(i => i.ItemCode == 8001).Statuses![GamePack.EnhanceCountStatusCode]);

        var great = SmeltScreen(stones: 3, roll: 5, swordEnhance: 2);
        great.Activate(Row(great).Code);
        great.Activate("901");
        Assert.Equal("제련 대실패: 철검이(가) 사라졌습니다", great.Message);
        Assert.Equal(2, great.CountInventory(5));
        Assert.DoesNotContain(great.UniqueItemInstances.Values, i => i.ItemCode == 8001);    // 타겟이 삭제됐다
    }

    [Fact]
    public void The_smelt_stone_setting_must_be_a_common_item()
    {
        GamePack Pack(string setting) => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            (GamePack.CommonItemInventoryMasterPath, "inventory_code\titem_name\tcarry_limit\n5\t제련석\t99\n"),
            (GamePack.GameSettingMasterPath, "setting_type\tvalue_type\tlong_value\tdecimal_value\ttext_value\n" + setting)));
        Assert.Equal(5, Pack("SmeltStoneItemCode\tLong\t5\t\t\n").SmeltStoneItemCode);
        Assert.Null(Pack("ListPageSize\tLong\t5\t\t\n").SmeltStoneItemCode);
        Assert.Contains("제련석", Assert.Throws<PackFormatException>(() => Pack("SmeltStoneItemCode\tLong\t6\t\t\n")).Message);
        Assert.Throws<PackFormatException>(() => Pack("SmeltStoneItemCode\tLong\t0\t\t\n"));
        Assert.Throws<PackFormatException>(() => Pack("SmeltStoneItemCode\tDecimal\t\t5\t\n"));
    }

    [Fact]
    public void A_piece_at_the_enhance_level_max_is_gray_in_smelt_mode()
    {
        Assert.False(Row(SmeltScreen(stones: 3, roll: 99, swordEnhance: 20)).Enabled);       // 상한 20
        Assert.True(Row(SmeltScreen(stones: 3, roll: 99, swordEnhance: 19)).Enabled);
    }
}
