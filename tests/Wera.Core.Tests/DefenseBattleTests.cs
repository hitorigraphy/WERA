using System.IO.Compression;
using Wera.Core;

namespace Wera.Core.Tests;

// 아지트 방어전: 턴 종료 때 위협도로 침입 판정을 해서 적이 나오면 방어전 화면이 로그를 한 줄씩 보여 준다.
// 한 층의 순서는 기본 시설 → 지은 시설(오퍼레이터가 차례로, 그다음 시설) → 보스(방어 파티와 자동 전투). 맨 위 층(가장 최근 확장)부터 아래로, 가장 처음 있던 층(마지막 번호)이 최종층이다.
public class DefenseBattleTests
{
    static GamePack SamplePackWith(params (string Path, string Text)[] extra)
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        var source = new MemoryStream();
        using (var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra")))
        using (var from = new ZipArchive(read))
        using (var to = new ZipArchive(source, ZipArchiveMode.Create, leaveOpen: true))
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

    const string EnemyHeader = "code\tpreset_party_code\tmin_threat\tconsume_threat\tpriority\tchance\n";
    const string DefenseHeader = "facility_code\tdefense\n";

    // 적: 프리셋 파티 2(전사 하나), 위협도 100을 한 번에 다 쓰고 100%로 침입한다. 정문(시설 1)의 방어력은 facilityDefense다.
    static GameSession Session(long facilityDefense = 0, string enemies = "1\t2\t1\t100\t1\t100\n")
    {
        var pack = SamplePackWith(
            ("master/hideout_defense_enemy_master_v1.tsv", EnemyHeader + enemies),
            ("master/facility_defense_master_v1.tsv", DefenseHeader + $"1\t{facilityDefense}\n"));
        var s = GameSession.NewGame(pack);
        s.PlayerFlags[100] = 100;
        s.NextRandom = n => n - 1;                               // 늘 가장 큰 값: 확률 100%는 통과하고 피해는 최대 방어력이다
        return s;
    }

    // 방어 파티(보스)로 용사를 세운다. hp는 보스의 체력(적 전사는 40이고 베기로 10씩 깎는다).
    static string Boss(GameSession s, int floor = 0, long hp = 40)
    {
        s.AddCharacter(7001);
        var id = s.InstanceIdOf(7001)!;
        s.Characters[id].Statuses[GamePack.HpStatusCode] = hp;
        s.HideoutParties[floor].Units[0] = new PartyUnit(7001, [], true, id);
        return id;
    }

    static string Log(IEnumerable<string> lines) => string.Join("\n", lines);

    [Fact]
    public void Without_an_intrusion_ending_the_turn_stays_on_the_home_screen_as_before()
    {
        var s = Session();
        s.PlayerFlags[100] = 0;                                    // 위협도가 없으면 적이 오지 않는다
        s.Activate(GameSession.EndTurnEntryCode);
        Assert.Equal(Screen.Home, s.Screen);
        Assert.StartsWith("턴 종료", s.Message);
        Assert.Empty(s.DefenseLog);
        Assert.False(s.HideoutInvaded);
    }

    [Fact]
    public void An_intrusion_opens_the_defense_screen_whose_log_advances_one_line_per_enter_and_then_returns_home()
    {
        var s = Session();
        s.Activate(GameSession.EndTurnEntryCode);
        Assert.Equal(Screen.Defense, s.Screen);
        Assert.True(s.AdvanceOnAnyKey);
        Assert.Equal(GameSession.NextDefenseStepCode, Assert.Single(s.Entries).Code);       // 세이브 같은 항목은 없다
        Assert.Single(s.DefenseLog);
        var shown = 0;
        while (s.Screen == Screen.Defense)
        {
            shown++;
            Assert.Equal(shown, s.DefenseLog.Count);
            s.Activate(GameSession.NextDefenseStepCode);
        }
        Assert.True(shown > 1);
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Empty(s.DefenseLog);
        Assert.Contains("방어전 종료", s.Message);
        Assert.Contains("아지트 침입당함", s.Message);
        Assert.Contains("적이 침입해 시설이 생산하지 못했습니다", s.Message);                // 지킬 파티가 없어 뚫렸고 시설 서킷은 건너뛴다
        Assert.False(s.HideoutInvaded);                                                     // 건너뛴 뒤 돌아간다
    }

    [Fact]
    public void An_enemy_party_that_the_facilities_wipe_out_is_captured_and_does_not_reach_the_boss()
    {
        var s = Session(facilityDefense: 100000);
        var boss = Boss(s);
        var log = Log(s.RunDefense());
        Assert.DoesNotContain("[보스]", log);
        Assert.Contains("구금: 전사", log);
        Assert.False(s.HideoutInvaded);
        var captive = Assert.Single(s.Characters.Values, c => c.CharacterCode == 7003);
        Assert.True(s.IsCapturedEnemy(captive.InstanceId));
        Assert.Null(s.Characters[boss].DefenseDayCount);                                     // 보스는 싸우지 않았다
    }

    [Fact]
    public void Operators_hit_one_at_a_time_before_the_facility_and_are_marked_as_engaged()
    {
        var pack = SamplePackWith(
            ("master/hideout_defense_enemy_master_v1.tsv", EnemyHeader + "1\t2\t1\t100\t1\t100\n"),
            ("master/facility_defense_master_v1.tsv", DefenseHeader + "1\t9\n"),
            ("master/facility_operator_slot_master_v1.tsv", "facility_code\toperator_slot_code\n2\t1\n1\t2\n"),
            ("master/operator_slot_defense_flow_master_v1.tsv", "operator_slot_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n2\t1\tAddDefense\t7\t0\t0\n"));
        var s = GameSession.NewGame(pack);
        s.PlayerFlags[100] = 100;
        s.NextRandom = n => n - 1;
        s.AddCharacter(7001);
        var op = s.InstanceIdOf(7001)!;
        Assert.True(s.SetHideoutOperator(0, false, 2, 7001));
        var lines = s.RunDefense().ToList();
        var operatorAt = lines.FindIndex(l => l.Contains("용사(경비): 방어력 7"));
        var facilityAt = lines.FindIndex(l => l.Contains("정문: 방어력 9"));
        Assert.True(operatorAt >= 0 && facilityAt > operatorAt, Log(lines));
        Assert.Contains("전사 -7 (남은 HP 33)", lines[operatorAt]);
        Assert.Equal(s.CurrentDayCount, s.Characters[op].DefenseDayCount);                    // 오퍼레이터는 오늘 방어전에 참가했다
    }

    [Fact]
    public void The_boss_wins_an_automatic_battle_and_the_enemy_is_captured()
    {
        var s = Session();
        s.NextRandom = _ => 0;                                                              // 첫 스킬(베기)을 쓰고 피해는 0이다(확률 100%는 통과)
        var boss = Boss(s, hp: 100);
        var log = Log(s.RunDefense());
        Assert.Contains("보스 전투 승리", log);
        Assert.Contains("구금: 전사", log);
        Assert.False(s.HideoutInvaded);
        Assert.Contains(s.Characters.Values, c => c.CharacterCode == 7003 && s.IsCapturedEnemy(c.InstanceId));
        Assert.Equal(s.CurrentDayCount, s.Characters[boss].DefenseDayCount);                 // 보스 파티는 싸웠다
    }

    [Fact]
    public void The_enemy_passes_when_the_boss_loses_and_the_final_floor_means_the_hideout_is_invaded()
    {
        var s = Session();
        s.NextRandom = _ => 0;
        var boss = Boss(s, hp: 5);
        var log = Log(s.RunDefense());
        Assert.Contains("보스 전투 패배", log);
        Assert.Contains("최종층을 뚫었습니다", log);
        Assert.True(s.HideoutInvaded);
        Assert.DoesNotContain(s.Characters.Values, c => c.CharacterCode == 7003);            // 아무도 구금되지 않았다
        Assert.Equal(s.CurrentDayCount, s.Characters[boss].DefenseDayCount);
    }

    [Fact]
    public void A_floor_whose_defenders_cannot_fight_is_passed_at_once()
    {
        var s = Session();
        s.NextRandom = _ => 0;
        var boss = Boss(s, hp: 0);                                                          // 쓰러진 방어 파티
        var log = Log(s.RunDefense());
        Assert.Contains("맞서 싸울 방어 파티가 없어 그대로 지나갑니다", log);
        Assert.True(s.HideoutInvaded);
        Assert.Null(s.Characters[boss].DefenseDayCount);
    }

    [Fact]
    public void Enemies_enter_from_the_newest_floor_and_each_floor_passed_rolls_a_new_intrusion()
    {
        var s = Session(enemies: "1\t2\t1\t50\t1\t100\n");
        s.InventoryItems[2] = new InventoryItem(100);
        Assert.True(s.ExpandHideout(1));
        Assert.Equal(2, s.HideoutFloorCount);
        var log = s.RunDefense().ToList();
        int At(string text) => log.FindIndex(l => l.Contains(text));
        Assert.Equal(0, s.Threat);                                                           // 위협도 100을 50씩 두 번 썼다
        Assert.True(At("침입 파티 1(전사 하나)이(가) 1층에 들어왔습니다") >= 0, Log(log));
        Assert.True(At("추가 침입!") > At("침입 파티 1(전사 하나)이(가) 1층을(를) 지나갔습니다"), Log(log));
        Assert.True(At("침입 파티 1(전사 하나)이(가) 2층에 들어왔습니다") < At("침입 파티 2(전사 하나)이(가) 1층에 들어왔습니다"), Log(log));    // 대기열: 앞 파티가 먼저
        Assert.Equal(2, log.Count(l => l.Contains("최종층을 뚫었습니다")));
        Assert.True(s.HideoutInvaded);
        Assert.EndsWith("침입 2팀, 격퇴 0팀, 구금 0명, 아지트 침입당함", log[^1]);
    }

    [Fact]
    public void Every_operator_who_hits_and_every_boss_fighter_is_marked_and_an_operator_leaves_the_boss_party()
    {
        var pack = SamplePackWith(
            ("master/hideout_defense_enemy_master_v1.tsv", EnemyHeader + "1\t2\t1\t100\t1\t100\n"),
            ("master/facility_defense_master_v1.tsv", DefenseHeader + "1\t0\n"),
            ("master/facility_operator_slot_master_v1.tsv", "facility_code\toperator_slot_code\n2\t1\n1\t2\n"),
            ("master/operator_slot_defense_flow_master_v1.tsv", "operator_slot_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n2\t1\tAddDefense\t1\t0\t0\n"));
        var s = GameSession.NewGame(pack);
        s.PlayerFlags[100] = 100;
        s.NextRandom = n => n - 1;
        var bossId = Boss(s);                                                               // 방어 파티의 용사 한 명
        s.AddCharacter(7002);                                                               // 다른 캐릭터가 오퍼레이터
        var operatorId = s.InstanceIdOf(7002)!;
        Assert.True(s.SetHideoutOperatorInstance(0, false, 2, operatorId));
        s.RunDefense().ToList();
        Assert.Equal(s.CurrentDayCount, s.Characters[operatorId].DefenseDayCount);          // 때린 오퍼레이터
        Assert.Equal(s.CurrentDayCount, s.Characters[bossId].DefenseDayCount);              // 싸운 보스 파티원

        // 방어 파티에 선 복사본을 오퍼레이터로 세우면 파티에서 빠지므로 한 사람이 두 역할로 두 번 세지는 일은 없다.
        var two = GameSession.NewGame(pack);
        var same = Boss(two);
        Assert.True(two.SetHideoutOperatorInstance(0, false, 2, same));
        Assert.DoesNotContain(two.HideoutParties[0].Units.OfType<PartyUnit>(), u => u.InstanceId == same);
    }
}
