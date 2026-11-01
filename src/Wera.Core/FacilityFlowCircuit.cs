namespace Wera.Core;

/// <summary>
/// 시설 하나의 흐름(<see cref="FacilityFlow"/>)을 흐름 번호 순서로 실행한다(턴 종료 뒤 시설마다). 이벤트 흐름과 같은 칸(SetFlag, ModFlag, AddInventory, 점프)에
/// 오퍼레이터(시설의 오퍼레이터 슬롯에 선 캐릭터)를 보는 칸이 더해진 실행기로, 화면에 나오는 칸은 없어 끝까지 멈추지 않고 달린다.
/// </summary>
public sealed class FacilityFlowCircuit
{
    readonly IReadOnlyList<FacilityFlow> flows;       // 흐름 번호 순
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
        Func<long, long>? random = null)
    {
        FacilityCode = facilityCode;
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
        while (next < flows.Count)
        {
            var flow = flows[next++];
            switch (flow.FlowType)
            {
                case FacilityFlowType.Finish:
                    return;
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
}
