using Wera.Core;

namespace Wera.Core.Tests;

// JumpInventory*: 가진 개수를 value(param_2)와 견주어 참이면 target_flow_number(param_3)로 점프, 거짓이면 다음 칸으로
public class JumpInventoryTests
{
    static readonly string[] Kinds = ["Equal", "NotEqual", "GreaterThan", "GreaterOrEqual", "LessThan", "LessOrEqual"];

    // 가진 개수 5를 value 5, 3, 9와 견준 결과(같다 / 크다 / 작다)
    public static IEnumerable<object[]> Cases =>
    [
        ["Equal", true, false, false],
        ["NotEqual", false, true, true],
        ["GreaterThan", false, true, false],
        ["GreaterOrEqual", true, true, false],
        ["LessThan", false, false, true],
        ["LessOrEqual", true, false, true],
    ];

    static string Files(bool viaAdv, string kind, long code, long value, out (string, string)[] files)
    {
        var eventRows = viaAdv
            ? "3001\t1\tAdv\t4001\t0\t0\n"
            : $"3001\t1\tJumpInventory{kind}\t{code}\t{value}\t3\n3001\t2\tAdv\t4001\t0\t0\n3001\t3\tAdv\t4002\t0\t0\n";
        var advRows = viaAdv
            ? $"4001\t1\tJumpInventory{kind}\t{code}\t{value}\t3\t\t\n4001\t2\tTextPlain\t0\t0\t0\t건너뛴 칸\t\n4001\t3\tTextPlain\t0\t0\t0\t도착한 칸\t\n"
            : "";
        files =
        [
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n3\t약초\t99\n4\t포션\t99\n"),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"),
            ("master/bunch_item_inventory_master_v1.tsv", "inventory_code\titem_name\tpart_item_code\tpart_amount\n9001\t세트\t3\t2\n9001\t세트\t4\t1\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + eventRows),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + advRows),
        ];
        return "";
    }

    static GameSession Session(bool viaAdv, string kind, long code, long value)
    {
        Files(viaAdv, kind, code, value, out var files);
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
    }

    /// <summary>점프했으면 true(도착한 칸이 나온다), 점프하지 않았으면 false. 세션으로 돌리므로 가진 개수를 세션이 알려 준다.</summary>
    static bool Jumped(GameSession s, bool viaAdv)
    {
        if (viaAdv) return new AdvFlowCircuit(s.Pack, 4001, s.PlayerFlags, s.AddInventory, s.CountInventory).Advance().Text == "도착한 칸";
        return s.StartEvent(3001)!.Advance().Code == 4002;                          // 점프하면 3번 칸(ADV 4002), 아니면 2번 칸(ADV 4001)
    }

    static GameSession WithCount(bool viaAdv, string kind, long code, long value, Action<GameSession> give)
    {
        var s = Session(viaAdv, kind, code, value);
        give(s);
        return s;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Each_comparison_jumps_only_when_true_for_a_common_item_in_events_and_advs(string kind, bool equal, bool greater, bool less)
    {
        foreach (var viaAdv in new[] { false, true })
        {
            Assert.Equal(equal, Jumped(WithCount(viaAdv, kind, 3, 5, s => s.AddInventory(3, 5)), viaAdv));
            Assert.Equal(greater, Jumped(WithCount(viaAdv, kind, 3, 3, s => s.AddInventory(3, 5)), viaAdv));
            Assert.Equal(less, Jumped(WithCount(viaAdv, kind, 3, 9, s => s.AddInventory(3, 5)), viaAdv));
        }
    }

    [Fact]
    public void The_count_is_zero_for_what_the_player_does_not_have()
    {
        foreach (var viaAdv in new[] { false, true })
        {
            Assert.True(Jumped(Session(viaAdv, "Equal", 3, 0), viaAdv));            // 없는 아이템은 0개
            Assert.False(Jumped(Session(viaAdv, "GreaterThan", 3, 0), viaAdv));
            Assert.True(Jumped(Session(viaAdv, "Equal", 8001, 0), viaAdv));          // 유니크 아이템 0개
            Assert.True(Jumped(Session(viaAdv, "Equal", 7001, 0), viaAdv));          // 캐릭터 미보유 0
        }
    }

    [Fact]
    public void The_count_by_kind_common_bunch_unique_and_character()
    {
        var s = Session(false, "Equal", 3, 0);
        s.AddInventory(3, 5);
        s.AddInventory(4, 2);
        Assert.Equal(5, s.CountInventory(3));                                       // 커먼: 보유 개수
        Assert.Equal(2, s.CountInventory(9001));                                    // 번치(약초×2 + 포션×1): 5÷2=2, 2÷1=2 → 2세트
        s.AddInventory(3, 1);                                                       // 약초 6, 포션 2
        Assert.Equal(2, s.CountInventory(9001));                                    // 포션이 모자란 쪽이 세트 수를 정한다
        s.AddInventory(4, 5);                                                       // 약초 6, 포션 7
        Assert.Equal(3, s.CountInventory(9001));                                    // 약초 6÷2=3, 포션 7÷1=7 → 3세트

        Assert.Equal(0, s.CountInventory(8001));
        s.AddInventory(8001, 1);
        s.AddInventory(8001, 1);
        Assert.Equal(2, s.CountInventory(8001));                                    // 유니크: 인스턴스 수

        Assert.Equal(0, s.CountInventory(7001));
        s.AddInventory(7001, 1);
        Assert.Equal(1, s.CountInventory(7001));                                    // 캐릭터: 보유하면 1
        Assert.Equal(1, s.CountInventory(1));                                       // 플레이어는 처음부터 보유

        Assert.Equal(0, s.CountInventory(123456));                                  // 마스터에 없는 코드는 0
    }

    [Fact]
    public void A_jump_inventory_flow_can_branch_on_what_AddInventory_just_gave()
    {
        // 이벤트: 약초를 1개 얻고(AddInventory), 1개 이상이면 3번으로 점프(JumpInventory) — 같은 이벤트 안에서 바뀐 인벤토리가 바로 보인다
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n3\t약초\t99\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"),
            ("master/event_flow_master_v1.tsv",
                "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" +
                "3001\t1\tAddInventory\t3\t1\t0\n3001\t2\tJumpInventoryGreaterOrEqual\t3\t1\t4\n3001\t3\tAdv\t4001\t0\t0\n3001\t4\tAdv\t4002\t0\t0\n"),
        };
        var s = GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
        Assert.Equal(4002, s.StartEvent(3001)!.Advance().Code);
    }

