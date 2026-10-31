using System.Globalization;

namespace Wera.Core;

public sealed partial class GameSession
{
    /// <summary>상세 화면이 열릴 때 쌓는 "열기 전" 화면: 돌아가기는 맨 위 것으로 돌아간다(상세에서 다른 상세로 들어가도 차례로 돌아온다).</summary>
    readonly Stack<DetailFrame> detailBack = new();

    sealed record DetailFrame(Screen Screen, long? Character, string? Equipment, long? Item, long? Skill);

    /// <summary>스킬 상세 화면이 보여 주는 스킬의 코드. 그 화면이 아니면 null.</summary>
    public long? DetailSkillCode { get; private set; }

    /// <summary>캐릭터 상세 화면이 보여 주는 캐릭터의 코드. 그 화면이 아니면 null.</summary>
    public long? DetailCharacterCode { get; private set; }

    /// <summary>장비 상세 화면이 보여 주는 장비(유니크 아이템 인스턴스)의 ID. 그 화면이 아니면 null.</summary>
    public string? DetailEquipmentId { get; private set; }

    /// <summary>아이템 상세 화면이 보여 주는 커먼 아이템의 코드. 그 화면이 아니면 null.</summary>
    public long? DetailItemCode { get; private set; }

    /// <summary>
    /// 상세 화면의 제목과 이름. 상세 화면의 내용은 아직 정의되지 않았다(앞으로 정의한다). 지금은 무엇을 보고 있는지 이름만 보여 준다.
    /// 상세 화면이 아니면 null.
    /// </summary>
    public (string Title, string Name)? DetailHeading => Screen switch
    {
        Screen.CharacterDetail when DetailCharacterCode is { } code => ("캐릭터 상세", Pack.FindCharacter(code)?.Name ?? Text(code)),
        Screen.EquipmentDetail when DetailEquipmentId is { } id && UniqueItemInstances.TryGetValue(id, out var item) =>
            ("장비 상세", Pack.FindUniqueItem(item.ItemCode)?.ItemName ?? Text(item.ItemCode)),
        Screen.ItemDetail when DetailItemCode is { } code => ("아이템 상세", Pack.ItemInventory?.FirstOrDefault(i => i.Code == code)?.Name ?? Text(code)),
        Screen.SkillDetail when DetailSkillCode is { } code => ("스킬 상세", SkillName(code)),
        _ => null,
    };

    /// <summary>캐릭터 상세 화면의 Exp 바 칸 수("Exp : " 뒤, 오른쪽 여백을 30px 남기고 끝까지 가는 길이).</summary>
    public const int CharacterDetailExpWidth = 61;

    /// <summary>캐릭터 상세 화면의 내용: 이름, 레벨, Exp 바, HP, XP, 상태값(character_display_order가 1 이상인 스테이터스, 작은 수부터), 장비 슬롯, 스킬 슬롯, 설명문.</summary>
    public sealed record CharacterDetailView(
        string Name, string LevelText, GaugeView Exp, GaugeView Hp, GaugeView Xp, XpTag XpTag,
        IReadOnlyList<UnitStatusLine> Specs, IReadOnlyList<UnitStatusLine> Equipment, IReadOnlyList<UnitStatusLine> Skills, string Description);

    /// <summary>지금 보는 캐릭터 상세 화면의 내용. 캐릭터 상세 화면이 아니거나 보유하지 않은 캐릭터면 null.</summary>
    public CharacterDetailView? CharacterDetail
    {
        get
        {
            if (Screen != Screen.CharacterDetail || DetailCharacterCode is not { } code || !Characters.TryGetValue(code, out var state)) return null;
            var character = state with { CharacterCode = code };
            var equipment = Pack.EquipmentSlotsOf(code)
                .Select(slot => new UnitStatusLine(slot.Name, EquippedItemOf(code, slot.Code) is { } target ? NameOf(target) : "비어 있음")).ToList();
            var skills = Pack.SkillSlotsOf(code)
                .Select(slot => new UnitStatusLine(slot.Name, EquippedSkillName(character, slot.Code) ?? "비어 있음")).ToList();
            var specs = (Pack.Statuses ?? []).Where(s => s.CharacterDisplayOrder > 0 && state.Statuses.ContainsKey(s.Code))
                .OrderBy(s => s.CharacterDisplayOrder).ThenBy(s => s.Code)
                .Select(s => new UnitStatusLine(s.Name, Text(state.Statuses[s.Code]))).ToList();
            var xp = XpLine(character, CharacterDetailExpWidth);
            return new CharacterDetailView(
                CharacterName(code), $"Lv.{state.Level}",
                Gauge.Render(state.Exp, state.ExpMax, gray: false, CharacterDetailExpWidth) with { Color = GaugeColor.White },   // Exp 바는 색 규칙을 따르지 않고 늘 흰색
                Gauge.Render(state.Hp, state.MaxHp, gray: state.IsDead, CharacterDetailExpWidth),
                xp.Gauge, xp.Tag, specs,
                equipment, skills, Pack.FindCharacter(code)?.Description ?? "");
        }
    }

