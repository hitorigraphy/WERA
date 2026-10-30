namespace Wera.Core;

/// <summary>스킬 서킷들(실행 서킷, 순서 서킷)이 함께 쓰는 캐시 값 목록의 작은 연산들.</summary>
public static class SkillCache
{
    public enum Op { Add, Subtract, Multiply, Divide }

    /// <summary>캐시의 index번 값. 없으면 null.</summary>
    public static decimal? At(List<decimal> cache, long index) => index >= 0 && index < cache.Count ? cache[(int)index] : null;

    /// <summary>캐시의 index번에 값을 쓴다(0~<see cref="GamePack.MaxCacheIndex"/>만). 캐시가 짧으면 모자란 칸을 0으로 채워 늘린다.</summary>
    public static void Write(List<decimal> cache, long index, decimal value)
    {
        if (index < 0 || index > GamePack.MaxCacheIndex) return;
        while (cache.Count <= index) cache.Add(0m);
        cache[(int)index] = value;
    }

    /// <summary>10의 digits제곱.</summary>
    public static decimal Pow10(long digits)
    {
        var result = 1m;
        for (var i = 0; i < digits; i++) result *= 10m;
        return result;
    }

    /// <summary>사칙연산. 0으로 나누거나 decimal 범위를 넘으면 null.</summary>
    public static decimal? Calculate(Op op, decimal left, decimal right)
    {
        try
        {
            return op switch
            {
                Op.Add => left + right,
                Op.Subtract => left - right,
                Op.Multiply => left * right,
                _ => right == 0m ? null : left / right,
            };
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
