namespace Wera.Core;

/// <summary>
/// 턴 시작 전 서킷: 전투의 턴이 시작되기 전(진형 당기기 뒤)에 실행되어 게임팩의 정의에 따라 유닛들을 조작한다. 살아 있는 유닛마다 한 번 돈다. 시전자(시전자는 그 유닛, 메인 타겟은 없음)와 서브 타겟들, 캐시(<see cref="CircuitBase.CacheValues"/>)를 갖고
/// 턴 시작 전 이펙트 마스터를 실행 번호 순서로 실행한다. 이펙트의 종류와 동작은 <see cref="CircuitBase"/>와 <see cref="EffectType"/>을 따른다.
/// </summary>
public sealed class BattleBeforeTurnCircuit(GamePack pack, UnitStore store, PartyUnit? caster, PartyUnit? mainTarget, IEnumerable<PartyUnit>? subTarget = null)
    : CircuitBase(pack, store, caster, mainTarget, subTarget)
{
    /// <summary>이펙트를 실행 번호 순서로 실행한다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다.</summary>
    public void Execute() =>
        Run(Pack.BattleBeforeTurnEffectsInOrder().Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
}
