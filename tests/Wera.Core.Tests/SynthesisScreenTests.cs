using Wera.Core;

namespace Wera.Core.Tests;

// 장비 강화 화면의 합성(타겟 고르기, 제물 고르기, 실행/취소)과 유니크 아이템 강화 스테이터스 마스터.
public class SynthesisScreenTests
{
    static GamePack SamplePack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(read);
    }

    static GameSession EnhanceScreen(GamePack pack, params string[] ids)
    {
        var s = GameSession.NewGame(pack);
        foreach (var _ in ids) s.AddUniqueItem(8001);
        s.Activate(GameSession.EquipmentEnhanceEntryCode);
        s.Activate("901");                                                                 // 합성
        return s;
    }

    static string Labels(GameSession s) => string.Join(",", s.Entries.Where(e => e.Action != EntryAction.ListPage).Select(e => $"{e.Label}{(e.Enabled ? "" : "x")}"));

    [Fact]
    public void Picking_a_target_enables_only_the_sacrifices_then_execute_applies_the_synthesis()
    {
        var s = EnhanceScreen(SamplePack(), "a", "b");
        var swords = s.Entries.Where(e => e.Label == "철검").ToList();
        Assert.Equal(2, swords.Count);
        Assert.Equal(["합성", "제련", "개조"], s.Entries.Where(e => e.Action == EntryAction.SelectEnhanceMode).Select(e => e.Label));
        s.Activate(swords[0].Code);                                                       // 타겟
        Assert.Equal(Screen.EquipmentEnhance, s.Screen);
        Assert.Equal(["순례자의 은단검x", "철검 (대상)x", "철검", "실행x", "취소", "돌아가기"], Labels(s).Split(','));
        Assert.Equal(["901", "902"], s.Entries.Where(e => e.Code is "901" or "902").Select(e => e.Code));
        Assert.Equal(Screen.EquipmentEnhance, s.Activate("901")!.Target);                  // 실행은 아직 눌리지 않는다

        var targetId = s.EnhanceTargetId!;
        s.Activate(swords[1].Code);                                                       // 제물 하나(제물 수 기본 1)로 다 찼다
        Assert.Equal(["순례자의 은단검x", "철검 (대상)x", "철검 (제물)x", "실행", "취소", "돌아가기"], Labels(s).Split(','));
        s.Activate("901");
        Assert.Equal("합성했습니다: 철검", s.Message);
        Assert.Equal([targetId], s.UniqueItemInstances.Where(kv => kv.Value.ItemCode == 8001).Select(kv => kv.Key));   // 제물은 없어졌다
        var item = s.UniqueItemInstances[targetId];
        Assert.Equal(1, item.Statuses![GamePack.EnhanceCountStatusCode]);                  // 흐름이 강화 횟수를 올렸고
        Assert.Equal(15, item.Statuses[40]);                                              // 강화 레벨 1의 값 5가 초깃값 10에 더해졌다
        Assert.Null(s.EnhanceTargetId);
        Assert.Empty(s.SynthesisSacrificeIds);
        Assert.Equal(["합성", "제련", "개조"], s.Entries.Where(e => e.Action == EntryAction.SelectEnhanceMode).Select(e => e.Label));
    }

    [Fact]
    public void Cancel_resets_the_target_and_sacrifices_at_any_time_and_every_sacrifice_must_be_chosen()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            (GamePack.UniqueItemInventoryMasterPath, "inventory_code\titem_name\n8001\t철검\n"),
            (GamePack.GameSettingMasterPath, "setting_type\tvalue_type\tlong_value\tdecimal_value\ttext_value\nSynthesisSacrificeCount\tLong\t2\t\t\n")));
        var s = EnhanceScreen(pack, "a", "b", "c");
        var swords = s.Entries.Where(e => e.Label == "철검").ToList();
        Assert.All(swords, e => Assert.True(e.Enabled));                                   // 같은 코드가 셋이라 제물 둘이 있다
        s.Activate(swords[0].Code);
        s.Activate(swords[1].Code);                                                       // 제물 하나 더 필요하다
        Assert.Equal(["철검 (대상)x", "철검 (제물)x", "철검", "실행x", "취소", "돌아가기"], Labels(s).Split(','));
        Assert.False(s.Entries.First(e => e.Code == "901").Enabled);
        s.Activate("902");                                                                // 취소
        Assert.Null(s.EnhanceTargetId);
        Assert.Empty(s.SynthesisSacrificeIds);
        Assert.Equal(EnhanceMode.Synthesis, s.EnhanceMode);

        s.Activate(swords[0].Code);
        s.Activate(swords[1].Code);
        s.Activate(swords[2].Code);                                                       // 다 골랐다
        Assert.True(s.Entries.First(e => e.Code == "901").Enabled);
        Assert.All(s.Entries.Where(e => e.Action == EntryAction.SelectSacrifice), e => Assert.False(e.Enabled));
        s.Activate("902");                                                                // 다 골라도 취소하면 리셋
        Assert.Empty(s.SynthesisSacrificeIds);
        Assert.Equal(3, s.UniqueItemInstances.Count(kv => kv.Value.ItemCode == 8001));
    }

    static GamePack EnhancePack(string enhanceRows) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n8\tenhance_count\n40\tATK\n"),
        (GamePack.UniqueItemInventoryMasterPath, "inventory_code\titem_name\n8001\t철검\n8002\t대검\n"),
        (GamePack.UniqueItemStatusMasterPath, "item_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n8001\t8\t0\t0\t99\n8001\t40\t10\t0\t30\n"),
        (GamePack.UniqueItemEnhanceStatusMasterPath, "item_code\tstatus_code\t강화_level\tvalue\n" + enhanceRows)));

    [Fact]
    public void The_enhance_status_master_adds_the_value_of_the_current_level_to_the_initial_value_within_the_range()
    {
        var pack = EnhancePack("8001\t40\t1\t5\n8001\t40\t2\t100\n");
        UniqueItemInstance At(long level) => pack.WithEnhanceStatuses(new UniqueItemInstance("x", 8001, 0, null,
            new Dictionary<long, long> { [8] = level, [40] = 999 }));
        Assert.Equal(15, At(1).Statuses![40]);
        Assert.Equal(30, At(2).Statuses![40]);                                            // 10 + 100은 최댓값 30으로
        Assert.Equal(10, At(0).Statuses![40]);                                            // 그 레벨의 행이 없으면 초깃값(+0)
        Assert.Equal(1, At(1).Statuses![8]);                                              // 다른 스테이터스는 그대로
        var plain = new UniqueItemInstance("y", 8002, 0, null, new Dictionary<long, long> { [8] = 1 });
        Assert.Same(plain, pack.WithEnhanceStatuses(plain));                              // 행이 없는 아이템은 그대로
    }

    [Theory]
    [InlineData("9999\t40\t1\t5\n")]                              // 없는 아이템
    [InlineData("8001\t77\t1\t5\n")]                              // 없는 스테이터스
    [InlineData("8001\t8\t1\t5\n8001\t8\t1\t6\n")]                // 같은 키 두 번
    [InlineData("8001\t40\t-1\t5\n")]                             // 음수 레벨
    [InlineData("8001\t40\tx\t5\n")]                              // 정수 아님
    [InlineData("8002\t40\t1\t5\n")]                              // 아이템 스테이터스 마스터에 없는 (아이템, 스테이터스)
    public void Bad_enhance_status_rows_are_rejected(string rows) => Assert.Throws<PackFormatException>(() => EnhancePack(rows));

    [Fact]
    public void The_sample_pack_has_the_enhance_status_master()
    {
        var pack = SamplePack();
        Assert.Equal(4, pack.UniqueItemEnhanceStatuses!.Count);
        Assert.Equal(new UniqueItemEnhanceStatus(8001, 40, 2, 12), pack.UniqueItemEnhanceStatuses[2]);
    }
}
