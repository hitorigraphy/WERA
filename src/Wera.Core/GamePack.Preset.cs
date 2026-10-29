using System.IO.Compression;

namespace Wera.Core;

/// <summary>프리셋 유닛 마스터의 한 행: 전투에 나오는 유닛 하나가 어느 캐릭터인가.</summary>
public sealed record PresetUnit(long Code, long CharacterCode);

/// <summary>프리셋 유닛 장비 마스터의 한 행: 유닛이 장비 슬롯에 끼고 있는 것(인벤토리 코드; 번치는 안 된다).</summary>
public sealed record PresetUnitEquip(long PresetUnitCode, long EquipmentSlotCode, long ItemCode);

/// <summary>프리셋 유닛 드롭 마스터의 한 행: 유닛을 쓰러뜨리면 얻는 인벤토리 코드와 개수.</summary>
public sealed record PresetUnitDrop(long PresetUnitCode, long ItemCode, long Amount);

/// <summary>
/// 프리셋 파티 마스터의 한 행: 이름과 3×3 진형. <see cref="UnitCodes"/>는 아홉 칸이고 순서는
/// 전위 왼쪽, 전위 가운데, 전위 오른쪽, 중위 왼쪽, 중위 가운데, 중위 오른쪽, 후위 왼쪽, 후위 가운데, 후위 오른쪽이다.
/// 칸이 비어 있으면 null이다.
/// </summary>
public sealed record PresetParty(long Code, string Name, IReadOnlyList<long?> UnitCodes);

/// <summary>전투 적 마스터의 한 행: 이 전투에 나올 수 있는 프리셋 파티와 그 가중치(1 이상).</summary>
public sealed record BattleEnemy(long BattleCode, long PresetPartyCode, long Weight);

// 전투 편성: preset_unit_master(code, character_code; code가 키), preset_unit_equip_master(preset_unit_code, equipment_slot_code, item_code; 앞의 두 열이 키),
// preset_unit_drop_master(preset_unit_code, item_code, amount; 앞의 두 열이 키),
// preset_party_master(code, name, front_left_unit_code ... back_right_unit_code; code가 키), battle_master(code, name; code가 키),
// battle_enemy_master(battle_code, preset_party_code, weight; 앞의 두 열이 키).
public sealed partial class GamePack
{
    public const string PresetUnitMasterPath = "master/preset_unit_master_v1.tsv";
    public const string PresetUnitEquipMasterPath = "master/preset_unit_equip_master_v1.tsv";
    public const string PresetUnitDropMasterPath = "master/preset_unit_drop_master_v1.tsv";
    public const string PresetPartyMasterPath = "master/preset_party_master_v1.tsv";
    public const string BattleEnemyMasterPath = "master/battle_enemy_master_v1.tsv";

    /// <summary>프리셋 파티 마스터의 진형 칸 열 이름(전위, 중위, 후위 순서이고 각 줄은 왼쪽, 가운데, 오른쪽 순서).</summary>
    public static IReadOnlyList<string> PresetPartyUnitColumns { get; } =
    [
        "front_left_unit_code", "front_center_unit_code", "front_right_unit_code",
        "middle_left_unit_code", "middle_center_unit_code", "middle_right_unit_code",
        "back_left_unit_code", "back_center_unit_code", "back_right_unit_code",
    ];

    /// <summary>프리셋 유닛 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<PresetUnit>? PresetUnits { get; init; }

    /// <summary>프리셋 유닛 장비 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<PresetUnitEquip>? PresetUnitEquips { get; init; }

    /// <summary>프리셋 유닛 드롭 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<PresetUnitDrop>? PresetUnitDrops { get; init; }

    /// <summary>프리셋 파티 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<PresetParty>? PresetParties { get; init; }

    /// <summary>전투 적 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<BattleEnemy>? BattleEnemies { get; init; }

