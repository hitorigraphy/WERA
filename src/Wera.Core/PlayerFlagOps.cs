namespace Wera.Core;

/// <summary>이벤트와 ADV의 SetFlag, ModFlag 칸이 함께 쓰는 플레이어 플래그 변경.</summary>
internal static class PlayerFlagOps
{
    /// <summary>플래그를 정한 값으로 바꾼다.</summary>
    public static void Set(Dictionary<long, long> flags, long flagCode, long value) => flags[flagCode] = value;

    /// <summary>플래그에 값을 더한다(음수면 뺀다). 없던 플래그는 0에서 시작하고, 값이 long 범위를 넘으면 끝값에서 멈춘다.</summary>
    public static void Mod(Dictionary<long, long> flags, long flagCode, long delta)
    {
        var current = flags.GetValueOrDefault(flagCode);
        var sum = unchecked(current + delta);
        if (delta > 0 && sum < current) sum = long.MaxValue;
        else if (delta < 0 && sum > current) sum = long.MinValue;
        flags[flagCode] = sum;
    }
}
