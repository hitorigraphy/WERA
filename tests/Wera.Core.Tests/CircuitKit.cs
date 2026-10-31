using Wera.Core;

namespace Wera.Core.Tests;

/// <summary>
/// 서킷 시험용 판: 캐릭터 인스턴스(마스터 초깃값)와 유니크 아이템 인스턴스를 든 저장소와, 그것들을 가리키는 파티 유닛을 만든다.
/// 서킷이 바꾼 값은 <see cref="Characters"/>와 <see cref="Items"/>에 그대로 반영된다.
/// </summary>
public sealed class CircuitField
{
    readonly GamePack pack;
    readonly Dictionary<long, PartyUnit> units = [];

    public CircuitField(GamePack pack, params long[] characters)
    {
        this.pack = pack;
        foreach (var code in characters) Characters[code] = pack.NewCharacterInstance(code, $"c{code}");
        Store = new UnitStore(Characters, Items);
    }

    public Dictionary<long, CharacterInstance> Characters { get; } = [];

    public Dictionary<string, UniqueItemInstance> Items { get; } = [];

    public UnitStore Store { get; }

    /// <summary>그 캐릭터의 파티 유닛(같은 코드면 같은 유닛).</summary>
    public PartyUnit Unit(long characterCode) =>
        units.TryGetValue(characterCode, out var unit) ? unit : units[characterCode] = new PartyUnit(characterCode);

    /// <summary>그 캐릭터의 유닛이 장비 슬롯에 유니크 아이템 하나를 낀다. 아이템 인스턴스의 ID를 돌려준다.</summary>
    public string Equip(long characterCode, long equipmentSlotCode, long itemCode)
    {
        var id = $"i{Items.Count}";
        Items[id] = new UniqueItemInstance(id, itemCode, 0, null, pack.MasterStatusesOfUniqueItem(itemCode));
        Unit(characterCode).Equipment[equipmentSlotCode] = EquipRef.Unique(id);
        return id;
    }

    /// <summary>그 캐릭터의 스킬 풀에 스킬을 익히고(마스터 초깃값) 스킬 슬롯에 장착한다.</summary>
    public void Learn(long characterCode, long skillSlotCode, long skillCode)
    {
        var character = Characters[characterCode];
        var pool = new List<SkillInstance>(character.SkillPool) { pack.NewSkillInstance(skillCode) };
        var slots = new Dictionary<long, long>(character.SkillSlots) { [skillSlotCode] = pool.Count - 1 };
        Characters[characterCode] = character with { SkillPool = pool, SkillSlots = slots };
    }
}
