namespace Wera.Core;

/// <summary>이벤트를 실행하다가 바깥(화면)이 해 줘야 하는 일의 종류.</summary>
public enum EventStepKind
{
    /// <summary>ADV를 재생한다. <see cref="EventStep.Code"/>가 adv_code다.</summary>
    Adv,
    /// <summary>전투를 개시한다. <see cref="EventStep.Code"/>가 battle_code다.</summary>
    Battle,
    /// <summary>이벤트가 끝났다.</summary>
    Finished,
}

/// <summary>이벤트를 실행하다 만난 일 하나. Finished면 FlowNumber와 Code는 0이다.</summary>
/// <param name="FlowNumber">이 일을 하라고 한 흐름 칸의 번호.</param>
public readonly record struct EventStep(EventStepKind Kind, long FlowNumber, long Code);

/// <summary>
/// 이벤트 하나를 흐름 번호 순서대로 실행한다.
/// 이벤트는 두 경우에 끝난다: (1) 더 진행할 흐름 번호가 없을 때, (2) 흐름 종류가 Finish인 칸을 만났을 때.
/// 칸에는 조건이 없다(조건에 따라 건너뛰려면 JumpFlag*로 뛰어넘는다).
/// 칸마다 종류에 따라: None은 아무 일도 없고, Jump는 param_1 번호부터 이어 가고, JumpFlag*는 플래그를 견주어 참이면 뒤쪽의 칸으로 점프하고 거짓이면 다음 칸으로 가고, SetFlag와 ModFlag는 플레이어 플래그를 바꾸고(멈추지 않고 다음 칸으로, 바뀐 플래그는 뒤의 칸 조건에 바로 보인다), Jump는 param_1 번호로 건너가 거기서부터 이어 가고,
/// Adv와 Battle은 바깥이 해야 하는 일이라 <see cref="Advance"/>가 그 일을 돌려주고 멈춘다(다음 호출이 그 뒤부터 이어 간다).
/// 조건은 그 칸에 닿을 때마다 현재 플래그로 판정하므로, ADV나 전투 사이에 플래그가 바뀌면 그 뒤의 칸에 반영된다.
/// </summary>
public sealed class EventFlowCircuit
{
    readonly IReadOnlyList<EventFlow> flows;       // 흐름 번호 순
    readonly Dictionary<long, long> flags;
    readonly Action<long, long>? addInventory;
    readonly Func<long, long>? countInventory;
    readonly Func<long, long, bool>? characterHasTag;
    readonly Func<long, long, bool>? uniqueItemHasTag;
    readonly Func<long, long>? random;
    int next;                                      // 다음에 볼 칸의 위치(flows 안의 인덱스)

    /// <summary>실행하는 이벤트의 코드.</summary>
    public long EventCode { get; }

    /// <summary>끝났는가(끝난 뒤의 <see cref="Advance"/>는 계속 Finished를 돌려준다).</summary>
    public bool IsFinished { get; private set; }

    /// <param name="flags">조건 판정에 쓰고 SetFlag, ModFlag가 바꾸는 플레이어 플래그. 실행 도중 바뀐 값이 보이도록 같은 사전을 그대로 넘긴다.</param>
    /// <param name="random">JumpRandom 칸이 부르는 동작(모수 → 0 이상 모수 미만의 무작위 정수). 세션이 넘기며, null이면 기본 난수를 쓴다.</param>
    /// <param name="characterHasTag">JumpCharacterHaveTag 칸이 부르는 동작(캐릭터 코드, 태그 코드 → 가졌는가). 세션이 넘기며, null이면 거짓으로 본다.</param>
    /// <param name="uniqueItemHasTag">JumpUniqueItemHaveTag 칸이 부르는 동작(유니크 아이템 코드, 태그 코드 → 가졌는가). 세션이 넘기며, null이면 거짓으로 본다.</param>
    /// <param name="countInventory">JumpInventory* 칸이 부르는 동작(인벤토리 코드 → 가진 개수). 세션이 넘기며, null이면 개수를 0으로 본다.</param>
    /// <param name="addInventory">AddInventory 칸이 부르는 동작(인벤토리 코드, 개수). 세션이 넘기며, null이면 AddInventory 칸은 아무 일도 하지 않는다.</param>
    public EventFlowCircuit(GamePack pack, long eventCode, Dictionary<long, long> flags, Action<long, long>? addInventory = null,
        Func<long, long>? countInventory = null,
        Func<long, long, bool>? characterHasTag = null,
        Func<long, long, bool>? uniqueItemHasTag = null,
        Func<long, long>? random = null)
    {
        EventCode = eventCode;
        this.flags = flags;
        this.addInventory = addInventory;
        this.countInventory = countInventory;
        this.characterHasTag = characterHasTag;
        this.uniqueItemHasTag = uniqueItemHasTag;
        this.random = random;
        flows = pack.FlowsOf(eventCode);
    }

