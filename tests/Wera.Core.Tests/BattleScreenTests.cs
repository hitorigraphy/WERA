using Wera.Core;

namespace Wera.Core.Tests;

// [110] 전투(임시): 정해진 전투(5001)를 시작하고 작전 화면(슬롯 네 줄과 가운데 영역)을 그린다.
public class BattleScreenTests
{
    // 예시 게임팩의 전투 5001: 적 파티 1(전사 둘, 마법사 둘; 가중치 1)과 적 파티 2(전사 하나; 가중치 3).
    static GamePack SamplePack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(zip);
    }

    static GameSession Started(long roll = 0)
    {
        var s = GameSession.NewGame(SamplePack());
        s.NextRandom = _ => roll;
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");     // 파티-A의 첫 칸에 당신(리더 태그 있음) → 리더
        Assert.NotNull(s.Activate("110"));
        return s;
    }

    static IEnumerable<string> Titles(GameSession s) => s.BattleRows.Select(r => r.Kind == BattleRowKind.Border ? "|" : (r.IsEnemy ? "적 " : "아군 ") + r.Title);

    [Fact]
    public void Entering_shows_enemy_back_middle_front_then_the_border_then_the_ally_front()
    {
        var s = Started();
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Equal(GameSession.TemporaryBattleCode, s.CurrentBattleCode);
        Assert.Equal(0, s.BattleTop);
        Assert.Equal(["적 후위", "적 중위", "적 전위", "|", "아군 전위"], Titles(s));
    }

    [Fact]
    public void The_enemy_rows_show_the_preset_party_and_the_ally_row_shows_my_party()
    {
        var s = Started(roll: 0);                                  // 가중치 1짜리 적 파티 1
        Assert.Equal(1, s.CurrentEnemyPartyCode);
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
        s.Activate("999");
        s.Activate("105"); s.Activate("0"); s.Activate("정예");
        s.Activate("999"); s.Activate("110");
        Assert.Equal("정예", s.BattleRows.Single(r => r.Kind == BattleRowKind.Border).RightText);
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
        Assert.Equal([false, true], s.Entries.Where(e => e.Action == EntryAction.BattleScroll).Select(e => e.Enabled));
        Assert.Equal(["101", "102", "103", "104", "105", "106", "1000", "1001", "999"], s.Entries.Select(e => e.Code));
        Assert.Equal(["이전 캐릭터", "행동 설정", "상태 확인", "다음 캐릭터", "아이템", "퇴각"], s.Entries.Where(e => e.Action == EntryAction.BattleCommand).Select(e => e.Label));
        foreach (var code in new[] { "101", "102", "103", "104", "105", "106" }) s.Activate(code);      // 아직 동작이 없다
        Assert.Equal((Screen.Battle, 0), (s.Screen, s.BattleTop));
        Assert.Equal(["▲", "▼"], s.Entries.Where(e => e.Action == EntryAction.BattleScroll).Select(e => e.Label));

        s.Activate("1001");                                        // 가운데 구성(적 중위부터)을 건너뛰고 바로 적 1 / 아군 3
        Assert.Equal(GameSession.BattleTopAllyHeavy, s.BattleTop);
        Assert.Equal(["적 전위", "|", "아군 전위", "아군 중위", "아군 후위"], Titles(s));
        Assert.Equal(Screen.Battle, s.Screen);
        Assert.Equal([true, false], s.Entries.Where(e => e.Action == EntryAction.BattleScroll).Select(e => e.Enabled));
        s.Activate("1001");                                        // 이미 그 구성이면 반응이 없다
        Assert.Equal(2, s.BattleTop);

        s.Activate("1000");                                        // 바로 적 3 / 아군 1로
        Assert.Equal(0, s.BattleTop);
        Assert.Equal(["적 후위", "적 중위", "적 전위", "|", "아군 전위"], Titles(s));
        s.Activate("1000");
        Assert.Equal(0, s.BattleTop);
    }

    [Fact]
    public void Starting_again_returns_to_the_top_and_back_leaves_for_home()
    {
        var s = Started();
        s.Activate("1001"); s.Activate("1001");
        s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
        s.Activate("110");
        Assert.Equal(0, s.BattleTop);
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
        Assert.Equal(["적 후위", "적 중위", "적 전위", "|", "아군 전위"], Titles(s));
    }
}
