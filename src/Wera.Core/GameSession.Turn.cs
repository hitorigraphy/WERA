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
