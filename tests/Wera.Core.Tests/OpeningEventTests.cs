using System.IO.Compression;
using Wera.Core;

namespace Wera.Core.Tests;

// 오프닝 이벤트: 게임 설정 OpeningEventCode가 있으면 세이브 없이 시작한 새 게임이 그 이벤트(조건 무시)를 먼저 재생하고 끝나면 홈으로 간다.
// 세이브를 불러올 때는 재생하지 않는다.
public class OpeningEventTests
{
    const string Settings = "master/game_setting_master_v1.tsv";
    const string EventMaster = "master/event_master_v1.tsv";
    const string EventConditions = "master/event_condition_master_v1.tsv";
    const string EventFlows = "master/event_flow_master_v1.tsv";
    const string FlowHeader = "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";

    static string Root()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        return root;
    }

    static string SampleText(string path)
    {
        using var zip = ZipFile.OpenRead(Path.Combine(Root(), "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        using var r = new StreamReader(zip.GetEntry(path)!.Open());
        return r.ReadToEnd();
    }

    static GamePack SamplePackWith(params (string Path, string Text)[] extra)
    {
        var source = new MemoryStream();
        using (var read = File.OpenRead(Path.Combine(Root(), "src", "Wera.Web", "wwwroot", "sample-pack.wra")))
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

    static (string, string) Opening(long code) =>
        (Settings, SampleText(Settings).TrimEnd() + $"\nOpeningEventCode\tLong\t{code}\t\t\t오프닝\n");

    static (string, string) Flows(string rows) => (EventFlows, FlowHeader + rows);

    static (string, string) Events(params long[] codes) =>
        (EventMaster, "code\tname\n" + string.Concat(codes.Select(c => $"{c}\t이벤트 {c}\n")));

    // ---- 설정 읽기와 검증 ----

    [Fact]
    public void The_setting_is_read_and_absent_means_no_opening()
    {
        Assert.Null(SamplePackWith().OpeningEventCode);
        Assert.Equal(3002, SamplePackWith(Opening(3002)).OpeningEventCode);
    }

    [Fact]
    public void An_opening_event_that_is_not_in_the_event_master_is_a_load_error()
    {
        Assert.Contains("OpeningEventCode '9999'", Assert.Throws<PackFormatException>(() => SamplePackWith(Opening(9999))).Message);
        Assert.Contains("OpeningEventCode", Assert.Throws<PackFormatException>(() => SamplePackWith(Opening(0))).Message);        // 최솟값 1 미만
    }

    [Fact]
    public void An_opening_event_needs_the_event_master_to_exist()
    {
        // 이벤트 마스터를 빼면 이벤트 흐름 마스터도 읽을 수 없으니 둘을 함께 뺀다(로드는 이벤트 흐름보다 설정 검증이 먼저 걸린다).
        var root = Root();
        var source = new MemoryStream();
        using (var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra")))
        using (var from = new ZipArchive(read))
        using (var to = new ZipArchive(source, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var e in from.Entries.Where(e => e.FullName != EventMaster && e.FullName != EventFlows && e.FullName != Settings))
            {
                using var input = e.Open();
                using var output = to.CreateEntry(e.FullName).Open();
                input.CopyTo(output);
            }
            using var w = new StreamWriter(to.CreateEntry(Settings).Open());
            w.Write(SampleText(Settings).TrimEnd() + "\nOpeningEventCode\tLong\t3002\t\t\t\n");
        }
        source.Position = 0;
        Assert.Contains("OpeningEventCode '3002'", Assert.Throws<PackFormatException>(() => GamePack.Load(source)).Message);
    }

    // ---- 새 게임 ----

    [Fact]
    public void Without_the_setting_a_new_game_starts_at_home_as_before()
    {
        var s = GameSession.NewGame(SamplePackWith());
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Null(s.CurrentEvent);
    }

    [Fact]
    public void A_new_game_plays_the_opening_event_then_goes_home_with_its_flags_set()
    {
        var s = GameSession.NewGame(SamplePackWith(Opening(3003), Events(3003), Flows(
            "3003\t1\tSetFlag\t7001\t5\t0\n3003\t2\tAdv\t4001\t0\t0\n3003\t3\tModFlag\t7001\t2\t0\n3003\t4\tFinish\t0\t0\t0\n")));
        Assert.Equal(Screen.EventPlay, s.Screen);
        Assert.Equal(5, s.PlayerFlags[7001]);                                        // 첫 칸은 이미 실행됐다
        Assert.NotNull(s.CurrentAdv);
        for (var i = 0; i < 50 && s.Screen == Screen.EventPlay; i++) s.Pick("1");     // ADV를 끝까지 넘긴다
        Assert.Equal(Screen.Home, s.Screen);                                         // 이벤트 목록이 아니라 홈
        Assert.Equal(7, s.PlayerFlags[7001]);
        Assert.Null(s.CurrentEvent);
    }

    [Fact]
    public void The_opening_ignores_event_conditions_and_party_participation()
    {
        var s = GameSession.NewGame(SamplePackWith(Opening(3003), Events(3003), Flows("3003\t1\tSetFlag\t7001\t1\t0\n"),
            (EventConditions, "event_code\tgroup_number\tflag_code\tcondition_type\tcondition_value\n3003\t1\t9999\tEqual\t1\n")));
        Assert.Null(s.StartEvent(3003));                                             // 일반 시작은 조건 때문에 막힌다
        Assert.Equal(Screen.Home, s.Screen);                                         // 오프닝은 조건과 상관없이 재생되어 이미 끝났다
        Assert.Equal(1, s.PlayerFlags[7001]);
        Assert.DoesNotContain(s.AvailableEvents, e => e.Code == 3003);               // 조건이 안 맞으니 이벤트 목록에는 안 나온다(특별 표시 없음)
    }

    [Fact]
    public void An_opening_event_with_no_flows_goes_straight_home()
    {
        var s = GameSession.NewGame(SamplePackWith(Opening(3003), Events(3003), Flows("")));
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Null(s.CurrentEvent);
    }

    [Fact]
    public void A_game_over_in_the_opening_shows_the_game_over_screen()
    {
        var s = GameSession.NewGame(SamplePackWith(Opening(3003), Events(3003), Flows("3003\t1\tGameOver\t0\t0\t0\n")));
        Assert.Equal(Screen.GameOver, s.Screen);
    }

    [Fact]
    public void An_opening_battle_runs_then_the_event_continues_to_home()
    {
        var s = GameSession.NewGame(SamplePackWith(Opening(3003), Events(3003), Flows(
            "3003\t1\tBattle\t5001\t2001\t0\n3003\t2\tSetFlag\t7001\t9\t0\n3003\t3\tFinish\t0\t0\t0\n")));
        Assert.Equal(Screen.Battle, s.Screen);                                       // 플레이어가 있는 파티로 전투가 열린다
    }

    [Fact]
    public void The_event_list_returns_to_the_event_list_after_a_normal_event_and_opening_does_not_leak()
    {
        var s = GameSession.NewGame(SamplePackWith(Opening(3003), Events(3003, 3004), Flows(
            "3003\t1\tFinish\t0\t0\t0\n3004\t1\tAdv\t4001\t0\t0\n3004\t2\tFinish\t0\t0\t0\n")));
        Assert.Equal(Screen.Home, s.Screen);
        s.Pick("103");
        s.Pick("3004");
        for (var i = 0; i < 50 && s.Screen == Screen.EventPlay; i++) s.Pick("1");
        Assert.Equal(Screen.Event, s.Screen);                                        // 보통의 이벤트는 이벤트 목록으로 돌아간다
    }

    // ---- 세이브 ----

    [Fact]
    public void Loading_a_save_does_not_replay_the_opening()
    {
        var pack = SamplePackWith(Opening(3003), Events(3003), Flows("3003\t1\tModFlag\t7001\t1\t0\n"));
        var s = GameSession.NewGame(pack);
        Assert.Equal(Screen.Home, s.Screen);
        Assert.Equal(1, s.PlayerFlags[7001]);
        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(Screen.Home, loaded.Screen);
        Assert.Equal(1, loaded.PlayerFlags[7001]);                                   // 다시 재생했다면 2가 된다
        Assert.Null(loaded.CurrentEvent);
    }

    [Fact]
    public void The_opening_screen_offers_no_save_so_a_save_is_never_made_mid_opening()
    {
        var s = GameSession.NewGame(SamplePackWith(Opening(3003), Events(3003), Flows("3003\t1\tAdv\t4001\t0\t0\n")));
        Assert.Equal(Screen.EventPlay, s.Screen);
        Assert.DoesNotContain(s.Entries, e => e.Code == GameSession.SaveEntryCode);
    }
}
