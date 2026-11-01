namespace Wera.Core;

/// <summary>
/// 합성 흐름 서킷: 유니크 아이템을 합성하면 합성의 타겟(합성하는 유니크 아이템 하나)과 제물 목록(<see cref="Sacrifices"/>)을 가지고 합성 흐름(마스터의 execute_number 순서)을 실행해
/// 게임팩의 정의에 따라 타겟의 데이터(예: 강화 횟수)를 조작하고 제물을 없앤다. 흐름이 끝나면 강화 레벨이 바뀌었다고 보고 유니크 아이템 강화 스테이터스 마스터의 값을 반영한다(<see cref="GamePack.WithEnhanceStatuses"/>). 파티 유닛이 없어 시전자와 파티 타겟은 없고, 타겟 아이템과 그 아이템이 가진 스킬이 처음부터 포커스되어 있다.
/// 흐름의 종류와 동작은 <see cref="UnitCircuitBase"/>와 <see cref="EffectType"/>을 따르고, 여기에 합성에만 있는 종류
/// (<see cref="SynthesisFlowType.FocusSynthesisTarget"/>, <see cref="SynthesisFlowType.FocusSacrifice"/>, <see cref="SynthesisFlowType.DeleteSacrifice"/>, <see cref="SynthesisFlowType.DeleteAllSacrifices"/>)가 더해진다.
/// 제물은 흐름이 없애야만 없어진다(흐름이 없으면 아무것도 없어지지 않는다). 실행이 끝나면 바뀐 스테이터스를 정수(소수는 버림)로 유니크 아이템 스테이터스 마스터 행의 최솟값~최댓값에 맞춰 저장소에 되돌리고, 없앤 제물은 저장소에서 지운다.
/// </summary>
public sealed class SynthesisFlowCircuit : UnitCircuitBase
{
    readonly UniqueItemInstance target;
    readonly List<UniqueItemInstance> deleted = [];

    /// <summary>합성의 타겟과 제물을 든 저장소로 실행한다(세션의 저장소를 쓰면 세션의 아이템이 그대로 바뀌고 지워진다).</summary>
    public SynthesisFlowCircuit(GamePack pack, UnitStore store, UniqueItemInstance target, IEnumerable<UniqueItemInstance> sacrifices)
        : base(pack, store, null, null)
    {
        this.target = target;
        Sacrifices = sacrifices.ToList();
        PresetFocus(item: target);
    }

    /// <summary>타겟과 제물만 든 저장소로 실행한다(세션 밖에서).</summary>
    public SynthesisFlowCircuit(GamePack pack, UniqueItemInstance target, IEnumerable<UniqueItemInstance>? sacrifices = null)
        : this(pack, UnitStore.Of([target, .. sacrifices ?? []]), target, sacrifices ?? [])
    {
    }

    /// <summary>제물 목록. 흐름은 0부터의 번호로 고른다.</summary>
    public IReadOnlyList<UniqueItemInstance> Sacrifices { get; }

    /// <summary>흐름이 없앤 제물들(없앤 순서).</summary>
    public IReadOnlyList<UniqueItemInstance> DeletedSacrifices => deleted;

    /// <summary>흐름을 실행 번호 순서로 실행하고 조작이 끝난 타겟을 돌려준다. 흐름이 없으면 원래 인스턴스를 그대로 돌려준다.</summary>
    public UniqueItemInstance Execute()
    {
        Run(Pack.SynthesisFlowsInOrder().Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.FlowType, e.Value1, e.Value2, e.Value3)).ToList());
        var result = Store.FindItem(target.InstanceId)!;
        var enhanced = Pack.WithEnhanceStatuses(result);       // 강화 레벨이 바뀌었다고 보고 강화 스테이터스를 반영한다
        if (!ReferenceEquals(enhanced, result)) Store.Replace(result, enhanced);
        return enhanced;
    }

    protected override void ApplyOther(CircuitEffect effect)
    {
        switch ((SynthesisFlowType)effect.TypeNumber)
        {
            case SynthesisFlowType.FocusSynthesisTarget:
                FocusItem(target);
                break;
            case SynthesisFlowType.FocusSacrifice:
                FocusItem(SacrificeAt(effect.Value1));
                break;
            case SynthesisFlowType.DeleteSacrifice:
                if (SacrificeAt(effect.Value1) is { } one) Delete(one);
                break;
            case SynthesisFlowType.DeleteAllSacrifices:
                foreach (var sacrifice in Sacrifices) Delete(sacrifice);
                break;
        }
    }

    UniqueItemInstance? SacrificeAt(long index) => index >= 0 && index < Sacrifices.Count ? Sacrifices[(int)index] : null;

    void Delete(UniqueItemInstance sacrifice)
    {
        if (deleted.Contains(sacrifice)) return;
        DeleteItem(sacrifice);
        deleted.Add(sacrifice);
    }
}
