namespace Wera.Core;

/// <summary>
/// 전투 승리 후 서킷: 전투에서 이기면(적이 모두 쓰러지면) 전투에 참가한 아군 유닛(캐릭터) 하나에 대해 전투 승리 후 이펙트(마스터의 execute_number 순서)를 실행해
/// 게임팩의 정의에 따라 그 캐릭터 데이터를 조작한다. 캐릭터(<see cref="Unit"/>)와 캐시(<see cref="CacheValues"/>)를 갖는다.
/// 이펙트의 종류와 동작은 <see cref="SkillExecuteCircuit"/>과 같고, 유닛이 하나뿐이라 Caster와 Target이 같은 유닛이다.
/// 실행이 끝나면 유닛의 스테이터스를 정수(소수는 버림)로 인스턴스에 되돌리고 <see cref="CharacterInstance.Clamped"/>로 범위를 맞춘다.
/// </summary>
public sealed class AfterBattleWinCircuit
{
    readonly GamePack pack;
    readonly CharacterInstance character;

    public AfterBattleWinCircuit(GamePack pack, CharacterInstance character)
    {
        this.pack = pack;
        this.character = character;
        Unit = BattleUnit.FromInstance(pack, character);
    }

    /// <summary>이펙트이 읽고 쓰는 캐릭터(Caster이자 Target).</summary>
    public BattleUnit Unit { get; }

    /// <summary>이펙트들이 공유하는 값 목록(cache_index가 이 목록의 번호).</summary>
    public List<decimal> CacheValues { get; } = [];

    /// <summary>
    /// 이펙트을 실행 번호 순서로 실행하고 조작이 끝난 캐릭터를 돌려준다. Jump와 JumpCache*는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다.
    /// 이펙트이 없으면 원래 인스턴스를 그대로 돌려준다.
    /// </summary>
    public CharacterInstance Execute()
    {
        var effects = pack.AfterBattleWinEffectsInOrder();
        if (effects.Count == 0) return character;
        for (var at = 0; at < effects.Count; at++)
        {
            var effect = effects[at];
            if (effect.EffectType == AfterBattleWinEffectType.Finish) break;
            if (JumpTarget(effect) is { } target)
            {
                var to = effects.ToList().FindIndex(f => f.ExecuteNumber == target);
                if (to > at) at = to - 1;
            }
            else Apply(effect);
        }
        return (character with { Statuses = Unit.ToStatuses() }).Clamped();
    }

    long? JumpTarget(AfterBattleWinEffect effect)
    {
        if (effect.EffectType == AfterBattleWinEffectType.Jump) return effect.Value1;
        if (AfterBattleWinJump.Of(effect.EffectType) is not { } comparison) return null;
        return CacheAt(effect.Value1) is { } left && CacheAt(effect.Value2) is { } right && SkillJump.Test(comparison, left, right)
            ? effect.Value3
            : null;
    }

    void Apply(AfterBattleWinEffect effect)
    {
        switch (effect.EffectType)
        {
            case AfterBattleWinEffectType.None:
                break;
            case AfterBattleWinEffectType.CacheToTargetModValue:
            case AfterBattleWinEffectType.CacheToCasterModValue:
                if (CacheAt(effect.Value2) is { } mod) Unit.ModValue(effect.Value1, mod);
                break;
            case AfterBattleWinEffectType.CacheToTargetSetValue:
            case AfterBattleWinEffectType.CacheToCasterSetValue:
                if (CacheAt(effect.Value2) is { } set) Unit.SetValue(effect.Value1, set);
                break;
            case AfterBattleWinEffectType.CasterValueToCache:
            case AfterBattleWinEffectType.TargetValueToCache:
                if (Unit.ValueOf(effect.Value1) is { } value) SkillCache.Write(CacheValues, effect.Value2, value);
                break;
            case AfterBattleWinEffectType.SetCacheValue:
                SkillCache.Write(CacheValues, effect.Value1, effect.Value2 / SkillCache.Pow10(effect.Value3));
                break;
            case AfterBattleWinEffectType.CacheAdd:
            case AfterBattleWinEffectType.CacheSubtract:
            case AfterBattleWinEffectType.CacheMultiply:
            case AfterBattleWinEffectType.CacheDivide:
                Calculate(effect);
                break;
        }
    }

    /// <summary>캐시 계산: cache[value_1] = cache[value_2] (연산) cache[value_3]. 칸이 없거나 0으로 나누거나 decimal 범위를 넘으면 아무 일도 하지 않는다.</summary>
    void Calculate(AfterBattleWinEffect effect)
    {
        if (CacheAt(effect.Value2) is not { } left || CacheAt(effect.Value3) is not { } right) return;
        var op = effect.EffectType switch
        {
            AfterBattleWinEffectType.CacheAdd => SkillCache.Op.Add,
            AfterBattleWinEffectType.CacheSubtract => SkillCache.Op.Subtract,
            AfterBattleWinEffectType.CacheMultiply => SkillCache.Op.Multiply,
            _ => SkillCache.Op.Divide,
        };
        if (SkillCache.Calculate(op, left, right) is { } result) SkillCache.Write(CacheValues, effect.Value1, result);
    }

    decimal? CacheAt(long index) => SkillCache.At(CacheValues, index);
}
