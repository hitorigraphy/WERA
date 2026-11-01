namespace Wera.Core;

/// <summary>ADV를 실행하다가 바깥(화면)이 보여 줘야 하는 장면의 종류.</summary>
public enum AdvStepKind
{
    /// <summary>나의 대사(TextMe).</summary>
    TextMe,
    /// <summary>상대의 대사(TextYou).</summary>
    TextYou,
    /// <summary>화자 없는 상자 텍스트(TextPlain): 가로 전체의 상자 안에 나온다.</summary>
    TextPlain,
    /// <summary>화자 없는, 상자 없이 글만 나오는 텍스트(TextLog).</summary>
    TextLog,
    /// <summary>선택지 묶음. 하나를 골라야 이어 갈 수 있다.</summary>
    Choice,
    /// <summary>ADV가 끝났다.</summary>
    Finished,
    /// <summary>고른 선택지(대사 로그에만 남는다). 문구는 Text다.</summary>
    Chosen,
}

/// <summary>선택지 하나: 보여 줄 문구와, 고르면 점프할 흐름 번호.</summary>
public readonly record struct AdvChoice(long FlowNumber, string Text, long JumpTo);

/// <summary>ADV를 실행하다 만난 장면 하나. 대사면 Text, 선택지 묶음이면 Choices가 차 있다.</summary>
/// <param name="SpeakerName">화자 이름(TextMe, TextYou). force_name이 있으면 그것, 없으면 캐릭터 이름, 캐릭터를 알 수 없으면 ???.</param>
/// <param name="CharacterCode">TextMe와 TextYou의 화자 캐릭터 코드. 없거나 유효하지 않으면 0(이미지는 No Image).</param>
/// <param name="Variation">TextMe와 TextYou의 화자 이미지 variation_number.</param>
public readonly record struct AdvStep(
    AdvStepKind Kind, long FlowNumber, string Text, IReadOnlyList<AdvChoice> Choices,
    string SpeakerName = "", long CharacterCode = 0, long Variation = 0)
{
    public static readonly AdvStep Finished = new(AdvStepKind.Finished, 0, "", []);

    /// <summary>대사를 꾸밈 문법(<see cref="RichText"/>)대로 읽은 것. 로드 때 검증했으므로 늘 읽힌다(대사가 아니면 빈 글).</summary>
    public RichText Rich => RichText.TryParse(Text, out _) ?? RichText.TryParse("", out _)!;
}

/// <summary>
/// ADV 하나를 흐름 번호 순서대로 실행한다. 이벤트 실행과 같은 규칙이다: 조건이 거짓인 칸은 건너뛰고(조건이 없으면 참),
/// 더 진행할 칸이 없거나 Finish를 만나면 끝나며, None은 아무 일도 없고, Jump는 param_1 번호부터 이어 가고, JumpFlag*는 플래그를 견주어 참이면 param_3 번호부터 이어 가고 거짓이면 다음 칸으로 간다.
/// SetFlag와 ModFlag는 플레이어 플래그를 바꾸고 멈추지 않고 다음 칸으로 간다(바뀐 플래그는 뒤의 칸 조건에 바로 보인다).
/// TextMe/TextYou/TextPlain/TextLog는 <see cref="Advance"/>가 그 대사를 돌려주고 멈춘다.
/// Choice는 같은 선택지 그룹(param_1)의 이어진 행들 중 조건을 만족하는 것을 한 묶음으로 돌려주고 멈추며(만족하는 것이 없으면 건너뛴다),
/// <see cref="Choose"/>로 하나를 고르면 그 선택지의 param_2 번호부터 이어 간다.
/// </summary>
public sealed class AdvFlowCircuit
{
    IReadOnlyList<AdvFlow> flows;                // 흐름 번호 순(서브모듈 안이면 그 서브모듈의 칸들)
    readonly IReadOnlyDictionary<long, IReadOnlyList<AdvFlow>>? submodules;
    readonly Dictionary<long, long> flags;
    readonly Action<long, long>? addInventory;
    readonly Func<long, long>? countInventory;
    readonly Func<long, long, bool>? characterHasTag;
    readonly Func<long, long, bool>? uniqueItemHasTag;
    readonly Func<long, long>? random;
    readonly GamePack pack;
    int next;
    IReadOnlyList<AdvChoice>? pending;           // 고르기를 기다리는 선택지 묶음