    /// <summary>프리셋 유닛 마스터: 캐릭터 코드는 캐릭터 인벤토리 마스터에 있어야 한다.</summary>
    static List<PresetUnit>? LoadPresetUnits(ZipArchive zip, List<CharacterEntry>? characters)
    {
        const string file = PresetUnitMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (characters is null) throw new PackFormatException($"{file}가 있으려면 {CharacterInventoryMasterPath}도 필요합니다.");

        var characterCodes = characters.Select(c => c.Code).ToHashSet();
        var rows = new List<PresetUnit>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, "code", "character_code"))
        {
            var code = ParseLong(file, "프리셋 유닛 코드", r[0]);
            var characterCode = ParseLong(file, $"프리셋 유닛 '{code}'의 캐릭터 코드", r[1]);
            if (!seen.Add(code))
                throw new PackFormatException($"{file}: 프리셋 유닛 코드 '{code}'가 중복됩니다.");
            if (!characterCodes.Contains(characterCode))
                throw new PackFormatException($"{file}: 프리셋 유닛 '{code}'의 캐릭터 코드 '{characterCode}'가 캐릭터 인벤토리 마스터에 없습니다.");
            rows.Add(new PresetUnit(code, characterCode));
        }
        return rows;
    }

    /// <summary>
    /// 프리셋 유닛 장비 마스터: 유닛은 프리셋 유닛 마스터에, 슬롯은 장비 슬롯 마스터에, 아이템은 인벤토리(커먼, 유니크, 캐릭터)에 있어야 한다.
    /// 유닛이 그 슬롯에 그 아이템을 낄 수 있는지(슬롯 태그 규칙)는 마스터를 모두 올린 뒤 <see cref="CheckPresetUnitEquips"/>가 본다.
    /// </summary>
    static List<PresetUnitEquip>? LoadPresetUnitEquips(
        ZipArchive zip, List<PresetUnit>? units, List<EquipmentSlotEntry>? slots, IReadOnlyDictionary<long, InventoryEntry> inventory)
    {
        const string file = PresetUnitEquipMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (units is null) throw new PackFormatException($"{file}가 있으려면 {PresetUnitMasterPath}도 필요합니다.");
        if (slots is null) throw new PackFormatException($"{file}가 있으려면 {EquipmentSlotMasterPath}도 필요합니다.");

        var unitCodes = units.Select(u => u.Code).ToHashSet();
        var slotCodes = slots.Select(s => s.Code).ToHashSet();
        var rows = new List<PresetUnitEquip>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "preset_unit_code", "equipment_slot_code", "item_code"))
        {
            var unit = ParseLong(file, "프리셋 유닛 코드", r[0]);
            var slot = ParseLong(file, $"프리셋 유닛 '{unit}'의 장비 슬롯 코드", r[1]);
            var item = ParseLong(file, $"프리셋 유닛 '{unit}'의 아이템 코드", r[2]);
            if (!unitCodes.Contains(unit))
                throw new PackFormatException($"{file}: 프리셋 유닛 코드 '{unit}'가 프리셋 유닛 마스터에 없습니다.");
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: 프리셋 유닛 '{unit}'의 장비 슬롯 코드 '{slot}'가 장비 슬롯 마스터에 없습니다.");
            if (!inventory.TryGetValue(item, out var entry))
                throw new PackFormatException($"{file}: 프리셋 유닛 '{unit}'의 아이템 코드 '{item}'가 인벤토리 마스터에 없습니다.");
            if (entry.Kind == InventoryKind.BunchItem)
                throw new PackFormatException($"{file}: 프리셋 유닛 '{unit}'의 아이템 코드 '{item}'는 번치 아이템이라 장비로 낄 수 없습니다.");
            if (!seen.Add((unit, slot)))
                throw new PackFormatException($"{file}: 프리셋 유닛 '{unit}'의 장비 슬롯 '{slot}'가 두 번 적혀 있습니다.");
            rows.Add(new PresetUnitEquip(unit, slot, item));
        }
        return rows;
    }

    /// <summary>프리셋 유닛 드롭 마스터: 유닛과 인벤토리 코드는 마스터에 있어야 하고, 개수는 1 이상이며 유니크 아이템과 캐릭터는 1이다.</summary>
    static List<PresetUnitDrop>? LoadPresetUnitDrops(
        ZipArchive zip, List<PresetUnit>? units, IReadOnlyDictionary<long, InventoryEntry> inventory)
    {
        const string file = PresetUnitDropMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (units is null) throw new PackFormatException($"{file}가 있으려면 {PresetUnitMasterPath}도 필요합니다.");

        var unitCodes = units.Select(u => u.Code).ToHashSet();
        var rows = new List<PresetUnitDrop>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "preset_unit_code", "item_code", "amount"))
        {
            var unit = ParseLong(file, "프리셋 유닛 코드", r[0]);
            var item = ParseLong(file, $"프리셋 유닛 '{unit}'의 아이템 코드", r[1]);
            var amount = ParseLong(file, $"프리셋 유닛 '{unit}'의 드롭 개수", r[2]);
            if (!unitCodes.Contains(unit))
                throw new PackFormatException($"{file}: 프리셋 유닛 코드 '{unit}'가 프리셋 유닛 마스터에 없습니다.");
            if (!inventory.TryGetValue(item, out var entry))
                throw new PackFormatException($"{file}: 프리셋 유닛 '{unit}'의 아이템 코드 '{item}'가 인벤토리 마스터에 없습니다.");
            if (amount < 1)
                throw new PackFormatException($"{file}: 프리셋 유닛 '{unit}'의 아이템 '{item}' 드롭 개수 '{amount}'는 1 이상이어야 합니다.");
            if (entry.Kind is InventoryKind.UniqueItem or InventoryKind.Character && amount != 1)
                throw new PackFormatException($"{file}: 프리셋 유닛 '{unit}'의 아이템 '{item}'는 유니크 아이템이나 캐릭터라 드롭 개수가 1이어야 합니다.");
            if (!seen.Add((unit, item)))
                throw new PackFormatException($"{file}: 프리셋 유닛 '{unit}'의 아이템 '{item}'가 두 번 적혀 있습니다.");
            rows.Add(new PresetUnitDrop(unit, item, amount));
        }
        return rows;
    }

    /// <summary>프리셋 파티 마스터: 진형 칸은 프리셋 유닛 코드이거나 비어 있고(빈 칸), 유닛이 하나도 없는 파티는 없다.</summary>
    static List<PresetParty>? LoadPresetParties(ZipArchive zip, List<PresetUnit>? units)
    {
        const string file = PresetPartyMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (units is null) throw new PackFormatException($"{file}가 있으려면 {PresetUnitMasterPath}도 필요합니다.");

        var unitCodes = units.Select(u => u.Code).ToHashSet();
        var rows = new List<PresetParty>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, ["code", "name", .. PresetPartyUnitColumns]))
        {
            var code = ParseLong(file, "프리셋 파티 코드", r[0]);
            if (!seen.Add(code))
                throw new PackFormatException($"{file}: 프리셋 파티 코드 '{code}'가 중복됩니다.");
            var slots = new long?[PresetPartyUnitColumns.Count];
            for (var i = 0; i < slots.Length; i++)
            {
                var cell = r[i + 2].Trim();
                if (cell.Length == 0) continue;
                var unit = ParseLong(file, $"프리셋 파티 '{code}'의 {PresetPartyUnitColumns[i]}", cell);
                if (!unitCodes.Contains(unit))
                    throw new PackFormatException($"{file}: 프리셋 파티 '{code}'의 {PresetPartyUnitColumns[i]} '{unit}'가 프리셋 유닛 마스터에 없습니다.");
                slots[i] = unit;
            }
            if (slots.All(s => s is null))
                throw new PackFormatException($"{file}: 프리셋 파티 '{code}'에 유닛이 하나도 없습니다.");
            rows.Add(new PresetParty(code, r[1], slots));
        }
        return rows;
    }

    /// <summary>전투 적 마스터: 전투와 프리셋 파티는 마스터에 있어야 하고 가중치는 1 이상이다.</summary>
    static List<BattleEnemy>? LoadBattleEnemies(ZipArchive zip, List<BattleEntry>? battles, List<PresetParty>? parties)
    {
        const string file = BattleEnemyMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (battles is null) throw new PackFormatException($"{file}가 있으려면 {BattleMasterPath}도 필요합니다.");
        if (parties is null) throw new PackFormatException($"{file}가 있으려면 {PresetPartyMasterPath}도 필요합니다.");

        var battleCodes = battles.Select(b => b.Code).ToHashSet();
        var partyCodes = parties.Select(p => p.Code).ToHashSet();
        var rows = new List<BattleEnemy>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "battle_code", "preset_party_code", "weight"))
        {
            var battle = ParseLong(file, "전투 코드", r[0]);
            var party = ParseLong(file, $"전투 '{battle}'의 프리셋 파티 코드", r[1]);
            var weight = ParseLong(file, $"전투 '{battle}'의 가중치", r[2]);
            if (!battleCodes.Contains(battle))
                throw new PackFormatException($"{file}: 전투 코드 '{battle}'가 전투 마스터에 없습니다.");
            if (!partyCodes.Contains(party))
                throw new PackFormatException($"{file}: 전투 '{battle}'의 프리셋 파티 코드 '{party}'가 프리셋 파티 마스터에 없습니다.");
            if (weight < 1)
                throw new PackFormatException($"{file}: 전투 '{battle}'의 프리셋 파티 '{party}' 가중치 '{weight}'는 1 이상이어야 합니다.");
            if (!seen.Add((battle, party)))
                throw new PackFormatException($"{file}: 전투 '{battle}'의 프리셋 파티 '{party}'가 두 번 적혀 있습니다.");
            rows.Add(new BattleEnemy(battle, party, weight));
        }
        return rows;
    }

    /// <summary>
    /// 마스터를 모두 올린 뒤의 검사: 프리셋 유닛이 장비 슬롯에 낀 것은 그 슬롯의 태그 규칙에 맞아야 하고(캐릭터와 아이템의 마스터 태그 기준),
    /// 캐릭터를 낀다면 자기 자신이 아니어야 한다.
    /// </summary>
    void CheckPresetUnitEquips()
    {
        var characterOf = (PresetUnits ?? []).ToDictionary(u => u.Code, u => u.CharacterCode);
        foreach (var e in PresetUnitEquips ?? [])
        {
            var character = characterOf[e.PresetUnitCode];
            var kind = Inventory[e.ItemCode].Kind;
            if (kind == InventoryKind.Character && e.ItemCode == character)
                throw new PackFormatException(
                    $"{PresetUnitEquipMasterPath}: 프리셋 유닛 '{e.PresetUnitCode}'는 자기 자신(캐릭터 '{character}')을 장비로 낄 수 없습니다.");

            var tags = (kind switch
            {
                InventoryKind.UniqueItem => MasterTagsOfUniqueItem(e.ItemCode),
                InventoryKind.CommonItem => MasterTagsOfCommonItem(e.ItemCode),
                _ => MasterTagsOfCharacter(e.ItemCode),
            }).ToHashSet();
            tags.UnionWith(MasterTagsOfCharacter(character));
            if (!Fits(character, e.EquipmentSlotCode, tags))
                throw new PackFormatException(
                    $"{PresetUnitEquipMasterPath}: 프리셋 유닛 '{e.PresetUnitCode}'(캐릭터 '{character}')의 장비 슬롯 '{e.EquipmentSlotCode}'에 아이템 '{e.ItemCode}'를 낄 수 없습니다(슬롯 태그 규칙).");
        }
    }

    // ---- 조회 ----

    public PresetUnit? FindPresetUnit(long code) =>
        Masters.PresetUnitRowTable.TryFindByCode(code, out var r) ? new PresetUnit(r.Code, r.CharacterCode) : null;

    /// <summary>프리셋 유닛이 끼고 있는 장비(장비 슬롯 코드 순).</summary>
    public IReadOnlyList<PresetUnitEquip> PresetUnitEquipsOf(long presetUnitCode) =>
        Masters.PresetUnitEquipRowTable.FindByPresetUnitCode(presetUnitCode)
            .OrderBy(r => r.EquipmentSlotCode)
            .Select(r => new PresetUnitEquip(r.PresetUnitCode, r.EquipmentSlotCode, r.ItemCode)).ToList();

    /// <summary>프리셋 유닛이 쓰러질 때 떨구는 것(아이템 코드 순).</summary>
    public IReadOnlyList<PresetUnitDrop> PresetUnitDropsOf(long presetUnitCode) =>
        Masters.PresetUnitDropRowTable.FindByPresetUnitCode(presetUnitCode)
            .OrderBy(r => r.ItemCode)
            .Select(r => new PresetUnitDrop(r.PresetUnitCode, r.ItemCode, r.Amount)).ToList();

    public PresetParty? FindPresetParty(long code) =>
        Masters.PresetPartyRowTable.TryFindByCode(code, out var r)
            ? new PresetParty(r.Code, r.Name,
            [
                r.FrontLeftUnitCode, r.FrontCenterUnitCode, r.FrontRightUnitCode,
                r.MiddleLeftUnitCode, r.MiddleCenterUnitCode, r.MiddleRightUnitCode,
                r.BackLeftUnitCode, r.BackCenterUnitCode, r.BackRightUnitCode,
            ])
            : null;

    /// <summary>전투에 나올 수 있는 프리셋 파티들(프리셋 파티 코드 순).</summary>
    public IReadOnlyList<BattleEnemy> BattleEnemiesOf(long battleCode) =>
        Masters.BattleEnemyRowTable.FindByBattleCode(battleCode)
            .OrderBy(r => r.PresetPartyCode)
            .Select(r => new BattleEnemy(r.BattleCode, r.PresetPartyCode, r.Weight)).ToList();
}
