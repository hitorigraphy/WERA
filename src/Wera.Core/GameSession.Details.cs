using System.Globalization;

namespace Wera.Core;

public sealed partial class GameSession
{
    /// <summary>상세 화면(캐릭터, 장비, 아이템)에서 돌아갈 화면: 상세 화면을 연 화면이다.</summary>
    Screen detailReturnScreen = Screen.Home;

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
        _ => null,
    };

    /// <summary>캐릭터 상세 화면의 Exp 바 칸 수("Exp : " 뒤, 오른쪽 여백을 30px 남기고 끝까지 가는 길이).</summary>
    public const int CharacterDetailExpWidth = 61;

    /// <summary>캐릭터 상세 화면의 내용: 이름, 레벨, Exp 바, HP, XP, (상태값은 아직 정해지지 않음), 장비 슬롯, 스킬 슬롯, 설명문.</summary>
    public sealed record CharacterDetailView(
        string Name, string LevelText, GaugeView Exp, GaugeView Hp, GaugeView Xp,
        IReadOnlyList<UnitStatusLine> Equipment, IReadOnlyList<UnitStatusLine> Skills, string Description);

    /// <summary>지금 보는 캐릭터 상세 화면의 내용. 캐릭터 상세 화면이 아니거나 보유하지 않은 캐릭터면 null.</summary>
    public CharacterDetailView? CharacterDetail
    {
        get
        {
            if (Screen != Screen.CharacterDetail || DetailCharacterCode is not { } code || !Characters.TryGetValue(code, out var state)) return null;
            var character = state with { CharacterCode = code };
            var equipment = Pack.EquipmentSlotsOf(code)
                .Select(slot => new UnitStatusLine(slot.Name, EquippedItemOf(code, slot.Code) is { } target ? NameOf(target) : "비어 있음")).ToList();
            var skills = Pack.SkillSlotTagsOf(code).Select(r => r.SkillSlotCode).Distinct().Order()
                .Select(Pack.FindSkillSlot).OfType<SkillSlotEntry>()
                .Select(slot => new UnitStatusLine(slot.Name, EquippedSkillName(character, slot.Code) ?? "비어 있음")).ToList();
            return new CharacterDetailView(
                CharacterName(code), $"Lv.{state.Level}",
                Gauge.Render(state.Xp, state.XpMax, gray: state.IsDead, CharacterDetailExpWidth),
                Gauge.Render(state.Hp, state.MaxHp, gray: state.IsDead),
                Gauge.Render(state.Xp, state.XpMax, gray: state.IsDead),
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

    /// <summary>상세 화면의 돌아가기는 상세 화면을 연 화면이다.</summary>
    IReadOnlyList<ScreenEntry> DetailEntries() => [new ScreenEntry(BackEntryCode, "돌아가기", detailReturnScreen)];

    /// <summary>상세 화면을 연다: Arg는 "C:캐릭터 코드", "S:파티 슬롯 번호", "E:장비 인스턴스 ID", "I:아이템 코드". 지금 화면이 돌아갈 화면이 된다.</summary>
    void OpenDetail(string arg, Screen target)
    {
        var value = arg[2..];
        DetailCharacterCode = null;
        DetailEquipmentId = null;
        DetailItemCode = null;
        switch (arg[0])
        {
            case 'C': DetailCharacterCode = long.Parse(value, CultureInfo.InvariantCulture); break;
            case 'S': DetailCharacterCode = Party[int.Parse(value, CultureInfo.InvariantCulture)]; break;
            case 'E': DetailEquipmentId = value; break;
            case 'I': DetailItemCode = long.Parse(value, CultureInfo.InvariantCulture); break;
        }
        if (DetailCharacterCode is null && DetailEquipmentId is null && DetailItemCode is null) return;      // 빈 슬롯 등: 열 수 없다
        detailReturnScreen = Screen;
        Screen = target;
    }
}
