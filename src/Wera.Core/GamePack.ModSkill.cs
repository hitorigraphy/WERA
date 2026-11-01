using System.IO.Compression;

namespace Wera.Core;

/// <summary>유니크 아이템 개조 스킬 마스터의 한 행: 유니크 아이템(UniqueItemCode)의 개조 슬롯(ModSlotCode)에 아이템이 끼워져 있으면 스킬(SkillCode)을 쓸 수 있다. Priority가 작을수록 우선이다.</summary>
public sealed record UniqueItemModSkill(long UniqueItemCode, long ModSlotCode, long Priority, long SkillCode);

// unique_item_mod_skill_master(unique_item_code, mod_slot_code; 두 열이 키, priority, skill_code). 선택이다.
// 같은 유니크 아이템에서 priority는 겹칠 수 없다(세컨더리 키 (unique_item_code, priority)): 둘 이상의 슬롯에 스킬이 있어도 우선되는 스킬이 하나로 정해진다.
public sealed partial class GamePack
{
    public const string UniqueItemModSkillMasterPath = "master/unique_item_mod_skill_master_v1.tsv";

    /// <summary>유니크 아이템 개조 스킬 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<UniqueItemModSkill>? UniqueItemModSkills { get; init; }

    /// <summary>세컨더리 키 (유니크 아이템, priority)로 찾는다.</summary>
    public UniqueItemModSkill? FindModSkillByPriority(long uniqueItemCode, long priority) =>
        UniqueItemModSkills?.FirstOrDefault(r => r.UniqueItemCode == uniqueItemCode && r.Priority == priority);

    /// <summary>
    /// 장비의 스킬 슬롯에 할당할 스킬: 아이템이 끼워진 개조 슬롯의 개조 스킬 중 priority가 가장 작은 것, 그런 개조 스킬이 없으면 기본 스킬
    /// (유니크 아이템 인벤토리 마스터의 skill_code). 둘 다 없으면 null.
    /// </summary>
    public long? AssignedSkillCode(UniqueItemInstance item)
    {
        var mods = item.Mods;
        var modSkill = mods is null ? null : (UniqueItemModSkills ?? [])
            .Where(r => r.UniqueItemCode == item.ItemCode && mods.ContainsKey(r.ModSlotCode)).OrderBy(r => r.Priority).FirstOrDefault();
        return modSkill?.SkillCode ?? FindUniqueItem(item.ItemCode)?.SkillCode;
    }

    /// <summary>
    /// 개조가 바뀐 뒤 스킬 슬롯을 다시 할당한다. 장비의 스킬 풀(<see cref="UniqueItemInstance.SkillPool"/>)은 한 번 들어온 스킬을 빼지 않고(스킬의 경험치 같은 스테이터스가 유지되도록)
    /// 지금 스킬 슬롯의 스킬(<see cref="UniqueItemInstance.Skill"/>)의 최신 값을 풀에 되적은 뒤, <see cref="AssignedSkillCode"/>의 스킬을 풀에서 꺼내(없으면 새로 만들어 풀에 넣고) 슬롯에 둔다.
    /// 할당할 스킬이 없으면 슬롯은 그대로다.
    /// </summary>
    public UniqueItemInstance WithAssignedSkill(UniqueItemInstance item)
    {
        var pool = new List<SkillInstance>(item.SkillPool ?? (item.Skill is { } current0 ? [current0] : []));
        if (item.Skill is { } current)
        {
            var at = pool.FindIndex(s => s.SkillCode == current.SkillCode);
            if (at >= 0) pool[at] = current; else pool.Add(current);
        }
        if (AssignedSkillCode(item) is not { } code) return item with { SkillPool = pool.Count > 0 ? pool : null };
        var assigned = pool.FirstOrDefault(s => s.SkillCode == code);
        if (assigned is null) pool.Add(assigned = NewSkillInstance(code));
        return item with { Skill = assigned, SkillPool = pool };
    }

    /// <summary>유니크 아이템 개조 스킬 마스터: 유니크 아이템은 유니크 아이템 인벤토리에, 개조 슬롯은 개조 슬롯 마스터에, 스킬은 스킬 마스터에 있어야 하고 키와 (유니크 아이템, priority)는 두 번 쓸 수 없다.</summary>
    static List<UniqueItemModSkill>? LoadUniqueItemModSkills(
        ZipArchive zip, IEnumerable<long>? uniqueItemCodes, List<ModSlotEntry>? modSlots, List<SkillEntry>? skills)
    {
        const string file = UniqueItemModSkillMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (uniqueItemCodes is null) throw new PackFormatException($"{file}가 있으려면 {UniqueItemInventoryMasterPath}도 필요합니다.");
        if (modSlots is null) throw new PackFormatException($"{file}가 있으려면 {ModSlotMasterPath}도 필요합니다.");
        if (skills is null) throw new PackFormatException($"{file}가 있으려면 {SkillMasterPath}도 필요합니다.");

        var uniques = uniqueItemCodes.ToHashSet();
        var slotCodes = modSlots.Select(s => s.Code).ToHashSet();
        var skillCodes = skills.Select(s => s.Code).ToHashSet();
        var rows = new List<UniqueItemModSkill>();
        var keys = new HashSet<(long, long)>();
        var priorities = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "unique_item_code", "mod_slot_code", "priority", "skill_code"))
        {
            var unique = ParseLong(file, "유니크 아이템 코드", r[0]);
            var who = $"유니크 아이템 '{unique}'의";
            var slot = ParseLong(file, $"{who} 개조 슬롯 코드", r[1]);
            var priority = ParseLong(file, $"{who} 개조 슬롯 '{slot}'의 priority", r[2]);
            var skill = ParseLong(file, $"{who} 개조 슬롯 '{slot}'의 skill_code", r[3]);
            if (!uniques.Contains(unique))
                throw new PackFormatException($"{file}: 유니크 아이템 '{unique}'가 {UniqueItemInventoryMasterPath}에 없습니다.");
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: {who} 개조 슬롯 '{slot}'가 {ModSlotMasterPath}에 없습니다.");
            if (!skillCodes.Contains(skill))
                throw new PackFormatException($"{file}: {who} 개조 슬롯 '{slot}'의 스킬 '{skill}'가 스킬 마스터에 없습니다.");
            if (!keys.Add((unique, slot)))
                throw new PackFormatException($"{file}: 같은 키(유니크 아이템 {unique}, 개조 슬롯 {slot})가 두 번 있습니다.");
            if (!priorities.Add((unique, priority)))
                throw new PackFormatException($"{file}: {who} priority '{priority}'가 두 번 있습니다(한 유니크 아이템에서 priority는 겹칠 수 없습니다).");
            rows.Add(new UniqueItemModSkill(unique, slot, priority, skill));
        }
        return rows;
    }
}
