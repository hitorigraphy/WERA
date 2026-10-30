namespace Wera.Core;

/// <summary>행동하는 쪽의 진영.</summary>
public enum BattleSide
{
    Enemy,
    Ally,
}

/// <summary>행동 순서를 정할 대상 하나: 진영, 슬롯 번호(0부터, 전위 왼쪽이 0이고 전위, 중위, 후위의 왼쪽, 가운데, 오른쪽 순서), 그 행동의 OrderPoint.</summary>
public readonly record struct BattleActor(BattleSide Side, int Slot, decimal OrderPoint);

/// <summary>
/// 행동 순서: 순서 서킷(<see cref="SkillOrderCircuit"/>)이 계산한 OrderPoint가 큰 쪽이 먼저 행동한다.
/// TODO: OrderPoint가 같을 때의 처리는 정해지지 않았다. 일단은 적군이 먼저, 같은 진영이면 슬롯 번호가 작은 쪽이 먼저다.
/// </summary>
public static class ActionOrder
{
    public static IReadOnlyList<BattleActor> Sort(IEnumerable<BattleActor> actors) =>
        actors.OrderByDescending(a => a.OrderPoint)
            .ThenBy(a => a.Side == BattleSide.Enemy ? 0 : 1)
            .ThenBy(a => a.Slot)
            .ToList();
}
