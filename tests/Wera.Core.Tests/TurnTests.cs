using Wera.Core;

namespace Wera.Core.Tests;

// time_slot_master(code, name; code가 키)와 turn_master(turn_count, year, month, day, time_slot_code; turn_count가 키).
// 게임은 가장 작은 턴에서 시작하고, 턴이 넘어가면 다음 턴의 날짜와 시간대로 바뀐다.
public class TurnTests
{
    const string Slots = "code\tname\n2\t낮\n1\t아침\n3\t저녁\n";
    const string TurnHeader = "turn_count\tyear\tmonth\tday\ttime_slot_code\n";

    // 파일 순서와 키 순서가 다르다: 턴 3이 먼저 적혀 있어도 턴 1이 첫 턴이다.
    const string Turns = TurnHeader +
        "3\t1\t1\t2\t1\n" +
        "1\t1\t1\t1\t1\n" +
        "2\t1\t1\t1\t2\n" +
        "10\t2\t12\t30\t3\n";

    static GamePack Load(string slots = Slots, string turns = Turns) =>
        GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/time_slot_master_v1.tsv", slots),
            ("master/turn_master_v1.tsv", turns)));

    // ---- 마스터 ----

    [Fact]
    public void Time_slots_load_in_file_order_and_are_found_by_code()
    {
        var pack = Load();
        Assert.Equal([2L, 1L, 3L], pack.TimeSlots!.Select(s => s.Code));
        Assert.Equal(new TimeSlotEntry(1, "아침"), pack.FindTimeSlot(1));
        Assert.Null(pack.FindTimeSlot(9));
        Assert.Equal(3, pack.Masters.TimeSlotRowTable.Count);
    }

    [Fact]
    public void Turns_are_keyed_by_turn_count_and_ordered_by_it()
    {
        var pack = Load();
        Assert.Equal(new TurnEntry(10, 2, 12, 30, 3), pack.FindTurn(10));
        Assert.Null(pack.FindTurn(4));
        Assert.Equal([3L, 1L, 2L, 10L], pack.Turns!.Select(t => t.TurnCount));                      // 파일 순서
        Assert.Equal([1L, 2L, 3L, 10L], pack.Masters.TurnRowTable.All.Select(t => t.TurnCount));     // 키 순서
        Assert.Equal(1, pack.FirstTurn.TurnCount);                                                    // 가장 작은 턴이 첫 턴
    }

    [Fact]
    public void The_next_turn_is_the_next_larger_turn_count_even_when_numbers_skip()
    {
        var pack = Load();
        Assert.Equal(2, pack.NextTurnAfter(1)!.TurnCount);
        Assert.Equal(10, pack.NextTurnAfter(3)!.TurnCount);          // 4~9는 없다
        Assert.Equal(10, pack.NextTurnAfter(5)!.TurnCount);          // 없는 턴 카운트에서도 다음으로 큰 것
        Assert.Null(pack.NextTurnAfter(10));
    }

    [Fact]
    public void Date_text_shows_year_month_day_and_the_time_slot_name()
    {
        var pack = Load();
        Assert.Equal("1년 1월 1일 아침", pack.DateText(pack.FindTurn(1)!));
        Assert.Equal("2년 12월 30일 저녁", pack.DateText(pack.FindTurn(10)!));
    }

    [Theory]
    [InlineData("code\tname\n1\t아침\n1\t또 아침\n", "중복")]
    [InlineData("code\tname\nx\t아침\n", "정수")]
    [InlineData("id\tname\n1\t아침\n", "헤더")]
    public void Bad_time_slot_masters_are_rejected(string slots, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(slots: slots, turns: TurnHeader + "1\t1\t1\t1\t1\n")).Message);

    [Theory]
    [InlineData("1\t1\t1\t1\t1\n1\t1\t1\t2\t1\n", "중복")]               // 같은 턴을 두 번
    [InlineData("1\t1\t1\t1\t9\n", "시간대 '9'")]                         // 시간대 마스터에 없다
    [InlineData("1\t1\t1\t1\n", "시간대")]                                // 칸이 모자란다(빈 시간대)
    [InlineData("x\t1\t1\t1\t1\n", "턴 카운트")]
    [InlineData("1\t1\t1\t1.5\t1\n", "일")]
    [InlineData("1\ta\t1\t1\t1\n", "연도")]
    [InlineData("1\t1\t\t1\t1\n", "월")]
    [InlineData("", "한 개도 없습니다")]                                 // 턴이 하나도 없다
    public void Bad_turn_masters_are_rejected(string rows, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(turns: TurnHeader + rows)).Message);

    [Fact]
    public void Turn_values_are_longs_and_the_calendar_is_not_range_checked()
    {
        var pack = Load(turns: TurnHeader + "5000000000\t9000000000\t13\t40\t1\n");           // 게임팩이 정하는 달력
        Assert.Equal(new TurnEntry(5000000000, 9000000000, 13, 40, 1), pack.FirstTurn);
    }

    [Fact]
    public void Both_masters_are_required_when_the_pack_is_loaded()
    {
        foreach (var path in new[] { GamePack.TimeSlotMasterPath, GamePack.TurnMasterPath })
            Assert.Contains(path, Assert.Throws<PackFormatException>(() =>
                GamePack.Load(CoreTests.PackZipWithout(path, ("pack.tsv", "id\tdemo\ntitle\t데모\n")))).Message);
    }

    // ---- 게임 중 ----

    [Fact]
    public void A_new_game_starts_at_the_smallest_turn()
    {
        var s = GameSession.NewGame(Load());
        Assert.Equal(1, s.TurnCount);
        Assert.Equal(new TurnEntry(1, 1, 1, 1, 1), s.CurrentTurn);
        Assert.Equal("1년 1월 1일 아침", s.CurrentDateText);
    }

    [Fact]
    public void Advancing_the_turn_moves_to_the_next_turns_date_and_time_slot()
    {
        var s = GameSession.NewGame(Load());
        Assert.True(s.AdvanceTurn());
        Assert.Equal("1년 1월 1일 낮", s.CurrentDateText);
        Assert.True(s.AdvanceTurn());
        Assert.Equal("1년 1월 2일 아침", s.CurrentDateText);           // 턴 3
        Assert.True(s.AdvanceTurn());
        Assert.Equal(10, s.TurnCount);                                  // 4~9는 건너뛴다
        Assert.Equal("2년 12월 30일 저녁", s.CurrentDateText);
    }

    [Fact]
    public void The_last_turn_stays_put()
    {
        var s = GameSession.NewGame(Load());
        while (s.AdvanceTurn()) { }
        Assert.Equal(10, s.TurnCount);
        Assert.False(s.AdvanceTurn());
        Assert.Equal(10, s.TurnCount);
    }

    [Fact]
    public void The_turn_is_saved_and_restored()
    {
        var s = GameSession.NewGame(Load());
        s.AdvanceTurn(); s.AdvanceTurn();

        var json = s.ToSaveJson();
        Assert.Contains("\"Turn\": 3", json);
        var loaded = GameSession.FromSaveJson(Load(), json);
        Assert.Equal(3, loaded.TurnCount);
        Assert.Equal("1년 1월 2일 아침", loaded.CurrentDateText);
    }

    [Fact]
    public void A_save_without_a_turn_starts_at_the_first_turn_and_an_unknown_turn_is_rejected()
    {
        var s = GameSession.FromSaveJson(Load(), "{\"Version\":1,\"PackId\":\"demo\"}");
        Assert.Equal(1, s.TurnCount);
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Load(), "{\"Version\":1,\"PackId\":\"demo\",\"Turn\":4}"));
    }

    [Fact]
    public void The_default_pack_has_a_start_turn_and_four_time_slots_per_day()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.zip"));
        var pack = GamePack.Load(zip);

        Assert.Equal(["아침", "낮", "저녁", "밤"], pack.TimeSlots!.Select(s => s.Name));
        var s = GameSession.NewGame(pack);
        Assert.Equal("1년 1월 1일 아침", s.CurrentDateText);
        s.AdvanceTurn(); s.AdvanceTurn(); s.AdvanceTurn(); s.AdvanceTurn();
        Assert.Equal("1년 1월 2일 아침", s.CurrentDateText);
    }
}
