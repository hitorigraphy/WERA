namespace Wera.Core;

public sealed partial class GameSession
{
    /// <summary>이벤트 진행 화면의 "[1] 다음".</summary>
    public const string NextEventStepCode = "1";

    /// <summary>진행 중인 이벤트. 이벤트 진행 화면이 아니면 null(세이브에는 담기지 않는다).</summary>
    public EventExecution? CurrentEvent { get; private set; }

    /// <summary>이벤트 진행 화면이 지금 보여 주는 장면(ADV 재생 또는 전투). 그 화면이 아니면 null.</summary>
    public EventStep? CurrentStep { get; private set; }

    /// <summary>이벤트 조건을 만족해 지금 시작할 수 있는 이벤트(이벤트 마스터 순서).</summary>
    public IReadOnlyList<GameEvent> AvailableEvents =>
        Pack.Events?.Where(e => e.IsAvailable(PlayerFlags)).ToList() ?? [];

    /// <summary>이벤트 진행 화면에 나오는 장면 안내: "ADV: 이름" 또는 "전투: 이름". 그 화면이 아니면 null.</summary>
    public string? CurrentStepText => CurrentStep switch
    {
        { Kind: EventStepKind.Adv } step => $"ADV: {Pack.FindAdv(step.Code)?.Name ?? Text(step.Code)}",
        { Kind: EventStepKind.Battle } step => $"전투: {Pack.FindBattle(step.Code)?.Name ?? Text(step.Code)}",
        _ => null,
    };

    /// <summary>이벤트를 시작해 첫 장면으로 간다. 시작할 수 없으면 이벤트 화면에 머문다.</summary>
    void BeginEvent(long eventCode)
    {
        CurrentEvent = StartEvent(eventCode);
        if (CurrentEvent is null)
        {
            Message = "이벤트를 시작할 수 없습니다.";
            return;
        }
        ShowNextEventStep();
    }

    /// <summary>진행 중인 이벤트의 다음 장면으로 간다. 이벤트가 끝나면 이벤트 화면으로 돌아간다.</summary>
    void ShowNextEventStep()
    {
        var step = CurrentEvent?.Advance() ?? new EventStep(EventStepKind.Finished, 0, 0);
        if (step.Kind == EventStepKind.Finished)
        {
            CurrentEvent = null;
            CurrentStep = null;
            Screen = Screen.Event;
            return;
        }
        CurrentStep = step;
        Screen = Screen.EventPlay;
    }
}
