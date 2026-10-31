namespace Wera.Core;

/// <summary>
/// 유니크 아이템 생성 후 서킷: 유니크 아이템을 새로 얻으면 그 아이템 하나에 대해 유니크 아이템 생성 후 이펙트(마스터의 execute_number 순서)을 실행해
/// 게임팩의 정의에 따라 유니크 아이템 데이터를 조작한다. 파티 유닛이 없어 시전자와 타겟은 없고, 그 아이템과 그 아이템이 가진 스킬(<see cref="UniqueItemInstance.Skill"/>, 없으면 포커스 없음)이 처음부터 포커스되어 있다.
/// 이펙트의 종류와 동작은 <see cref="CircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// 실행이 끝나면 바뀐 스테이터스를 정수(소수는 버림)로 유니크 아이템 스테이터스 마스터 행의 최솟값~최댓값에 맞춰 인스턴스에 되돌린다.
/// </summary>
public sealed class UniqueItemAfterCreateCircuit : CircuitBase
{
    readonly UniqueItemInstance item;

    public UniqueItemAfterCreateCircuit(GamePack pack, UniqueItemInstance item)
        : base(pack, UnitStore.Of(item), null, null)
    {
        this.item = item;
        PresetFocus(item: item);
    }

    /// <summary>이펙트을 실행 번호 순서로 실행하고 조작이 끝난 유니크 아이템을 돌려준다. 이펙트이 없으면 원래 인스턴스를 그대로 돌려준다.</summary>
    public UniqueItemInstance Execute()
    {
        Run(Pack.UniqueItemAfterCreateEffectsOf(item.ItemCode).Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
        return Store.FindItem(item.InstanceId)!;
    }
}
