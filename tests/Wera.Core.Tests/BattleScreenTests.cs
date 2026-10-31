using Wera.Core;

namespace Wera.Core.Tests;

// [110] 전투(임시): 정해진 전투(5001)를 시작하고 작전 화면(슬롯 네 줄과 가운데 영역)을 그린다.
public class BattleScreenTests
{
    static GamePack SamplePack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(zip);
    }

    // 예시 게임팩의 전투 5001: 적 파티 1(전사 둘, 마법사 둘; 가중치 1)과 적 파티 2(전사 하나; 가중치 3).
    static GameSession Started(long roll = 0, bool twoUnits = false)
    {
        var s = GameSession.NewGame(SamplePack());
        s.NextRandom = _ => roll;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");     // 파티-A의 첫 칸에 당신(리더 태그 있음) → 리더
        if (twoUnits)
        {
            s.AddCharacter(7001);
            s.Party[1] = 7001;                                                       // 둘째 칸에 용사(리더가 아닌 유닛)
        }
        Assert.NotNull(s.Activate("110"));
        return s;
    }

    static string Titles(GameSession s) =>
        string.Join(",", s.BattleRows.Select(r => r.Kind == BattleRowKind.Border ? "|" : (r.IsEnemy ? "적 " : "아군 ") + r.Title));

    static IEnumerable<string> BattleCodes(GameSession s) => s.Entries.Where(e => e.Code != "999").Select(e => e.Code);

    static ScreenEntry Entry(GameSession s, string code) => s.Entries.Single(e => e.Code == code);

    static IReadOnlyList<PartySlotView> AllyFront(GameSession s) =>
        s.BattleRows.First(r => !r.IsEnemy && r.Kind == BattleRowKind.Slots).Slots;

    const string EnemyHeavy = "적 후위,적 중위,적 전위,|,아군 전위";
    const string AllyHeavy = "적 전위,|,아군 전위,아군 중위,아군 후위";

    [Fact]
    public void Entering_forces_enemy_one_ally_three_because_a_turn_has_begun()
    {
        var s = Started();
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Equal(GameSession.TemporaryBattleCode, s.CurrentBattleCode);
        Assert.Equal(GameSession.BattleTopAllyHeavy, s.BattleTop);
        Assert.Equal(AllyHeavy, Titles(s));
    }

    [Fact]
    public void The_enemy_rows_show_the_preset_party_and_the_ally_row_shows_my_party()
    {
        var s = Started(roll: 0);                                  // 가중치 1짜리 적 파티 1
        Assert.Equal(1, s.CurrentEnemyPartyCode);
        s.Activate("1000");                                        // 적 3 / 아군 1 구성으로
        var rows = s.BattleRows;
        Assert.Equal(["빈 슬롯", "마법사", "빈 슬롯"], rows[0].Slots.Select(v => v.Name));       // 적 후위: 가운데
        Assert.Equal(["빈 슬롯", "마법사", "빈 슬롯"], rows[1].Slots.Select(v => v.Name));       // 적 중위: 가운데
        Assert.Equal(["전사", "빈 슬롯", "전사"], rows[2].Slots.Select(v => v.Name));           // 적 전위: 양쪽
        Assert.Equal(["당신", "빈 슬롯", "빈 슬롯"], rows[4].Slots.Select(v => v.Name));         // 아군 전위
        Assert.True(rows[2].Slots[0].Hp.Text.Length > 0);
    }

    [Fact]
    public void The_border_shows_the_enemy_party_name_the_battle_name_and_my_party_name()
    {
        var s = Started();
        var border = s.BattleRows.Single(r => r.Kind == BattleRowKind.Border);
        Assert.Equal(("전사 둘과 마법사", "테스트 전투", "파티-A"), (border.LeftText, border.CenterText, border.RightText));
        var renamed = GameSession.NewGame(SamplePack());
        renamed.Activate("105"); renamed.Activate("1"); renamed.Activate("1"); renamed.Activate("999");
        renamed.Activate("105"); renamed.Activate("0"); renamed.Activate("정예");
        renamed.Activate("999"); renamed.Activate("110");
        Assert.Equal("정예", renamed.BattleRows.Single(r => r.Kind == BattleRowKind.Border).RightText);
    }

    [Fact]
    public void The_enemy_party_is_drawn_by_weight()
    {
        Assert.Equal(1, Started(roll: 0).CurrentEnemyPartyCode);   // 가중치 1 + 3 = 4: 0은 첫 파티
        Assert.Equal(2, Started(roll: 1).CurrentEnemyPartyCode);   // 1~3은 둘째 파티
        Assert.Equal(2, Started(roll: 3).CurrentEnemyPartyCode);
        Assert.Equal("전사 하나", Started(roll: 2).BattleRows.Single(r => r.Kind == BattleRowKind.Border).LeftText);
    }

    [Fact]
    public void The_buttons_jump_between_enemy_three_ally_one_and_enemy_one_ally_three()
    {
        var s = Started();
        Assert.Equal(["101", "102", "103", "104", "105", "106", "1000", "1001"], BattleCodes(s));
        Assert.Equal([true, false], s.Entries.Where(e => e.Action == EntryAction.BattleScroll).Select(e => e.Enabled));   // 지금 적 1 / 아군 3
        Assert.Equal(["▲", "▼"], s.Entries.Where(e => e.Action == EntryAction.BattleScroll).Select(e => e.Label));

        s.Activate("1000");                                        // 가운데 구성(적 중위부터)을 건너뛰고 바로 적 3 / 아군 1
        Assert.Equal(GameSession.BattleTopEnemyHeavy, s.BattleTop);
        Assert.Equal(EnemyHeavy, Titles(s));
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Equal([false, true], s.Entries.Where(e => e.Action == EntryAction.BattleScroll).Select(e => e.Enabled));
        s.Activate("1000");                                        // 이미 그 구성이면 반응이 없다
        Assert.Equal(0, s.BattleTop);

        s.Activate("1001");
        Assert.Equal(AllyHeavy, Titles(s));
    }

    [Fact]
    public void There_is_no_back_entry_during_a_battle()
    {
        var s = Started();
        Assert.DoesNotContain(s.Entries, e => e.Code == "999");
        Assert.Null(s.Activate("999"));
        Assert.Equal(Screen.Battle, s.Screen);
    }

    [Fact]
    public void Each_enemy_unit_gets_a_temporary_character_instance_when_the_battle_starts()
    {
        var s = Started(roll: 0);                                  // 적 파티 1: 전사 둘, 마법사 둘
        Assert.Equal(4, s.EnemyInstances.Count);
        Assert.All(s.EnemyInstances, kv => Assert.Equal($"enemy-{kv.Key}", kv.Value.InstanceId));
        Assert.All(s.EnemyInstances.Values, c => Assert.True(c.Hp > 0));
        Assert.NotSame(s.EnemyInstances[0], s.EnemyInstances.Values.Skip(1).First());
        Assert.Empty(Started(roll: 0).ToSaveJson().Split("enemy-").Skip(1));       // 세이브에 들어가지 않는다
    }

    [Fact]
    public void Enemy_units_are_party_units_with_temporary_equipment_and_the_unit_store_finds_them()
    {
        var s = Started(roll: 0);                                  // 적 파티 1: 전사(7003) 둘, 마법사(7002) 둘
        Assert.Equal(4, s.EnemyUnits.Count(u => u is not null));
        var store = s.CreateUnitStore();
        var warriors = s.EnemyUnits.Select((u, slot) => (Unit: u, Slot: slot)).Where(x => x.Unit?.CharacterCode == 7003).ToList();
        Assert.Equal(2, warriors.Count);
        foreach (var (unit, slot) in warriors)
        {
            Assert.Equal(s.EnemyInstances[slot], store.CharacterOf(unit!));       // 같은 캐릭터여도 슬롯의 인스턴스를 찾는다
            Assert.Equal([1L, 2L], unit!.Equipment.Keys.Order());                 // 철검(무기), 판금갑옷(방어구)
            Assert.Equal(8001, store.ItemOf(unit, 1)!.ItemCode);
            Assert.Equal($"enemy-{slot}-1", store.ItemOf(unit, 1)!.InstanceId);
        }
        Assert.NotSame(store.ItemOf(warriors[0].Unit!, 1), store.ItemOf(warriors[1].Unit!, 1));
        Assert.Empty(s.ToSaveJson().Split("enemy-").Skip(1));                    // 적 장비도 세이브에 들어가지 않는다
        Assert.Empty(Started(roll: 0).UniqueItemInstances.Keys.Where(k => k.StartsWith("enemy-")));
    }

    [Fact]
    public void A_pack_without_the_battle_starts_nothing_but_the_screen_still_opens()
    {
        var s = GameSession.NewGame(GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"))));
        s.Party[0] = 1;
        s.PartyInstance.Units[0] = new PartyUnit(1, [], IsLeader: true);
        s.Activate("110");
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Null(s.CurrentBattleCode);
        Assert.Null(s.CurrentEnemyPartyCode);
        Assert.Equal(AllyHeavy, Titles(s));
    }

    // ---- 보는 캐릭터와 행동 설정 ----

    [Fact]
    public void The_first_unit_is_the_character_being_set_and_its_name_is_selected_even_for_the_leader()
    {
        var s = Started(twoUnits: true);
        Assert.Equal(0, s.BattleCurrentSlot);
        var ally = AllyFront(s);
        Assert.Equal([true, false, false], ally.Select(v => v.IsSelected));
        Assert.True(ally[0].IsLeader);                             // 리더이지만 선택되어 파란색이다(화면은 selected가 leader를 덮어쓴다)
        Assert.False(ally[1].IsLeader);
    }

    [Fact]
    public void Previous_and_next_character_move_through_the_units_in_slot_order_and_wrap_and_force_the_view()
    {
        var s = Started(twoUnits: true);
        s.Activate("1000");                                        // 자유롭게 적 3 / 아군 1로 바꿔 두었다
        s.Activate("101");                                         // 다음 캐릭터
        Assert.Equal(1, s.BattleCurrentSlot);
        Assert.Equal(GameSession.BattleTopAllyHeavy, s.BattleTop); // 강제로 적 1 / 아군 3
        var front = AllyFront(s);
        Assert.Equal([false, true, false], front.Select(v => v.IsSelected));
        Assert.True(front[0].IsLeader);                            // 리더는 선택되지 않았을 때 연두색

        s.Activate("101");                                         // 끝에서 처음으로
        Assert.Equal(0, s.BattleCurrentSlot);
        s.Activate("1000");
        s.Activate("104");                                         // 이전 캐릭터: 처음에서 끝으로
        Assert.Equal(1, s.BattleCurrentSlot);
        Assert.Equal(GameSession.BattleTopAllyHeavy, s.BattleTop);
    }

    [Fact]
    public void Action_settings_swap_the_commands_for_skill_candidates_and_show_enemy_three_ally_one()
    {
        var s = Started(twoUnits: true);
        s.Activate("102");
        Assert.Equal(GameSession.BattleCommandMode.ActionCandidates, s.BattleCommands);
        Assert.Equal(GameSession.BattleTopEnemyHeavy, s.BattleTop);
        Assert.Equal(["101", "102", "103", "104", "106", "1000", "1001"], BattleCodes(s));
        Assert.Equal(["스킬1", "스킬2", "스킬3", "스킬4", "취소"],
            s.Entries.Where(e => e.Action is EntryAction.BattleChooseSkill or EntryAction.BattleCancelActions).Select(e => e.Label));

        s.Activate("1001");                                        // 이후 ▲▼는 행동 후보에서도 자유롭다
        Assert.Equal(GameSession.BattleTopAllyHeavy, s.BattleTop);
        Assert.Equal(GameSession.BattleCommandMode.ActionCandidates, s.BattleCommands);
        s.Activate("1000");
        Assert.Equal(GameSession.BattleTopEnemyHeavy, s.BattleTop);
    }

    [Fact]
    public void Choosing_a_skill_then_a_target_sets_the_action_and_moves_to_the_next_character_and_cancel_sets_nothing()
    {
        var s = Started(twoUnits: true);
        s.Activate("102"); s.Activate("106");                      // 취소
        Assert.Equal(GameSession.BattleCommandMode.Basic, s.BattleCommands);
        Assert.Null(s.BattleActionOf(0));

        s.Activate("102"); s.Activate("103");                      // 스킬3 → 대상 후보
        Assert.Equal(GameSession.BattleCommandMode.TargetCandidates, s.BattleCommands);
        Assert.Null(s.BattleActionOf(0));                          // 대상을 고르기 전에는 행동이 아니다
        Assert.Equal(["1", "3", "5", "8", "106"], s.Entries.Where(e => e.Action is EntryAction.BattleChooseTarget or EntryAction.BattleCancelActions).Select(e => e.Code));
        s.Activate("106");                                         // 취소
        Assert.Equal(GameSession.BattleCommandMode.Basic, s.BattleCommands);
        Assert.Null(s.BattleActionOf(0));

        s.Activate("102"); s.Activate("103"); s.Activate("3");     // 스킬3, 적 슬롯 2(코드 3)
        Assert.Equal((3, 2), (s.BattleActionOf(0)!.Value, s.BattleTargetOf(0)!.Value));
        Assert.Equal(GameSession.BattleCommandMode.Basic, s.BattleCommands);
        Assert.Equal(1, s.BattleCurrentSlot);                      // 다음 캐릭터로 넘어간다
    }

    [Fact]
    public void Defeated_enemies_cannot_be_targeted()
    {
        var s = Started();
        s.EnemyInstances[2].Statuses[GamePack.HpStatusCode] = 0;      // 적 슬롯 2를 쓰러뜨린다
        s.Activate("102"); s.Activate("101");
        Assert.Equal(["1", "5", "8"], s.Entries.Where(e => e.Action == EntryAction.BattleChooseTarget).Select(e => e.Code));
    }

    [Fact]
    public void Execute_is_gray_until_every_unit_has_an_action()
    {
        var s = Started(twoUnits: true);
        Assert.Equal(("실행", false), (Entry(s, "106").Label, Entry(s, "106").Enabled));
        Assert.False(s.BattleAllActionsSet);

        s.Activate("102"); s.Activate("101"); s.Activate("1");     // 첫 유닛: 스킬1 → 다음 캐릭터로
        Assert.False(Entry(s, "106").Enabled);                     // 둘째 유닛이 아직
        s.Activate("102"); s.Activate("104"); s.Activate("1");     // 둘째 유닛: 스킬4
        Assert.True(s.BattleAllActionsSet);
        Assert.True(Entry(s, "106").Enabled);
        Assert.Equal((1, 4), (s.BattleActionOf(0)!.Value, s.BattleActionOf(1)!.Value));

        s.Activate("106");                                         // 아직 동작은 없다
        Assert.Equal(Screen.Battle, s.Screen);
    }

    [Fact]
    public void A_chosen_action_replaces_the_name_with_the_skill_name()
    {
        var s = Started(twoUnits: true);
        Assert.Equal(["당신", "용사", "빈 슬롯"], AllyFront(s).Select(v => v.Name));
        s.Activate("102"); s.Activate("103"); s.Activate("1");     // 첫 유닛: 스킬3
        Assert.Equal(["스킬3", "용사", "빈 슬롯"], AllyFront(s).Select(v => v.Name));
    }

    [Fact]
    public void With_a_single_unit_previous_and_next_stay_on_it()
    {
        var s = Started();
        s.Activate("104");
        Assert.Equal(0, s.BattleCurrentSlot);
        s.Activate("101");
        Assert.Equal(0, s.BattleCurrentSlot);
        s.Activate("102"); s.Activate("102"); s.Activate("1");
        Assert.True(s.BattleAllActionsSet);                        // 유닛이 하나뿐이면 하나만 정하면 실행할 수 있다
    }

    // 모든 호출이 첫 후보를 고르게 한다(적의 스킬과 대상, 쓰러진 대상 바꾸기)
    static void FirstOnly(GameSession s) => s.NextRandom = _ => 0;

    [Fact]
    public void Execute_has_everyone_act_once_in_order_point_order_then_starts_the_next_turn()
    {
        var s = Started(twoUnits: true);
        FirstOnly(s);
        var enemyCount = s.EnemyInstances.Count;
        s.Activate("102"); s.Activate("101"); s.Activate("1");       // 첫 유닛: 스킬1 → 적 슬롯 0
        s.Activate("102"); s.Activate("102"); s.Activate("1");       // 둘째 유닛: 스킬2 → 적 슬롯 0
        s.Activate("106");

        Assert.Equal(s.BattleLog.Count, s.BattleLogLines.Count);
        Assert.All(s.BattleLogLines, l => Assert.Contains(" → ", l));
        Assert.Equal(GameSession.BattleOutcomeKind.None, s.BattleOutcome);
        Assert.Equal(2 + enemyCount, s.BattleLog.Count);             // 전원이 1회씩
        Assert.Equal(s.BattleLog.OrderByDescending(a => a.OrderPoint).Select(a => (a.IsEnemy, a.Slot)), s.BattleLog.Select(a => (a.IsEnemy, a.Slot)));
        Assert.True(s.EnemyInstances[0].Hp < s.EnemyInstances[0].MaxHp);   // 스킬 피해가 들어갔다
        Assert.Null(s.BattleActionOf(0));                            // 다음 턴: 행동은 지워지고 첫 유닛부터
        Assert.Equal(0, s.BattleCurrentSlot);
        Assert.False(Entry(s, "106").Enabled);
    }

    [Fact]
    public void Execute_is_a_win_when_every_enemy_falls_and_a_loss_when_every_ally_falls()
    {
        var win = Started(roll: 1);                                   // 적 파티 2: 전사 하나
        FirstOnly(win);
        var slot = win.EnemyInstances.Keys.Single();
        win.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 5;
        win.Activate("102"); win.Activate("102"); win.Activate((slot + 1).ToString());
        win.Activate("106");
        Assert.Equal(GameSession.BattleOutcomeKind.Win, win.BattleOutcome);
        Assert.DoesNotContain(win.Entries, e => e.Action != EntryAction.BattleScroll && e.Code != "999");   // 커맨드는 없다

        var lose = Started(roll: 1);
        FirstOnly(lose);
        slot = lose.EnemyInstances.Keys.Single();
        lose.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 100;
        lose.EnemyInstances[slot].Statuses[GamePack.MaxHpStatusCode] = 100;
        lose.Characters[1].Statuses[GamePack.HpStatusCode] = 1;
        lose.Activate("102"); lose.Activate("101"); lose.Activate((slot + 1).ToString());
        lose.Activate("106");
        Assert.Equal(GameSession.BattleOutcomeKind.Lose, lose.BattleOutcome);
    }
}
