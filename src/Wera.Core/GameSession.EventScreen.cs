namespace Wera.Core;

public sealed partial class GameSession
{
    /// <summary>이벤트 진행 화면의 "[1] 다음"(선택지 장면에서는 [1]부터가 선택지).</summary>
    public const string NextEventStepCode = "1";

    /// <summary>진행 중인 이벤트. 이벤트 진행 화면이 아니면 null(세이브에는 담기지 않는다).</summary>
    public EventFlowCircuit? CurrentEvent { get; private set; }

    /// <summary>이벤트 진행 화면이 지금 보여 주는 장면(ADV 재생 또는 전투). 그 화면이 아니면 null.</summary>
    public EventStep? CurrentStep { get; private set; }

    /// <summary>재생 중인 ADV. 이벤트 장면이 ADV가 아니면 null.</summary>
    public AdvFlowCircuit? CurrentAdv { get; private set; }

    /// <summary>재생 중인 교류 흐름(교류 진행 화면). 교류 흐름은 ADV 실행기와 따로의 실행기(<see cref="CommunicationFlowCircuit"/>)가 돌린다.</summary>
    public CommunicationFlowCircuit? CurrentCommunicationFlow { get; private set; }

    /// <summary>재생 중인 ADV가 지금 보여 주는 장면(대사 또는 선택지 묶음). ADV가 아니면 null.</summary>
    public AdvStep? CurrentAdvStep { get; private set; }

    readonly List<AdvStep> advLog = [];

