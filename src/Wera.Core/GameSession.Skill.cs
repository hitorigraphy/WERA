using System.Globalization;

namespace Wera.Core;

/// <summary>스킬 풀 한 줄: 풀 안의 번호(0부터), 스킬 이름, 지금 장착 중인 스킬 슬롯의 이름(장착하지 않았으면 null).</summary>
public sealed record SkillPoolRow(int Index, string Name, string? EquippedSlotName);

// 스킬 설정: 파티 설정의 [103] 모드. 캐릭터를 고르면 그 캐릭터의 스킬 슬롯(장착한 스킬)과 스킬 풀 버튼이 나오고,
// 슬롯을 고르면 그 슬롯에 넣을 수 있는 풀 안의 스킬이 나오며 고르면 장착한다. 스킬 풀을 고르면 익힌 스킬 목록을 보여 준다.
public sealed partial class GameSession
{
    /// <summary>스킬 설정 화면의 "[0] 스킬 풀": 슬롯 목록 맨 아래(구분선 아래)에서 익힌 스킬 목록으로 간다.</summary>
    public const string SkillPoolEntryCode = "0";

    /// <summary>스킬 슬롯 선택 화면에서 지금 보는 스킬 슬롯의 코드.</summary>
    public long? CurrentSkillSlotCode { get; private set; }

    CharacterInstance? CurrentCharacterInstance =>
        CurrentCharacterCode is { } code && Characters.TryGetValue(code, out var instance) ? instance : null;

    /// <summary>지금 보는 캐릭터의 스킬 슬롯들(스킬 슬롯 코드순). 스킬 슬롯 마스터의 모든 슬롯이다(태그 규칙이 없는 슬롯에는 아무것도 넣을 수 없다).</summary>
    public IReadOnlyList<SkillSlotEntry> CurrentSkillSlots =>
        CurrentCharacterCode is { } character
            ? Pack.SkillSlotsOf(character)
            : [];

    /// <summary>스킬 슬롯 하나에 장착한 스킬의 이름. 비었으면 null.</summary>
    string? EquippedSkillName(CharacterInstance character, long slotCode) =>
        character.SkillSlots.TryGetValue(slotCode, out var index) && index >= 0 && index < character.SkillPool.Count
            ? SkillName(character.SkillPool[(int)index].SkillCode)
            : null;

    string SkillName(long skillCode) => Pack.FindSkill(skillCode)?.Name ?? Text(skillCode);

    /// <summary>스킬 설정 화면의 슬롯 한 줄: "스킬 슬롯 이름 : 장착한 스킬(없으면 비어 있음)".</summary>
    string SkillSlotLabel(SkillSlotEntry slot) =>
        $"{slot.Name} : {(CurrentCharacterInstance is { } c ? EquippedSkillName(c, slot.Code) : null) ?? "비어 있음"}";

    /// <summary>지금 보는 캐릭터의 스킬 풀(익힌 순서). 장착 중이면 어느 슬롯인지 알려 준다.</summary>
    public IReadOnlyList<SkillPoolRow> SkillPoolRows
    {
        get
        {
            if (CurrentCharacterInstance is not { } character) return [];
            return character.SkillPool.Select((skill, index) => new SkillPoolRow(
                index, SkillName(skill.SkillCode),
                character.SkillSlots.Where(kv => kv.Value == index).Select(kv => Pack.FindSkillSlot(kv.Key)?.Name).FirstOrDefault())).ToList();
        }
    }

    /// <summary>지금 보는 스킬 슬롯에 넣을 수 있는 풀 안의 스킬들(풀 번호순).</summary>
    public IReadOnlyList<SkillPoolRow> SlottableSkillRows
    {
        get
        {
            if (CurrentCharacterInstance is not { } character || CurrentSkillSlotCode is not { } slot) return [];
            return SkillPoolRows.Where(r => Pack.CanSlotSkill(character.CharacterCode, slot, character.SkillPool[r.Index].SkillCode)).ToList();
        }
    }

    IReadOnlyList<ScreenEntry> SkillCharacterEntries() =>
        CurrentSkillSlots
            .Select(s => new ScreenEntry(Text(s.Code), SkillSlotLabel(s), Screen.SkillSlotSelect))
            .Append(new ScreenEntry(SkillPoolEntryCode, "스킬 풀", Screen.SkillPoolList))
            .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.SkillSetup))
            .ToList();

    IReadOnlyList<ScreenEntry> SkillSlotSelectEntries()
    {
        var entries = new List<ScreenEntry>();
        if (CurrentCharacterInstance is { } character && CurrentSkillSlotCode is { } slot && EquippedSkillName(character, slot) is not null)
            entries.Add(new ScreenEntry(ClearEntryCode, "비우기", Screen.SkillCharacter, EntryAction.ClearSkillSlot));
        entries.AddRange(SlottableSkillRows.Select(r => new ScreenEntry(
            Text(r.Index + 1), r.EquippedSlotName is null ? r.Name : $"{r.Name} ({r.EquippedSlotName})",
            Screen.SkillCharacter, EntryAction.EquipSkill, Text(r.Index))));
        entries.Add(new ScreenEntry(BackEntryCode, "돌아가기", Screen.SkillCharacter));
        return entries;
    }

    /// <summary>
    /// 지금 보는 스킬 슬롯에 풀 안의 스킬(번호)을 장착한다. 같은 스킬(풀 번호)이 다른 슬롯에 있었으면 그 슬롯은 비고,
    /// 이 슬롯에 있던 스킬은 풀로 돌아간다. 이 슬롯에 넣을 수 없는 스킬이면 아무 일도 하지 않는다.
    /// </summary>
    string EquipSkillFromScreen(int index)
    {
        if (CurrentCharacterInstance is not { } character || CurrentSkillSlotCode is not { } slot) return "";
        if (index < 0 || index >= character.SkillPool.Count || !Pack.CanSlotSkill(character.CharacterCode, slot, character.SkillPool[index].SkillCode))
            return "이 슬롯에는 장착할 수 없습니다.";
        var slots = character.SkillSlots.Where(kv => kv.Value != index).ToDictionary(kv => kv.Key, kv => kv.Value);
        slots[slot] = index;
        Characters[character.CharacterCode] = character with { SkillSlots = slots };
        return $"장착: {SkillName(character.SkillPool[index].SkillCode)} → {Pack.FindSkillSlot(slot)?.Name}";
    }

    string ClearSkillSlotFromScreen()
    {
        if (CurrentCharacterInstance is not { } character || CurrentSkillSlotCode is not { } slot) return "";
        var name = EquippedSkillName(character, slot);
        var slots = new Dictionary<long, long>(character.SkillSlots);
        slots.Remove(slot);
        Characters[character.CharacterCode] = character with { SkillSlots = slots };
        return name is null ? "" : $"해제: {name}";
    }
}
