namespace Wera.Core;

/// <summary>
/// 오퍼레이터 슬롯 하나의 방어력 흐름(<see cref="DefenseFlow"/>)을 흐름 번호 순서로 실행해, 그 슬롯에 선 캐릭터 한 명의 최대 방어력을 계산한다.
/// 화면에 나오는 칸이 없어 끝까지 멈추지 않고 달리며, 방어력은 0 아래로 내려가지 않는다.
/// </summary>
public sealed class DefenseFlowCircuit
{
    IReadOnlyList<DefenseFlow> flows;                // 흐름 번호 순(서브모듈 안이면 그 서브모듈의 칸들)
    readonly IReadOnlyDictionary<long, IReadOnlyList<DefenseFlow>>? submodules;
    readonly long operatorCode;
    readonly Func<long, long, long> statusOf;         // (캐릭터 코드, 스테이터스 코드) → 값
    readonly Func<long, long, bool> characterHasTag;
    readonly IReadOnlyDictionary<long, long> flags;
    readonly Func<long, long>? random;
    long defense;
    int next;

    /// <param name="operatorCode">슬롯에 선 캐릭터 코드.</param>
    public DefenseFlowCircuit(
        IReadOnlyList<DefenseFlow> flows, long operatorCode, Func<long, long, long> statusOf, Func<long, long, bool> characterHasTag,
        IReadOnlyDictionary<long, long> flags, Func<long, long>? random = null, IReadOnlyDictionary<long, IReadOnlyList<DefenseFlow>>? submodules = null)
    {
        this.submodules = submodules;
        this.flows = flows;
        this.operatorCode = operatorCode;
        this.statusOf = statusOf;
        this.characterHasTag = characterHasTag;
        this.flags = flags;
        this.random = random;
    }

    /// <summary>서킷을 끝까지 실행해 최대 방어력(0 이상)을 돌려준다.</summary>
    public long Execute()
    {
        while (More())
        {
            var flow = flows[next++];
            switch (flow.FlowType)
            {
                case DefenseFlowType.Finish:
                    if (!Return()) return Math.Max(0, defense);      // 서브모듈 안의 Finish는 서브모듈만 끝낸다
                    break;
                case DefenseFlowType.JumpToSubmodule:
                    Call(flow.Param1);
                    break;
                case DefenseFlowType.Jump:
                    JumpTo(flow.Param1);
                    break;
                case DefenseFlowType.AddDefense:
                    defense = Add(defense, flow.Param1);
                    break;
                case DefenseFlowType.AddDefenseByStatus:
                    defense = Add(defense, (long)((decimal)statusOf(operatorCode, flow.Param1) * flow.Param2 / 100m));
                    break;
                case var jump when FlagJump.Of(jump.ToString()) is { } comparison:
                    if (FlagJump.Test(comparison, flags.GetValueOrDefault(flow.Param1), flow.Param2)) JumpTo(flow.Param3);
                    break;
                case DefenseFlowType.JumpOperatorHaveTag:
                    if (characterHasTag(operatorCode, flow.Param1)) JumpTo(flow.Param3);
                    break;
                case DefenseFlowType.JumpRandom:
                    if (RandomJump.Hit(flow.Param1, RandomJump.Roll(flow.Param2, random))) JumpTo(flow.Param3);
                    break;
            }
        }
        return Math.Max(0, defense);
    }

    // long 범위를 넘으면 끝값에서 멈춘다.
    static long Add(long current, long delta)
    {
        var sum = unchecked(current + delta);
        if (delta > 0 && sum < current) return long.MaxValue;
        if (delta < 0 && sum > current) return long.MinValue;
        return sum;
    }

    void JumpTo(long flowNumber) => next = flows.TakeWhile(f => f.FlowNumber < flowNumber).Count();

    // 서브모듈 호출 스택(한 단계): 부른 흐름과 돌아갈 자리. 서브모듈 안에서 멈춰도(대사, 선택지, 전투) 다음 호출은 그 자리에서 이어 가고, 끝나면 부른 칸의 다음으로 돌아온다.
    IReadOnlyList<DefenseFlow>? callerFlows;
    int callerNext;

    /// <summary>서브모듈을 부른다(서브모듈 안에서는 부를 수 없고, 없는 서브모듈이면 아무 일도 하지 않는다. 로드가 막는다).</summary>
    void Call(long code)
    {
        if (callerFlows is not null || submodules is null || !submodules.TryGetValue(code, out var submodule)) return;
        (callerFlows, callerNext) = (flows, next);
        (flows, next) = (submodule, 0);
    }

    /// <summary>서브모듈 안이면 부른 흐름의 다음 칸으로 돌아간다. 서브모듈 안이 아니면 false.</summary>
    bool Return()
    {
        if (callerFlows is null) return false;
        (flows, next, callerFlows) = (callerFlows, callerNext, null);
        return true;
    }

    /// <summary>더 볼 칸이 있는가(서브모듈의 끝이면 부른 흐름으로 돌아가 이어 본다).</summary>
    bool More()
    {
        while (next >= flows.Count)
            if (!Return()) return false;
        return true;
    }
}
