namespace Wera.Core;

/// <summary>
/// 제련 흐름 서킷: 유니크 아이템을 제련석으로 제련하면 제련의 타겟(제련하는 유니크 아이템 하나)을 가지고 제련 흐름(마스터의 execute_number 순서)을 실행해
/// 게임팩의 정의에 따라 타겟의 데이터(예: 강화 횟수)를 조작하고 제련석을 소모하고 타겟을 없앤다(확률은 <see cref="SmeltFlowType.RandomToCache"/>와 JumpCache* 이펙트로 흐름이 정한다).
/// 파티 유닛이 없어 시전자와 파티 타겟은 없고, 타겟 아이템과 그 아이템이 가진 스킬이 처음부터 포커스되어 있다.
/// 흐름의 종류와 동작은 <see cref="UnitCircuitBase"/>와 <see cref="EffectType"/>을 따르고, 여기에 제련에만 있는 종류
/// (<see cref="SmeltFlowType.FocusSmeltTarget"/>, <see cref="SmeltFlowType.DeleteSmeltTarget"/>, <see cref="SmeltFlowType.ConsumeSmeltStone"/>, <see cref="SmeltFlowType.RandomToCache"/>)가 더해진다.
/// 제련석은 흐름이 소모해야만 소모된다(소모한 수는 <see cref="StonesConsumed"/>이고 호출한 쪽이 인벤토리에서 뺀다). 타겟은 흐름이 없애야만 없어진다.
/// 실행이 끝나면 바뀐 스테이터스를 정수(소수는 버림)로 유니크 아이템 스테이터스 마스터 행의 최솟값~최댓값에 맞춰 저장소에 되돌리고, 없앤 타겟은 저장소에서 지운다.
/// 타겟이 남았으면 강화 레벨이 바뀌었다고 보고 유니크 아이템 강화 스테이터스 마스터의 값을 반영한다(<see cref="GamePack.WithEnhanceStatuses"/>).
/// </summary>
public sealed class SmeltFlowCircuit : UnitCircuitBase
{
    readonly UniqueItemInstance target;
    readonly Func<long, long> nextRandom;

    /// <param name="nextRandom">0 이상 모수 미만의 무작위 정수를 돌려주는 동작(세션의 <see cref="GameSession.NextRandom"/>). 생략하면 <see cref="Random.Shared"/>.</param>
    public SmeltFlowCircuit(GamePack pack, UnitStore store, UniqueItemInstance target, Func<long, long>? nextRandom = null)
        : base(pack, store, null, null)
    {
        this.target = target;
        this.nextRandom = nextRandom ?? (bound => Random.Shared.NextInt64(bound));
        PresetFocus(item: target);
    }

    /// <summary>타겟만 든 저장소로 실행한다(세션 밖에서).</summary>
    public SmeltFlowCircuit(GamePack pack, UniqueItemInstance target, Func<long, long>? nextRandom = null)
        : this(pack, UnitStore.Of(target), target, nextRandom)
    {
    }

    /// <summary>흐름이 소모한 제련석 수.</summary>
    public long StonesConsumed { get; private set; }

    /// <summary>흐름이 타겟을 없앴는가.</summary>
    public bool TargetDeleted { get; private set; }

    /// <summary>흐름을 실행 번호 순서로 실행하고 조작이 끝난 타겟을 돌려준다(타겟을 없앴으면 null). 흐름이 없으면 원래 인스턴스를 그대로 돌려준다.</summary>
    public UniqueItemInstance? Execute()
    {
        Run(Pack.SmeltFlowsInOrder().Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.FlowType, e.Value1, e.Value2, e.Value3)).ToList(), Pack.SubmodulesOf(GamePack.SmeltSubmoduleMasterPath));
        if (TargetDeleted) return null;
        var result = Store.FindItem(target.InstanceId)!;
        var enhanced = Pack.WithEnhanceStatuses(result);       // 강화 레벨이 바뀌었다고 보고 강화 스테이터스를 반영한다
        if (!ReferenceEquals(enhanced, result)) Store.Replace(result, enhanced);
        return enhanced;
    }

    protected override void ApplyOther(CircuitEffect effect)
    {
        switch ((SmeltFlowType)effect.TypeNumber)
        {
            case SmeltFlowType.FocusSmeltTarget:
                FocusItem(target);
                break;
            case SmeltFlowType.DeleteSmeltTarget:
                DeleteItem(target);
                TargetDeleted = true;
                break;
            case SmeltFlowType.ConsumeSmeltStone:
                StonesConsumed++;
                break;
            case SmeltFlowType.RandomToCache:
                SkillCache.Write(CacheValues, effect.Value1, nextRandom(effect.Value2));
                break;
        }
    }
}
