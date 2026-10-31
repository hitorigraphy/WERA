using System.Globalization;

namespace Wera.Core;

/// <summary>디버그 목록 한 줄: 마스터의 코드와 이름.</summary>
public sealed record DebugListItem(long Code, string Name);

// 디버그(400): 시스템 메뉴 아래의 개발용 화면. 마스터에 있는 캐릭터, 유니크 아이템(장비), 커먼 아이템을 페이지당 20개씩 늘어놓고 누르면 하나 추가한다.
public sealed partial class GameSession
{
    /// <summary>디버그 목록의 한 페이지에 나오는 항목 수.</summary>
    public const int DebugPageSize = 20;

    /// <summary>디버그 화면의 버튼: [401] 캐릭터 추가, [402] 장비 추가, [403] 아이템 추가.</summary>
    public const string DebugAddCharacterEntryCode = "401";
    public const string DebugAddEquipmentEntryCode = "402";
    public const string DebugAddItemEntryCode = "403";

    /// <summary>추가 화면의 [101] 이전 페이지, [102] 다음 페이지(항목은 페이지 안의 번호 [1]~[20]).</summary>
    public const string DebugPreviousPageEntryCode = "101";
    public const string DebugNextPageEntryCode = "102";

    /// <summary>디버그 추가 화면에서 보고 있는 페이지(0부터). 추가 화면에 들어올 때마다 0으로 돌아간다.</summary>
    public int DebugPage { get; private set; }

    /// <summary>[404] 스킬 추가: 먼저 스킬을 받을 가진 캐릭터를 고르고, 그 캐릭터의 스킬 풀에 마스터의 스킬을 하나씩 더한다.</summary>
    public const string DebugAddSkillEntryCode = "404";

    /// <summary>디버그 스킬 추가에서 고른 캐릭터의 코드.</summary>
    public long? DebugSkillTarget { get; private set; }

    static bool IsDebugAddScreen(Screen screen) =>
        screen is Screen.DebugAddCharacter or Screen.DebugAddEquipment or Screen.DebugAddItem or Screen.DebugSkillCharacter or Screen.DebugAddSkill;

    /// <summary>지금 디버그 추가 화면의 전체 목록(마스터 순서): 캐릭터 인벤토리, 유니크 아이템 인벤토리, 커먼 아이템 인벤토리. 추가 화면이 아니면 빈 목록.</summary>
    public IReadOnlyList<DebugListItem> DebugList => Screen switch
    {
        Screen.DebugAddCharacter => (Pack.CharacterInventory ?? []).Select(c => new DebugListItem(c.Code, c.Name)).ToList(),
        Screen.DebugAddEquipment => (Pack.UniqueItems ?? []).Select(i => new DebugListItem(i.ItemCode, i.ItemName)).ToList(),
        Screen.DebugAddItem => (Pack.ItemInventory ?? []).Select(i => new DebugListItem(i.Code, i.Name)).ToList(),
        Screen.DebugSkillCharacter => Characters.Keys.Order().Select(c => new DebugListItem(c, Pack.FindCharacter(c)?.Name ?? Text(c))).ToList(),
        Screen.DebugAddSkill => (Pack.Skills ?? []).Select(s => new DebugListItem(s.Code, s.Name)).ToList(),
        _ => [],
    };

    /// <summary>디버그 목록의 페이지 수(항목이 없어도 1).</summary>
    public int DebugPageCount => Math.Max(1, (DebugList.Count + DebugPageSize - 1) / DebugPageSize);

    /// <summary>지금 페이지의 항목들(최대 <see cref="DebugPageSize"/>개).</summary>
    public IReadOnlyList<DebugListItem> DebugPageItems => DebugList.Skip(DebugPage * DebugPageSize).Take(DebugPageSize).ToList();

    IReadOnlyList<ScreenEntry> DebugEntries()
    {
        if (Screen == Screen.Debug)
            return
            [
                new ScreenEntry(DebugAddCharacterEntryCode, "캐릭터 추가", Screen.DebugAddCharacter),
                new ScreenEntry(DebugAddEquipmentEntryCode, "장비 추가", Screen.DebugAddEquipment),
                new ScreenEntry(DebugAddItemEntryCode, "아이템 추가", Screen.DebugAddItem),
                new ScreenEntry(DebugAddSkillEntryCode, "스킬 추가", Screen.DebugSkillCharacter),
                new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home),
            ];

        var items = DebugPageItems;
        var picking = Screen == Screen.DebugSkillCharacter;      // 캐릭터를 고르면 스킬 목록으로 간다
        return items
            .Select((item, i) => picking
                ? new ScreenEntry(Text(i + 1), $"{item.Name} ({item.Code})", Screen.DebugAddSkill, EntryAction.DebugSelectSkillCharacter, Text(item.Code))
                : new ScreenEntry(Text(i + 1), $"{item.Name} ({item.Code})", Screen, EntryAction.DebugAdd, Text(item.Code)))
            .Append(new ScreenEntry(DebugPreviousPageEntryCode, "이전 페이지", Screen, EntryAction.DebugPage, "-1", Enabled: DebugPage > 0))
            .Append(new ScreenEntry(DebugNextPageEntryCode, "다음 페이지", Screen, EntryAction.DebugPage, "1", Enabled: DebugPage < DebugPageCount - 1))
            .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen == Screen.DebugAddSkill ? Screen.DebugSkillCharacter : Screen.Debug))
            .ToList();
    }

    void ChangeDebugPage(int delta) => DebugPage = Math.Clamp(DebugPage + delta, 0, DebugPageCount - 1);

    /// <summary>항목의 마스터 코드로 하나 추가하고 결과 안내를 돌려준다: 캐릭터는 이미 가졌으면 못 얻고, 아이템은 소지 한도에서 멈춘다.</summary>
    string DebugAddOne(long code)
    {
        var name = DebugList.FirstOrDefault(d => d.Code == code)?.Name ?? Text(code);
        switch (Screen)
        {
            case Screen.DebugAddCharacter:
                return AddCharacter(code) ? $"캐릭터 추가: {name}" : $"이미 가지고 있는 캐릭터입니다: {name}";
            case Screen.DebugAddEquipment:
                return AddUniqueItem(code) is not null ? $"장비 추가: {name}" : $"장비를 추가하지 못했습니다: {name}";
            case Screen.DebugAddItem:
                var before = CountInventory(code);
                AddInventory(code, 1);
                return CountInventory(code) > before ? $"아이템 추가: {name} (보유 {CountInventory(code)})" : $"소지 한도라 더 추가할 수 없습니다: {name}";
            case Screen.DebugAddSkill:
                if (DebugSkillTarget is not { } target || !Characters.TryGetValue(target, out var character)) return "";
                Characters[target] = character with { SkillPool = [.. character.SkillPool, Pack.NewSkillInstance(code)] };
                return $"스킬 추가: {name} → {Pack.FindCharacter(target)?.Name ?? Text(target)} (스킬 풀 {character.SkillPool.Count + 1}개)";
            default:
                return "";
        }
    }
}
