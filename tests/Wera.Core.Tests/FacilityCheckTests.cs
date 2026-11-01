using System.IO.Compression;
using Wera.Core;

namespace Wera.Core.Tests;

// 시설 확인: 아지트 화면에서 들어가는 읽기 전용 화면. 층마다 기본 시설, 지은 시설의 오퍼레이터(슬롯, 선 캐릭터, 최대 방어력), 시설 방어력, 생산 내용(시설 흐름을 읽은 정적 설명)을 보여 준다.
public class FacilityCheckTests
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

    // 정문(시설 1)에 경비 슬롯(2)이 있고 경비 방어력은 7이다. 정문의 방어력은 9.
    static GameSession Session(params (string Path, string Text)[] extra)
    {
        var pack = SamplePackWith([
            ("master/facility_defense_master_v1.tsv", "facility_code\tdefense\n1\t9\n2\t30\n"),
            ("master/facility_operator_slot_master_v1.tsv", "facility_code\toperator_slot_code\n2\t1\n1\t2\n"),
            ("master/operator_slot_defense_flow_master_v1.tsv", "operator_slot_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n2\t1\tAddDefense\t7\t0\t0\n"),
            .. extra]);
        var s = GameSession.NewGame(pack);
        s.NextRandom = n => n - 1;
        return s;
    }

    static string Joined(GameSession s) => string.Join("\n", s.FacilityCheckLines.Select(l => new string(' ', l.Indent * 2) + l.Text));

    [Fact]
    public void The_hideout_screen_opens_the_read_only_facility_check_and_back_returns_to_the_hideout()
    {
        var s = Session();
        s.Activate(GameSession.HideoutEntryCode);
        Assert.Equal(Screen.Hideout, s.Screen);
        Assert.Contains(s.Entries, e => e.Code == GameSession.FacilityCheckEntryCode && e.Label == "시설 확인");
        Assert.Empty(s.FacilityCheckLines);                                                  // 다른 화면에서는 비어 있다
        s.Activate(GameSession.FacilityCheckEntryCode);
        Assert.Equal(Screen.FacilityCheck, s.Screen);
        Assert.Equal(GameSession.BackEntryCode, Assert.Single(s.Entries).Code);
        Assert.NotEmpty(s.FacilityCheckLines);
        s.Activate(GameSession.BackEntryCode);
        Assert.Equal(Screen.Hideout, s.Screen);
    }

    [Fact]
    public void Each_floor_lists_its_facilities_with_operators_with_their_defense_and_the_facility_defense()
    {
        var s = Session();
        s.AddCharacter(7001);
        Assert.True(s.SetHideoutOperator(0, false, 2, 7001));
        s.Activate(GameSession.HideoutEntryCode);
        s.Activate(GameSession.FacilityCheckEntryCode);
        var text = Joined(s);
        Assert.Contains("1층", text);
        Assert.Contains("[기본 시설] 정문", text);
        Assert.Contains("오퍼레이터 경비: 용사 (최대 방어력 7)", text);
        Assert.Contains("시설 방어력: 9", text);
        Assert.Contains("생산: 생산 없음", text);                                              // 정문에는 시설 흐름이 없다
        var emptied = Session();
        emptied.Activate(GameSession.HideoutEntryCode);
        emptied.Activate(GameSession.FacilityCheckEntryCode);
        Assert.Contains("오퍼레이터 경비: 비어 있음 (최대 방어력 0)", Joined(emptied));
    }

    [Fact]
    public void Production_is_read_from_the_flow_including_submodules_and_notes_what_may_vary()
    {
        var s = Session();
        var iron = s.Pack.FindItem(4)!.Name;
        Assert.Equal($"{iron} +3, {iron} +1 (오퍼레이터에 따라 달라질 수 있음)", s.FacilityProductionText(2));
        Assert.Equal("생산 없음", s.FacilityProductionText(1));

        var sub = Session(
            ("master/facility_flow_master_v1.tsv", "facility_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n2\t1\tJumpRandom\t50\t100\t3\n2\t2\tJumpToSubmodule\t7\t0\t0\n2\t3\tFinish\t0\t0\t0\n"),
            ("master/facility_submodule_master_v1.tsv", "submodule_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n7\t1\tAddInventory\t4\t-2\t0\n"));
        Assert.Equal($"{iron} -2 (조건·확률에 따라 달라질 수 있음)", sub.FacilityProductionText(2));
    }

    [Fact]
    public void A_floor_without_any_facility_says_so_and_the_screen_does_not_change_the_game()
    {
        var s = Session();
        s.Activate(GameSession.HideoutEntryCode);
        var before = s.ToSaveJson();
        s.Activate(GameSession.FacilityCheckEntryCode);
        s.Activate(GameSession.BackEntryCode);
        var again = s.ToSaveJson();
        Assert.Equal(before, again);
        s.Activate(GameSession.FacilityCheckEntryCode);
        Assert.Equal(s.HideoutFloorCount, s.FacilityCheckLines.Count(l => l.Indent == 0));
        Assert.Equal(1, GameSession.FromSaveJson(s.Pack, again).HideoutFloorCount);
    }
}
