namespace Wera.Core;

public sealed partial class GameSession
{
    /// <summary>이벤트 진행 화면의 "[1] 다음"(선택지 장면에서는 [1]부터가 선택지).</summary>
    public const string NextEventStepCode = "1";

    /// <summary>진행 중인 이벤트. 이벤트 진행 화면이 아니면 null(세이브에는 담기지 않는다).</summary>
    public EventExecution? CurrentEvent { get; private set; }

    /// <summary>이벤트 진행 화면이 지금 보여 주는 장면(ADV 재생 또는 전투). 그 화면이 아니면 null.</summary>
    public EventStep? CurrentStep { get; private set; }

    /// <summary>재생 중인 ADV. 이벤트 장면이 ADV가 아니면 null.</summary>
    public AdvExecution? CurrentAdv { get; private set; }

    /// <summary>재생 중인 ADV가 지금 보여 주는 장면(대사 또는 선택지 묶음). ADV가 아니면 null.</summary>
    public AdvStep? CurrentAdvStep { get; private set; }

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

    /// <summary>"다음": ADV를 재생 중이면 ADV의 다음 장면으로, 아니면 이벤트의 다음 장면으로 간다.</summary>
    void ContinueEvent()
    {
        if (CurrentAdv is not null) ShowNextAdvStep();
        else ShowNextEventStep();
    }

    /// <summary>진행 중인 이벤트의 다음 장면으로 간다. 이벤트가 끝나면 이벤트 화면으로 돌아간다.</summary>
    void ShowNextEventStep()
    {
        CurrentAdv = null;
        CurrentAdvStep = null;
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
        if (step.Kind == EventStepKind.Adv)
        {
            CurrentAdv = new AdvExecution(Pack, step.Code, PlayerFlags);
            ShowNextAdvStep();                          // 보여 줄 장면이 없는 ADV는 바로 이벤트의 다음 장면으로 넘어간다
        }
    }

    /// <summary>재생 중인 ADV의 다음 장면으로 간다. ADV가 끝나면 이벤트의 다음 장면으로 간다.</summary>
    void ShowNextAdvStep()
    {
        var step = CurrentAdv!.Advance();
        if (step.Kind == AdvStepKind.Finished) ShowNextEventStep();
        else CurrentAdvStep = step;
    }

    /// <summary>선택지 장면에서 index번째(0부터) 선택지를 고른다.</summary>
    void ChooseAdv(int index)
    {
        if (CurrentAdv?.Choose(index) == true) ShowNextAdvStep();
    }

    /// <summary>이벤트 진행 화면의 항목: 선택지 장면이면 [1]부터 선택지들, 아니면 [1] 다음.</summary>
    IReadOnlyList<ScreenEntry> EventPlayEntries() =>
        CurrentAdvStep is { Kind: AdvStepKind.Choice } choice
            ? choice.Choices
                .Select((c, i) => new ScreenEntry(Text(i + 1), c.Text, Screen.EventPlay, EntryAction.ChooseAdv, Arg: i.ToString()))
                .ToList()
            : [new ScreenEntry(NextEventStepCode, "다음", Screen.EventPlay, EntryAction.NextEventStep)];
}
