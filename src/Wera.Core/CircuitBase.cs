namespace Wera.Core;

/// <summary>서킷이 실행하는 이펙트 하나: 실행 번호와 이펙트 종류의 번호(<see cref="EffectType"/>의 번호)와 값 셋. 모든 이펙트 마스터의 행을 이 모양으로 바꿔 같은 방식으로 실행한다.</summary>
public readonly record struct CircuitEffect(long ExecuteNumber, int TypeNumber, long Value1, long Value2, long Value3);

/// <summary>
/// 서킷들의 공통 바탕. 서킷은 시전자(<see cref="Caster"/>), 메인 타겟(<see cref="MainTarget"/>), 서브 타겟들(<see cref="SubTarget"/>)이라는 파티 유닛과
/// 유닛이 든 캐릭터, 장비, 스킬 가운데 이펙트가 <b>포커스</b>한 것(<see cref="FocusedCharacterInstance"/>, <see cref="FocusedUniqueItemInstance"/>, <see cref="FocusedSkill"/>)을 가지고,
/// 이펙트들은 <see cref="CacheValues"/>로 값을 주고받는다. 스테이터스는 포커스한 것하고만 주고받고(유닛에는 직접 쓰지 않는다),
/// 서킷 안의 스테이터스 값은 소수(decimal)로 들고 있다가 끝날 때 정수(소수는 버림)로, 각 스테이터스의 최솟값~최댓값으로 맞춰 저장소(<see cref="UnitStore"/>)에 되돌린다.
/// 점프(Jump, JumpCache*)는 뒤의 실행 번호로 건너뛰고 Finish는 거기서 끝낸다(점프는 뒤로만 가므로 반드시 끝난다).
/// </summary>
public abstract class CircuitBase
{
    // 서킷이 만지는 인스턴스마다 하나씩 만든 작업 사본: 소수 버퍼와 지금까지 바뀐 인스턴스를 가진다. 같은 인스턴스는 하나만 만든다.
    sealed class CharacterWork(CharacterInstance original, StatusBuffer buffer)
    {
        public CharacterInstance Original { get; } = original;
        public CharacterInstance Instance { get; set; } = original;
        public StatusBuffer Buffer { get; } = buffer;
    }

    sealed class ItemWork(UniqueItemInstance original, StatusBuffer buffer)
    {
        public UniqueItemInstance Original { get; } = original;
        public UniqueItemInstance Instance { get; set; } = original;
        public StatusBuffer Buffer { get; } = buffer;
    }

    sealed class SkillWork(CharacterWork owner, int index, StatusBuffer buffer)
    {
        public CharacterWork Owner { get; } = owner;
        public int Index { get; } = index;
        public StatusBuffer Buffer { get; } = buffer;
        public SkillInstance Skill => Owner.Instance.SkillPool[Index];
    }

    readonly Dictionary<CharacterInstance, CharacterWork> characterWorks = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<UniqueItemInstance, ItemWork> itemWorks = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<(CharacterWork, int), SkillWork> skillWorks = [];
    CharacterWork? focusedCharacter;
    ItemWork? focusedItem;
    SkillWork? focusedSkill;

    protected CircuitBase(GamePack pack, UnitStore store, PartyUnit? caster, PartyUnit? mainTarget, IEnumerable<PartyUnit>? subTarget = null)
    {
        Pack = pack;
        Store = store;
        Caster = caster;
        MainTarget = mainTarget;
        SubTarget = subTarget?.ToList() ?? [];
    }

    protected GamePack Pack { get; }
    protected UnitStore Store { get; }

    /// <summary>시전자(없을 수 있다).</summary>
    public PartyUnit? Caster { get; }

    /// <summary>메인 타겟(없을 수 있다).</summary>
    public PartyUnit? MainTarget { get; }

    /// <summary>서브 타겟들. 이펙트는 0부터의 번호로 고른다.</summary>
    public List<PartyUnit> SubTarget { get; }

    /// <summary>포커스한 캐릭터 인스턴스(지금까지 바뀐 값). 포커스가 없으면 null.</summary>
    public CharacterInstance? FocusedCharacterInstance => focusedCharacter?.Instance;

