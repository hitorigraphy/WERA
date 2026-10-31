using System.Globalization;

namespace Wera.Core;

// 전투 화면의 슬롯 번호와 캐릭터 상태 확인(상세) 페이지.
public sealed partial class GameSession
{
    /// <summary>전투 화면 슬롯 번호의 시작: 적 슬롯은 슬롯 번호 + 1([1]~[9]), 아군 슬롯은 슬롯 번호 + 11([11]~[19]).</summary>
    public const int BattleEnemyNumberBase = 1, BattleAllyNumberBase = 11;

    /// <summary>전투 화면 슬롯 하나의 번호(코드). 이 번호나 슬롯의 이미지를 누르면 그 캐릭터의 상태 확인 페이지로 간다.</summary>
    public static string BattleSlotNumber(bool isEnemy, int slot) =>
        (slot + (isEnemy ? BattleEnemyNumberBase : BattleAllyNumberBase)).ToString(CultureInfo.InvariantCulture);

    /// <summary>상태 확인 페이지가 보여 주는 유닛: 적 여부와 슬롯 번호.</summary>
    bool statusIsEnemy;
    int statusSlot;

    /// <summary>상태 확인 페이지의 한 줄: 이름과 값.</summary>
    public sealed record UnitStatusLine(string Label, string Value);

    /// <summary>상태 확인 페이지의 내용: 이름과 레벨, 스테이터스, 장비 슬롯, 스킬 슬롯.</summary>
    public sealed record UnitStatusView(string Name, string LevelText, GaugeView Hp, GaugeView Xp,
        IReadOnlyList<UnitStatusLine> Statuses, IReadOnlyList<UnitStatusLine> Equipment, IReadOnlyList<UnitStatusLine> Skills);

    /// <summary>지금 보는 상태 확인 페이지의 내용. 유닛을 찾을 수 없으면 null.</summary>
    public UnitStatusView? UnitStatus
    {
        get
        {
            var unit = statusIsEnemy ? enemyUnits[statusSlot] : PartyInstance.Units[statusSlot];
            var store = CreateUnitStore();
            if (unit is null || store.CharacterOf(unit) is not { } character) return null;

            var statuses = character.Statuses.OrderBy(s => s.Key)
                .Select(s => new UnitStatusLine(Pack.FindStatus(s.Key)?.Name ?? Text(s.Key), Text(s.Value))).ToList();
            var equipment = Pack.EquipmentSlotsOf(unit.CharacterCode)
                .Select(slot => new UnitStatusLine(slot.Name,
                    unit.Equipment.TryGetValue(slot.Code, out var equip) ? BattleEquipName(store, equip) : "비어 있음")).ToList();
            var skills = Pack.SkillSlotTagsOf(unit.CharacterCode).Select(r => r.SkillSlotCode).Distinct().Order()
                .Select(Pack.FindSkillSlot).OfType<SkillSlotEntry>()
                .Select(slot => new UnitStatusLine(slot.Name, EquippedSkillName(character, slot.Code) ?? "비어 있음")).ToList();
            return new UnitStatusView(CharacterName(unit.CharacterCode), $"Lv.{character.Level}",
                Gauge.Render(character.Hp, character.MaxHp, gray: character.IsDead),
                Gauge.Render(character.Xp, character.XpMax, gray: character.IsDead), statuses, equipment, skills);
        }
    }

    /// <summary>장비 슬롯에 낀 것의 이름(유니크 아이템은 아군의 보유품과 적의 임시 장비 모두). 마스터에 없으면 코드 그대로.</summary>
    string BattleEquipName(UnitStore store, EquipRef equip) => (equip.Kind switch
    {
        EquipKind.UniqueItem => store.FindItem(equip.Instance ?? "") is { } item ? Pack.FindUniqueItem(item.ItemCode)?.ItemName : null,
        EquipKind.CommonItem => Pack.FindItem(equip.Code)?.Name,
        EquipKind.Character => Pack.FindCharacter(equip.Code)?.Name,
        _ => null,
    }) ?? equip.Key;

    /// <summary>
    /// 작전 화면 슬롯의 번호 항목: 기본 커맨드에서는 상태 확인으로 가고, 행동 후보(스킬 목록)에서는 모두 고를 수 없으며,
    /// 대상 후보에서는 고를 수 있는 슬롯(<see cref="BattleTargets"/>)만 대상 선택이다.
    /// </summary>
    IEnumerable<ScreenEntry> BattleSlotEntries()
    {
        switch (BattleCommands)
        {
            case BattleCommandMode.Basic:
                foreach (var slot in enemyInstances.Keys.Order())
                    yield return new ScreenEntry(BattleSlotNumber(true, slot), "", Screen.UnitStatus, EntryAction.BattleViewUnit, $"E{slot}");
                foreach (var slot in BattleUnitSlots())
                    yield return new ScreenEntry(BattleSlotNumber(false, slot), "", Screen.UnitStatus, EntryAction.BattleViewUnit, $"A{slot}");
                break;
            case BattleCommandMode.MoveCandidates:
                foreach (var slot in BattleMoveSlots())
                    yield return new ScreenEntry(BattleSlotNumber(false, slot), "", Screen.Battle, EntryAction.BattleMoveTo, slot.ToString(CultureInfo.InvariantCulture));
                break;
            case BattleCommandMode.TargetCandidates:
                foreach (var target in BattleTargets())
                    yield return new ScreenEntry(BattleSlotNumber(target.IsEnemy, target.Slot), "", Screen.Battle, EntryAction.BattleChooseTarget,
                        $"{(target.IsEnemy ? 'E' : 'A')}{target.Slot}");
                break;
        }
    }

    void ViewBattleUnit(string arg)
    {
        statusIsEnemy = arg[0] == 'E';
        statusSlot = int.Parse(arg.AsSpan(1), CultureInfo.InvariantCulture);
    }

    /// <summary>[107] 도망: 전투를 끝내고(도망은 늘 성공한다) 전투에 참가한 아군 유닛마다 도망 후 서킷을 돌린다. 바뀐 값은 남는다.</summary>
    void RunFromBattle()
    {
        if (BattleOutcome != BattleOutcomeKind.None) return;
        var store = CreateUnitStore();
        foreach (var slot in BattleUnitSlots())
            new AfterBattleRunCircuit(Pack, store, PartyInstance.Units[slot]!).Execute();
        battleLog.Clear();
        BattleOutcome = BattleOutcomeKind.Run;
        BattleCommands = BattleCommandMode.Basic;
    }
}