    /// <summary>
    /// 재생 중인 ADV에서 지금까지 보여 준 대사들과 고른 선택지(오래된 것부터, 마지막이 최신). 화면은 이것을 위에서 아래로 이어 붙이고
    /// 새 대사가 나올 때마다 맨 아래로 스크롤한다. 새 ADV가 시작되거나 이벤트 장면이 바뀌면 비워진다.
    /// </summary>
    public IReadOnlyList<AdvStep> AdvLog => advLog;

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
        if (CurrentAdv is not null || CurrentCommunicationFlow is not null) ShowNextAdvStep();
        else ShowNextEventStep();
    }

    /// <summary>진행 중인 이벤트의 다음 장면으로 간다. 이벤트가 끝나면 이벤트 화면으로 돌아간다.</summary>
    void ShowNextEventStep()
    {
        CurrentAdv = null;
        CurrentCommunicationFlow = null;
        CurrentAdvStep = null;
        advLog.Clear();
        var step = CurrentEvent?.Advance() ?? new EventStep(EventStepKind.Finished, 0, 0);
        if (step.Kind == EventStepKind.Finished)
        {
            CurrentEvent = null;
            CurrentStep = null;
            Screen = Screen.Event;
            return;
        }
        if (step.Kind == EventStepKind.GameOver)
        {
            CurrentEvent = null;
            CurrentStep = null;
            gameOver = true;
            Screen = Screen.GameOver;
            return;
        }
        CurrentStep = step;
        Screen = Screen.EventPlay;
        if (step.Kind == EventStepKind.Adv)
        {
            CurrentAdv = new AdvFlowCircuit(Pack, step.Code, PlayerFlags, AddInventory, CountInventory, CharacterHasTag, UniqueItemHasTag, NextRandom);
            ShowNextAdvStep();                          // 보여 줄 장면이 없는 ADV는 바로 이벤트의 다음 장면으로 넘어간다
        }
        else if (step.Kind == EventStepKind.Battle) StartEventBattle(step);
    }

    /// <summary>
    /// 이벤트의 Battle 칸: 전투 화면으로 간다. 전투가 끝나면 [999]가 이벤트 진행 화면으로 돌아와 다음 칸부터 이어 간다.
    /// 전투에 나가는 파티는 플레이어(캐릭터 1, 당신)가 있는 파티로 고정이다(파티 A, B, C 어느 쪽이든). 지금 보던 파티는 전투가 끝나면 되돌린다.
    /// </summary>
    void StartEventBattle(EventStep step)
    {
        battleReturnScreen = Screen.EventPlay;
        battlePartyBefore = CurrentPartyIndex;
        var playerParty = Array.FindIndex(Parties, p => p.IndexOf(GamePack.PlayerCharacterCode) >= 0);
        if (playerParty >= 0) CurrentPartyIndex = playerParty;
        battleResultFlag = step.ResultFlag;
        BeginBattle(step.Code);
        Screen = Screen.Battle;
    }

    /// <summary>재생 중인 ADV의 다음 장면으로 간다. ADV가 끝나면 이벤트의 다음 장면으로 간다.</summary>
    void ShowNextAdvStep()
    {
        var step = CurrentCommunicationFlow is { } communicationFlow ? communicationFlow.Advance() : CurrentAdv!.Advance();
        if (step.Kind == AdvStepKind.Finished)
        {
            if (Screen == Screen.CommunicationPlay) EndCommunication();
            else if (CurrentEvent is null && CurrentDungeonCode is not null) ReturnToDungeonAfterAdv();      // 던전의 Adv 효과가 연 ADV
            else ShowNextEventStep();
        }
        else
        {
            CurrentAdvStep = step;
            if (step.Kind != AdvStepKind.Choice) advLog.Add(step);       // 선택지는 목록으로만 나오고 로그에는 대사만 쌓인다
        }
    }

    /// <summary>선택지 장면에서 index번째(0부터) 선택지를 고른다.</summary>
    void ChooseAdv(int index)
    {
        if (CurrentAdvStep is not { Kind: AdvStepKind.Choice } choice || index < 0 || index >= choice.Choices.Count) return;
        var picked = choice.Choices[index];
        if ((CurrentCommunicationFlow is { } communicationFlow ? communicationFlow.Choose(index) : CurrentAdv?.Choose(index)) != true) return;
        advLog.Add(new AdvStep(AdvStepKind.Chosen, picked.FlowNumber, picked.Text, []));      // 고른 선택지는 로그에 남는다
        ShowNextAdvStep();
    }

    /// <summary>
    /// 지금 장면이 아무 키나 누르면(또는 눌러서) 다음으로 넘어가는 장면인가: 이벤트 진행 화면이고 선택지 장면이 아닐 때.
    /// 이런 장면은 "[1] 다음"을 화면에 보여 주지 않지만, 코드 1을 <see cref="Activate"/>하면 넘어간다.
    /// </summary>
    public bool AdvanceOnAnyKey => Screen is Screen.EventPlay or Screen.CommunicationPlay && CurrentAdvStep is not { Kind: AdvStepKind.Choice };

    /// <summary>선택지 항목. 문구는 꾸밈 문법을 적용해 보여 주고(Label은 꾸밈을 뗀 글), 로드 때 문법을 검증하므로 늘 읽힌다.</summary>
    ScreenEntry ChoiceEntry(int index, AdvChoice choice)
    {
        var rich = RichText.TryParse(choice.Text, out _);
        return new ScreenEntry(Text(index + 1), rich?.PlainText ?? choice.Text, Screen, EntryAction.ChooseAdv,
            Arg: index.ToString(), LabelHtml: rich?.ToHtml());
    }

    /// <summary>이벤트 진행 화면의 항목: 선택지 장면이면 [1]부터 선택지들, 아니면 [1] 다음.</summary>
    IReadOnlyList<ScreenEntry> EventPlayEntries() =>
        CurrentAdvStep is { Kind: AdvStepKind.Choice } choice
            ? choice.Choices
                .Select((c, i) => ChoiceEntry(i, c))
                .ToList()
            : [new ScreenEntry(NextEventStepCode, "다음", Screen, EntryAction.NextEventStep)];

    // ---- 교류 진행 ----

    /// <summary>교류 진행 화면에서 재생 중인 교류의 코드. 그 화면이 아니면 null(세이브에는 담기지 않는다).</summary>
    public long? CurrentCommunicationCode { get; private set; }

    /// <summary>교류 화면에서 고른 그룹에 속한 교류들(교류 코드순).</summary>
    IEnumerable<CommunicationEntry> CurrentGroupCommunications =>
        (Pack.Communications ?? []).Where(c => c.GroupCode == CurrentCommunicationGroupCode).OrderBy(c => c.Code);

    /// <summary>교류를 시작해 흐름 재생 화면으로 간다. 재생할 흐름이 없으면 교류 화면에 머문다.</summary>
    void BeginCommunication(long communicationCode)
    {
        var communication = Pack.Communications?.FirstOrDefault(c => c.Code == communicationCode);
        var flows = communication is null ? [] : CommunicationFlowOf(communication.Code);
        if (communication is null || flows.Count == 0)
        {
            Message = "교류 내용이 없습니다.";
            return;
        }
        CurrentCommunicationCode = communicationCode;
        advLog.Clear();
        CurrentAdvStep = null;
        CurrentAdv = null;
        CurrentCommunicationFlow = new CommunicationFlowCircuit(Pack, communicationCode, flows, (communication.IsAssist ? AssistCode : PartnerCode) ?? 0,
            PlayerFlags, AddInventory, CountInventory, CharacterHasTag, UniqueItemHasTag, NextRandom);
        Screen = Screen.CommunicationPlay;
        ShowNextAdvStep();                              // 보여 줄 장면이 없는 흐름은 바로 교류 화면으로 돌아간다
    }

    /// <summary>교류 흐름이 끝났다: 위쪽 슬롯은 그대로이고 아래가 다시 교류 그룹/교류 목록이 된다(고른 그룹은 그대로).</summary>
    void EndCommunication()
    {
        CurrentCommunicationCode = null;
        CurrentAdv = null;
        CurrentCommunicationFlow = null;
        CurrentAdvStep = null;
        advLog.Clear();
        Screen = Screen.Interaction;
    }

    // ---- 교류 이력 ----

    /// <summary>교류 이력에 보여 주는 커맨드 수.</summary>
    public const int CommunicationHistoryLength = 3;

    readonly List<string> communicationHistory = [];      // 오래된 것부터

    /// <summary>
    /// 교류 화면과 교류 재생 화면에서 지금까지 한 커맨드의 이름(코드 번호 없이), 직전 것이 맨 앞이고 <see cref="CommunicationHistoryLength"/>개까지.
    /// 그룹 고르기, 교류 시작, 선택지 고르기가 커맨드다("다음"과 돌아가기는 아니다). 교류 화면에 들어올 때마다 비워지고 저장하지 않는다.
    /// </summary>
    public IReadOnlyList<string> CommunicationHistory =>
        communicationHistory.AsEnumerable().Reverse().Take(CommunicationHistoryLength).ToList();

    /// <summary>교류 화면(재생 포함)에서 한 커맨드의 이름을 이력에 남긴다.</summary>
    void RecordCommunicationCommand(ScreenEntry? entry)
    {
        if (Screen is not (Screen.Interaction or Screen.CommunicationPlay)) return;
        if (entry?.Action is EntryAction.SelectCommunicationGroup or EntryAction.StartCommunication or EntryAction.ChooseAdv)
            communicationHistory.Add(entry.Label);
    }
}