    /// <summary>스테이터스 확인 등 확인 화면의 돌아가기: 던전에서 열었으면 던전으로, 아니면 홈으로.</summary>
    Screen CheckBackScreen => CurrentDungeonCode is not null ? Screen.DungeonMove : Screen.Home;

    /// <summary>스테이터스 확인 화면: 보는 파티의 슬롯을 파티 설정처럼 그린다. 캐릭터가 있는 슬롯(1~9)을 고르면 캐릭터 상세 화면으로 간다.</summary>
    IReadOnlyList<ScreenEntry> StatusCheckEntries() =>
    [
        .. PartySlotViews.Select(v => new ScreenEntry((v.Slot + 1).ToString(CultureInfo.InvariantCulture), v.Name, Screen.CharacterDetail,
            EntryAction.OpenDetail, $"S:{v.Slot.ToString(CultureInfo.InvariantCulture)}", Enabled: v.CharacterCode is not null)),
        new ScreenEntry(BackEntryCode, "돌아가기", CheckBackScreen),
    ];

    /// <summary>동료 확인 화면: 가진 캐릭터마다 [OrderNumber]로 고르면 캐릭터 상세 화면으로 간다.</summary>
    IReadOnlyList<ScreenEntry> CompanionCheckEntries() =>
    [
        .. OwnedCharacters.Select(c => new ScreenEntry(c.OrderNumber.ToString(CultureInfo.InvariantCulture), Pack.FindCharacter(c.CharacterCode)?.Name ?? Text(c.CharacterCode),
            Screen.CharacterDetail, EntryAction.OpenDetail, $"C:{c.CharacterCode.ToString(CultureInfo.InvariantCulture)}")),
        new ScreenEntry(BackEntryCode, "돌아가기", CheckBackScreen),
    ];

    /// <summary>장비 확인 화면: 가진 장비마다 [OrderNumber]로 고르면 장비 상세 화면으로 간다.</summary>
    IReadOnlyList<ScreenEntry> EquipmentCheckEntries() =>
    [
        .. OwnedUniqueItems.Select(i => new ScreenEntry(i.OrderNumber.ToString(CultureInfo.InvariantCulture), Pack.FindUniqueItem(i.ItemCode)?.ItemName ?? Text(i.ItemCode),
            Screen.EquipmentDetail, EntryAction.OpenDetail, $"E:{i.InstanceId}")),
        new ScreenEntry(BackEntryCode, "돌아가기", CheckBackScreen),
    ];

    /// <summary>아이템 확인 화면: 가진 커먼 아이템마다 [아이템 코드]로 고르면 아이템 상세 화면으로 간다.</summary>
    IReadOnlyList<ScreenEntry> ItemCheckEntries() =>
    [
        .. (Pack.ItemInventory ?? []).Where(i => InventoryItems.GetValueOrDefault(i.Code).Amount > 0)
            .Select(i => new ScreenEntry(i.Code.ToString(CultureInfo.InvariantCulture), i.Name, Screen.ItemDetail, EntryAction.OpenDetail, $"I:{i.Code.ToString(CultureInfo.InvariantCulture)}")),
        new ScreenEntry(BackEntryCode, "돌아가기", CheckBackScreen),
    ];

