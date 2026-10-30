using Wera.Core;

namespace Wera.Core.Tests;

// JumpRandom: param_1 확률, param_2 모수, param_3 target_flow_number. rand(모수) < 확률 이면 점프
public class JumpRandomTests
{
    static GameSession Session(bool viaAdv, long chance, long bound)
    {
        var eventRows = viaAdv
            ? "3001\t1\tAdv\t4001\t0\t0\n"
            : $"3001\t1\tJumpRandom\t{chance}\t{bound}\t3\n3001\t2\tAdv\t4001\t0\t0\n3001\t3\tAdv\t4002\t0\t0\n";
        var advRows = viaAdv
            ? $"4001\t1\tJumpRandom\t{chance}\t{bound}\t3\t\t\n4001\t2\tTextPlain\t0\t0\t0\t건너뛴 칸\t\n4001\t3\tTextPlain\t0\t0\t0\t도착한 칸\t\n"
            : "";
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + eventRows),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + advRows),
        };
        return GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
    }

    /// <summary>뽑힌 값이 roll로 정해졌을 때 점프했는가. bound는 칸의 모수가 그대로 난수 동작에 넘어갔는지 확인하는 데 쓴다.</summary>
    static bool Jumped(bool viaAdv, long chance, long bound, long roll, out long askedBound)
    {
        var s = Session(viaAdv, chance, bound);
        long asked = -1;
        s.NextRandom = b => { asked = b; return roll; };
        askedBound = 0;
        bool jumped = viaAdv
            ? new AdvFlowCircuit(s.Pack, 4001, s.PlayerFlags, random: s.NextRandom).Advance().Text == "도착한 칸"
            : s.StartEvent(3001)!.Advance().Code == 4002;
        askedBound = asked;
        return jumped;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void It_jumps_exactly_when_the_roll_is_below_the_chance(bool viaAdv)
    {
        // 30, 100, 5 → rand(100) < 30 이면 점프: 0..29는 점프, 30..99는 점프하지 않는다
        Assert.True(Jumped(viaAdv, 30, 100, 0, out _));
        Assert.True(Jumped(viaAdv, 30, 100, 29, out _));
        Assert.False(Jumped(viaAdv, 30, 100, 30, out _));
        Assert.False(Jumped(viaAdv, 30, 100, 99, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_bound_is_what_the_dice_are_rolled_with(bool viaAdv)
    {
        Jumped(viaAdv, 30, 100, 0, out var asked);
        Assert.Equal(100, asked);
        Jumped(viaAdv, 1, 6, 0, out asked);
        Assert.Equal(6, asked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_chance_of_zero_or_less_never_jumps_and_one_at_or_above_the_bound_always_does(bool viaAdv)
    {
        foreach (var roll in new long[] { 0, 50, 99 })
        {
            Assert.False(Jumped(viaAdv, 0, 100, roll, out _));
            Assert.False(Jumped(viaAdv, -5, 100, roll, out _));
            Assert.True(Jumped(viaAdv, 100, 100, roll, out _));
            Assert.True(Jumped(viaAdv, 1000, 100, roll, out _));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_default_random_gives_about_the_stated_share(bool viaAdv)
    {
        const int trials = 4000;
        var hits = 0;
        var s = Session(viaAdv, 50, 100);                                           // 한 번 만들어 두고 같은 칸을 되풀이해 돌린다
        for (var i = 0; i < trials; i++)
        {
            var jumped = viaAdv
                ? new AdvFlowCircuit(s.Pack, 4001, s.PlayerFlags).Advance().Text == "도착한 칸"             // 난수 동작을 안 주면 기본 난수
                : s.StartEvent(3001)!.Advance().Code == 4002;                                          // 세션의 기본 난수
            if (jumped) hits++;
        }
        Assert.InRange(hits, trials * 0.4, trials * 0.6);
    }

    [Fact]
    public void A_bound_of_one_always_rolls_zero()
    {
        var s = Session(true, 1, 1);                                                // rand(1)은 늘 0이므로 0 < 1 → 늘 점프
        for (var i = 0; i < 50; i++)
            Assert.Equal("도착한 칸", new AdvFlowCircuit(s.Pack, 4001, s.PlayerFlags).Advance().Text);
    }

    // ---- 로드 검증 ----

    static string Error(bool viaAdv, long chance, long bound, long target)
    {
        var files = viaAdv
            ? new (string, string)[]
            {
                ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"),
                ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" +
                    $"4001\t1\tJumpRandom\t{chance}\t{bound}\t{target}\t\t\n4001\t5\tFinish\t0\t0\t0\t\t\n4002\t7\tFinish\t0\t0\t0\t\t\n"),
            }
            : new (string, string)[]
            {
                ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/event_master_v1.tsv", "code\tname\n3001\tA\n3002\tB\n"),
                ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" +
                    $"3001\t1\tJumpRandom\t{chance}\t{bound}\t{target}\n3001\t5\tFinish\t0\t0\t0\n3002\t7\tFinish\t0\t0\t0\n"),
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
    public void The_bound_must_be_at_least_one_and_the_target_a_larger_flow_of_the_same_owner(bool viaAdv)
    {
        Assert.Equal("", Error(viaAdv, 30, 100, 5));
        Assert.Equal("", Error(viaAdv, -3, 1, 5));                                  // 확률은 어떤 정수든 된다
        Assert.Contains("모수 '0'는 1 이상", Error(viaAdv, 30, 0, 5));
        Assert.Contains("모수 '-100'는 1 이상", Error(viaAdv, 30, -100, 5));
        Assert.Contains("보다 커야", Error(viaAdv, 30, 100, 1));
        Assert.Contains("없습니다", Error(viaAdv, 30, 100, 9));
        Assert.Contains("없습니다", Error(viaAdv, 30, 100, 7));                       // 다른 이벤트/ADV의 흐름
    }

    [Fact]
    public void The_enum_numbers_are_fixed()
    {
        Assert.Equal(22, (int)EventFlowType.JumpRandom);
        Assert.Equal(24, (int)AdvFlowType.JumpRandom);
        Assert.True(HaveTagJump.IsConditionalJump("JumpRandom"));
    }
}
