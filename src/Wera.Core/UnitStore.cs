namespace Wera.Core;

/// <summary>
/// 서킷이 파티 유닛(<see cref="PartyUnit"/>)에서 그 유닛의 캐릭터 인스턴스와 장비(유니크 아이템 인스턴스)를 찾고, 바뀐 인스턴스를 돌려 쓰는 저장소.
/// 아군 유닛은 코드로 보유 캐릭터를, 장비는 인스턴스 ID로 보유 유니크 아이템을 찾는다. 적 유닛은 같은 캐릭터가 여럿일 수 있어서
/// 유닛 객체(참조)가 어느 적 슬롯인지로 임시 캐릭터 인스턴스를 찾는다(적 장비도 임시 유니크 아이템 인스턴스다).
/// 모아 둔 사전은 세션의 것을 그대로 가리키므로 서킷이 끝나고 되돌린 값이 곧 세션의 값이다.
/// </summary>
public sealed class UnitStore
{
    readonly Dictionary<long, CharacterInstance> characters;
    readonly Dictionary<string, UniqueItemInstance> items;
    readonly IReadOnlyList<PartyUnit?> enemyUnits;
    readonly Dictionary<int, CharacterInstance> enemyCharacters;
    readonly Dictionary<string, UniqueItemInstance> enemyItems;

    public UnitStore(
        Dictionary<long, CharacterInstance> characters, Dictionary<string, UniqueItemInstance> items,
        IReadOnlyList<PartyUnit?>? enemyUnits = null, Dictionary<int, CharacterInstance>? enemyCharacters = null,
        Dictionary<string, UniqueItemInstance>? enemyItems = null)
    {
        this.characters = characters;
        this.items = items;
        this.enemyUnits = enemyUnits ?? [];
        this.enemyCharacters = enemyCharacters ?? [];
        this.enemyItems = enemyItems ?? [];
    }

    /// <summary>캐릭터 인스턴스 하나만 든 저장소(세션 밖에서 하나를 초기화할 때).</summary>
    public static UnitStore Of(CharacterInstance character) =>
        new(new Dictionary<long, CharacterInstance> { [character.CharacterCode] = character }, []);

    /// <summary>유니크 아이템 인스턴스 하나만 든 저장소.</summary>
    public static UnitStore Of(UniqueItemInstance item) =>
        new([], new Dictionary<string, UniqueItemInstance> { [item.InstanceId] = item });

    int EnemySlotOf(PartyUnit unit)
    {
        for (var slot = 0; slot < enemyUnits.Count; slot++)
            if (ReferenceEquals(enemyUnits[slot], unit)) return slot;
        return -1;
    }

    /// <summary>유닛의 캐릭터 인스턴스. 찾을 수 없으면 null.</summary>
    public CharacterInstance? CharacterOf(PartyUnit unit)
    {
        if (EnemySlotOf(unit) is var slot and >= 0)
            return enemyCharacters.TryGetValue(slot, out var enemy) ? enemy : null;
        return characters.TryGetValue(unit.CharacterCode, out var ally) ? ally : null;
    }

    /// <summary>유닛이 장비 슬롯에 낀 유니크 아이템 인스턴스. 슬롯이 비었거나 유니크 아이템이 아니면(커먼 아이템, 캐릭터) null.</summary>
    public UniqueItemInstance? ItemOf(PartyUnit unit, long equipmentSlotCode) =>
        unit.Equipment.TryGetValue(equipmentSlotCode, out var equip) && equip is { Kind: EquipKind.UniqueItem, Instance: { } id } ? FindItem(id) : null;

    /// <summary>인스턴스 ID로 유니크 아이템 인스턴스(보유나 적 장비)를 찾는다. 없으면 null.</summary>
    public UniqueItemInstance? FindItem(string id) =>
        items.TryGetValue(id, out var item) ? item : enemyItems.TryGetValue(id, out var temp) ? temp : null;

    /// <summary>서킷이 바꾼 캐릭터를 원래 있던 자리(보유 캐릭터나 적 슬롯)에 돌려 쓴다. 원래 인스턴스를 찾을 수 없으면 아무 일도 하지 않는다.</summary>
    public void Replace(CharacterInstance old, CharacterInstance now)
    {
        if (characters.TryGetValue(old.CharacterCode, out var ally) && ReferenceEquals(ally, old)) characters[old.CharacterCode] = now;
        else
            foreach (var (slot, enemy) in enemyCharacters)
                if (ReferenceEquals(enemy, old)) { enemyCharacters[slot] = now; return; }
    }

    /// <summary>서킷이 바꾼 유니크 아이템을 원래 있던 자리(보유나 적 장비)에 돌려 쓴다.</summary>
    public void Replace(UniqueItemInstance old, UniqueItemInstance now)
    {
        if (items.TryGetValue(old.InstanceId, out var own) && ReferenceEquals(own, old)) items[old.InstanceId] = now;
        else if (enemyItems.TryGetValue(old.InstanceId, out var temp) && ReferenceEquals(temp, old)) enemyItems[old.InstanceId] = now;
    }
}
