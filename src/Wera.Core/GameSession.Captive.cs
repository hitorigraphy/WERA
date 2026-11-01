namespace Wera.Core;

// 구금된 아군: 던전 안 전투에서 졌을 때 전투 패배 후 서킷의 CaptureFocusedCharacter 이펙트가 정한 캐릭터. 구금된 아군 태그(게임 설정 CapturedAllyTagCode)가 붙고
// 그 던전(의 인스턴스)에 갇힌다. 장비는 그대로 들고 가고, 구금된 아군은 파티 편성, 캐릭터 확인 등 어디에도 나오지 않으며 그가 낀 유니크 아이템도 나오지 않는다.
public sealed partial class GameSession
{
    /// <summary>던전에 갇힌 구금된 아군: 던전 코드 → 갇힌 유닛들(캐릭터 인스턴스와 그가 낀 장비). 던전을 다른 파티로 클리어해(던전 이벤트의 ReleaseCaptives 흐름) 구출한다. 클리어 자체는 플래그로 다룬다.</summary>
    public Dictionary<long, List<PartyUnit>> DungeonCaptives { get; } = [];

    // 던전 전투에서 졌을 때 서킷이 구금하라고 한 캐릭터들(전투 화면을 떠날 때 한꺼번에 적용한다).
    readonly List<string> pendingCaptures = [];

    /// <summary>구금된 아군인가(게임 설정 CapturedAllyTagCode의 태그를 가졌다).</summary>
    public bool IsCapturedAlly(string instanceId) =>
        Pack.CapturedAllyTagCode is { } tag && Characters.ContainsKey(instanceId) && CharacterTags(instanceId).Contains(tag);

    IEnumerable<PartyUnit> CaptiveUnits => DungeonCaptives.Values.SelectMany(units => units);

    /// <summary>
    /// 이 캐릭터를 구금된 아군으로 이 던전에 가둔다: 파티와 아지트에서 빠지고(장비는 유닛과 함께 던전에 남는다) 구금된 아군 태그가 붙는다.
    /// 구금된 아군 태그가 설정되지 않았거나, 플레이어(캐릭터 1)이거나, 이미 구금됐거나, 보유하지 않았으면 false.
    /// </summary>
    public bool CaptureAlly(string instanceId, long dungeonCode)
    {
        if (Pack.CapturedAllyTagCode is not { } tag || !Characters.TryGetValue(instanceId, out var character)
            || character.CharacterCode == GamePack.PlayerCharacterCode || IsCapturedAlly(instanceId)) return false;
        var unit = AllParties.SelectMany(p => p.Units).OfType<PartyUnit>().FirstOrDefault(u => IdOf(u) == instanceId)
            ?? new PartyUnit(character.CharacterCode, [], false, instanceId);
        RemoveFromParties(instanceId);
        RemoveFromHideout(instanceId);
        if (!DungeonCaptives.TryGetValue(dungeonCode, out var captives)) DungeonCaptives[dungeonCode] = captives = [];
        captives.Add(new PartyUnit(character.CharacterCode, new Dictionary<long, EquipRef>(unit.Equipment), false, instanceId));
        return AddCharacterTag(instanceId, tag) || CharacterTags(instanceId).Contains(tag);
    }

    /// <summary>
    /// 이 던전에 갇힌 구금된 아군을 모두 구출한다: 구금된 아군 태그가 떨어지고 던전의 갇힌 목록에서 빠져 보유 캐릭터로 돌아온다(파티에는 돌아오지 않는다).
    /// 그들이 낀 장비는 더 이상 갇히지 않아 보유 장비로 돌아온다. 구출한 캐릭터 수를 돌려준다.
    /// </summary>
    public int RescueCaptives(long dungeonCode)
    {
        if (Pack.CapturedAllyTagCode is not { } tag || !DungeonCaptives.Remove(dungeonCode, out var units)) return 0;
        foreach (var unit in units)
            if (IdOf(unit) is { } id) RemoveCharacterTag(id, tag);
        return units.Count;
    }

    /// <summary>진 던전 전투에서 서킷이 구금하라고 한 캐릭터들을 지금 던전에 가둔다(전투 화면을 떠날 때).</summary>
    void ApplyPendingCaptures()
    {
        if (CurrentDungeonCode is { } dungeon)
            foreach (var id in pendingCaptures.Distinct()) CaptureAlly(id, dungeon);
        pendingCaptures.Clear();
    }
}
