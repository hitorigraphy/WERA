using System.IO.Compression;
using Wera.Core;

namespace Wera.Core.Tests;

// 구금된 아군: 던전 안 전투에서 졌을 때 전투 패배 후 서킷의 CaptureFocusedCharacter 이펙트가 정한 캐릭터가 구금된 아군 태그를 얻어 그 던전에 갇힌다.
// 구금된 아군(과 그가 낀 장비)은 어디에도 나오지 않는다. JumpNotInDungeon은 던전 안 전투가 아니면 점프한다. 샘플 게임팩의 구금된 아군 태그는 11이다.
public class CapturedAllyTests
{
    const string Header = "execute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
    const string Capture = Header + "1\tCaptureFocusedCharacter\t0\t0\t0\n";

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

    const string RunnableBattle = "code\tname\n5001\t전사\n";

    // 파티-B에 용사(7001) 혼자 세워 던전 1999의 고정 전투에서 지고 홈으로 돌아온 세션.
    static GameSession LostDungeonWith(string effects)
    {
        var s = GameSession.NewGame(SamplePackWith(("master/after_battle_lose_effect_master_v1.tsv", effects)));
        s.Pick("105"); s.Pick("1"); s.Pick("1"); s.Pick("999");
        s.AddCharacter(7001);
        s.Parties[1].Units[0] = s.Parties[0].Units[0]! with { CharacterCode = 7001, InstanceId = "" };
        s.Pick("101"); s.Pick("1999"); s.Pick("12");
        s.NextRandom = _ => 99;
        foreach (var c in "RRDDRRUURRDD") s.PressDungeonKey("wsad"["UDLR".IndexOf(c)]);
        s.NextRandom = _ => 1;
        s.PressDungeonKey('d');
        s.NextRandom = _ => 0;
        foreach (var enemy in s.EnemyInstances.Values)
        {
            enemy.Statuses[GamePack.HpStatusCode] = 100;
            enemy.Statuses[GamePack.MaxHpStatusCode] = 100;
        }
        s.Characters[7001].Statuses[GamePack.HpStatusCode] = 1;
        s.Pick("102"); s.Pick("101"); s.Pick((s.EnemyInstances.Keys.First() + 1).ToString());
        s.Pick("107");
        s.Pick("999");
        return s;
    }

    [Fact]
    public void A_party_member_the_circuit_captures_in_a_lost_dungeon_battle_is_tagged_hidden_and_locked_in_that_dungeon()
    {
        var s = LostDungeonWith(Capture);
        Assert.Equal(Screen.Home, s.Screen);
        var id = s.InstanceIdOf(7001)!;
        Assert.True(s.IsCapturedAlly(id));
        Assert.Contains(11L, s.CharacterTags(id));
        Assert.Equal(id, s.DungeonCaptives[1999].Single().InstanceId);
        Assert.Null(s.Parties[1].Units[0]);                                          // 파티에서 빠졌다
        Assert.DoesNotContain(s.OwnedCharacters, c => c.InstanceId == id);           // 캐릭터 확인에 나오지 않는다
        Assert.Contains(s.OwnedCharacters, c => c.CharacterCode == 1);               // 플레이어는 그대로
    }

    [Fact]
    public void Nothing_is_captured_without_the_effect_and_the_player_is_never_captured()
    {
        var plain = LostDungeonWith(Header + "1\tNone\t0\t0\t0\n");
        Assert.False(plain.IsCapturedAlly(plain.InstanceIdOf(7001)!));
        Assert.Empty(plain.DungeonCaptives);

        var s = GameSession.NewGame(SamplePackWith());
        Assert.False(s.CaptureAlly(s.InstanceIdOf(1)!, 1999));                       // 플레이어는 구금되지 않는다
        s.AddCharacter(7001);
        Assert.True(s.CaptureAlly(s.InstanceIdOf(7001)!, 1999));
        Assert.False(s.CaptureAlly(s.InstanceIdOf(7001)!, 1999));                    // 이미 구금됐다
    }