    /// <summary>포커스한 유니크 아이템 인스턴스(지금까지 바뀐 값). 포커스가 없으면 null.</summary>
    public UniqueItemInstance? FocusedUniqueItemInstance => focusedItem?.Instance;

    /// <summary>포커스한 스킬(지금까지 바뀐 값). 포커스가 없으면 null.</summary>
    public SkillInstance? FocusedSkill => focusedSkill?.Skill;

    /// <summary>이펙트들이 공유하는 값 목록(cache_index가 이 목록의 번호).</summary>
    public List<decimal> CacheValues { get; } = [];

    // ---- 작업 사본 ----

    CharacterWork WorkOf(CharacterInstance character)
    {
        if (characterWorks.TryGetValue(character, out var work)) return work;
        var buffer = StatusBuffer.From(character.Statuses,
            status => Pack.CharacterStatusAt(character.CharacterCode, status, character.Level) is { } row ? (row.MinValue, row.MaxValue) : null);
        return characterWorks[character] = new CharacterWork(character, buffer);
    }

    ItemWork WorkOf(UniqueItemInstance item)
    {
        if (itemWorks.TryGetValue(item, out var work)) return work;
        var statuses = item.Statuses ?? Pack.MasterStatusesOfUniqueItem(item.ItemCode);
        var buffer = StatusBuffer.From(statuses,
            status => Pack.UniqueItemStatusesOf(item.ItemCode).FirstOrDefault(r => r.StatusCode == status) is { } r ? (r.MinValue, r.MaxValue) : null);
        return itemWorks[item] = new ItemWork(item, buffer);
    }

    SkillWork? WorkOf(CharacterWork owner, int index)
    {
        if (index < 0 || index >= owner.Instance.SkillPool.Count) return null;
        if (skillWorks.TryGetValue((owner, index), out var work)) return work;
        var skill = owner.Instance.SkillPool[index];
        var buffer = StatusBuffer.From(skill.Statuses ?? Pack.MasterStatusesOfSkill(skill.SkillCode),
            status => Pack.SkillStatusesOf(skill.SkillCode).FirstOrDefault(r => r.StatusCode == status) is { } r ? (r.MinValue, r.MaxValue) : null);
        return skillWorks[(owner, index)] = new SkillWork(owner, index, buffer);
    }

    // ---- 포커스 ----

    /// <summary>서킷을 시작하기 전에 포커스를 미리 정한다(캐릭터 하나나 아이템 하나를 초기화하는 서킷).</summary>
    protected void PresetFocus(CharacterInstance? character = null, UniqueItemInstance? item = null)
    {
        if (character is not null) focusedCharacter = WorkOf(character);
        if (item is not null) focusedItem = WorkOf(item);
    }

    void FocusCharacter(PartyUnit? unit) =>
        focusedCharacter = unit is not null && Store.CharacterOf(unit) is { } character ? WorkOf(character) : null;

    void FocusEquipment(PartyUnit? unit, long equipmentSlotCode) =>
        focusedItem = unit is not null && Store.ItemOf(unit, equipmentSlotCode) is { } item ? WorkOf(item) : null;

    void FocusSkill(PartyUnit? unit, long skillSlotCode)
    {
        focusedSkill = null;
        if (unit is null || Store.CharacterOf(unit) is not { } character) return;
        var owner = WorkOf(character);
        if (owner.Instance.SkillSlots.TryGetValue(skillSlotCode, out var index)) focusedSkill = WorkOf(owner, (int)index);
    }

    /// <summary>스킬을 만들어 포커스한 캐릭터의 스킬 풀 맨 뒤에 더하고 그 스킬을 포커스한다. 포커스한 캐릭터가 없으면 false.</summary>
    protected bool AddSkillToFocusedPool(long skillCode)
    {
        if (focusedCharacter is not { } owner) return false;
        var pool = new List<SkillInstance>(owner.Instance.SkillPool) { Pack.NewSkillInstance(skillCode) };
        owner.Instance = owner.Instance with { SkillPool = pool };
        focusedSkill = WorkOf(owner, pool.Count - 1);
        return true;
    }