    [Fact]
    public void Without_a_session_the_count_is_zero()
    {
        var s = Session(true, "GreaterThan", 3, 0);
        s.AddInventory(3, 5);
        var run = new AdvFlowCircuit(s.Pack, 4001, s.PlayerFlags);                    // 세션 없이 직접 돌리면 가진 개수를 알 수 없어 0으로 본다
        Assert.Equal("건너뛴 칸", run.Advance().Text);
    }

    // ---- 로드 검증 ----

    static string Error(bool viaAdv, string kind, long code, long target, long? flow = null)
    {
        var f = flow ?? 1;
        var files = viaAdv
            ? new (string, string)[]
            {
                ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"),
                ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" +
                    $"4001\t{f}\tJumpInventory{kind}\t{code}\t1\t{target}\t\t\n4001\t5\tFinish\t0\t0\t0\t\t\n4002\t7\tFinish\t0\t0\t0\t\t\n"),
            }
            : new (string, string)[]
            {
                ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/event_master_v1.tsv", "code\tname\n3001\tA\n3002\tB\n"),
                ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" +
                    $"3001\t{f}\tJumpInventory{kind}\t{code}\t1\t{target}\n3001\t5\tFinish\t0\t0\t0\n3002\t7\tFinish\t0\t0\t0\n"),
            };
        try
        {
            GamePack.Load(CoreTests.PackZip(files));
            return "";
        }
        catch (PackFormatException ex) { return ex.Message; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_code_must_be_in_the_inventory_code_space_and_the_target_must_be_a_larger_flow_of_the_same_owner(bool viaAdv)
    {
        Assert.Equal("", Error(viaAdv, "Equal", 2, 5));                              // 금화(커먼 아이템 2)
        Assert.Equal("", Error(viaAdv, "GreaterThan", 1, 5));                        // 캐릭터 1(플레이어)
        Assert.Contains("인벤토리 코드 '777'가 인벤토리 마스터에 없습니다", Error(viaAdv, "Equal", 777, 5));
        Assert.Contains("보다 커야", Error(viaAdv, "Equal", 2, 1));                    // 자기 자신
        Assert.Contains("보다 커야", Error(viaAdv, "Equal", 2, 0));                    // 앞쪽
        Assert.Contains("없습니다", Error(viaAdv, "Equal", 2, 9));                     // 없는 흐름
        Assert.Contains("없습니다", Error(viaAdv, "Equal", 2, 7));                     // 다른 이벤트/ADV의 흐름
    }

    [Fact]
    public void The_enum_numbers_are_fixed_and_the_names_cover_all_six_comparisons()
    {
        Assert.Equal([14, 15, 16, 17, 18, 19],
            new[] { EventFlowType.JumpInventoryEqual, EventFlowType.JumpInventoryNotEqual, EventFlowType.JumpInventoryGreaterThan,
                    EventFlowType.JumpInventoryGreaterOrEqual, EventFlowType.JumpInventoryLessThan, EventFlowType.JumpInventoryLessOrEqual }
                .Select(t => (int)t));
        Assert.Equal([16, 17, 18, 19, 20, 21],
            new[] { AdvFlowType.JumpInventoryEqual, AdvFlowType.JumpInventoryNotEqual, AdvFlowType.JumpInventoryGreaterThan,
                    AdvFlowType.JumpInventoryGreaterOrEqual, AdvFlowType.JumpInventoryLessThan, AdvFlowType.JumpInventoryLessOrEqual }
                .Select(t => (int)t));
        Assert.All(Kinds, k => Assert.NotNull(InventoryJump.Of("JumpInventory" + k)));
        Assert.Null(InventoryJump.Of("JumpFlagEqual"));
        Assert.Null(FlagJump.Of("JumpInventoryEqual"));
        Assert.Null(InventoryJump.Of("JumpInventoryNope"));
    }
}
