namespace Wera.Core;

// 게임 안의 시간: 지금이 몇 번째 턴인가. 턴이 넘어가면 턴 마스터의 다음 턴의 날짜와 시간대로 바뀐다.
public sealed partial class GameSession
{
    /// <summary>지금의 턴 카운트. 턴 마스터에 있는 값이다(새 게임은 가장 작은 턴).</summary>
    public long TurnCount { get; private set; }

    /// <summary>지금의 턴(날짜와 시간대).</summary>
    public TurnEntry CurrentTurn => Pack.FindTurn(TurnCount) ?? Pack.FirstTurn;

    /// <summary>화면에 보이는 게임상 날짜와 시간대: "1년 1월 1일 아침".</summary>
    public string CurrentDateText => Pack.DateText(CurrentTurn);

    /// <summary>지금 날짜의 누계 날짜(턴 마스터의 day_count).</summary>
    public long CurrentDayCount => CurrentTurn.DayCount;

    /// <summary>
    /// 이 캐릭터가 지금 이벤트나 던전 탐사에 참가할 수 있는가: 보유한 캐릭터이고, 이 턴에 이미 참가하지 않았고(한 턴에 한 번),
    /// 방어전에 참가한 날의 다음날(day_count + 1)이 아니어야 한다.
    /// </summary>
    public bool CanExplore(long characterCode) =>
        Characters.TryGetValue(characterCode, out var c)
        && c.ExploreTurn != TurnCount
        && !(c.DefenseDayCount is { } defended && CurrentDayCount == defended + 1);

    /// <summary>이벤트에 참가하는 캐릭터들: 플레이어(캐릭터 1, 당신)가 속한 파티의 유닛 전원과 플레이어(보유 중이면) 자신. 플레이어가 어느 파티에도 없으면 비어 있다.</summary>
    IEnumerable<long> EventParticipants() =>
        (Parties.FirstOrDefault(p => p.IndexOf(GamePack.PlayerCharacterCode) >= 0)?.Units.OfType<PartyUnit>().Select(u => u.CharacterCode) ?? [])
            .Concat(Characters.ContainsKey(GamePack.PlayerCharacterCode) ? [GamePack.PlayerCharacterCode] : []).Distinct();

    /// <summary>이 파티의 유닛이 한 명이라도 지금 이벤트와 던전 탐사에 참가할 수 없으면 false(파티 전원이 참가할 수 있어야 한다).</summary>
    public bool PartyCanExplore(int partyIndex) =>
        Parties[partyIndex].Units.OfType<PartyUnit>().All(u => CanExplore(u.CharacterCode));

    /// <summary>플레이어가 속한 파티가 이벤트에 참가할 수 있는가: 그 파티의 누구라도 참가할 수 없으면 이벤트는 회색이다.</summary>
    public bool CanStartEvent => EventParticipants().All(CanExplore);

    /// <summary>교류에 참가하는 캐릭터들: 주인공(캐릭터 1), 파트너, 어시스트(보유하고 있고 지정된 것만).</summary>
    IEnumerable<long> CommunicationParticipants() =>
        new long?[] { GamePack.PlayerCharacterCode, PartnerCode, AssistCode }.OfType<long>().Where(Characters.ContainsKey).Distinct();

    /// <summary>교류를 할 수 있는가: 주인공, 파트너, 어시스트 중 누구라도 참가할 수 없으면 교류는 할 수 없다(홈의 교류 버튼은 회색이다).</summary>
    public bool CanStartCommunication => CommunicationParticipants().All(CanExplore);

    /// <summary>이 턴에 참가했다고 기록한다(이벤트 시작, 던전 입장, 교류 시작).</summary>
    void RecordExploreOf(IEnumerable<long> characters)
    {
        foreach (var code in characters.ToList()) RecordExplore(code);
    }

    /// <summary>이 캐릭터가 이 턴에 이벤트나 던전 탐사에 참가했다고 기록한다. 보유하지 않은 캐릭터면 false.</summary>
    public bool RecordExplore(long characterCode)
    {
        if (!Characters.TryGetValue(characterCode, out var c)) return false;
        Characters[characterCode] = c with { ExploreTurn = TurnCount };
        return true;
    }

    /// <summary>이 캐릭터가 오늘(지금 day_count) 아지트 방어전에 참가했다고 기록한다. 보유하지 않은 캐릭터면 false.</summary>
    public bool RecordDefense(long characterCode)
    {
        if (!Characters.TryGetValue(characterCode, out var c)) return false;
        Characters[characterCode] = c with { DefenseDayCount = CurrentDayCount };
        return true;
    }

    /// <summary>
    /// 턴을 하나 넘긴다: 턴 마스터에서 다음으로 큰 턴 카운트로 간다. 마지막 턴이라 다음이 없으면 그대로 두고 false.
    /// 턴이 넘어가는 행동이 이 함수를 부른다.
    /// </summary>
    public bool AdvanceTurn()
    {
        if (Pack.NextTurnAfter(TurnCount) is not { } next) return false;
        TurnCount = next.TurnCount;
        return true;
    }
}