    /// <summary>포커스한 스킬(포커스한 캐릭터의 풀 안)을 스킬 슬롯에 장착한다. 같은 스킬이 다른 슬롯에 있었으면 옮겨 온다. 장착하지 못하면 false.</summary>
    protected bool SetFocusedSkillToSlot(long skillSlotCode)
    {
        if (focusedSkill is not { } skill || focusedCharacter is not { } owner || !ReferenceEquals(skill.Owner, owner)) return false;
        var slots = owner.Instance.SkillSlots.Where(s => s.Value != skill.Index).ToDictionary(s => s.Key, s => s.Value);
        slots[skillSlotCode] = skill.Index;
        owner.Instance = owner.Instance with { SkillSlots = slots };
        return true;
    }

    PartyUnit? SubTargetAt(long index) => index >= 0 && index < SubTarget.Count ? SubTarget[(int)index] : null;

    // ---- 실행 ----

    /// <summary>
    /// 이펙트를 실행 번호 순서로 실행하고 끝나면 바뀐 인스턴스를 저장소에 되돌린다. 종류가 <see cref="EffectType"/>에 없는 번호는
    /// <see cref="ApplyOther"/>에 맡긴다(서킷마다 가진 고유한 이펙트).
    /// </summary>
    protected void Run(IReadOnlyList<CircuitEffect> effects)
    {
        for (var at = 0; at < effects.Count; at++)
        {
            var effect = effects[at];
            var known = Enum.IsDefined(typeof(EffectType), effect.TypeNumber);
            var type = (EffectType)effect.TypeNumber;
            if (known && type == EffectType.Finish) break;
            if (known && JumpTarget(type, effect) is { } target)
            {
                var to = effects.ToList().FindIndex(e => e.ExecuteNumber == target);
                if (to > at) at = to - 1;
            }
            else if (known) Apply(type, effect);
            else ApplyOther(effect);
        }
        Commit();
    }

    /// <summary><see cref="EffectType"/>에 없는 번호의 이펙트를 실행한다(기본은 아무 일도 하지 않는다).</summary>
    protected virtual void ApplyOther(CircuitEffect effect)
    {
    }

    long? JumpTarget(EffectType type, CircuitEffect effect)
    {
        if (type == EffectType.Jump) return effect.Value1;
        if (EffectArgs.JumpComparison(type) is not { } comparison) return null;
        return CacheAt(effect.Value1) is { } left && CacheAt(effect.Value2) is { } right && SkillJump.Test(comparison, left, right)
            ? effect.Value3
            : null;
    }

