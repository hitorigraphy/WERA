namespace Wera.Core;

/// <summary>
/// 스킬 자동 타겟 서킷: 스킬을 쓰는 유닛이 메인 타겟을 자동으로 정할 때 쓴다. 시전자와 메인 타겟이 될 수 있는 유닛들(후보)을 받고, 후보들은 서브 타겟 자리에 번호 0부터 등록된다.
/// 스킬의 자동 타겟 이펙트(스킬 자동 타겟 이펙트 마스터)를 실행 번호 순서로 한 바퀴 돌린 뒤의 <see cref="ResultIndex"/>가 고른 후보의 번호다.
/// 이펙트는 후보를 FocusSubTarget*로 포커스해 스테이터스를 읽기만 하고 바꾸지 않으며, SubTargetToResult로 Result를 놓는다. 점프와 Finish 규칙은 <see cref="SkillExecuteCircuit"/>과 같다.
/// </summary>
public sealed class SkillAutoTargetCircuit(GamePack pack, UnitStore store, long skillCode, PartyUnit? caster, IEnumerable<PartyUnit> candidates)
    : CircuitBase(pack, store, caster, null, candidates)
{
    public long SkillCode { get; } = skillCode;

    /// <summary>
    /// 서킷의 결과: SubTargetToResult가 놓은 후보 번호(마지막으로 실행한 것). 놓지 않았으면 null이고, 후보 목록을 벗어난 번호면 적절하지 못한 값이다
    /// (<see cref="ChosenOf"/>가 null을 돌려준다).
    /// </summary>
    public long? ResultIndex { get; private set; }

    /// <summary>스킬의 자동 타겟 이펙트를 실행 번호 순서로 한 바퀴 실행한다.</summary>
    public void Execute() =>
        Run(Pack.SkillAutoTargetEffectsOf(SkillCode).Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());

    /// <summary>Result가 적절한 값(후보 번호)이면 그 후보의 번호를, 아니면 null(바깥에서 랜덤으로 1체를 고른다).</summary>
    public int? ChosenOf(int candidateCount) =>
        ResultIndex is { } i && i >= 0 && i < candidateCount ? (int)i : null;

    protected override void ApplyOther(CircuitEffect effect)
    {
        if (effect.TypeNumber == (int)AutoTargetEffectType.SubTargetToResult) ResultIndex = effect.Value1;
    }
}
