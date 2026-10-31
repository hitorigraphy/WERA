namespace Wera.Core;

/// <summary>
/// 전투에서 스킬의 이펙트가 읽고 쓰는 유닛. 스테이터스 값은 캐릭터 스테이터스 마스터의 행(초깃값, 최솟값, 최댓값)에서 만들고
/// decimal로 다룬다. 값을 바꾸면 그 스테이터스의 최솟값~최댓값으로 맞춘다.
/// </summary>
public sealed class BattleUnit
{
    readonly Dictionary<long, (decimal Value, decimal Min, decimal Max)> statuses = [];

    public long CharacterCode { get; }

    BattleUnit(long characterCode) => CharacterCode = characterCode;

    /// <summary>
    /// 캐릭터의 마스터로 유닛을 만든다: 마스터에 행이 있는 모든 스테이터스를 그 레벨의 행(없으면 레벨 0 행)의 초깃값으로 채운다.
    /// 레벨을 주지 않으면 level 스테이터스의 레벨 0 초깃값이다.
    /// </summary>
    public static BattleUnit Create(GamePack pack, long characterCode, long? level = null)
    {
        var at = level ?? pack.InitialCharacterStatus(characterCode, GamePack.LevelStatusCode)?.InitialValue ?? 1;
        var unit = new BattleUnit(characterCode);
        foreach (var status in pack.CharacterStatusesOf(characterCode).Select(r => r.StatusCode).Distinct())
            if (pack.CharacterStatusAt(characterCode, status, at) is { } row)
                unit.statuses[status] = (row.InitialValue, row.MinValue, row.MaxValue);
        return unit;
    }

    /// <summary>
    /// 캐릭터 인스턴스로 유닛을 만든다: 인스턴스의 모든 스테이터스를 현재 값으로 채우고, 범위는 인스턴스의 레벨에 맞는 캐릭터 스테이터스 마스터 행의
    /// 최솟값~최댓값이다(행이 없는 스테이터스는 범위 제한이 없다).
    /// </summary>
    public static BattleUnit FromInstance(GamePack pack, CharacterInstance character)
    {
        var unit = new BattleUnit(character.CharacterCode);
        foreach (var (status, value) in character.Statuses)
        {
            var row = pack.CharacterStatusAt(character.CharacterCode, status, character.Level);
            unit.statuses[status] = (value, row?.MinValue ?? decimal.MinValue, row?.MaxValue ?? decimal.MaxValue);
        }
        return unit;
    }

    /// <summary>스테이터스 값들을 정수로 돌려준다(소수는 버림).</summary>
    public Dictionary<long, long> ToStatuses() =>
        statuses.ToDictionary(kv => kv.Key, kv => (long)Math.Truncate(Math.Clamp(kv.Value.Value, long.MinValue, long.MaxValue)));

    /// <summary>스테이터스 값. 이 유닛에게 그 스테이터스가 없으면 null.</summary>
    public decimal? ValueOf(long statusCode) => statuses.TryGetValue(statusCode, out var s) ? s.Value : null;

    /// <summary>스테이터스 값을 바꾼다(최솟값~최댓값으로 맞춤). 그 스테이터스가 없으면 false.</summary>
    public bool SetValue(long statusCode, decimal value)
    {
        if (!statuses.TryGetValue(statusCode, out var s)) return false;
        statuses[statusCode] = s with { Value = Math.Clamp(value, s.Min, s.Max) };
        return true;
    }

    /// <summary>스테이터스 값에 더한다(음수면 뺀다). 그 스테이터스가 없으면 false.</summary>
    public bool ModValue(long statusCode, decimal delta) =>
        statuses.TryGetValue(statusCode, out var s) && SetValue(statusCode, s.Value + delta);
}
