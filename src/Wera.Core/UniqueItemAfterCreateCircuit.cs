namespace Wera.Core;

/// <summary>
/// 유니크 아이템 생성 후 서킷: 유니크 아이템을 새로 얻으면 그 아이템 하나에 대해 유니크 아이템 생성 후 이펙트(마스터의 item_code가 같은 행들, execute_number 순서)를 실행해
/// 게임팩의 정의에 따라 유니크 아이템의 초기 데이터를 조작한다. 유니크 아이템(<see cref="Unit"/>)과 캐시(<see cref="CacheValues"/>)를 갖는다.
/// 이펙트의 종류와 동작은 <see cref="SkillExecuteCircuit"/>과 같고, 유니크 아이템이 하나뿐이라 Caster와 Target이 같은 유닛이다.
/// 실행이 끝나면 유닛의 스테이터스를 정수(소수는 버림)로 인스턴스에 되돌린다(범위는 유니크 아이템 스테이터스 마스터 행의 최솟값~최댓값).
/// </summary>
public sealed class UniqueItemAfterCreateCircuit
{
    readonly GamePack pack;
    readonly UniqueItemInstance item;

    public UniqueItemAfterCreateCircuit(GamePack pack, UniqueItemInstance item)
    {
        this.pack = pack;
        this.item = item;
        Unit = BattleUnit.FromStatuses(item.ItemCode, item.Statuses ?? pack.MasterStatusesOfUniqueItem(item.ItemCode),
            status => pack.UniqueItemStatusesOf(item.ItemCode).FirstOrDefault(r => r.StatusCode == status) is { } r ? (r.MinValue, r.MaxValue) : null);
    }

    /// <summary>이펙트가 읽고 쓰는 유니크 아이템(Caster이자 Target).</summary>
    public BattleUnit Unit { get; }

    /// <summary>이펙트들이 공유하는 값 목록(cache_index가 이 목록의 번호).</summary>
    public List<decimal> CacheValues { get; } = [];

    /// <summary>
    /// 이펙트를 실행 번호 순서로 실행하고 조작이 끝난 유니크 아이템을 돌려준다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다.
    /// 이펙트가 없으면 원래 인스턴스를 그대로 돌려준다.
    /// </summary>
    public UniqueItemInstance Execute()
    {
        var effects = pack.UniqueItemAfterCreateEffectsOf(item.ItemCode);
        if (effects.Count == 0) return item;
        for (var at = 0; at < effects.Count; at++)
        {
            var effect = effects[at];
            if (effect.EffectType == UniqueItemAfterCreateEffectType.Finish) break;
            if (JumpTarget(effect) is { } target)
            {
                var to = effects.ToList().FindIndex(f => f.ExecuteNumber == target);
                if (to > at) at = to - 1;
            }
            else Apply(effect);
        }
        return item with { Statuses = Unit.ToStatuses() };
    }

    long? JumpTarget(UniqueItemAfterCreateEffect effect)
    {
        if (effect.EffectType == UniqueItemAfterCreateEffectType.Jump) return effect.Value1;
        if (UniqueItemAfterCreateJump.Of(effect.EffectType) is not { } comparison) return null;
        return CacheAt(effect.Value1) is { } left && CacheAt(effect.Value2) is { } right && SkillJump.Test(comparison, left, right)
            ? effect.Value3
            : null;
    }

    void Apply(UniqueItemAfterCreateEffect effect)
    {
        switch (effect.EffectType)
        {
            case UniqueItemAfterCreateEffectType.None:
                break;
            case UniqueItemAfterCreateEffectType.CacheToTargetModValue:
            case UniqueItemAfterCreateEffectType.CacheToCasterModValue:
                if (CacheAt(effect.Value2) is { } mod) Unit.ModValue(effect.Value1, mod);
                break;
            case UniqueItemAfterCreateEffectType.CacheToTargetSetValue:
            case UniqueItemAfterCreateEffectType.CacheToCasterSetValue:
                if (CacheAt(effect.Value2) is { } set) Unit.SetValue(effect.Value1, set);
                break;
            case UniqueItemAfterCreateEffectType.CasterValueToCache:
            case UniqueItemAfterCreateEffectType.TargetValueToCache:
                if (Unit.ValueOf(effect.Value1) is { } value) SkillCache.Write(CacheValues, effect.Value2, value);
                break;
            case UniqueItemAfterCreateEffectType.SetCacheValue:
                SkillCache.Write(CacheValues, effect.Value1, effect.Value2 / SkillCache.Pow10(effect.Value3));
                break;
            case UniqueItemAfterCreateEffectType.CacheAdd:
            case UniqueItemAfterCreateEffectType.CacheSubtract:
            case UniqueItemAfterCreateEffectType.CacheMultiply:
            case UniqueItemAfterCreateEffectType.CacheDivide:
                Calculate(effect);
                break;
        }
    }

    /// <summary>캐시 계산: cache[value_1] = cache[value_2] (연산) cache[value_3]. 칸이 없거나 0으로 나누거나 decimal 범위를 넘으면 아무 일도 하지 않는다.</summary>
    void Calculate(UniqueItemAfterCreateEffect effect)
    {
        if (CacheAt(effect.Value2) is not { } left || CacheAt(effect.Value3) is not { } right) return;
        var op = effect.EffectType switch
        {
            UniqueItemAfterCreateEffectType.CacheAdd => SkillCache.Op.Add,
            UniqueItemAfterCreateEffectType.CacheSubtract => SkillCache.Op.Subtract,
            UniqueItemAfterCreateEffectType.CacheMultiply => SkillCache.Op.Multiply,
            _ => SkillCache.Op.Divide,
        };
        if (SkillCache.Calculate(op, left, right) is { } result) SkillCache.Write(CacheValues, effect.Value1, result);
    }

    decimal? CacheAt(long index) => SkillCache.At(CacheValues, index);
}
