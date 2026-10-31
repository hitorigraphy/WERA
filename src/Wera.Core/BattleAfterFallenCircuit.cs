namespace Wera.Core;

/// <summary>
/// 쓰러진 직후 서킷: 전투에서 유닛이 쓰러진 직후(그 행동이 끝난 뒤)에 실행되어 게임팩의 정의에 따라 유닛들을 조작한다. 쓰러진 유닛마다 한 번 돈다. 시전자(시전자는 쓰러진 유닛, 메인 타겟은 그 행동을 한(쓰러뜨린) 유닛)와 서브 타겟들, 캐시(<see cref="CircuitBase.CacheValues"/>)를 갖고
/// 쓰러진 직후 이펙트 마스터를 실행 번호 순서로 실행한다. 이펙트의 종류와 동작은 <see cref="CircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// </summary>
public sealed class BattleAfterFallenCircuit(GamePack pack, UnitStore store, PartyUnit? caster, PartyUnit? mainTarget, IEnumerable<PartyUnit>? subTarget = null)
    : CircuitBase(pack, store, caster, mainTarget, subTarget)
{
    /// <summary>이펙트를 실행 번호 순서로 실행한다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다.</summary>
    public void Execute() =>
        Run(Pack.BattleAfterFallenEffectsInOrder().Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
}
