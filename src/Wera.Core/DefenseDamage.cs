namespace Wera.Core;

/// <summary>
/// 방어전에서 오퍼레이터나 시설이 부딪칠 때의 피해 계산. 피해량은 0 이상 최대 방어력 이하에서 무작위로 정하고,
/// 쓰러지지 않은 적들에게 무작위로 나눠 배분한다(한 명의 체력을 넘는 몫은 아직 서 있는 다른 적에게 다시 나눈다).
/// </summary>
public static class DefenseDamage
{
    /// <summary>피해량: 0 이상 maxDefense 이하의 무작위 정수(maxDefense가 0 이하면 0).</summary>
    public static long Roll(long maxDefense, Func<long, long> random) =>
        maxDefense <= 0 ? 0 : random(maxDefense == long.MaxValue ? maxDefense : maxDefense + 1);

    /// <summary>
    /// damage를 체력이 남은 적(hp &gt; 0)들에게 나눠 적마다 입는 피해를 돌려준다(hp와 같은 길이, 쓰러진 적은 0).
    /// 적마다 체력을 넘는 피해는 받지 않고, 합계는 damage와 살아 있는 적의 체력 합 중 작은 값이다.
    /// </summary>
    public static long[] Distribute(long damage, IReadOnlyList<long> hp, Func<long, long> random)
    {
        var dealt = new long[hp.Count];
        var alive = Enumerable.Range(0, hp.Count).Where(i => hp[i] > 0).ToList();
        while (damage > 0 && alive.Count > 0)
        {
            for (var i = alive.Count - 1; i > 0; i--)                      // 몫을 받는 순서를 무작위로 섞는다(앞 순서가 유리하지 않도록)
            {
                var j = (int)random(i + 1);
                (alive[i], alive[j]) = (alive[j], alive[i]);
            }
            var remaining = damage;
            long excess = 0;
            for (var n = 0; n < alive.Count; n++)
            {
                var index = alive[n];
                var share = n == alive.Count - 1 ? remaining : random(remaining + 1);
                remaining -= share;
                var apply = Math.Min(share, hp[index] - dealt[index]);
                dealt[index] += apply;
                excess += share - apply;
            }
            damage = excess;
            alive = alive.Where(i => dealt[i] < hp[i]).ToList();
        }
        return dealt;
    }
}
