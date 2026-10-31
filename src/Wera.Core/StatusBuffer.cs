namespace Wera.Core;

/// <summary>
/// 서킷 안에서 스테이터스 값을 소수(decimal)로 들고 있는 작업용 버퍼. 값은 스테이터스 코드 → (현재 값, 최솟값, 최댓값)이고
/// 값을 바꾸면 그 스테이터스의 최솟값~최댓값으로 맞춘다. 서킷이 끝나면 <see cref="ToStatuses"/>로 정수(소수는 버림)로 돌려 인스턴스에 쓴다.
/// </summary>
public sealed class StatusBuffer
{
    readonly Dictionary<long, (decimal Value, decimal Min, decimal Max)> statuses = [];

    /// <summary>값을 한 번이라도 바꿨는가(서킷이 끝날 때 인스턴스에 되돌릴지 정한다).</summary>
    public bool Changed { get; private set; }

    /// <summary>
    /// 스테이터스 값들(코드 → 현재 값)로 버퍼를 만든다. 범위는 <paramref name="range"/>가 스테이터스 코드마다 (최솟값, 최댓값)으로 알려 주고
    /// null이면 범위 제한이 없다.
    /// </summary>
    public static StatusBuffer From(IReadOnlyDictionary<long, long> values, Func<long, (long Min, long Max)?> range)
    {
        var buffer = new StatusBuffer();
        foreach (var (status, value) in values)
        {
            var r = range(status);
            buffer.statuses[status] = (value, r?.Min ?? decimal.MinValue, r?.Max ?? decimal.MaxValue);
        }
        return buffer;
    }

    /// <summary>스테이터스 값들을 정수로 돌려준다(소수는 버림).</summary>
    public Dictionary<long, long> ToStatuses() =>
        statuses.ToDictionary(kv => kv.Key, kv => (long)Math.Truncate(Math.Clamp(kv.Value.Value, long.MinValue, long.MaxValue)));

    /// <summary>스테이터스 값. 그 스테이터스가 없으면 null.</summary>
    public decimal? ValueOf(long statusCode) => statuses.TryGetValue(statusCode, out var s) ? s.Value : null;

    /// <summary>스테이터스 값을 바꾼다(최솟값~최댓값으로 맞춤). 그 스테이터스가 없으면 false.</summary>
    public bool SetValue(long statusCode, decimal value)
    {
        if (!statuses.TryGetValue(statusCode, out var s)) return false;
        statuses[statusCode] = s with { Value = Math.Clamp(value, s.Min, s.Max) };
        Changed = true;
        return true;
    }

    /// <summary>스테이터스 값에 더한다(음수면 뺀다). 그 스테이터스가 없거나 decimal 범위를 넘으면 false.</summary>
    public bool ModValue(long statusCode, decimal delta)
    {
        if (!statuses.TryGetValue(statusCode, out var s)) return false;
        try { return SetValue(statusCode, s.Value + delta); }
        catch (OverflowException) { return false; }
    }
}
