using Wera.Core;

namespace Wera.Core.Tests;

// 방어전 피해: 0~최대 방어력의 무작위 피해를 서 있는 적들에게 무작위로 나눠 배분한다.
public class DefenseDamageTests
{
    [Fact]
    public void The_damage_is_between_zero_and_the_max_defense()
    {
        Assert.Equal(0, DefenseDamage.Roll(30, _ => 0));
        Assert.Equal(30, DefenseDamage.Roll(30, bound => bound - 1));       // 모수는 최대 방어력 + 1
        Assert.Equal(0, DefenseDamage.Roll(0, _ => throw new InvalidOperationException()));
        Assert.Equal(0, DefenseDamage.Roll(-5, _ => throw new InvalidOperationException()));
    }

    [Fact]
    public void The_damage_is_split_among_living_enemies_only_and_never_over_their_hp()
    {
        var random = new Random(1);
        for (var trial = 0; trial < 500; trial++)
        {
            long[] hp = [random.Next(0, 20), random.Next(0, 20), 0, random.Next(0, 20)];
            var damage = random.Next(0, 80);
            var dealt = DefenseDamage.Distribute(damage, hp, bound => random.NextInt64(bound));
            Assert.Equal(Math.Min(damage, hp.Sum()), dealt.Sum());          // 쓸 수 있는 만큼 모두 쓴다
            Assert.Equal(0, dealt[2]);                                      // 쓰러진 적은 맞지 않는다
            for (var i = 0; i < hp.Length; i++) Assert.InRange(dealt[i], 0, hp[i]);
        }
    }

    [Fact]
    public void An_overkill_share_is_passed_on_to_the_enemies_still_standing()
    {
        // 적 둘(체력 3, 100)에 피해 50: 첫 번째 몫이 3을 넘으면 넘친 만큼 다시 나뉘어 결국 50이 모두 들어간다.
        long[] hp = [3, 100];
        var dealt = DefenseDamage.Distribute(50, hp, bound => bound - 1);
        Assert.Equal(50, dealt.Sum());
        Assert.True(dealt[0] <= 3);
    }

    [Fact]
    public void No_damage_or_no_living_enemy_deals_nothing()
    {
        Assert.Equal([0, 0], DefenseDamage.Distribute(0, [5, 5], _ => 0));
        Assert.Equal([0, 0], DefenseDamage.Distribute(10, [0, 0], _ => 0));
        Assert.Empty(DefenseDamage.Distribute(10, [], _ => 0));
    }
}
