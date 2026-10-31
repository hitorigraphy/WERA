using Wera.Core;

namespace Wera.Core.Tests;

// 전투: 이벤트 3002의 ADV 4002가 Battle 칸으로 전투(5001)를 시작하고 작전 화면(슬롯 네 줄과 가운데 영역)을 그린다.
public class BattleScreenTests
{
    static GamePack SamplePack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(zip);
    }

    /// <summary>예시 게임팩에 마스터 파일(경로, 내용)을 더한(같은 경로면 바꾼) 게임팩.</summary>
    /// <summary>전투 5001이 도망 가능(can_run)인 예시 팩(예시 팩의 전투 5001은 도망 불가다).</summary>
    static GamePack RunnablePack() => SamplePackWith(("master/battle_master_v1.tsv", "code\tname\tcan_run\n5001\t테스트 전투\ttrue\n"));

    static GamePack SamplePackWith(params (string Path, string Text)[] extra)
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        var source = new MemoryStream();
        using (var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra")))
        using (var from = new System.IO.Compression.ZipArchive(read))
        using (var to = new System.IO.Compression.ZipArchive(source, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var e in from.Entries.Where(e => extra.All(x => x.Path != e.FullName)))
            {
                using var input = e.Open();
                using var output = to.CreateEntry(e.FullName).Open();
                input.CopyTo(output);
            }
            foreach (var (path, text) in extra)
            {
                using var w = new StreamWriter(to.CreateEntry(path).Open());
                w.Write(text);
            }
        }
        source.Position = 0;
        return GamePack.Load(source);
    }

    // 예시 게임팩의 전투 5001: 적 파티 1(전사 둘, 마법사 둘; 가중치 1)과 적 파티 2(전사 하나; 가중치 3).
    static GameSession Started(long roll = 0, bool twoUnits = false, bool canRun = false)
    {
        var s = GameSession.NewGame(canRun ? RunnablePack() : SamplePack());
        s.NextRandom = _ => roll;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");     // 파티-A의 첫 칸에 당신(리더 태그 있음) → 리더
        if (twoUnits)
        {
            s.AddCharacter(7001);
            s.Party[1] = 7001;                                                       // 둘째 칸에 용사(리더가 아닌 유닛)
        }
        Assert.NotNull(EnterBattle(s));
        return s;
    }

    /// <summary>이벤트 3002(ADV 4002의 첫 칸이 Battle 5001)를 시작해 전투 화면으로 간다.</summary>
    static ScreenEntry? EnterBattle(GameSession s)
    {
        s.Activate("103");
        return s.Activate("3002");
    }

    static string Titles(GameSession s) =>
        string.Join(",", s.BattleRows.Select(r => r.Kind == BattleRowKind.Border ? "|" : (r.IsEnemy ? "적 " : "아군 ") + r.Title));

    static IEnumerable<string> BattleCodes(GameSession s) => s.Entries.Where(e => e.Code != "999" && e.Action != EntryAction.BattleViewUnit).Select(e => e.Code);

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
        Assert.Equal(5001, s.CurrentBattleCode);
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
        renamed.Activate("999"); EnterBattle(renamed);
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
        Assert.Equal(["101", "102", "103", "104", "105", "106", "107", "1000", "1001"], BattleCodes(s));
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
        Assert.Equal(["101", "102", "103", "104", "105", "106", "1000", "1001"], BattleCodes(s));
        Assert.Equal(["베기", "불꽃", "치유", "집중", "부활", "취소"],
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

        s.Activate("102"); s.Activate("101");                      // 스킬1 → 대상 후보
        Assert.Equal(GameSession.BattleCommandMode.TargetCandidates, s.BattleCommands);
        Assert.Null(s.BattleActionOf(0));                          // 대상을 고르기 전에는 행동이 아니다
        Assert.Equal(["101", "1", "3", "5", "8"], s.Entries.Where(e => e.Action is EntryAction.BattleChooseTarget or EntryAction.BattleCancelActions).Select(e => e.Code));
        Assert.Equal(["101", "1000", "1001"], BattleCodes(s).Where(c => c != "1" && c != "3" && c != "5" && c != "8"));   // 하단 커맨드는 [101] 취소뿐
        s.Activate("101");                                         // 취소
        Assert.Equal(GameSession.BattleCommandMode.Basic, s.BattleCommands);
        Assert.Null(s.BattleActionOf(0));

        s.Activate("102"); s.Activate("101"); s.Activate("3");     // 스킬1, 적 슬롯 2(코드 3)
        Assert.Equal((1001, 2), (s.BattleActionOf(0)!.Value, s.BattleTargetOf(0)!.Value));
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
        Assert.Equal(("실행", false), (Entry(s, "107").Label, Entry(s, "107").Enabled));
        Assert.False(s.BattleAllActionsSet);

        s.Activate("102"); s.Activate("101"); s.Activate("1");     // 첫 유닛: 스킬1 → 다음 캐릭터로
        Assert.False(Entry(s, "107").Enabled);                     // 둘째 유닛이 아직
        s.Activate("102"); s.Activate("104"); s.Activate("12");     // 둘째 유닛: 스킬4(자신)
        Assert.True(s.BattleAllActionsSet);
        Assert.True(Entry(s, "107").Enabled);
        Assert.Equal((1001, 1004), (s.BattleActionOf(0)!.Value, s.BattleActionOf(1)!.Value));

        s.Activate("107");                                         // 아직 동작은 없다
        Assert.Equal(Screen.Battle, s.Screen);
    }

    [Fact]
    public void A_chosen_action_replaces_the_name_with_the_skill_name()
    {
        var s = Started(twoUnits: true);
        Assert.Equal(["당신", "용사", "빈 슬롯"], AllyFront(s).Select(v => v.Name));
        s.Activate("102"); s.Activate("103"); s.Activate("11");     // 첫 유닛: 스킬3(같은 편)
        Assert.Equal(["치유", "용사", "빈 슬롯"], AllyFront(s).Select(v => v.Name));
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
        s.Activate("107");

        Assert.Equal(s.BattleLog.Count, s.BattleLogLines.Count);
        Assert.All(s.BattleLogLines.Where(l => !l.StartsWith("↳")), l => Assert.Contains(" → ", l));
        Assert.Contains("당신 → 적 ", s.BattleLogLines[0] + s.BattleLogLines[1] + s.BattleLogLines[2]);
        Assert.Contains(s.BattleLog, x => !x.IsEnemy && x.Slot == 0 && x.Damage == 10);   // 스킬1은 피해 10
        Assert.Contains(s.BattleLogLines, l => l.EndsWith("(피해 10)"));
        Assert.Equal(GameSession.BattleOutcomeKind.None, s.BattleOutcome);
        var acts = s.BattleLog.Where(a => !a.IsSub).ToList();         // 서브 타겟 기록은 행동 기록 뒤에 따로 이어진다
        Assert.Equal(2 + enemyCount, acts.Count);                    // 전원이 1회씩
        Assert.Equal(acts.OrderByDescending(a => a.OrderPoint).Select(a => (a.IsEnemy, a.Slot)), acts.Select(a => (a.IsEnemy, a.Slot)));
        Assert.True(s.EnemyInstances[0].Hp < s.EnemyInstances[0].MaxHp);   // 스킬 피해가 들어갔다
        Assert.Null(s.BattleActionOf(0));                            // 다음 턴: 행동은 지워지고 첫 유닛부터
        Assert.Equal(0, s.BattleCurrentSlot);
        Assert.False(Entry(s, "107").Enabled);
    }

    [Fact]
    public void Execute_is_a_win_when_every_enemy_falls_and_a_loss_when_every_ally_falls()
    {
        var win = Started(roll: 1);                                   // 적 파티 2: 전사 하나
        FirstOnly(win);
        var slot = win.EnemyInstances.Keys.Single();
        win.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 5;
        win.Activate("102"); win.Activate("102"); win.Activate((slot + 1).ToString());
        win.Activate("107");
        Assert.Equal(GameSession.BattleOutcomeKind.Win, win.BattleOutcome);
        Assert.DoesNotContain(win.Entries, e => e.Action != EntryAction.BattleScroll && e.Action != EntryAction.BattleViewUnit && e.Code != "999");   // 커맨드는 없다

        var lose = Started(roll: 1);
        FirstOnly(lose);
        slot = lose.EnemyInstances.Keys.Single();
        lose.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 100;
        lose.EnemyInstances[slot].Statuses[GamePack.MaxHpStatusCode] = 100;
        lose.Characters[1].Statuses[GamePack.HpStatusCode] = 1;
        lose.Activate("102"); lose.Activate("101"); lose.Activate((slot + 1).ToString());
        lose.Activate("107");
        Assert.Equal(GameSession.BattleOutcomeKind.Lose, lose.BattleOutcome);
    }

    [Fact]
    public void Basic_commands_are_next_action_item_previous_move_execute_and_run()
    {
        var s = Started(canRun: true);
        Assert.Equal(["다음 캐릭터", "행동 설정", "아이템", "이전 캐릭터", "이동", "도망", "실행"],
            s.Entries.Where(e => e.Action != EntryAction.BattleViewUnit && e.Action != EntryAction.BattleScroll && e.Code != "999").Select(e => e.Label));
        Assert.DoesNotContain(s.Entries, e => e.Label == "상태 확인");
        Assert.True(Entry(s, "106").Enabled);                       // 도망은 행동을 정하지 않아도 누를 수 있다
    }

    [Fact]
    public void Battle_slots_have_numbers_and_choosing_one_opens_that_characters_status_page()
    {
        var s = Started();
        Assert.Equal("11", GameSession.BattleSlotNumber(false, 0));    // 아군 슬롯 번호 + 11
        Assert.Equal("1", GameSession.BattleSlotNumber(true, 0));      // 적 슬롯 번호 + 1
        var enemySlot = s.EnemyInstances.Keys.First();
        s.Activate(GameSession.BattleSlotNumber(true, enemySlot));
        Assert.Equal(Screen.UnitStatus, s.Screen);
        var enemy = s.UnitStatus!;
        Assert.Equal(s.Pack.FindCharacter(s.EnemyInstances[enemySlot].CharacterCode)!.Name, enemy.Name);
        Assert.Contains(enemy.Statuses, l => l.Value == s.EnemyInstances[enemySlot].Hp.ToString());

        s.Activate("999");
        Assert.Equal(Screen.Battle, s.Screen);
        s.Activate("11");                                           // 아군 슬롯 0
        Assert.Equal("당신", s.UnitStatus!.Name);
        Assert.Equal("Lv.1", s.UnitStatus.LevelText);
    }

    [Fact]
    public void While_choosing_a_target_the_enemy_numbers_pick_the_target_instead_of_opening_the_status_page()
    {
        var s = Started();
        FirstOnly(s);
        var slot = s.EnemyInstances.Keys.First();
        s.Activate("102"); s.Activate("101");
        s.Activate(GameSession.BattleSlotNumber(true, slot));
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Equal(slot, s.BattleTargetOf(0));
    }

    [Fact]
    public void Run_ends_the_battle_and_removes_the_commands()
    {
        var s = Started(canRun: true);
        s.Activate("106");
        Assert.Equal(GameSession.BattleOutcomeKind.Run, s.BattleOutcome);
        Assert.DoesNotContain(s.Entries, e => e.Action != EntryAction.BattleScroll && e.Action != EntryAction.BattleViewUnit && e.Code != "999");
    }

    [Fact]
    public void Status_pages_open_only_from_the_basic_commands_and_slots_are_gray_while_choosing_a_skill()
    {
        var s = Started();
        Assert.Contains(s.Entries, e => e.Action == EntryAction.BattleViewUnit);
        Assert.All(s.BattleRows.Where(r => r.Kind == BattleRowKind.Slots).SelectMany(r => r.Slots), v => Assert.False(v.IsGray));

        s.Activate("102");                                         // 스킬 목록: 모든 슬롯이 회색이고 번호를 눌러도 상태 확인이 아니다
        Assert.DoesNotContain(s.Entries, e => e.Action is EntryAction.BattleViewUnit or EntryAction.BattleChooseTarget);
        Assert.All(s.BattleRows.Where(r => r.Kind == BattleRowKind.Slots).SelectMany(r => r.Slots), v => Assert.True(v.IsGray));
        s.Activate("11");
        Assert.Equal(Screen.Battle, s.Screen);
    }

    [Fact]
    public void Choosing_a_skill_makes_the_selectable_targets_white_and_the_rest_gray_and_the_slot_number_picks_the_target()
    {
        var s = Started(twoUnits: true);
        FirstOnly(s);
        s.EnemyInstances[2].Statuses[GamePack.HpStatusCode] = 0;      // 쓰러진 적은 고를 수 없다
        s.Activate("102"); s.Activate("101");
        var enemyRows = s.BattleRows.Where(r => r.IsEnemy && r.Kind == BattleRowKind.Slots).SelectMany(r => r.Slots).Where(v => !v.IsEmpty).ToList();
        Assert.Equal([(0, false), (2, true), (4, false), (7, false)], enemyRows.OrderBy(v => v.Slot).Select(v => (v.Slot, v.IsGray)));
        Assert.All(s.BattleRows.Where(r => !r.IsEnemy && r.Kind == BattleRowKind.Slots).SelectMany(r => r.Slots), v => Assert.True(v.IsGray));   // 아군은 대상이 아니다

        s.Activate("3");                                           // 쓰러진 적의 번호: 아무 일도 없다
        Assert.Equal(GameSession.BattleCommandMode.TargetCandidates, s.BattleCommands);
        s.Activate("11");                                          // 아군 번호도 대상이 아니다
        Assert.Equal(Screen.Battle, s.Screen);
        s.Activate("5");                                           // 적 슬롯 4
        Assert.Equal(4, s.BattleTargetOf(0));
        Assert.Equal(GameSession.BattleCommandMode.Basic, s.BattleCommands);
    }

    // 예시 게임팩: 스킬1(1001) 적, 스킬2(1002) 적, 스킬3(1003) 같은 편, 스킬4(1004) 자신
    static List<string> SelectableNumbers(GameSession s) =>
        s.BattleRows.Where(r => r.Kind == BattleRowKind.Slots).SelectMany(r => r.Slots.Select(v => (r.IsEnemy, v)))
            .Where(x => !x.v.IsEmpty && !x.v.IsGray).Select(x => GameSession.BattleSlotNumber(x.IsEnemy, x.v.Slot)).Order().ToList();

    [Fact]
    public void Selectable_targets_follow_the_skills_target_type()
    {
        var s = Started(twoUnits: true);
        s.Activate("102"); s.Activate("101");                      // 스킬1: 적
        Assert.Equal(["1", "3", "5", "8"], SelectableNumbers(s));
        s.Activate("101");                                         // 취소
        s.Activate("102"); s.Activate("103");                      // 스킬3: 같은 편(자신 포함)
        Assert.Equal(["11", "12"], SelectableNumbers(s));
        s.Activate("101");
        s.Activate("102"); s.Activate("104");                      // 스킬4: 자신(보는 캐릭터: 슬롯 0)
        Assert.Equal(["11"], SelectableNumbers(s));
    }

    [Fact]
    public void An_ally_target_heals_and_the_log_shows_the_ally_as_the_target()
    {
        var s = Started(twoUnits: true);
        FirstOnly(s);
        s.Characters[7001].Statuses[GamePack.HpStatusCode] = 10;
        s.Activate("102"); s.Activate("103"); s.Activate("12");   // 첫 유닛: 스킬3 → 용사(슬롯 1)
        Assert.Equal((1003, false, 1), (s.BattleActionOf(0)!.Value, s.BattleTargetIsEnemyOf(0)!.Value, s.BattleTargetOf(0)!.Value));
        s.Activate("102"); s.Activate("104"); s.Activate("12");   // 둘째 유닛: 스킬4 → 자신
        s.Activate("107");
        Assert.Contains(s.BattleLog, a => !a.IsEnemy && a.Slot == 0 && !a.TargetIsEnemy && a.TargetSlot == 1 && a.Damage == -10);
        Assert.Contains(s.BattleLogLines, l => l.StartsWith("당신 → 용사 : 치유") && l.EndsWith("(회복 10)"));
    }

    [Fact]
    public void Skill_candidates_are_the_equipped_skills_in_slot_order_and_the_chosen_skill_is_its_code()
    {
        var s = Started();
        s.Characters[1] = s.Characters[1] with
        {
            SkillPool = [new SkillInstance(1002), new SkillInstance(1001), new SkillInstance(1004)],
            SkillSlots = new Dictionary<long, long> { [3] = 2, [1] = 0 },           // 슬롯 1 불꽃, 슬롯 3 집중, 베기는 풀에만
        };
        s.Activate("102");
        Assert.Equal([("101", "불꽃"), ("102", "집중"), ("106", "취소")],
            s.Entries.Where(e => e.Action is EntryAction.BattleChooseSkill or EntryAction.BattleCancelActions).Select(e => (e.Code, e.Label)));
        s.Activate("101"); s.Activate("1");
        Assert.Equal(1002, s.BattleActionOf(0));
    }

    [Fact]
    public void A_unit_without_equipped_skills_needs_no_action_and_does_not_act()
    {
        var s = Started(twoUnits: true);
        FirstOnly(s);
        s.Characters[7001] = s.Characters[7001] with { SkillSlots = [] };           // 용사는 장착한 스킬이 없다
        s.Activate("102"); s.Activate("101"); s.Activate("1");                      // 당신만 행동을 정한다
        Assert.True(s.BattleAllActionsSet);
        s.Activate("107");
        Assert.DoesNotContain(s.BattleLog, a => !a.IsEnemy && a.Slot == 1);         // 용사는 행동하지 않았다
        Assert.Contains(s.BattleLog, a => !a.IsEnemy && a.Slot == 0);
    }

    [Fact]
    public void An_enemy_picks_among_its_equipped_skills_only_and_does_not_act_without_any()
    {
        var s = Started(roll: 1);                                                     // 적 파티 2: 전사 하나
        FirstOnly(s);
        var slot = s.EnemyInstances.Keys.Single();
        s.EnemyInstances[slot].SkillSlots.Clear();
        s.EnemyInstances[slot].SkillSlots[2] = 1;                                     // 전사는 불꽃(풀 번호 1)만 장착
        s.Activate("102"); s.Activate("101"); s.Activate((slot + 1).ToString());
        s.Activate("107");
        Assert.Equal([1002L], s.BattleLog.Where(a => a.IsEnemy).Select(a => a.SkillCode));

        s.EnemyInstances[slot].SkillSlots.Clear();                                    // 이제 장착한 스킬이 없다
        s.Activate("102"); s.Activate("101"); s.Activate((slot + 1).ToString());
        s.Activate("107");
        Assert.DoesNotContain(s.BattleLog, a => a.IsEnemy);
    }

    static IEnumerable<(string Code, string Label)> ItemCandidates(GameSession s) =>
        s.Entries.Where(e => e.Action is EntryAction.BattleChooseItem or EntryAction.BattleCancelActions).Select(e => (e.Code, e.Label));

    [Fact]
    public void The_item_command_lists_owned_items_with_a_skill_and_gray_out_the_slots()
    {
        var s = Started();
        s.Activate("103");
        Assert.Equal([("106", "취소")], ItemCandidates(s));                          // 쓸 수 있는 아이템이 없다
        Assert.Equal(GameSession.BattleCommandMode.ItemCandidates, s.BattleCommands);
        Assert.All(AllyFront(s), v => Assert.True(v.IsGray));

        s.Activate("106");
        s.AddInventory(3, 2);                                                       // 약초 2개(skill_code 있음)
        s.AddInventory(4, 5);                                                       // 철광석(skill_code 없음)
        s.AddUniqueItem(8001);                                                      // 철검(skill_code 있음)
        s.AddUniqueItem(8002);                                                      // 샘플 마스터에 있으면 skill_code가 없는 유니크는 후보가 아님
        s.Activate("103");
        Assert.Equal([("101", "약초 ×2"), ("102", "철검"), ("106", "취소")], ItemCandidates(s));
    }

    [Fact]
    public void Choosing_an_item_goes_to_target_selection_by_its_skill_and_uses_it_on_execute()
    {
        var s = Started();
        FirstOnly(s);
        s.AddInventory(3, 2);
        s.Activate("103"); s.Activate("101");                                       // 약초(치유 1003, 대상은 아군)
        Assert.Equal(GameSession.BattleCommandMode.TargetCandidates, s.BattleCommands);
        Assert.Equal(["11"], s.Entries.Where(e => e.Action == EntryAction.BattleChooseTarget).Select(e => e.Code));
        s.Activate("11");
        Assert.Equal(1003, s.BattleActionOf(0));
        Assert.Equal("C3", s.BattleItemOf(0));

        s.Activate("107");
        Assert.Equal(1, s.InventoryItems[3].Amount);                                // 약초는 하나 줄었다
        Assert.Contains(s.BattleLog, a => !a.IsEnemy && a.SkillCode == 1003);
    }

    [Fact]
    public void A_unique_item_is_not_used_up_and_a_chosen_skill_replaces_the_item_action()
    {
        var s = Started();
        FirstOnly(s);
        var id = s.AddUniqueItem(8001)!;
        s.Activate("103"); s.Activate("101");                                       // 철검(베기 1001, 대상은 적)
        s.Activate("1");
        Assert.Equal((1001L, "U" + id), (s.BattleActionOf(0)!.Value, s.BattleItemOf(0)));
        s.Activate("107");
        Assert.True(s.UniqueItemInstances.ContainsKey(id));

        s.AddInventory(3, 1);
        s = Started();
        s.AddInventory(3, 1);
        s.Activate("103"); s.Activate("101");                                       // 약초를 고른 뒤
        s.Activate("11");
        Assert.Equal("C3", s.BattleItemOf(0));
        s.Activate("102"); s.Activate("101"); s.Activate("1");                      // 다시 스킬로 행동을 바꾸면 아이템 행동은 사라진다
        Assert.Null(s.BattleItemOf(0));
    }

    [Fact]
    public void An_item_already_reserved_by_another_unit_is_not_offered_again_and_cancel_leaves_no_action()
    {
        var s = Started(twoUnits: true);
        s.AddInventory(3, 1);
        s.Activate("103"); s.Activate("101"); s.Activate("11");                     // 첫째 유닛이 약초 하나를 쓰기로 했다
        Assert.Equal(1, s.BattleCurrentSlot);
        s.Activate("103");
        Assert.Equal([("106", "취소")], ItemCandidates(s));                          // 둘째 유닛에게는 남은 약초가 없다

        s.Activate("106");
        Assert.Equal(GameSession.BattleCommandMode.Basic, s.BattleCommands);
        Assert.Null(s.BattleActionOf(1));
        Assert.Null(s.BattleItemOf(1));
    }

    [Fact]
    public void The_move_command_lets_any_other_ally_slot_including_empty_ones_be_picked_by_slot_number()
    {
        var s = Started(twoUnits: true);
        Assert.Equal("이동", Entry(s, "105").Label);
        s.Activate("105");
        Assert.Equal(GameSession.BattleCommandMode.MoveCandidates, s.BattleCommands);
        Assert.Equal(["101"], s.Entries.Where(e => e.Action == EntryAction.BattleCancelActions).Select(e => e.Code));
        Assert.Equal(["12", "13", "14", "15", "16", "17", "18", "19"], s.Entries.Where(e => e.Action == EntryAction.BattleMoveTo).Select(e => e.Code));
        Assert.Equal(GameSession.BattleTopAllyHeavy, s.BattleTop);

        s.Activate("101");                                                          // 취소
        Assert.Equal(GameSession.BattleCommandMode.Basic, s.BattleCommands);
        Assert.Null(s.BattleMoveOf(0));
    }

    [Fact]
    public void Moving_onto_an_occupied_slot_sets_the_other_unit_to_move_back_and_the_units_swap_on_execute()
    {
        var s = Started(twoUnits: true);
        FirstOnly(s);
        var (first, second) = (s.PartyInstance.Units[0]!.CharacterCode, s.PartyInstance.Units[1]!.CharacterCode);
        s.Activate("105"); s.Activate("12");                                        // 슬롯 0이 슬롯 1로 이동
        Assert.Equal((1, 0), (s.BattleMoveOf(0)!.Value, s.BattleMoveOf(1)!.Value));
        Assert.True(s.BattleAllActionsSet);                                         // 둘 다 행동이 정해졌다
        Assert.Equal(["이동", "이동"], AllyFront(s).Take(2).Select(v => v.Name));

        s.Activate("107");
        Assert.Equal((second, first), (s.PartyInstance.Units[0]!.CharacterCode, s.PartyInstance.Units[1]!.CharacterCode));
        Assert.DoesNotContain(s.BattleLog, a => !a.IsEnemy && !a.IsMove);          // 이동한 유닛은 스킬을 쓰지 않았다
        Assert.Equal(["당신 : 이동 → [12]", "용사 : 이동 → [11]"], s.BattleLogLines.Take(2));  // 이동이 먼저 로그에 남는다
    }

    [Fact]
    public void Changing_the_action_of_a_moved_unit_resets_the_linked_unit_and_the_next_character_goes_to_it()
    {
        var s = Started(twoUnits: true);
        s.Activate("105"); s.Activate("12");                                        // 서로 이동으로 묶였다
        Assert.Equal(1, s.BattleCurrentSlot);
        s.Activate("102"); s.Activate("101"); s.Activate("1");                      // 슬롯 1이 스킬로 행동을 바꾼다

        Assert.Null(s.BattleMoveOf(0));                                             // 묶였던 슬롯 0은 행동이 지워졌다
        Assert.Null(s.BattleMoveOf(1));
        Assert.NotNull(s.BattleActionOf(1));
        Assert.False(s.BattleAllActionsSet);
        Assert.False(Entry(s, "107").Enabled);
        Assert.Equal(0, s.BattleCurrentSlot);                                       // 다음 캐릭터는 행동이 지워진 쪽으로 돌아온다
    }

    [Fact]
    public void Moving_into_an_empty_slot_moves_only_that_unit_and_an_empty_target_slot_is_taken_once()
    {
        var s = Started(twoUnits: true);
        FirstOnly(s);
        s.Activate("105"); s.Activate("19");                                        // 슬롯 0이 빈 슬롯 8로 이동
        Assert.Null(s.BattleMoveOf(1));
        s.Activate("105");
        Assert.DoesNotContain(s.Entries, e => e.Action == EntryAction.BattleMoveTo && e.Code == "19");     // 이미 정해진 빈 칸은 고를 수 없다
        s.Activate("101");
        s.Activate("102"); s.Activate("101"); s.Activate("1");                      // 슬롯 1은 스킬을 쓴다
        Assert.True(s.BattleAllActionsSet);

        s.Activate("107");
        Assert.Null(s.PartyInstance.Units[0]);
        Assert.NotNull(s.PartyInstance.Units[5]);                                   // 이동한 유닛은 8번에 갔다가, 앞줄이 모두 비어 다음 턴 시작 전 진형 당기기로 5번이 되었다
        Assert.Equal("당신 : 이동 → [16]", s.BattleLogLines[0]);              // 로그도 당겨진 자리를 따라간다
    }

    [Fact]
    public void The_skills_sub_targets_are_filled_into_the_circuit_around_the_main_target()
    {
        var s = Started();                                                          // 적 파티 1: 슬롯 0, 2, 4, 7
        FirstOnly(s);
        var hp = s.EnemyInstances.ToDictionary(kv => kv.Key, kv => kv.Value.Hp);
        s.Activate("102"); s.Activate("102"); s.Activate("1");                      // 스킬 2(불꽃: 같은 줄에 번지고 서브 0번에게 피해 10)를 적 슬롯 0에
        s.Activate("107");
        Assert.Equal(20, hp[0] - s.EnemyInstances[0].Hp);                           // 메인 타겟
        Assert.Equal(10, hp[2] - s.EnemyInstances[2].Hp);                           // 같은 줄(전위)의 슬롯 2가 서브 타겟 0번
        Assert.Equal(0, hp[4] - s.EnemyInstances[4].Hp);                            // 다른 줄은 영향 없음
        Assert.Equal(0, hp[7] - s.EnemyInstances[7].Hp);
        var i = s.BattleLog.ToList().FindIndex(a => !a.IsEnemy && a.Slot == 0 && !a.IsSub && a.SkillCode == 1002);
        Assert.True(s.BattleLog[i + 1].IsSub);                                      // 메인 타겟의 기록 바로 뒤에 서브 타겟의 기록
        Assert.Equal((true, 2, 10L), (s.BattleLog[i + 1].TargetIsEnemy, s.BattleLog[i + 1].TargetSlot, s.BattleLog[i + 1].Damage));
        Assert.Equal("↳ 적 전사 (피해 10)", s.BattleLogLines[i + 1]);
    }

    [Fact]
    public void After_hit_circuits_run_for_the_main_target_and_every_sub_target()
    {
        // 피격 후: 맞은 쪽(그 서킷의 메인 타겟)의 hp를 1 더 깎는다. 예시 팩에 피격 후 이펙트 마스터를 더해 시험한다
        var pack = SamplePackWith((GamePack.AfterHitEffectMasterPath, "execute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n1\tSetCacheValue\t0\t-1\t0\n2\tFocusMainTargetCharacter\t0\t0\t0\n3\tCacheToFocusedCharacterModValue\t2\t0\t0\n"));
        var s = GameSession.NewGame(pack);
        s.NextRandom = _ => 0;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        Assert.NotNull(EnterBattle(s));
        var hp = s.EnemyInstances.ToDictionary(kv => kv.Key, kv => kv.Value.Hp);
        s.Activate("102"); s.Activate("102"); s.Activate("1");
        s.Activate("107");
        Assert.Equal(21, hp[0] - s.EnemyInstances[0].Hp);                           // 메인: 불꽃 20 + 피격 후 1
        Assert.Equal(11, hp[2] - s.EnemyInstances[2].Hp);                           // 서브 0번: 불꽃 10 + 피격 후 1
        Assert.Equal(0, hp[4] - s.EnemyInstances[4].Hp);
    }

    [Fact]
    public void Sub_targets_that_fall_end_the_battle_when_they_were_the_last_and_do_not_act_afterwards()
    {
        var s = Started();                                                          // 적 파티 1: 슬롯 0, 2, 4, 7
        FirstOnly(s);
        foreach (var slot in new[] { 4, 7 }) s.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 0;      // 이미 쓰러짐
        foreach (var slot in new[] { 0, 2 }) s.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 1;      // 같은 줄의 둘은 한 방에 쓰러진다
        s.Activate("102"); s.Activate("102"); s.Activate("1");                      // 불꽃: 메인 슬롯 0, 서브 슬롯 2
        s.Activate("107");

        Assert.Equal(GameSession.BattleOutcomeKind.Win, s.BattleOutcome);           // 메인과 서브가 모두 쓰러져 승리
        Assert.True(s.EnemyInstances[0].IsDead && s.EnemyInstances[2].IsDead);
        Assert.DoesNotContain(s.BattleLog, a => a.IsEnemy && !a.IsSub);             // 쓰러진 적은 행동하지 못했다
        Assert.Contains(s.BattleLog, a => a.IsSub && a.TargetSlot == 2 && a.Damage == 1);   // 서브 타겟의 피해는 남은 hp만큼
    }

    [Fact]
    public void A_sub_target_that_falls_does_not_get_its_own_action_and_the_battle_goes_on_while_enemies_remain()
    {
        var s = Started();
        FirstOnly(s);
        s.EnemyInstances[2].Statuses[GamePack.HpStatusCode] = 1;                    // 서브 타겟이 될 슬롯 2만 쓰러진다
        s.Activate("102"); s.Activate("102"); s.Activate("1");
        s.Activate("107");

        Assert.True(s.EnemyInstances[2].IsDead);
        Assert.Equal(GameSession.BattleOutcomeKind.None, s.BattleOutcome);          // 슬롯 4와 7이 남아 있다
        var firstHit = s.BattleLog.ToList().FindIndex(a => a.IsSub && a.TargetSlot == 2);
        Assert.DoesNotContain(s.BattleLog.Skip(firstHit), a => a.IsEnemy && !a.IsSub && a.Slot == 2);   // 쓰러진 뒤에는 그 슬롯의 적이 행동하지 않는다
    }

    // 자동 타겟 이펙트 마스터를 더한 예시 팩으로 시작한다(스킬 1001~1004 모두 같은 이펙트; 적 파티 2: 전사 하나가 아군 둘 중 하나를 노린다)
    static GameSession StartedWithAutoTarget(string effects)
    {
        var rows = string.Concat(new[] { 1001, 1002, 1003, 1004 }.SelectMany(skill => effects.TrimEnd('\n').Split('\n').Select((row, i) => $"{skill}\t{i + 1}\t{row}\n")));
        var pack = SamplePackWith((GamePack.SkillAutoTargetEffectMasterPath, "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n" + rows));
        var s = GameSession.NewGame(pack);
        s.NextRandom = _ => 1;                                                      // 적 파티 2(전사 하나)
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        s.AddCharacter(7001);
        s.Party[1] = 7001;
        Assert.NotNull(EnterBattle(s));
        s.NextRandom = _ => 0;                                                      // 그 뒤의 랜덤은 늘 첫 후보
        return s;
    }

    // 아군 둘이 적 하나를 노리게 하고 실행한다(적이 쓰러지지 않도록 hp를 크게)
    static void Go(GameSession s)
    {
        var slot = s.EnemyInstances.Keys.Single();
        s.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 9999;
        for (var i = 0; i < 2; i++) { s.Activate("102"); s.Activate("101"); s.Activate((slot + 1).ToString()); }
        s.Activate("107");
    }

    static IEnumerable<GameSession.BattleActed> EnemyActs(GameSession s) => s.BattleLog.Where(a => a.IsEnemy && !a.IsSub);

    [Fact]
    public void An_enemys_main_target_follows_the_auto_target_circuits_result()
    {
        var s = StartedWithAutoTarget("SubTargetToResult\t1\t0\t0\n");              // 후보 1번(둘째 아군)
        Go(s);
        var act = EnemyActs(s).Single();
        Assert.Equal((false, 1), (act.TargetIsEnemy, act.TargetSlot));              // 랜덤이면 첫 후보인 슬롯 0이었다
    }

    [Fact]
    public void The_circuit_can_read_candidates_to_pick_the_weakest()
    {
        // 후보 0과 1의 hp를 읽어 더 작은 쪽을 고른다(같으면 0번)
        const string pickLowerHp =
            "FocusSubTargetCharacter\t0\t0\t0\nFocusedCharacterValueToCache\t2\t0\t0\nFocusSubTargetCharacter\t1\t0\t0\nFocusedCharacterValueToCache\t2\t1\t0\n" +
            "JumpCacheLessOrEqual\t0\t1\t8\nSubTargetToResult\t1\t0\t0\nFinish\t0\t0\t0\nSubTargetToResult\t0\t0\t0\n";
        var s = StartedWithAutoTarget(pickLowerHp);
        s.Characters[1].Statuses[GamePack.HpStatusCode] = 40;
        s.Characters[7001].Statuses[GamePack.HpStatusCode] = 5;
        Go(s);
        Assert.Equal(1, EnemyActs(s).Single().TargetSlot);                          // hp가 더 작은 슬롯 1

        var t = StartedWithAutoTarget(pickLowerHp);
        t.Characters[1].Statuses[GamePack.HpStatusCode] = 5;
        t.Characters[7001].Statuses[GamePack.HpStatusCode] = 40;
        Go(t);
        Assert.Equal(0, EnemyActs(t).Single().TargetSlot);
    }

    [Fact]
    public void An_unsuitable_result_falls_back_to_a_random_candidate()
    {
        var s = StartedWithAutoTarget("SubTargetToResult\t9\t0\t0\n");               // 후보 번호가 범위 밖
        s.NextRandom = _ => 1;                                                      // 랜덤은 늘 둘째 후보
        Go(s);
        Assert.Equal(1, EnemyActs(s).Single().TargetSlot);                          // 서킷이 아니라 랜덤이 골랐다

        var none = StartedWithAutoTarget("None\t0\t0\t0\n");                        // Result를 놓지 않아도 랜덤
        none.NextRandom = _ => 1;
        Go(none);
        Assert.Equal(1, EnemyActs(none).Single().TargetSlot);
    }

    [Fact]
    public void The_sample_pack_enemies_aim_at_the_weaker_of_the_first_two_allies()
    {
        var s = Started(roll: 1, twoUnits: true);                                   // 적 파티 2: 전사 하나
        FirstOnly(s);                                                               // 랜덤이었다면 첫 후보(슬롯 0)
        s.Characters[1].Statuses[GamePack.HpStatusCode] = 40;
        s.Characters[7001].Statuses[GamePack.HpStatusCode] = 5;
        var slot = s.EnemyInstances.Keys.Single();
        s.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 9999;
        for (var i = 0; i < 2; i++) { s.Activate("102"); s.Activate("101"); s.Activate((slot + 1).ToString()); }
        s.Activate("107");
        Assert.All(s.BattleLog.Where(a => a.IsEnemy && !a.IsSub && a.SkillCode is 1001 or 1002), a => Assert.Equal(1, a.TargetSlot));
        Assert.Contains(s.BattleLog, a => a.IsEnemy && !a.IsSub && a.SkillCode is 1001 or 1002);
    }

    // 스킬 3(치유)은 메인 타겟 없음(None), 스킬 4(집중)는 아군 기절 유닛(AllyFallen)으로 바꾼 예시 팩
    static GameSession StartedWithFallenTypes()
    {
        var pack = SamplePackWith((GamePack.SkillMasterPath, "code\tname\tmain_target_type\tsub_target_type\n1001\t베기\tEnemy\t\n1002\t불꽃\tEnemy\tSameRow\n1003\t치유\tNone\t\n1004\t집중\tAllyFallen\t\n1005\t부활\tAllyFallen\t\n"));
        var s = GameSession.NewGame(pack);
        s.NextRandom = _ => 0;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        s.AddCharacter(7001);
        s.Party[1] = 7001;
        Assert.NotNull(EnterBattle(s));
        return s;
    }

    [Fact]
    public void A_skill_without_a_main_target_is_not_an_action_candidate_and_fallen_types_target_fallen_units()
    {
        var s = StartedWithFallenTypes();
        s.Activate("102");
        Assert.Equal(["베기", "불꽃", "집중", "부활", "취소"],                                  // 치유(None)는 후보가 아니다
            s.Entries.Where(e => e.Action is EntryAction.BattleChooseSkill or EntryAction.BattleCancelActions).Select(e => e.Label));

        s.Activate("103");                                                          // 집중: 아군 기절 유닛
        Assert.Empty(s.Entries.Where(e => e.Action == EntryAction.BattleChooseTarget));      // 쓰러진 아군이 없다
        s.Activate("101");
        s.Characters[7001].Statuses[GamePack.HpStatusCode] = 0;                     // 용사가 쓰러졌다
        s.Activate("102"); s.Activate("103");
        Assert.Equal(["12"], s.Entries.Where(e => e.Action == EntryAction.BattleChooseTarget).Select(e => e.Code));
    }

    [Fact]
    public void An_auto_target_result_that_points_at_a_fallen_unit_falls_back_to_a_valid_random_target()
    {
        var rows = "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n1001\t1\tSubTargetToResult\t0\t\t\n1002\t1\tSubTargetToResult\t0\t\t\n";
        var s = GameSession.NewGame(SamplePackWith((GamePack.SkillAutoTargetEffectMasterPath, rows)));
        s.NextRandom = _ => 1;                                                      // 적 파티 2: 전사 하나
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");
        s.AddCharacter(7001);
        s.Party[1] = 7001;
        Assert.NotNull(EnterBattle(s));
        s.NextRandom = _ => 0;
        s.Characters[1].Statuses[GamePack.HpStatusCode] = 0;                        // 후보 0번(슬롯 0)이 이미 쓰러져 있다
        var slot = s.EnemyInstances.Keys.Single();
        s.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 9999;
        Assert.True(s.BattleAllActionsSet == false);
        s.Activate("101");                                                          // 쓰러진 슬롯 0을 건너뛰고
        s.Activate("102"); s.Activate("101"); s.Activate((slot + 1).ToString());     // 살아 있는 용사(슬롯 1)만 행동한다
        Assert.True(s.BattleAllActionsSet);                                         // 쓰러진 유닛은 행동을 정하지 않아도 된다
        s.Activate("107");
        Assert.All(s.BattleLog.Where(a => a.IsEnemy && !a.IsSub && a.SkillCode is 1001 or 1002), a => Assert.Equal(1, a.TargetSlot));   // 쓰러진 슬롯 0을 고른 Result는 부적절해 랜덤(유효한 후보뿐)
        Assert.Contains(s.BattleLog, a => a.IsEnemy && !a.IsSub && a.SkillCode is 1001 or 1002);
    }

    [Fact]
    public void The_sample_revive_skill_raises_a_fallen_ally_and_the_fallen_unit_needs_no_action()
    {
        var s = Started(twoUnits: true);
        FirstOnly(s);
        s.Characters[7001].Statuses[GamePack.HpStatusCode] = 0;                     // 용사가 쓰러져 있다
        s.Activate("102"); s.Activate("105");                                       // 스킬 5: 부활(같은 편의 기절 유닛)
        Assert.Equal(["12"], s.Entries.Where(e => e.Action == EntryAction.BattleChooseTarget).Select(e => e.Code));
        s.Activate("12");
        Assert.True(s.BattleAllActionsSet);                                         // 쓰러진 용사는 행동을 정하지 않아도 된다
        s.Activate("107");
        Assert.Contains(s.BattleLog, a => !a.IsEnemy && a.Slot == 0 && a.SkillCode == 1005 && a.TargetSlot == 1 && a.Damage == -20);
        Assert.Contains(s.BattleLogLines, l => l.StartsWith("당신 → 용사 : 부활") && l.EndsWith("(회복 20)"));
    }

    [Fact]
    public void Before_the_next_turn_an_enemy_side_with_a_dead_front_row_is_pulled_once_with_its_instances()
    {
        var s = Started();                                                          // 적 파티 1: 슬롯 0, 2(전열), 4(중열), 7(후열)
        FirstOnly(s);
        var middle = s.EnemyInstances[4];
        var back = s.EnemyInstances[7];
        s.EnemyInstances[0].Statuses[GamePack.HpStatusCode] = 0;
        s.EnemyInstances[2].Statuses[GamePack.HpStatusCode] = 0;                    // 전열이 모두 쓰러져 있다
        s.Activate("102"); s.Activate("101"); s.Activate("5");                      // 중열 슬롯 4를 공격
        s.Activate("107");
        Assert.Equal(middle.CharacterCode, s.EnemyInstances[1].CharacterCode); Assert.Equal(middle.OrderNumber, s.EnemyInstances[1].OrderNumber);                                 // 중열이 전열로
        Assert.Equal(back.OrderNumber, s.EnemyInstances[4].OrderNumber);                                  // 후열이 중열로
        Assert.NotNull(s.EnemyUnits[6]);                                            // 쓰러진 전열은 후열로(열 번호 유지)
        Assert.NotNull(s.EnemyUnits[8]);
        Assert.Null(s.EnemyUnits[0]);
        Assert.Null(s.EnemyUnits[7]);
    }

    [Fact]
    public void A_living_front_unit_keeps_the_formation_even_when_the_middle_row_is_empty()
    {
        var s = Started();
        FirstOnly(s);
        s.EnemyInstances[4].Statuses[GamePack.HpStatusCode] = 0;                    // 중열이 쓰러져도 전열이 살아 있으면 그대로
        s.Activate("102"); s.Activate("101"); s.Activate("1");
        s.Activate("107");
        Assert.NotNull(s.EnemyUnits[0]);
        Assert.NotNull(s.EnemyUnits[7]);
        Assert.Null(s.EnemyUnits[1]);
    }

    [Fact]
    public void After_the_battle_ends_the_back_entry_returns_to_the_adv_and_continues_after_the_battle_step()
    {
        var s = Started(canRun: true);
        Assert.DoesNotContain(s.Entries, e => e.Code == "999");                     // 전투 중에는 돌아가기가 없다
        s.Activate("106");                                                          // 도망
        Assert.Equal(Screen.Battle, s.Screen);
        s.Activate("999");
        Assert.Equal(Screen.EventPlay, s.Screen);                                   // 전투를 시작한 화면(ADV)
        Assert.Equal(AdvStepKind.TextLog, s.CurrentAdvStep!.Value.Kind);
        Assert.Equal("전투에서 패배했습니다.", s.CurrentAdvStep!.Value.Text);          // Battle 칸 다음부터 이어 간다(도망은 결과 2라 승리 분기로 가지 않는다)
        Assert.Equal(2, s.PlayerFlags[2001]);
        s.Activate("1");
        Assert.Equal(Screen.Event, s.Screen);                                       // ADV와 이벤트가 끝난다
    }

    [Fact]
    public void The_battle_always_uses_the_party_that_has_the_player_and_the_viewed_party_is_restored()
    {
        var s = GameSession.NewGame(RunnablePack());
        s.NextRandom = _ => 0;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");     // 파티-A에 당신
        s.Activate("105"); s.Activate("12");                                        // 파티-B(1번)를 보고 있다
        s.Activate("999");
        Assert.Equal(1, s.CurrentPartyIndex);
        s.Activate("103"); s.Activate("3002");
        var border = s.BattleRows.Single(r => r.Kind == BattleRowKind.Border);
        Assert.Equal("파티-A", border.RightText);                                    // 보던 파티와 상관없이 당신이 있는 파티
        s.Activate("106"); s.Activate("999");
        Assert.Equal(1, s.CurrentPartyIndex);                                       // 끝나면 보던 파티로
    }

    [Fact]
    public void A_win_drops_the_defeated_enemies_items_after_the_win_circuits_and_a_run_drops_nothing()
    {
        var win = Started(roll: 1);                                                 // 적 파티 2: 전사 하나(프리셋 유닛 1: 약초 1개)
        FirstOnly(win);
        var before = win.CountInventory(3);
        var slot = win.EnemyInstances.Keys.Single();
        win.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 5;
        win.Activate("102"); win.Activate("102"); win.Activate((slot + 1).ToString());
        win.Activate("107");
        Assert.Equal(GameSession.BattleOutcomeKind.Win, win.BattleOutcome);
        Assert.Equal(before + 1, win.CountInventory(3));
        Assert.Single(win.BattleDropLines);
        Assert.EndsWith(" ×1", win.BattleDropLines[0]);

        var run = Started(roll: 1, canRun: true);
        var runBefore = run.CountInventory(3);
        run.Activate("106");
        Assert.Equal(runBefore, run.CountInventory(3));
        Assert.Empty(run.BattleDropLines);
    }

    [Fact]
    public void An_adv_battle_step_needs_an_existing_battle_and_is_a_battle_step_for_the_screen()
    {
        var ex = Assert.Throws<PackFormatException>(() => SamplePackWith((GamePack.AdvFlowMasterPath, "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n4001\t1\tBattle\t9999\t0\t0\t\t\n")));
        Assert.Contains("9999", ex.Message);
        var pack = SamplePack();
        Assert.Equal(26, (int)AdvFlowType.Battle);
        Assert.Equal(new AdvStep(AdvStepKind.Battle, 1, "", [], BattleCode: 5001, BattleResultFlag: 2001), new AdvFlowCircuit(pack, 4002, new Dictionary<long, long>()).Advance());
    }

    [Fact]
    public void A_battle_that_cannot_be_run_from_has_the_run_command_gray_and_the_result_goes_into_the_flag_for_branching()
    {
        var s = Started(roll: 1);                                                   // 예시 팩의 전투 5001은 도망 불가
        Assert.False(s.BattleCanRun);
        Assert.False(Entry(s, "106").Enabled);
        s.Activate("106");
        Assert.Equal(GameSession.BattleOutcomeKind.None, s.BattleOutcome);          // 눌러도 반응이 없다

        FirstOnly(s);
        var slot = s.EnemyInstances.Keys.Single();
        s.EnemyInstances[slot].Statuses[GamePack.HpStatusCode] = 5;
        s.Activate("102"); s.Activate("102"); s.Activate((slot + 1).ToString());
        s.Activate("107");
        Assert.Equal(GameSession.BattleOutcomeKind.Win, s.BattleOutcome);
        Assert.False(s.PlayerFlags.ContainsKey(2001));                              // 돌아가기 전에는 아직
        s.Activate("999");
        Assert.Equal(1, s.PlayerFlags[2001]);                                       // 승리 1
        Assert.Equal("전투에서 승리했습니다.", s.CurrentAdvStep!.Value.Text);          // JumpFlagEqual로 승리 분기
    }

    [Fact]
    public void The_battle_master_can_run_column_is_optional_and_checked()
    {
        Assert.True(Started(canRun: true).BattleCanRun);
        var bad = Assert.Throws<PackFormatException>(() => SamplePackWith(("master/battle_master_v1.tsv", "code" + (char)9 + "name" + (char)9 + "can_run" + (char)10 + "5001" + (char)9 + "전투" + (char)9 + "아니오" + (char)10)));
        Assert.Contains("can_run", bad.Message);
        Assert.True(SamplePackWith(("master/battle_master_v1.tsv", "code" + (char)9 + "name" + (char)10 + "5001" + (char)9 + "전투" + (char)10)).FindBattle(5001)!.CanRun);   // 열이 없으면 도망 가능
    }
}
