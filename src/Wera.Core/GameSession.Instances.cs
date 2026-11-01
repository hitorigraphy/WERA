namespace Wera.Core;

// 같은 캐릭터를 여러 명(인스턴스) 가질 수 있다: 캐릭터 인벤토리 마스터의 carry_limit이 한 코드를 가질 수 있는 최대 수다("특별한 캐릭터는 1").
// 보유한 캐릭터를 가리키는 것(파티 유닛, 오퍼레이터, 파트너/어시스트, 태그, 상태이상, 장비 등)은 모두 인스턴스 ID(ULID 문자열)로 가리킨다.
// 마스터가 정한 곳(흐름 칸, 교환, 태그 조건)에서 "코드가 X인 캐릭터"는 그 코드의 어느 인스턴스든을 뜻하고, 하나를 골라야 할 때는 첫 인스턴스(OrderNumber순, 같으면 InstanceId순)다.
public sealed partial class GameSession
{
    /// <summary>이 캐릭터 코드의 보유 인스턴스들: OrderNumber순이고 같으면 InstanceId순.</summary>
    public IReadOnlyList<CharacterInstance> CharacterInstancesOf(long characterCode) => Characters.InstancesOf(characterCode);

    /// <summary>이 캐릭터 코드의 첫 보유 인스턴스. 보유하지 않았으면 null.</summary>
    public CharacterInstance? FirstInstanceOf(long characterCode) => Characters.FirstOf(characterCode);

    /// <summary>이 캐릭터 코드의 첫 보유 인스턴스의 ID. 보유하지 않았으면 null.</summary>
    public string? InstanceIdOf(long characterCode) => Characters.FirstOf(characterCode)?.InstanceId;

    /// <summary>이 캐릭터 코드를 몇 명 가졌는가.</summary>
    public int OwnedCountOf(long characterCode) => Characters.CountOf(characterCode);

    /// <summary>캐릭터 인스턴스의 캐릭터 코드. 보유하지 않은 ID면 null.</summary>
    public long? CodeOfInstance(string? instanceId) =>
        instanceId is not null && Characters.TryGetValue(instanceId, out var character) ? character.CharacterCode : null;

    /// <summary>한 코드를 가질 수 있는 최대 수: 캐릭터 인벤토리 마스터의 carry_limit(플레이어는 늘 1). 마스터에 없으면 0.</summary>
    public long CharacterLimitOf(long characterCode) => CharacterLimitOf(Pack, characterCode);

    static long CharacterLimitOf(GamePack pack, long characterCode) =>
        characterCode == GamePack.PlayerCharacterCode ? 1 : pack.FindCharacter(characterCode)?.CarryLimit ?? 0;

    /// <summary>이 캐릭터를 하나 더 얻을 수 있는가: 마스터에 있고 보유 수가 소지 한도보다 적다.</summary>
    public bool CanAddCharacter(long characterCode) =>
        Pack.FindCharacter(characterCode) is not null && Characters.CountOf(characterCode) < CharacterLimitOf(characterCode);

    /// <summary>이 유닛이 가리키는 보유 캐릭터 인스턴스의 ID. 유닛의 ID가 비어 있으면 그 코드의 첫 인스턴스. 보유하지 않았으면 null.</summary>
    string? IdOf(PartyUnit unit) => unit.InstanceId.Length > 0 ? unit.InstanceId : InstanceIdOf(unit.CharacterCode);

    /// <summary>파티에서 이 인스턴스의 유닛이 있는 칸(없으면 -1). ID가 빈 유닛은 그 코드의 첫 인스턴스로 본다.</summary>
    int IndexOfId(PartyInstance party, string instanceId)
    {
        for (var i = 0; i < party.Units.Length; i++)
            if (party.Units[i] is { } unit && IdOf(unit) == instanceId) return i;
        return -1;
    }

    /// <summary>이 인스턴스가 파티(방어 파티 포함)나 오퍼레이터 슬롯 어딘가에 서 있는가.</summary>
    bool IsPlaced(string instanceId) =>
        AllParties.Any(p => IndexOfId(p, instanceId) >= 0) || hideoutOperators.ContainsValue(instanceId);

    /// <summary>
    /// 코드로 캐릭터를 가리켜 어딘가에 새로 세울 때 고르는 인스턴스: 아직 파티와 오퍼레이터 슬롯 어디에도 서지 않은 첫 인스턴스,
    /// 모두 어딘가에 서 있으면 첫 인스턴스. 보유하지 않았으면 null.
    /// </summary>
    string? PlaceableInstanceOf(long characterCode)
    {
        var all = Characters.InstancesOf(characterCode).Where(c => !IsCapturedEnemy(c.InstanceId) && !IsCapturedAlly(c.InstanceId)).ToList();
        return (all.FirstOrDefault(c => !IsPlaced(c.InstanceId)) ?? all.FirstOrDefault())?.InstanceId;
    }

    /// <summary>구금된 적인가(게임 설정 CapturedEnemyTagCode의 태그를 가졌다). 구금된 적은 교류만 할 수 있어 파티, 아지트(방어 파티, 오퍼레이터), 던전, 이벤트, 전투에는 쓸 수 없다.</summary>
    public bool IsCapturedEnemy(string instanceId) =>
        Pack.CapturedEnemyTagCode is { } tag && Characters.ContainsKey(instanceId) && CharacterTags(instanceId).Contains(tag);

    /// <summary>
    /// 방어전에서 진 적을 구금된 적으로 보유 캐릭터에 들인다: 소지 한도(carry_limit)를 넘지 않으면 새 인스턴스를 만들고 구금된 적 태그를 붙여 그 ID를 돌려준다.
    /// 한도가 가득 찼거나 구금된 적 태그가 설정되지 않았으면 null.
    /// </summary>
    public string? AddCapturedEnemy(long characterCode)
    {
        if (Pack.CapturedEnemyTagCode is not { } tag) return null;
        var id = AddCharacterInstance(characterCode);
        if (id is null) return null;
        AddCharacterTag(id, tag);
        return id;
    }
}
