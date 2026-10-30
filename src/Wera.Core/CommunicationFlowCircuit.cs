namespace Wera.Core;

/// <summary>
/// 교류 흐름 하나(듀오 → 솔로 → 기본 순으로 찾은 것)를 흐름 번호 순서대로 실행한다. ADV와 규칙은 같지만 교류 흐름 종류(<see cref="CommunicationFlowType"/>)에 맞춘
/// 따로의 실행기다: Text는 상대 캐릭터(교류가 is_assist면 어시스트, 아니면 파트너)의 대사이고 TextMe와 force_name은 없다.
/// 장면은 ADV와 같은 <see cref="AdvStep"/>으로 돌려주므로 화면은 그대로 쓴다. 슬롯이 비어 있어 화자 코드가 0이면 이름은 ???이다.
/// </summary>
public sealed class CommunicationFlowCircuit
{
    readonly IReadOnlyList<CommunicationFlowStep> flows;       // 흐름 번호 순
    readonly Dictionary<long, long> flags;
    readonly Action<long, long>? addInventory;
    readonly Func<long, long>? countInventory;
    readonly Func<long, long, bool>? characterHasTag;
    readonly Func<long, long, bool>? uniqueItemHasTag;
    readonly Func<long, long>? random;
    readonly GamePack pack;
    readonly long speakerCode;
    int next;
    IReadOnlyList<AdvChoice>? pending;                         // 고르기를 기다리는 선택지 묶음

    public long CommunicationCode { get; }

    public bool IsFinished { get; private set; }

    /// <param name="speakerCode">Text의 화자 캐릭터 코드(상대 슬롯의 캐릭터). 슬롯이 비어 있으면 0.</param>
    /// <param name="flows">흐름 번호 순의 흐름 칸들.</param>
    public CommunicationFlowCircuit(GamePack pack, long communicationCode, IReadOnlyList<CommunicationFlowStep> flows, long speakerCode,
        Dictionary<long, long> flags, Action<long, long>? addInventory = null, Func<long, long>? countInventory = null,
        Func<long, long, bool>? characterHasTag = null, Func<long, long, bool>? uniqueItemHasTag = null, Func<long, long>? random = null)
    {
        CommunicationCode = communicationCode;
        this.pack = pack;
        this.flows = flows;
        this.speakerCode = speakerCode;
        this.flags = flags;
        this.addInventory = addInventory;
        this.countInventory = countInventory;
        this.characterHasTag = characterHasTag;
        this.uniqueItemHasTag = uniqueItemHasTag;
        this.random = random;
    }

    /// <summary>다음 장면(대사 또는 선택지 묶음)을 돌려준다. 끝났으면 Finished. 선택지를 고르기 전에는 같은 선택지 묶음을 다시 돌려준다.</summary>
    public AdvStep Advance()
    {
        if (pending is not null) return new AdvStep(AdvStepKind.Choice, pending[0].FlowNumber, "", pending);

        while (!IsFinished)
        {
            if (next >= flows.Count)
            {
                IsFinished = true;
                break;
            }

            var flow = flows[next++];

            switch (flow.FlowType)
            {
                case CommunicationFlowType.Finish:
                    IsFinished = true;
                    break;
                case CommunicationFlowType.Jump:
                    JumpTo(flow.Param1);
                    break;
                case var jump when FlagJump.Of(jump.ToString()) is { } comparison:
                    if (FlagJump.Test(comparison, flags.GetValueOrDefault(flow.Param1), flow.Param2)) JumpTo(flow.Param3);
                    break;
                case CommunicationFlowType.SetFlag:
                    PlayerFlagOps.Set(flags, flow.Param1, flow.Param2);
                    break;
                case CommunicationFlowType.ModFlag:
                    PlayerFlagOps.Mod(flags, flow.Param1, flow.Param2);
                    break;
                case var jump when InventoryJump.Of(jump.ToString()) is { } comparison:
                    if (FlagJump.Test(comparison, countInventory?.Invoke(flow.Param1) ?? 0, flow.Param2)) JumpTo(flow.Param3);
                    break;
                case CommunicationFlowType.JumpCharacterHaveTag:
                    if (characterHasTag?.Invoke(flow.Param1, flow.Param2) ?? false) JumpTo(flow.Param3);
                    break;
                case CommunicationFlowType.JumpUniqueItemHaveTag:
                    if (uniqueItemHasTag?.Invoke(flow.Param1, flow.Param2) ?? false) JumpTo(flow.Param3);
                    break;
                case CommunicationFlowType.JumpRandom:
                    // 0 이상 모수 미만의 값을 뽑아 확률보다 작으면 target_flow_number로 간다. 모수는 로드 때 1 이상임을 확인했다.
                    if (RandomJump.Hit(flow.Param1, RandomJump.Roll(flow.Param2, random))) JumpTo(flow.Param3);
                    break;
                case CommunicationFlowType.AddInventory:
                    addInventory?.Invoke(flow.Param1, flow.Param2);
                    break;
                case CommunicationFlowType.Text:
                    var speaker = pack.FindCharacter(speakerCode);
                    return new AdvStep(AdvStepKind.TextYou, flow.FlowNumber, flow.Text, [], speaker?.Name ?? "???", speaker?.Code ?? 0, 0);
                case CommunicationFlowType.TextPlain:
                    return new AdvStep(AdvStepKind.TextPlain, flow.FlowNumber, flow.Text, []);
                case CommunicationFlowType.TextLog:
                    return new AdvStep(AdvStepKind.TextLog, flow.FlowNumber, flow.Text, []);
                case CommunicationFlowType.Choice:
                    // 이 칸부터 같은 그룹의 이어진 선택지를 모은다.
                    var group = flow.Param1;
                    var choices = new List<AdvChoice>();
                    var at = next - 1;
                    for (; at < flows.Count && flows[at].FlowType == CommunicationFlowType.Choice && flows[at].Param1 == group; at++)
                        choices.Add(new AdvChoice(flows[at].FlowNumber, flows[at].Text, flows[at].Param2));
                    next = at;                           // 묶음 전체를 지나쳤다
                    pending = choices;
                    return new AdvStep(AdvStepKind.Choice, choices[0].FlowNumber, "", choices);
            }
        }
        return AdvStep.Finished;
    }

    /// <summary>기다리던 선택지 묶음에서 index번째(0부터)를 골라 그 선택지의 점프 번호부터 이어 간다. 고를 수 없으면 false.</summary>
    public bool Choose(int index)
    {
        if (pending is null || index < 0 || index >= pending.Count) return false;
        var target = pending[index].JumpTo;
        pending = null;
        JumpTo(target);
        return true;
    }

    void JumpTo(long flowNumber) => next = flows.TakeWhile(f => f.FlowNumber < flowNumber).Count();
}
