using System.IO.Compression;
using Wera.Core;

namespace Wera.Core.Tests;

// 아지트가 여럿인 게임팩: hideout_master와 선택 열 hideout_code, 현재 아지트 전환(SwitchHideout), 아지트별 상태 보존, 세이브.
public class MultiHideoutTests
{
    static string Root()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        return root;
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

    const string Masters = "master/hideout_master_v1.tsv";
    const string Floors = "master/hideout_floor_master_v1.tsv";
    const string Expands = "master/hideout_expand_master_v1.tsv";
    const string Enemies = "master/hideout_defense_enemy_master_v1.tsv";
    const string Settings = "master/game_setting_master_v1.tsv";
    const string Events = "master/event_flow_master_v1.tsv";
    const string TwoHideouts = "hideout_code\tname\n1\t본거지\n2\t별장\n";
    const string FlowHeader = "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
    const string EnemyHeader = "hideout_code\tcode\tpreset_party_code\tmin_threat\tconsume_threat\tpriority\tchance\n";

    static string SampleText(string path)
    {
        using var zip = ZipFile.OpenRead(Path.Combine(Root(), "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        using var r = new StreamReader(zip.GetEntry(path)!.Open());
        return r.ReadToEnd();
    }

    static GamePack Two(params (string Path, string Text)[] extra) =>
        SamplePackWith([(Masters, TwoHideouts), .. extra]);

    static void Throws(string expected, params (string Path, string Text)[] extra)
    {
        var e = Assert.Throws<PackFormatException>(() => SamplePackWith(extra));
        Assert.Contains(expected, e.Message);
    }

    // ---- 아지트 마스터가 없으면 지금까지와 같다 ----

    [Fact]
    public void Without_a_hideout_master_there_is_one_hideout_with_code_1()
    {
        var s = GameSession.NewGame(SamplePackWith());
        Assert.Null(s.Pack.Hideouts);
        Assert.Equal(GamePack.DefaultHideoutCode, s.CurrentHideoutCode);
        Assert.Null(s.CurrentHideoutName);
        Assert.Equal([1L], s.HideoutCodes);
        Assert.True(s.SwitchHideout(1));
        Assert.False(s.SwitchHideout(2));
        Assert.Equal(1, s.HideoutFloorCount);
    }

    // ---- 아지트별 마스터 ----

    [Fact]
    public void The_start_hideout_is_the_first_row_or_the_game_setting()
    {
        Assert.Equal(1, GameSession.NewGame(Two()).CurrentHideoutCode);
        var s = GameSession.NewGame(Two((Settings, SampleText(Settings) + "StartHideoutCode\tLong\t2\t\t\t시작 아지트\n")));
        Assert.Equal(2, s.CurrentHideoutCode);
        Assert.Equal("별장", s.CurrentHideoutName);
    }

    [Fact]
    public void A_hideout_specific_floor_row_replaces_the_shared_row_only_for_that_hideout()
    {
        var pack = Two((Floors, "hideout_code\tfloor_order\tfacility_code\n\t1\t1\n2\t1\t2\n"));
        Assert.Equal(1, pack.BaseFacilityOf(1, 1));
        Assert.Equal(2, pack.BaseFacilityOf(2, 1));
        var s = GameSession.NewGame(pack);
        Assert.Equal(1, s.Pack.BaseFacilityOf(s.CurrentHideoutCode, 1));
        Assert.True(s.SwitchHideout(2));
        Assert.Equal(2, s.Pack.BaseFacilityOf(s.CurrentHideoutCode, 1));
    }

    [Fact]
    public void Shared_expand_rows_apply_to_every_hideout_and_specific_rows_replace_the_whole_count()
    {
        var pack = Two((Expands, "hideout_code\texpand_count\tgroup_code\titem_code\tamount\n"
            + "\t1\t1\t2\t100\n\t2\t1\t2\t300\n2\t1\t1\t2\t500\n"));
        Assert.Equal(100, pack.HideoutExpandGroupsOf(1, 1)[0].Costs[0].Amount);
        Assert.Equal(500, pack.HideoutExpandGroupsOf(2, 1)[0].Costs[0].Amount);
        Assert.Equal(300, pack.HideoutExpandGroupsOf(2, 2)[0].Costs[0].Amount);
    }

    [Fact]
    public void Defense_enemies_are_chosen_per_hideout()
    {
        var pack = Two((Enemies, EnemyHeader + "\t1\t2\t10\t10\t1\t100\n2\t1\t1\t30\t30\t1\t100\n2\t2\t2\t10\t10\t2\t100\n"));
        Assert.Single(pack.DefenseEnemiesOf(1));
        Assert.Equal(2, pack.DefenseEnemiesOf(1)[0].PresetPartyCode);
        Assert.Equal([1L, 2L], pack.DefenseEnemiesOf(2).Select(e => e.Code).Order().ToArray());
        Assert.Equal(1, pack.DefenseEnemiesOf(2).First(e => e.Code == 1).PresetPartyCode);
    }

    // ---- 전환과 상태 보존 ----

    static GameSession TwoSession()
    {
        var s = GameSession.NewGame(Two(("master/facility_operator_slot_master_v1.tsv", "facility_code\toperator_slot_code\n2\t1\n1\t2\n")));
        s.InventoryItems[2] = new InventoryItem(1000);
        s.InventoryItems[4] = new InventoryItem(100);
        s.InventoryItems[5] = new InventoryItem(100);
        return s;
    }

    [Fact]
    public void Switching_keeps_each_hideouts_floors_and_parties()
    {
        var s = TwoSession();
        Assert.True(s.ExpandHideout(1));
        Assert.Equal(2, s.HideoutFloorCount);
        Assert.True(s.SwitchHideout(2));
        Assert.Equal(1, s.HideoutFloorCount);                       // 별장은 처음 상태
        Assert.True(s.ExpandHideout(1));
        Assert.True(s.ExpandHideout(1));
        Assert.Equal(3, s.HideoutFloorCount);
        Assert.True(s.SwitchHideout(1));
        Assert.Equal(2, s.HideoutFloorCount);
        Assert.True(s.SwitchHideout(2));
        Assert.Equal(3, s.HideoutFloorCount);
    }

    [Fact]
    public void A_character_stands_in_one_hideout_at_a_time_and_placing_elsewhere_moves_them()
    {
        var s = TwoSession();
        s.AddCharacter(7001);
        Assert.True(s.SetHideoutOperator(0, false, 2, 7001));
        Assert.True(s.SwitchHideout(2));
        Assert.Null(s.HideoutOperatorOf(0, false, 2));                // 다른 아지트의 자리는 따로다
        Assert.True(s.SetHideoutOperator(0, false, 2, 7001));         // 별장에 세우면 본거지에서 빠진다
        Assert.True(s.SwitchHideout(1));
        Assert.Null(s.HideoutOperatorOf(0, false, 2));
        Assert.True(s.SwitchHideout(2));
        Assert.Equal(7001, s.HideoutOperatorOf(0, false, 2));
    }

    [Fact]
    public void Defense_battle_only_uses_the_current_hideout()
    {
        var s = GameSession.NewGame(Two((Enemies, EnemyHeader + "1\t1\t2\t1\t100\t1\t100\n")));
        s.PlayerFlags[100] = 100;
        s.NextRandom = n => n - 1;
        Assert.True(s.SwitchHideout(2));
        Assert.Empty(s.RunDefense());                               // 별장에는 적이 없다
        Assert.Equal(100, s.PlayerFlags[100]);
        Assert.True(s.SwitchHideout(1));
        Assert.NotEmpty(s.RunDefense());
        Assert.Equal(0, s.PlayerFlags[100]);
    }

    // ---- 이벤트에서 전환 ----

    [Fact]
    public void An_event_flow_switches_the_hideout_after_a_flag_check()
    {
        var pack = Two((Events, FlowHeader
            + "3001\t1\tJumpFlagLessThan\t1001\t1\t3\n3001\t2\tSwitchHideout\t2\t0\t0\n3001\t3\tFinish\t0\t0\t0\n"));
        var s = GameSession.NewGame(pack);
        void Run() { var run = s.StartEvent(3001)!; while (run.Advance().Kind != EventStepKind.Finished) { } }
        s.PlayerFlags[1001] = 0;
        Run();
        Assert.Equal(1, s.CurrentHideoutCode);
        s.PlayerFlags[1001] = 1;
        Run();
        Assert.Equal(2, s.CurrentHideoutCode);
    }

    // ---- 세이브 ----

    [Fact]
    public void The_save_keeps_every_hideout_and_the_current_one()
    {
        var s = TwoSession();
        s.AddCharacter(7001);
        Assert.True(s.SetHideoutOperator(0, false, 2, 7001));
        Assert.True(s.ExpandHideout(1));
        Assert.True(s.SwitchHideout(2));
        Assert.True(s.ExpandHideout(1));
        Assert.True(s.ExpandHideout(1));
        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal(2, loaded.CurrentHideoutCode);
        Assert.Equal(3, loaded.HideoutFloorCount);
        Assert.True(loaded.SwitchHideout(1));
        Assert.Equal(2, loaded.HideoutFloorCount);
        Assert.Equal(7001, loaded.HideoutOperatorOf(1, false, 2));     // 확장해서 처음 층이 한 칸 내려갔다
    }

    [Fact]
    public void A_save_with_an_unknown_current_hideout_is_rejected()
    {
        var s = TwoSession();
        var json = s.ToSaveJson();
        var bad = System.Text.RegularExpressions.Regex.Replace(json, @"""CurrentHideout""\s*:\s*1", "\"CurrentHideout\":9");
        Assert.NotEqual(json, bad);
        Assert.ThrowsAny<Exception>(() => GameSession.FromSaveJson(s.Pack, bad));
    }

    // ---- 검증 ----

    [Fact]
    public void Bad_hideout_data_is_rejected_when_the_pack_loads()
    {
        Throws("아지트 '9'가", (Masters, TwoHideouts), (Floors, "hideout_code\tfloor_order\tfacility_code\n9\t1\t1\n"));
        Throws("hideout_code '2'가 있으려면", (Floors, "hideout_code\tfloor_order\tfacility_code\n2\t1\t1\n"));
        Throws("StartHideoutCode", (Settings, SampleText(Settings) + "StartHideoutCode\tLong\t5\t\t\t\n"));
        Throws("StartHideoutCode", (Masters, TwoHideouts), (Settings, SampleText(Settings) + "StartHideoutCode\tLong\t5\t\t\t\n"));
        Throws("SwitchHideout", (Masters, TwoHideouts), (Events, FlowHeader + "3001\t1\tSwitchHideout\t7\t0\t0\n"));
        Throws("SwitchHideout", (Events, FlowHeader + "3001\t1\tSwitchHideout\t2\t0\t0\n"));
    }

    // ---- 모드 병합 ----

    [Fact]
    public void The_hideout_code_column_is_an_optional_key_in_the_merge_registry()
    {
        var key = PackMasterKeys.All[GamePack.HideoutFloorMasterPath];
        Assert.Equal(["hideout_code", "floor_order"], key.Key);
        Assert.Equal(["hideout_code"], key.OptionalKey);
    }
}
