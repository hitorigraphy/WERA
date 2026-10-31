namespace Wera.Core;

/// <summary>
/// 캐릭터 생성 후 서킷: 캐릭터를 새로 얻으면(또는 적 유닛의 임시 캐릭터를 만들면) 그 캐릭터에 대해 캐릭터 생성 후 이펙트(마스터의 execute_number 순서)을 실행해
/// 게임팩의 정의에 따라 캐릭터 데이터를 조작한다. 그 캐릭터가 시전자이자 메인 타겟이고(장비 없는 유닛) 처음부터 포커스되어 있으며 서브 타겟은 없다.
/// 이펙트의 종류와 동작은 <see cref="CircuitBase"/>와 <see cref="EffectType"/>을 따르고, 여기에 생성 후에만 있는 종류
/// (<see cref="CharacterAfterCreateEffectType.AddSkillToPool"/>, <see cref="CharacterAfterCreateEffectType.AddEquipment"/>,
/// <see cref="CharacterAfterCreateEffectType.SetSkillToSlot"/>, <see cref="CharacterAfterCreateEffectType.SetEquipmentToSlot"/>)이 더해진다.
/// 실행이 끝나면 바뀐 스테이터스를 정수(소수는 버림)로 각 스테이터스의 범위에 맞춰 인스턴스에 되돌린다.
/// </summary>
public sealed class CharacterAfterCreateCircuit : CircuitBase
{
    readonly Dictionary<string, UniqueItemInstance> items;
    readonly Func<long, UniqueItemInstance?>? createItem;
    readonly string idPrefix;
    int created;

    public CharacterAfterCreateCircuit(GamePack pack, CharacterInstance character)
        : this(pack, character, [], null)
    {
    }

    /// <param name="items">유니크 아이템 인스턴스를 둘 곳(세션의 보유 아이템이나 적의 임시 장비). 서킷이 바꾼 아이템도 여기에 되돌린다.</param>
    /// <param name="createItem">AddEquipment가 유니크 아이템을 얻는 방법(세션은 보유 아이템으로 얻는다). null이면 서킷이 <paramref name="items"/>에 직접 만든다.</param>
    public CharacterAfterCreateCircuit(GamePack pack, CharacterInstance character, Dictionary<string, UniqueItemInstance> items, Func<long, UniqueItemInstance?>? createItem)
        : base(pack, new UnitStore(new Dictionary<long, CharacterInstance> { [character.CharacterCode] = character }, items), new PartyUnit(character.CharacterCode), null)
    {
        this.items = items;
        this.createItem = createItem;
        idPrefix = character.InstanceId;
        PresetFocus(character: character);
    }

    /// <summary>서킷이 쓰는 유닛: 이 캐릭터 + SetEquipmentToSlot이 낀 장비. 실행한 뒤의 장비를 이 유닛에서 읽는다.</summary>
    public PartyUnit Unit => Caster!;

    /// <summary>이펙트을 실행 번호 순서로 실행하고 조작이 끝난 캐릭터를 돌려준다. 이펙트이 없으면 원래 인스턴스를 그대로 돌려준다.</summary>
    public CharacterInstance Execute()
    {
        Run(Pack.CharacterAfterCreateEffectsOf(Caster!.CharacterCode).Select(e => new CircuitEffect(e.ExecuteNumber, (int)e.EffectType, e.Value1, e.Value2, e.Value3)).ToList());
        return Store.CharacterOf(Caster!)!;
    }

    protected override void ApplyOther(CircuitEffect effect)
    {
        switch ((CharacterAfterCreateEffectType)effect.TypeNumber)
        {
            case CharacterAfterCreateEffectType.AddSkillToPool:
                AddSkillToFocusedPool(effect.Value1);
                break;
            case CharacterAfterCreateEffectType.AddEquipment:
                if ((createItem?.Invoke(effect.Value1) ?? CreateItem(effect.Value1)) is { } item) FocusItem(item);
                break;
            case CharacterAfterCreateEffectType.SetSkillToSlot:
                SetFocusedSkillToSlot(effect.Value1);
                break;
            case CharacterAfterCreateEffectType.SetEquipmentToSlot:
                if (FocusedUniqueItemInstance is { } focused)
                {
                    var equip = EquipRef.Unique(focused.InstanceId);
                    foreach (var slot in Unit.Equipment.Where(e => e.Value == equip).Select(e => e.Key).ToList()) Unit.Equipment.Remove(slot);
                    Unit.Equipment[effect.Value1] = equip;
                }
                break;
        }
    }

    /// <summary>유니크 아이템 한 개를 이 서킷의 아이템 모음에 새로 만든다(생성 후 서킷이 돈다). 마스터에 없으면 null.</summary>
    UniqueItemInstance? CreateItem(long itemCode)
    {
        if (Pack.FindUniqueItem(itemCode) is null) return null;
        var id = $"{idPrefix}-item-{++created}";
        return items[id] = new UniqueItemAfterCreateCircuit(Pack, new UniqueItemInstance(id, itemCode, items.Count,
            Pack.MasterTagsOfUniqueItem(itemCode).ToList(), Pack.MasterStatusesOfUniqueItem(itemCode))).Execute();
    }
}
