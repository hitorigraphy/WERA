using Wera.Core;

namespace Wera.Core.Tests;

// 이벤트와 ADV의 AddInventory 칸: param_1이 인벤토리 코드, param_2가 개수
public class AddInventoryTests
{
    /// <summary>이벤트 3001이 (via adv가 아니면) 직접 AddInventory 칸들을 갖거나, ADV 4001이 그 칸들을 갖는다.</summary>
    static GameSession Session(bool viaAdv, params (long Code, long Amount)[] ops)
    {
        var eventRows = viaAdv
            ? "3001\t1\tAdv\t4001\t0\t0\n"
            : string.Concat(ops.Select((o, i) => $"3001\t{i + 1}\tAddInventory\t{o.Code}\t{o.Amount}\t0\n"));
        var advRows = viaAdv
            ? string.Concat(ops.Select((o, i) => $"4001\t{i + 1}\tAddInventory\t{o.Code}\t{o.Amount}\t0\t\t\n"))
            : "";
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n3\t약초\t5\n4\t포션\t10\n"),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"),
            ("master/bunch_item_inventory_master_v1.tsv", "inventory_code\titem_name\tpart_item_code\tpart_amount\n9001\t세트\t3\t2\n9001\t세트\t4\t1\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t1\t2\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\t인사\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + eventRows),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + advRows),
        };
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
    }

    static GameSession Play(bool viaAdv, params (long Code, long Amount)[] ops)
    {
        var s = Session(viaAdv, ops);
        s.Activate("103");
        s.Activate("3001");
        return s;
    }

    static long Have(GameSession s, long item) => s.InventoryItems.GetValueOrDefault(item).Amount;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_common_item_is_added_removed_and_stops_at_the_carry_limit_and_at_zero(bool viaAdv)
    {
        Assert.Equal(3, Have(Play(viaAdv, (3, 3)), 3));
        Assert.Equal(5, Have(Play(viaAdv, (3, 3), (3, 10)), 3));                   // 소지 한도 5에서 멈춘다
        Assert.Equal(3, Have(Play(viaAdv, (3, 5), (3, -2)), 3));
        var s = Play(viaAdv, (3, 4), (3, -10));
        Assert.Equal(0, Have(s, 3));
        Assert.DoesNotContain(3, s.InventoryItems.Keys);                           // 0개면 보유 목록에서 사라진다
        Assert.Equal(0, Have(Play(viaAdv, (3, -4)), 3));                           // 없는 것을 빼도 아무 일이 없다
        Assert.Equal(0, Have(Play(viaAdv, (3, 0)), 3));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_bunch_is_unpacked_into_its_parts_for_adding_and_removing(bool viaAdv)
    {
        var s = Play(viaAdv, (9001, 2));
        Assert.Equal((4, 2), (Have(s, 3), Have(s, 4)));
        s = Play(viaAdv, (9001, 2), (9001, -1));
        Assert.Equal((2, 1), (Have(s, 3), Have(s, 4)));
        s = Play(viaAdv, (9001, 100));                                              // 부품마다 소지 한도(약초 5, 포션 10)에서 멈춘다
        Assert.Equal((5, 10), (Have(s, 3), Have(s, 4)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_unique_item_comes_and_goes_one_instance_at_a_time(bool viaAdv)
    {
        var s = Play(viaAdv, (8001, 1), (8001, 1));
        var both = s.InstancesOf(8001).ToList();
        Assert.Equal(2, both.Count);

        s = Play(viaAdv, (8001, 1), (8001, 1), (8001, -1));
        Assert.Single(s.InstancesOf(8001));
        Assert.Empty(Play(viaAdv, (8001, 1), (8001, -1), (8001, -1)).InstancesOf(8001));      // 없을 때 빼도 아무 일이 없다
        Assert.Empty(Play(viaAdv, (8001, -1)).InstancesOf(8001));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_character_joins_once_and_leaves_but_the_player_stays(bool viaAdv)
    {
        var s = Play(viaAdv, (7001, 1), (7001, 1));
        Assert.True(s.Characters.ContainsKey(7001));
        Assert.Equal(2, s.Characters.Count);                                        // 플레이어와 용사

        s = Play(viaAdv, (7001, 1), (7002, 1), (7001, -1));
        Assert.False(s.Characters.ContainsKey(7001));
        Assert.True(s.Characters.ContainsKey(7002));
        Assert.True(s.Characters.ContainsKey(GamePack.PlayerCharacterCode));
        Assert.Empty(Play(viaAdv, (7002, -1)).Characters.Keys.Where(c => c == 7002));          // 없는 캐릭터를 내보내도 아무 일이 없다
    }

    [Fact]
    public void An_item_gained_here_is_saved_and_a_call_without_a_callback_does_nothing()
    {
        var s = Play(false, (3, 2));
        Assert.Equal(2, Have(GameSession.FromSaveJson(s.Pack, s.ToSaveJson()), 3));

        var fresh = Session(false, (3, 2));
        var run = new EventFlowCircuit(fresh.Pack, 3001, fresh.PlayerFlags);           // 세션 없이 직접 돌리면 인벤토리를 건드릴 수 없다
        run.Advance();
        Assert.Equal(0, Have(fresh, 3));
    }

    [Fact]
    public void The_session_method_ignores_unknown_codes_and_zero()
    {
        var s = Session(false);
        s.AddInventory(99999, 5);
        s.AddInventory(3, 0);
        Assert.Empty(s.InventoryItems);
    }

    // ---- 로드 검증 ----

    static string Error(bool viaAdv, params (long Code, long Amount)[] ops) =>
        Assert.Throws<PackFormatException>(() => Session(viaAdv, ops)).Message;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_code_must_be_in_the_inventory_code_space(bool viaAdv)
    {
        Assert.Contains("인벤토리 코드 '12345'가 인벤토리 마스터에 없습니다", Error(viaAdv, (12345, 1)));
        Assert.NotNull(Session(viaAdv, (3, -7), (9001, 3), (2, 0)));                  // 커먼과 번치는 개수가 자유다
    }

    [Theory]
    [InlineData(false, 8001)]
    [InlineData(true, 8001)]
    [InlineData(false, 7001)]
    [InlineData(true, 7001)]
    public void A_unique_item_or_character_count_must_be_plus_or_minus_one(bool viaAdv, long code)
    {
        Assert.NotNull(Session(viaAdv, (code, 1), (code, -1)));
        foreach (var bad in new long[] { 0, 2, -2, 100 })
            Assert.Contains("1 또는 -1이어야", Error(viaAdv, (code, bad)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_player_character_cannot_be_removed(bool viaAdv)
    {
        Assert.NotNull(Session(viaAdv, (1, 1)));
        Assert.Contains("플레이어 캐릭터 '1'를 내보낼 수 없습니다", Error(viaAdv, (1, -1)));
    }

    [Fact]
    public void The_enum_numbers_are_fixed()
    {
        Assert.Equal(7, (int)EventFlowType.AddInventory);
        Assert.Equal(9, (int)AdvFlowType.AddInventory);
    }
}