    /// <summary>
    /// 바깥이 해야 하는 다음 일(ADV 재생, 전투 개시)을 만날 때까지 칸을 실행하고 그 일을 돌려준다.
    /// 더 할 일이 없거나 Finish를 만나면 Finished를 돌려주고 이벤트를 끝낸다.
    /// </summary>
    public EventStep Advance()
    {
        while (!IsFinished)
        {
            if (next >= flows.Count)
            {
                IsFinished = true;                       // 더 진행할 흐름 번호가 없다
                break;
            }

            var flow = flows[next++];

            switch (flow.FlowType)
            {
                case EventFlowType.Finish:
                    IsFinished = true;
                    break;
                case EventFlowType.SetFlag:
                    PlayerFlagOps.Set(flags, flow.Param1, flow.Param2);
                    break;
                case EventFlowType.ModFlag:
                    PlayerFlagOps.Mod(flags, flow.Param1, flow.Param2);
                    break;
                case EventFlowType.JumpCharacterHaveTag:
                    if (characterHasTag?.Invoke(flow.Param1, flow.Param2) ?? false) next = flows.TakeWhile(f => f.FlowNumber < flow.Param3).Count();
                    break;
                case EventFlowType.JumpUniqueItemHaveTag:
                    if (uniqueItemHasTag?.Invoke(flow.Param1, flow.Param2) ?? false) next = flows.TakeWhile(f => f.FlowNumber < flow.Param3).Count();
                    break;
                case EventFlowType.JumpRandom:
                    // 0 이상 모수 미만의 값을 뽑아 확률보다 작으면 target_flow_number로 간다. 모수는 로드 때 1 이상임을 확인했다.
                    if (RandomJump.Hit(flow.Param1, RandomJump.Roll(flow.Param2, random))) next = flows.TakeWhile(f => f.FlowNumber < flow.Param3).Count();
                    break;
                case EventFlowType.AddInventory:
                    addInventory?.Invoke(flow.Param1, flow.Param2);
                    break;
                case EventFlowType.Jump:
                    // 점프할 번호의 칸으로 간다(그 칸의 조건이 거짓이면 그다음 큰 번호부터 이어 간다). 번호는 늘 뒤쪽이다.
                    next = flows.TakeWhile(f => f.FlowNumber < flow.Param1).Count();
                    break;
                case var jump when FlagJump.Of(jump) is { } comparison:
                    // 플래그(없으면 0)와 flag_value를 견주어 참이면 target_flow_number의 칸으로 간다(그 칸의 조건이 거짓이면 그다음 큰 번호부터). 번호는 늘 뒤쪽이다.
                    if (FlagJump.Test(comparison, flags.GetValueOrDefault(flow.Param1), flow.Param2))
                        next = flows.TakeWhile(f => f.FlowNumber < flow.Param3).Count();
                    break;
                case var jump when InventoryJump.Of(jump) is { } comparison:
                    // 가진 개수(세션이 알려 준다. 세션 없이 돌리면 0)를 견주어 참이면 target_flow_number의 칸으로 간다.
                    if (FlagJump.Test(comparison, countInventory?.Invoke(flow.Param1) ?? 0, flow.Param2))
                        next = flows.TakeWhile(f => f.FlowNumber < flow.Param3).Count();
                    break;
                case EventFlowType.Adv:
                    return new EventStep(EventStepKind.Adv, flow.FlowNumber, flow.Param1);
                case EventFlowType.Battle:
                    return new EventStep(EventStepKind.Battle, flow.FlowNumber, flow.Param1);
                // None: 아무 일도 하지 않고 다음 칸으로
            }
        }
        return new EventStep(EventStepKind.Finished, 0, 0);
    }
}

public sealed partial class GameSession
{
    /// <summary>
    /// 이벤트를 시작한다: 이벤트 마스터에 있고 이벤트 조건(event_condition_master)을 지금 플래그로 만족하면 실행기를 돌려주고,
    /// 없거나 조건이 안 맞으면 null. 실행기는 이 세션의 플래그를 그대로 보므로 실행 도중 바뀐 플래그가 뒤의 칸 조건에 반영된다.
    /// </summary>
    public EventFlowCircuit? StartEvent(long eventCode)
    {
        if (Pack.FindEvent(eventCode) is not { } gameEvent || !gameEvent.IsAvailable(PlayerFlags)) return null;
        return new EventFlowCircuit(Pack, eventCode, PlayerFlags, AddInventory, CountInventory, CharacterHasTag, UniqueItemHasTag, NextRandom);
    }
}