    public long AdvCode { get; }

    public bool IsFinished { get; private set; }

    /// <param name="flags">조건 판정에 쓰고 SetFlag, ModFlag가 바꾸는 플레이어 플래그. 같은 사전을 그대로 넘긴다.</param>
/// <param name="random">JumpRandom 칸이 부르는 동작(모수 → 0 이상 모수 미만의 무작위 정수). 세션이 넘기며, null이면 기본 난수를 쓴다.</param>
    /// <param name="characterHasTag">JumpCharacterHaveTag 칸이 부르는 동작(캐릭터 코드, 태그 코드 → 가졌는가). 세션이 넘기며, null이면 거짓으로 본다.</param>
    /// <param name="uniqueItemHasTag">JumpUniqueItemHaveTag 칸이 부르는 동작(유니크 아이템 코드, 태그 코드 → 가졌는가). 세션이 넘기며, null이면 거짓으로 본다.</param>
    /// <param name="countInventory">JumpInventory* 칸이 부르는 동작(인벤토리 코드 → 가진 개수). 세션이 넘기며, null이면 개수를 0으로 본다.</param>
    /// <param name="addInventory">AddInventory 칸이 부르는 동작(인벤토리 코드, 개수). 세션이 넘기며, null이면 AddInventory 칸은 아무 일도 하지 않는다.</param>
    public AdvFlowCircuit(GamePack pack, long advCode, Dictionary<long, long> flags, Action<long, long>? addInventory = null,
        Func<long, long>? countInventory = null,
        Func<long, long, bool>? characterHasTag = null,
        Func<long, long, bool>? uniqueItemHasTag = null,
        Func<long, long>? random = null)
        : this(pack, advCode, pack.AdvFlowsOf(advCode), flags, addInventory, countInventory, characterHasTag, uniqueItemHasTag, random, pack.AdvSubmodules)
    {
    }

    /// <summary>
    /// 흐름 칸들을 직접 받아 실행한다. flows는 흐름 번호 순이어야 한다(교류 흐름은 이 실행기가 아니라 <see cref="CommunicationFlowCircuit"/>이 돌린다).
    /// submodules는 JumpToSubmodule 칸이 부르는 서브모듈들(서브모듈 코드 → 칸들)이고 null이면 서브모듈이 없다.
    /// </summary>
    public AdvFlowCircuit(GamePack pack, long advCode, IReadOnlyList<AdvFlow> flows, Dictionary<long, long> flags,
        Action<long, long>? addInventory = null,
        Func<long, long>? countInventory = null,
        Func<long, long, bool>? characterHasTag = null,
        Func<long, long, bool>? uniqueItemHasTag = null,
        Func<long, long>? random = null,
        IReadOnlyDictionary<long, IReadOnlyList<AdvFlow>>? submodules = null)
    {
        AdvCode = advCode;
        this.submodules = submodules;
        this.pack = pack;
        this.flags = flags;
        this.addInventory = addInventory;
        this.countInventory = countInventory;
        this.characterHasTag = characterHasTag;
        this.uniqueItemHasTag = uniqueItemHasTag;
        this.random = random;
        this.flows = flows;
    }

