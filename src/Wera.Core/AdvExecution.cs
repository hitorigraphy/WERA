namespace Wera.Core;

/// <summary>ADV를 실행하다가 바깥(화면)이 보여 줘야 하는 장면의 종류.</summary>
public enum AdvStepKind
{
    /// <summary>나의 대사(TextMe).</summary>
    TextMe,
    /// <summary>상대의 대사(TextYou).</summary>
    TextYou,
    /// <summary>화자 없는 일반 텍스트(TextNormal).</summary>
    TextNormal,
    /// <summary>선택지 묶음. 하나를 골라야 이어 갈 수 있다.</summary>
    Choice,
    /// <summary>ADV가 끝났다.</summary>
    Finished,
}

/// <summary>선택지 하나: 보여 줄 문구와, 고르면 점프할 흐름 번호.</summary>
public readonly record struct AdvChoice(long FlowNumber, string Text, long JumpTo);

/// <summary>ADV를 실행하다 만난 장면 하나. 대사면 Text, 선택지 묶음이면 Choices가 차 있다.</summary>
public readonly record struct AdvStep(
    AdvStepKind Kind, long FlowNumber, string Text, IReadOnlyList<AdvChoice> Choices)
{
    public static readonly AdvStep Finished = new(AdvStepKind.Finished, 0, "", []);
}

/// <summary>
/// ADV 하나를 흐름 번호 순서대로 실행한다. 이벤트 실행과 같은 규칙이다: 조건이 거짓인 칸은 건너뛰고(조건이 없으면 참),
/// 더 진행할 칸이 없거나 Finish를 만나면 끝나며, None은 아무 일도 없고, Jump는 param_1 번호부터 이어 간다.
/// TextMe/TextYou/TextNormal은 <see cref="Advance"/>가 그 대사를 돌려주고 멈춘다.
/// Choice는 같은 선택지 그룹(param_1)의 이어진 행들 중 조건을 만족하는 것을 한 묶음으로 돌려주고 멈추며(만족하는 것이 없으면 건너뛴다),
/// <see cref="Choose"/>로 하나를 고르면 그 선택지의 param_2 번호부터 이어 간다.
/// </summary>
public sealed class AdvExecution
{
    readonly IReadOnlyList<AdvFlow> flows;       // 흐름 번호 순
    readonly IReadOnlyDictionary<long, long> flags;
    int next;
    IReadOnlyList<AdvChoice>? pending;           // 고르기를 기다리는 선택지 묶음

    public long AdvCode { get; }

    public bool IsFinished { get; private set; }

    public AdvExecution(GamePack pack, long advCode, IReadOnlyDictionary<long, long> flags)
    {
        AdvCode = advCode;
        this.flags = flags;
        flows = pack.AdvFlowsOf(advCode);
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
            if (!flow.IsAvailable(flags)) continue;

            switch (flow.FlowType)
            {
                case AdvFlowType.Finish:
                    IsFinished = true;
                    break;
                case AdvFlowType.Jump:
                    JumpTo(flow.Param1);
                    break;
                case AdvFlowType.TextMe:
                    return new AdvStep(AdvStepKind.TextMe, flow.FlowNumber, flow.Text, []);
                case AdvFlowType.TextYou:
                    return new AdvStep(AdvStepKind.TextYou, flow.FlowNumber, flow.Text, []);
                case AdvFlowType.TextNormal:
                    return new AdvStep(AdvStepKind.TextNormal, flow.FlowNumber, flow.Text, []);
                case AdvFlowType.Choice:
                    // 이 칸부터 같은 그룹의 이어진 선택지를 모은다(조건이 거짓인 선택지는 빠진다).
                    var group = flow.Param1;
                    var choices = new List<AdvChoice>();
                    var at = next - 1;
                    for (; at < flows.Count && flows[at].FlowType == AdvFlowType.Choice && flows[at].Param1 == group; at++)
                        if (flows[at].IsAvailable(flags))
                            choices.Add(new AdvChoice(flows[at].FlowNumber, flows[at].Text, flows[at].Param2));
                    next = at;                           // 묶음 전체를 지나쳤다
                    if (choices.Count == 0) break;       // 고를 수 있는 선택지가 없으면 건너뛴다
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