    [Fact]
    public void JumpNotInDungeon_jumps_outside_a_dungeon_and_captures_inside_one()
    {
        var pack = SamplePackWith(("master/after_battle_lose_effect_master_v1.tsv",
            Header + "1\tJumpNotInDungeon\t3\t0\t0\n2\tCaptureFocusedCharacter\t0\t0\t0\n3\tFinish\t0\t0\t0\n"));
        var character = pack.NewCharacterInstance(7001, "c1", 0);
        var unit = new PartyUnit(7001);
        var captured = new List<string>();
        new AfterBattleLoseCircuit(pack, UnitStore.Of(character), unit, inDungeon: false, capture: c => captured.Add(c.InstanceId)).Execute();
        Assert.Empty(captured);                                                      // 던전 밖: 점프해 구금하지 않는다
        new AfterBattleLoseCircuit(pack, UnitStore.Of(character), unit, inDungeon: true, capture: c => captured.Add(c.InstanceId)).Execute();
        Assert.Equal(["c1"], captured);
        new AfterBattleLoseCircuit(pack, UnitStore.Of(character), unit, inDungeon: false, capture: c => captured.Add(c.InstanceId)).Execute();
        Assert.Single(captured);
    }

    [Fact]
    public void The_captive_and_what_it_wears_survive_the_save_and_the_effect_master_is_validated()
    {
        var s = LostDungeonWith(Capture);
        var id = s.InstanceIdOf(7001)!;
        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.True(loaded.IsCapturedAlly(id));
        Assert.Equal(id, loaded.DungeonCaptives[1999].Single().InstanceId);
        Assert.DoesNotContain(loaded.OwnedCharacters, c => c.InstanceId == id);

        Assert.Contains("점프 대상", Assert.Throws<PackFormatException>(() =>
            SamplePackWith(("master/after_battle_lose_effect_master_v1.tsv", Header + "1\tJumpNotInDungeon\t1\t0\t0\n"))).Message);
        Assert.Contains("점프 대상", Assert.Throws<PackFormatException>(() =>
            SamplePackWith(("master/after_battle_lose_effect_master_v1.tsv", Header + "1\tJumpNotInDungeon\t9\t0\t0\n"))).Message);
    }

    [Fact]
    public void Rescuing_a_dungeon_frees_its_captives_back_to_the_owned_characters_but_not_into_a_party()
    {
        var s = LostDungeonWith(Capture);
        var id = s.InstanceIdOf(7001)!;
        Assert.Equal(0, s.RescueCaptives(1998));                                     // 다른 던전에는 갇힌 이가 없다
        Assert.True(s.IsCapturedAlly(id));
        Assert.Equal(1, s.RescueCaptives(1999));
        Assert.False(s.IsCapturedAlly(id));
        Assert.DoesNotContain(11L, s.CharacterTags(id));
        Assert.Empty(s.DungeonCaptives);
        Assert.Contains(s.OwnedCharacters, c => c.InstanceId == id);                 // 다시 캐릭터 확인에 나온다
        Assert.DoesNotContain(s.Parties.SelectMany(p => p.Units).OfType<PartyUnit>(), u => s.InstanceIdOf(u.CharacterCode) == id);   // 파티에는 돌아오지 않는다
        Assert.Equal(0, s.RescueCaptives(1999));                                     // 두 번째는 할 일이 없다
    }

    [Fact]
    public void The_ReleaseCaptives_flow_calls_the_session_hook_and_does_nothing_without_one()
    {
        var pack = SamplePackWith();
        EventFlow[] flows = [new(1, 1, EventFlowType.ReleaseCaptives, 0, 0, 0), new(1, 2, EventFlowType.SetFlag, 7, 1, 0)];
        var flags = new Dictionary<long, long>();
        var released = 0;
        new EventFlowCircuit(pack, 1, flags, flows: flows) { OnReleaseCaptives = () => released++ }.Advance();
        Assert.Equal(1, released);
        Assert.Equal(1, flags[7]);                                                   // 이어서 다음 칸(클리어 플래그)도 실행한다
        new EventFlowCircuit(pack, 1, new Dictionary<long, long>(), flows: flows).Advance();
        Assert.Equal(1, released);
    }
}
