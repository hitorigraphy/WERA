namespace Wera.Core;

/// <summary>
/// 임베디드 이펙트 1번: 타겟의 HP를 밸류 1만큼 늘린다(음수면 줄인다). HP는 0~최대 HP 안에 머문다.
/// 밸류 2는 최대 실행 횟수, 밸류 3은 쓰지 않는다. 죽은 캐릭터도 같은 규칙으로 HP가 바뀐다.
/// </summary>
public sealed class HealEffect(Character target, long value1, long value2, long value3)
    : EmbeddedEffect(target, value1, value2, value3)
{
    public const long Code = 1;

    public override long EffectCode => Code;

    public override void Execute()
    {
        var state = Target.State;
        // 매우 큰 값이어도 넘치지 않게 long 범위 안에서 더한다.
        var hp = (decimal)state.Hp + Value1;
        Target.State = state with { Hp = (long)Math.Clamp(hp, 0m, Math.Max(0, state.MaxHp)) };
    }
}