    void Apply(EffectType type, CircuitEffect effect)
    {
        switch (type)
        {
            case EffectType.FocusCasterCharacter: FocusCharacter(Caster); break;
            case EffectType.FocusMainTargetCharacter: FocusCharacter(MainTarget); break;
            case EffectType.FocusSubTargetCharacter: FocusCharacter(SubTargetAt(effect.Value1)); break;
            case EffectType.FocusCasterEquipment: FocusEquipment(Caster, effect.Value1); break;
            case EffectType.FocusMainTargetEquipment: FocusEquipment(MainTarget, effect.Value1); break;
            case EffectType.FocusSubTargetEquipment: FocusEquipment(SubTargetAt(effect.Value1), effect.Value2); break;
            case EffectType.FocusCasterSkill: FocusSkill(Caster, effect.Value1); break;
            case EffectType.FocusMainTargetSkill: FocusSkill(MainTarget, effect.Value1); break;
            case EffectType.FocusSubTargetSkill: FocusSkill(SubTargetAt(effect.Value1), effect.Value2); break;

            case EffectType.CacheToFocusedCharacterModValue:
                if (focusedCharacter is { } cm && CacheAt(effect.Value2) is { } cmod && cm.Buffer.ModValue(effect.Value1, cmod)) Refresh(cm);
                break;
            case EffectType.CacheToFocusedCharacterSetValue:
                if (focusedCharacter is { } cs && CacheAt(effect.Value2) is { } cset && cs.Buffer.SetValue(effect.Value1, cset)) Refresh(cs);
                break;
            case EffectType.FocusedCharacterValueToCache:
                if (focusedCharacter?.Buffer.ValueOf(effect.Value1) is { } cv) SkillCache.Write(CacheValues, effect.Value2, cv);
                break;

            case EffectType.CacheToFocusedItemModValue:
                if (focusedItem is { } im && CacheAt(effect.Value2) is { } imod && im.Buffer.ModValue(effect.Value1, imod)) Refresh(im);
                break;
            case EffectType.CacheToFocusedItemSetValue:
                if (focusedItem is { } iss && CacheAt(effect.Value2) is { } iset && iss.Buffer.SetValue(effect.Value1, iset)) Refresh(iss);
                break;
            case EffectType.FocusedItemValueToCache:
                if (focusedItem?.Buffer.ValueOf(effect.Value1) is { } iv) SkillCache.Write(CacheValues, effect.Value2, iv);
                break;

            case EffectType.CacheToFocusedSkillModValue:
                if (focusedSkill is { } sm && CacheAt(effect.Value2) is { } smod && sm.Buffer.ModValue(effect.Value1, smod)) Refresh(sm);
                break;
            case EffectType.CacheToFocusedSkillSetValue:
                if (focusedSkill is { } ss && CacheAt(effect.Value2) is { } sset && ss.Buffer.SetValue(effect.Value1, sset)) Refresh(ss);
                break;
            case EffectType.FocusedSkillValueToCache:
                if (focusedSkill?.Buffer.ValueOf(effect.Value1) is { } sv) SkillCache.Write(CacheValues, effect.Value2, sv);
                break;

            case EffectType.SetCacheValue:
                SkillCache.Write(CacheValues, effect.Value1, effect.Value2 / SkillCache.Pow10(effect.Value3));
                break;
            case EffectType.CacheAdd:
            case EffectType.CacheSubtract:
            case EffectType.CacheMultiply:
            case EffectType.CacheDivide:
                Calculate(type, effect);
                break;
        }
    }

    // 버퍼가 바뀌면 작업 사본의 인스턴스에 정수(소수는 버림)로 되돌려, 포커스한 인스턴스의 값이 늘 최신이도록 한다.
    static void Refresh(CharacterWork work) =>
        work.Instance = work.Instance with { Statuses = work.Buffer.ToStatuses() };

    static void Refresh(ItemWork work) =>
        work.Instance = work.Instance with { Statuses = work.Buffer.ToStatuses() };

    static void Refresh(SkillWork work)
    {
        var pool = new List<SkillInstance>(work.Owner.Instance.SkillPool);
        pool[work.Index] = pool[work.Index] with { Statuses = work.Buffer.ToStatuses() };
        work.Owner.Instance = work.Owner.Instance with { SkillPool = pool };
    }

    /// <summary>캐시 계산: cache[value_1] = cache[value_2] (연산) cache[value_3]. 칸이 없거나 0으로 나누거나 decimal 범위를 넘으면 아무 일도 하지 않는다.</summary>
    void Calculate(EffectType type, CircuitEffect effect)
    {
        if (CacheAt(effect.Value2) is not { } left || CacheAt(effect.Value3) is not { } right) return;
        var op = type switch
        {
            EffectType.CacheAdd => SkillCache.Op.Add,
            EffectType.CacheSubtract => SkillCache.Op.Subtract,
            EffectType.CacheMultiply => SkillCache.Op.Multiply,
            _ => SkillCache.Op.Divide,
        };
        if (SkillCache.Calculate(op, left, right) is { } result) SkillCache.Write(CacheValues, effect.Value1, result);
    }

    /// <summary>값을 바꾼 캐릭터(범위에 맞춤), 유니크 아이템을 저장소에 되돌린다. 값을 바꾸지 않은 것은 그대로 둔다.</summary>
    void Commit()
    {
        foreach (var work in characterWorks.Values)
            if (!ReferenceEquals(work.Instance, work.Original)) Store.Replace(work.Original, work.Instance.Clamped());
        foreach (var work in itemWorks.Values)
            if (work.Buffer.Changed) Store.Replace(work.Original, work.Instance);
    }

    /// <summary>캐시의 index번 값. 없으면 null.</summary>
    protected decimal? CacheAt(long index) => SkillCache.At(CacheValues, index);
}