    /// <summary>
    /// 상세 화면의 돌아가기는 상세 화면을 연 화면이다. 캐릭터 상세는 그 앞에 장비 슬롯 줄(1번부터)과 스킬 슬롯 줄(101번부터) 항목이 있다:
    /// 장비 줄은 낀 것이 유니크 아이템이면 장비 상세, 커먼 아이템이면 아이템 상세, 보유 캐릭터면 캐릭터 상세로, 스킬 줄은 스킬 상세로 간다(빈 줄은 고를 수 없다).
    /// </summary>
    IReadOnlyList<ScreenEntry> DetailEntries()
    {
        var back = new ScreenEntry(BackEntryCode, "돌아가기", detailBack.Count > 0 ? detailBack.Peek().Screen : Screen.Home, EntryAction.CloseDetail);
        if (Screen != Screen.CharacterDetail || DetailCharacterCode is not { } code || !Characters.TryGetValue(code, out var state)) return [back];

        var rows = new List<ScreenEntry>();
        var number = 1;
        foreach (var slot in Pack.EquipmentSlotsOf(code))
        {
            var target = EquippedItemOf(code, slot.Code);
            var (screen, arg) = target switch
            {
                { Kind: EquipKind.UniqueItem } t when UniqueItemInstances.ContainsKey(t.Instance ?? "") => (Screen.EquipmentDetail, $"E:{t.Instance}"),
                { Kind: EquipKind.CommonItem } t => (Screen.ItemDetail, $"I:{t.Code.ToString(CultureInfo.InvariantCulture)}"),
                { Kind: EquipKind.Character } t when Characters.ContainsKey(t.Code) => (Screen.CharacterDetail, $"C:{t.Code.ToString(CultureInfo.InvariantCulture)}"),
                _ => (Screen.CharacterDetail, (string?)null),
            };
            rows.Add(new ScreenEntry((number++).ToString(CultureInfo.InvariantCulture), slot.Name, screen, EntryAction.OpenDetail, arg, Enabled: arg is not null));
        }
        number = 101;
        foreach (var slot in Pack.SkillSlotsOf(code))
        {
            var skill = state.SkillSlots.TryGetValue(slot.Code, out var index) && index >= 0 && index < state.SkillPool.Count ? state.SkillPool[(int)index].SkillCode : (long?)null;
            rows.Add(new ScreenEntry((number++).ToString(CultureInfo.InvariantCulture), slot.Name, Screen.SkillDetail, EntryAction.OpenDetail,
                skill is { } s ? $"K:{s.ToString(CultureInfo.InvariantCulture)}" : null, Enabled: skill is not null));
        }
        return [.. rows, back];
    }

    /// <summary>상세 화면을 연다: Arg는 "C:캐릭터 코드", "S:파티 슬롯 번호", "E:장비 인스턴스 ID", "I:아이템 코드", "K:스킬 코드". 지금 화면(과 그 화면이 보던 것)이 돌아갈 곳이 된다.</summary>
    void OpenDetail(string arg, Screen target)
    {
        var value = arg[2..];
        var from = new DetailFrame(Screen, DetailCharacterCode, DetailEquipmentId, DetailItemCode, DetailSkillCode);
        long? character = null, item = null, skill = null;
        string? equipment = null;
        switch (arg[0])
        {
            case 'C': character = long.Parse(value, CultureInfo.InvariantCulture); break;
            case 'S': character = Party[int.Parse(value, CultureInfo.InvariantCulture)]; break;
            case 'E': equipment = value; break;
            case 'I': item = long.Parse(value, CultureInfo.InvariantCulture); break;
            case 'K': skill = long.Parse(value, CultureInfo.InvariantCulture); break;
        }
        if (character is null && equipment is null && item is null && skill is null) return;      // 빈 슬롯 등: 열 수 없다
        if (!IsDetailScreen(Screen)) detailBack.Clear();                                             // 목록 화면에서 처음 여는 것이면 새로 쌓는다
        detailBack.Push(from);
        (DetailCharacterCode, DetailEquipmentId, DetailItemCode, DetailSkillCode) = (character, equipment, item, skill);
        Screen = target;
    }

    static bool IsDetailScreen(Screen screen) =>
        screen is Screen.CharacterDetail or Screen.EquipmentDetail or Screen.ItemDetail or Screen.SkillDetail;

    /// <summary>상세 화면의 돌아가기: 열기 전의 화면으로, 그 화면이 상세 화면이었으면 보던 것도 되살린다.</summary>
    void CloseDetail()
    {
        if (detailBack.Count == 0) { Screen = Screen.Home; return; }
        var f = detailBack.Pop();
        (DetailCharacterCode, DetailEquipmentId, DetailItemCode, DetailSkillCode) = (f.Character, f.Equipment, f.Item, f.Skill);
        Screen = f.Screen;
    }
}
