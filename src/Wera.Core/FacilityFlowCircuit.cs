namespace Wera.Core;

/// <summary>
/// 시설 하나의 흐름(<see cref="FacilityFlow"/>)을 흐름 번호 순서로 실행한다(턴 종료 뒤 시설마다). 이벤트 흐름과 같은 칸(SetFlag, ModFlag, AddInventory, 점프)에
/// 오퍼레이터(시설의 오퍼레이터 슬롯에 선 캐릭터)를 보는 칸이 더해진 실행기로, 화면에 나오는 칸은 없어 끝까지 멈추지 않고 달린다.
/// </summary>
public sealed class FacilityFlowCircuit
{
    IReadOnlyList<FacilityFlow> flows;                // 흐름 번호 순(서브모듈 안이면 그 서브모듈의 칸들)
    readonly IReadOnlyDictionary<long, IReadOnlyList<FacilityFlow>>? submodules;
    readonly Func<long, long?> operatorOf;
    readonly Dictionary<long, long> flags;
    readonly Action<long, long> addInventory;
    readonly Func<long, long> countInventory;
    readonly Func<long, long, bool> characterHasTag;
    readonly Func<long, long, bool> uniqueItemHasTag;
    readonly Func<long, long>? random;
    int next;

    public long FacilityCode { get; }

    /// <param name="operatorOf">오퍼레이터 슬롯 코드로 그 슬롯에 선 캐릭터 코드를 찾는다(비었으면 null).</param>
    public FacilityFlowCircuit(
        long facilityCode, IReadOnlyList<FacilityFlow> flows, Func<long, long?> operatorOf, Dictionary<long, long> flags,
        Action<long, long> addInventory, Func<long, long> countInventory, Func<long, long, bool> characterHasTag, Func<long, long, bool> uniqueItemHasTag,
        Func<long, long>? random = null, IReadOnlyDictionary<long, IReadOnlyList<FacilityFlow>>? submodules = null)
    {
        FacilityCode = facilityCode;
        this.submodules = submodules;
        this.flows = flows;
        this.operatorOf = operatorOf;
        this.flags = flags;
        this.addInventory = addInventory;
        this.countInventory = countInventory;
        this.characterHasTag = characterHasTag;
        this.uniqueItemHasTag = uniqueItemHasTag;
        this.random = random;
    }

    public void Execute()
    {
        while (More())
        {
            var flow = flows[next++];
            switch (flow.FlowType)
            {
                case FacilityFlowType.Finish:
                    if (!Return()) return;               // 서브모듈 안의 Finish는 서브모듈만 끝낸다
                    break;
                case FacilityFlowType.JumpToSubmodule:
                    Call(flow.Param1);
                    break;
                case FacilityFlowType.Jump:
                    JumpTo(flow.Param1);
                    break;
                case FacilityFlowType.SetFlag:
                    PlayerFlagOps.Set(flags, flow.Param1, flow.Param2);
                    break;
                case FacilityFlowType.ModFlag:
                    PlayerFlagOps.Mod(flags, flow.Param1, flow.Param2);
                    break;
                case FacilityFlowType.AddInventory:
                    addInventory(flow.Param1, flow.Param2);
                    break;
                case var jump when FlagJump.Of(jump.ToString()) is { } comparison:
                    if (FlagJump.Test(comparison, flags.GetValueOrDefault(flow.Param1), flow.Param2)) JumpTo(flow.Param3);
                    break;
                case var jump when InventoryJump.Of(jump.ToString()) is { } comparison:
                    if (FlagJump.Test(comparison, countInventory(flow.Param1), flow.Param2)) JumpTo(flow.Param3);
                    break;
                case FacilityFlowType.JumpCharacterHaveTag:
                    if (characterHasTag(flow.Param1, flow.Param2)) JumpTo(flow.Param3);
                    break;
                case FacilityFlowType.JumpUniqueItemHaveTag:
                    if (uniqueItemHasTag(flow.Param1, flow.Param2)) JumpTo(flow.Param3);
                    break;
                case FacilityFlowType.JumpRandom:
                    if (RandomJump.Hit(flow.Param1, RandomJump.Roll(flow.Param2, random))) JumpTo(flow.Param3);
                    break;
                case FacilityFlowType.JumpOperatorExists:
                    if (operatorOf(flow.Param1) is not null) JumpTo(flow.Param3);
                    break;
                case FacilityFlowType.JumpOperatorNotExists:
                    if (operatorOf(flow.Param1) is null) JumpTo(flow.Param3);
                    break;
                case FacilityFlowType.JumpOperatorHaveTag:
                    if (operatorOf(flow.Param1) is { } op && characterHasTag(op, flow.Param2)) JumpTo(flow.Param3);
                    break;
            }
        }
    }

    void JumpTo(long flowNumber) => next = flows.TakeWhile(f => f.FlowNumber < flowNumber).Count();

    // 서브모듈 호출 스택(한 단계): 부른 흐름과 돌아갈 자리. 서브모듈 안에서 멈춰도(대사, 선택지, 전투) 다음 호출은 그 자리에서 이어 가고, 끝나면 부른 칸의 다음으로 돌아온다.
    IReadOnlyList<FacilityFlow>? callerFlows;
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
