namespace Wera.Core;

/// <summary>
/// 로드 후 유니크 아이템 서킷: 세이브를 불러온 뒤 보유 유니크 아이템 하나에 대해 로드 후 유니크 아이템 흐름(마스터의 execute_number 순서)을 실행해
/// 게임팩의 정의에 따라 유니크 아이템 데이터를 조작한다. 유니크 아이템(<see cref="Unit"/>)과 캐시(<see cref="CacheValues"/>)를 갖는다.
/// 흐름의 종류와 동작은 <see cref="SkillExecuteCircuit"/>과 같고, 유니크 아이템이 하나뿐이라 Caster와 Target이 같은 유닛이다.
/// 실행이 끝나면 유닛의 스테이터스를 정수(소수는 버림)로 인스턴스에 되돌린다(범위는 유니크 아이템 스테이터스 마스터 행의 최솟값~최댓값).
/// </summary>
public sealed class AfterLoadUniqueItemCircuit
{
    readonly GamePack pack;
    readonly UniqueItemInstance item;

    public AfterLoadUniqueItemCircuit(GamePack pack, UniqueItemInstance item)
    {
        this.pack = pack;
        this.item = item;
        Unit = BattleUnit.FromStatuses(item.ItemCode, item.Statuses ?? pack.MasterStatusesOfUniqueItem(item.ItemCode),
            status => pack.UniqueItemStatusesOf(item.ItemCode).FirstOrDefault(r => r.StatusCode == status) is { } r ? (r.MinValue, r.MaxValue) : null);
    }

    /// <summary>흐름이 읽고 쓰는 유니크 아이템(Caster이자 Target).</summary>
    public BattleUnit Unit { get; }

    /// <summary>흐름들이 공유하는 값 목록(cache_index가 이 목록의 번호).</summary>
    public List<decimal> CacheValues { get; } = [];

    /// <summary>
    /// 흐름을 실행 번호 순서로 실행하고 조작이 끝난 유니크 아이템을 돌려준다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다.
    /// 흐름이 없으면 원래 인스턴스를 그대로 돌려준다.
    /// </summary>
    public UniqueItemInstance Execute()
    {
        var flows = pack.AfterLoadUniqueItemFlowsInOrder();
        if (flows.Count == 0) return item;
        for (var at = 0; at < flows.Count; at++)
        {
            var flow = flows[at];
            if (flow.FlowType == LoadUniqueItemFlowType.Finish) break;
            if (JumpTarget(flow) is { } target)
            {
                var to = flows.ToList().FindIndex(f => f.ExecuteNumber == target);
                if (to > at) at = to - 1;
            }
            else Apply(flow);
        }
        return item with { Statuses = Unit.ToStatuses() };
    }

    long? JumpTarget(LoadUniqueItemFlow flow)
    {
        if (flow.FlowType == LoadUniqueItemFlowType.Jump) return flow.Value1;
        if (UniqueItemFlowJump.Of(flow.FlowType) is not { } comparison) return null;
        return CacheAt(flow.Value1) is { } left && CacheAt(flow.Value2) is { } right && SkillJump.Test(comparison, left, right)
            ? flow.Value3
            : null;
    }

    void Apply(LoadUniqueItemFlow flow)
    {
        switch (flow.FlowType)
        {
            case LoadUniqueItemFlowType.None:
                break;
            case LoadUniqueItemFlowType.CacheToTargetModValue:
            case LoadUniqueItemFlowType.CacheToCasterModValue:
                if (CacheAt(flow.Value2) is { } mod) Unit.ModValue(flow.Value1, mod);
                break;
            case LoadUniqueItemFlowType.CacheToTargetSetValue:
            case LoadUniqueItemFlowType.CacheToCasterSetValue:
                if (CacheAt(flow.Value2) is { } set) Unit.SetValue(flow.Value1, set);
                break;
            case LoadUniqueItemFlowType.CasterValueToCache:
            case LoadUniqueItemFlowType.TargetValueToCache:
                if (Unit.ValueOf(flow.Value1) is { } value) SkillCache.Write(CacheValues, flow.Value2, value);
                break;
            case LoadUniqueItemFlowType.SetCacheValue:
                SkillCache.Write(CacheValues, flow.Value1, flow.Value2 / SkillCache.Pow10(flow.Value3));
                break;
            case LoadUniqueItemFlowType.CacheAdd:
            case LoadUniqueItemFlowType.CacheSubtract:
            case LoadUniqueItemFlowType.CacheMultiply:
            case LoadUniqueItemFlowType.CacheDivide:
                Calculate(flow);
                break;
        }
    }

    /// <summary>캐시 계산: cache[value_1] = cache[value_2] (연산) cache[value_3]. 칸이 없거나 0으로 나누거나 decimal 범위를 넘으면 아무 일도 하지 않는다.</summary>
    void Calculate(LoadUniqueItemFlow flow)
    {
        if (CacheAt(flow.Value2) is not { } left || CacheAt(flow.Value3) is not { } right) return;
        var op = flow.FlowType switch
        {
            LoadUniqueItemFlowType.CacheAdd => SkillCache.Op.Add,
            LoadUniqueItemFlowType.CacheSubtract => SkillCache.Op.Subtract,
            LoadUniqueItemFlowType.CacheMultiply => SkillCache.Op.Multiply,
            _ => SkillCache.Op.Divide,
        };
        if (SkillCache.Calculate(op, left, right) is { } result) SkillCache.Write(CacheValues, flow.Value1, result);
    }

    decimal? CacheAt(long index) => SkillCache.At(CacheValues, index);
}