    /// <summary>다음 장면(대사 또는 선택지 묶음)을 돌려준다. 끝났으면 Finished. 선택지를 고르기 전에는 같은 선택지 묶음을 다시 돌려준다.</summary>
    public AdvStep Advance()
    {
        if (pending is not null) return new AdvStep(AdvStepKind.Choice, pending[0].FlowNumber, "", pending);

        while (!IsFinished)
        {
            if (!More())
            {
                IsFinished = true;
                break;
            }

            var flow = flows[next++];

            switch (flow.FlowType)
            {
                case AdvFlowType.Finish:
                    if (!Return()) IsFinished = true;    // 서브모듈 안의 Finish는 서브모듈만 끝낸다
                    break;
                case AdvFlowType.JumpToSubmodule:
                    Call(flow.Param1);
                    break;
                case AdvFlowType.Jump:
                    JumpTo(flow.Param1);
                    break;
                case var jump when FlagJump.Of(jump) is { } comparison:
                    if (FlagJump.Test(comparison, flags.GetValueOrDefault(flow.Param1), flow.Param2)) JumpTo(flow.Param3);
                    break;
                case AdvFlowType.SetFlag:
                    PlayerFlagOps.Set(flags, flow.Param1, flow.Param2);
                    break;
                case AdvFlowType.ModFlag:
                    PlayerFlagOps.Mod(flags, flow.Param1, flow.Param2);
                    break;
                case var jump when InventoryJump.Of(jump) is { } comparison:
                    if (FlagJump.Test(comparison, countInventory?.Invoke(flow.Param1) ?? 0, flow.Param2)) JumpTo(flow.Param3);
                    break;
                case AdvFlowType.JumpCharacterHaveTag:
                    if (characterHasTag?.Invoke(flow.Param1, flow.Param2) ?? false) JumpTo(flow.Param3);
                    break;
                case AdvFlowType.JumpUniqueItemHaveTag:
                    if (uniqueItemHasTag?.Invoke(flow.Param1, flow.Param2) ?? false) JumpTo(flow.Param3);
                    break;
                case AdvFlowType.JumpRandom:
                    // 0 이상 모수 미만의 값을 뽑아 확률보다 작으면 target_flow_number로 간다. 모수는 로드 때 1 이상임을 확인했다.
                    if (RandomJump.Hit(flow.Param1, RandomJump.Roll(flow.Param2, random))) JumpTo(flow.Param3);
                    break;
                case AdvFlowType.AddInventory:
                    addInventory?.Invoke(flow.Param1, flow.Param2);
                    break;
                case AdvFlowType.TextMe:
                    return Line(AdvStepKind.TextMe, flow);
                case AdvFlowType.TextYou:
                    return Line(AdvStepKind.TextYou, flow);
                case AdvFlowType.TextPlain:
                    return new AdvStep(AdvStepKind.TextPlain, flow.FlowNumber, flow.Text, []);
                case AdvFlowType.TextLog:
                    return new AdvStep(AdvStepKind.TextLog, flow.FlowNumber, flow.Text, []);
                case AdvFlowType.Choice:
                    // 이 칸부터 같은 그룹의 이어진 선택지를 모은다(조건에 따라 숨기려면 JumpFlag*로 다른 선택지 묶음으로 뛴다).
                    var group = flow.Param1;
                    var choices = new List<AdvChoice>();
                    var at = next - 1;
                    for (; at < flows.Count && flows[at].FlowType == AdvFlowType.Choice && flows[at].Param1 == group; at++)
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

    /// <summary>
    /// 대사 장면. TextMe와 TextYou는 같은 규칙으로 화자를 정한다: param_1이 캐릭터 코드, param_2가 그 이미지의 variation_number이고,
    /// 0이거나 없는 캐릭터면 이름은 ???, 이미지는 No Image다. force_name이 있으면 그것이 이름이 된다.
    /// </summary>
    AdvStep Line(AdvStepKind kind, AdvFlow flow)
    {
        var speaker = pack.FindCharacter(flow.Param1);
        return new AdvStep(kind, flow.FlowNumber, flow.Text, [],
            Named(flow, speaker?.Name ?? "???"), speaker?.Code ?? 0, speaker is null ? 0 : flow.Param2);
    }

    /// <summary>force_name이 비어 있지 않으면 그 이름, 아니면 기본 이름.</summary>
    static string Named(AdvFlow flow, string fallback) => string.IsNullOrEmpty(flow.ForceName) ? fallback : flow.ForceName;

    void JumpTo(long flowNumber) => next = flows.TakeWhile(f => f.FlowNumber < flowNumber).Count();

    // 서브모듈 호출 스택(한 단계): 부른 흐름과 돌아갈 자리. 서브모듈 안에서 멈춰도(대사, 선택지, 전투) 다음 호출은 그 자리에서 이어 가고, 끝나면 부른 칸의 다음으로 돌아온다.
    IReadOnlyList<AdvFlow>? callerFlows;
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
