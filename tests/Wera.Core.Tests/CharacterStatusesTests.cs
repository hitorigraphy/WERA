using Wera.Core;

namespace Wera.Core.Tests;

// 캐릭터 인스턴스의 Statuses(스테이터스 코드 → 값), SkillPool(익힌 스킬들), SkillSlots(스킬 슬롯 코드 → SkillPool 번호).
public class CharacterStatusesTests
{
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
        "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n";

    static GamePack Pack(string statusMaster, string characterStatus) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/character_inventory_master_v1.tsv", Characters),
        (GamePack.StatusMasterPath, statusMaster),
        (GamePack.CharacterStatusMasterPath, characterStatus),
        (GamePack.SkillSlotMasterPath, "code\tname\n1\t슬롯1\n2\t슬롯2\n"),
        (GamePack.SkillMasterPath, "code\tname\n1\t베기\n2\t불꽃\n")));

    const string Statuses = "status_code\tstatus_name\n1\tlevel\n2\thp\n3\txp\n4\tmax_hp\n5\txp_max\n6\tmana\n";
    const string Head = "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n";

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack FullPack() => Pack(Statuses, Head + Rows(
        "1 1 0 1 1 99", "1 2 0 40 0 40", "1 3 0 0 0 30", "1 4 0 40 0 9999", "1 5 0 30 0 999999",
        "7001 1 0 1 1 99", "7001 2 0 25 0 40", "7001 3 0 4 0 30", "7001 4 0 60 0 9999", "7001 5 0 80 0 999999", "7001 6 0 12 0 50",
        "7002 1 0 1 1 99", "7002 2 0 20 0 20"));

    [Fact]
    public void A_new_character_gets_every_status_the_master_lists_for_it()
    {
        var c = FullPack().NewCharacterInstance(7001);
        Assert.Equal(
            new Dictionary<long, long> { [1] = 1, [2] = 25, [3] = 4, [4] = 60, [5] = 80, [6] = 12, [8] = 0, [9] = 0 }.OrderBy(kv => kv.Key),
            c.Statuses.OrderBy(kv => kv.Key));
        Assert.Equal((1L, 25L, 60L, 4L, 80L), (c.Level, c.Hp, c.MaxHp, c.Xpp, c.XppMax));
        Assert.Empty(c.SkillPool);
        Assert.Empty(c.SkillSlots);
    }

    [Fact]
    public void Max_hp_and_xpp_max_fall_back_to_the_hp_and_xpp_row_maximums_and_then_to_defaults()
    {
        var pack = FullPack();
        var c = pack.NewCharacterInstance(7002);                                 // max_hp, xpp, xpp_max 행이 없다
        Assert.Equal((1L, 20L, 20L, 0L, 0L), (c.Level, c.Hp, c.MaxHp, c.Xpp, c.XppMax));
        Assert.Equal([1L, 2L, 3L, 4L, 5L, 8L, 9L], c.Statuses.Keys.Order());      // 예약 코드는 늘 들어간다(exp 8, exp_max 9 포함)

        var bare = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"))).NewCharacterInstance(7001);
        Assert.Equal((1L, 1L, 1L, 0L, 0L), (bare.Level, bare.Hp, bare.MaxHp, bare.Xpp, bare.XppMax));
    }

    [Fact]
    public void Reserved_codes_are_five_and_the_clamp_uses_the_max_status()
    {
        Assert.Equal([1L, 2L, 3L, 4L, 5L],
            [GamePack.LevelStatusCode, GamePack.HpStatusCode, GamePack.XppStatusCode, GamePack.MaxHpStatusCode, GamePack.XppMaxStatusCode]);
        var c = CharacterInstance.Stats(0, 99, 10, 99, 20).Clamped();
        Assert.Equal((1L, 10L, 10L, 20L, 20L), (c.Level, c.Hp, c.MaxHp, c.Xpp, c.XppMax));
        var healed = CharacterInstance.Stats(1, 5, 10, 0, 0).WithStatus(GamePack.HpStatusCode, 7);
        Assert.Equal(7, healed.Hp);
    }

    [Fact]
    public void WithStatus_makes_a_new_instance_and_leaves_the_old_one()
    {
        var old = CharacterInstance.Stats(1, 5, 10, 0, 0);
        var changed = old.WithStatus(6, 3);
        Assert.False(old.Statuses.ContainsKey(6));
        Assert.Equal(3, changed.Statuses[6]);
        Assert.NotEqual(old, changed);
        Assert.Equal(old, CharacterInstance.Stats(1, 5, 10, 0, 0));          // 내용이 같으면 같다
    }

    [Fact]
    public void Statuses_skill_pool_and_skill_slots_survive_the_save()
    {
        var pack = FullPack();
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);
        s.Characters[7001] = s.Characters[7001].WithStatus(6, 33) with
        {
            SkillPool = [pack.NewSkillInstance(1), pack.NewSkillInstance(2)],
            SkillSlots = new Dictionary<long, long> { [1] = 1, [2] = 0 },
        };
        var json = s.ToSaveJson();
        Assert.DoesNotContain("\"Hp\"", json);                                       // 고정 필드는 더 이상 쓰지 않는다
        var loaded = GameSession.FromSaveJson(pack, json);
        Assert.Equal(s.Characters[7001], loaded.Characters[7001]);
        Assert.Equal(33, loaded.Characters[7001].Statuses[6]);
        Assert.Equal([1L, 2L], loaded.Characters[7001].SkillPool.Select(k => k.SkillCode));
        Assert.Equal(1, loaded.Characters[7001].SkillSlots[1]);
    }

    [Fact]
    public void An_old_save_with_fixed_fields_is_moved_into_statuses()
    {
        var pack = FullPack();
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);
        // 7001의 항목을 예전 형식(Level, Hp, MaxHp, Xpp, XppMax만 있고 Statuses가 없음)으로 바꿔 끼운다.
        var root = System.Text.Json.Nodes.JsonNode.Parse(s.ToSaveJson())!;
        root["Characters"]!["7001"] = new System.Text.Json.Nodes.JsonObject
        {
            ["InstanceId"] = s.Characters[7001].InstanceId, ["CharacterCode"] = 7001, ["OrderNumber"] = 1,
            ["Level"] = 5, ["Hp"] = 30, ["MaxHp"] = 44, ["Xpp"] = 3, ["XppMax"] = 77,
        };
        var loaded = GameSession.FromSaveJson(pack, root.ToJsonString());
        var c = loaded.Characters[7001];
        Assert.Equal((5L, 30L, 44L, 3L, 77L), (c.Level, c.Hp, c.MaxHp, c.Xpp, c.XppMax));
        Assert.Equal(5, c.Statuses[GamePack.LevelStatusCode]);
        Assert.Null(c.LegacyLevel);
        Assert.DoesNotContain("\"Hp\"", loaded.ToSaveJson());                       // 다시 저장하면 새 형식이다
    }

    [Fact]
    public void A_save_pointing_at_unknown_skills_slots_or_pool_numbers_is_rejected()
    {
        var pack = FullPack();
        string With(Func<CharacterInstance, CharacterInstance> change)
        {
            var s = GameSession.NewGame(pack);
            s.AddCharacter(7001);
            s.Characters[7001] = change(s.Characters[7001]);
            return s.ToSaveJson();
        }

        Assert.NotNull(GameSession.FromSaveJson(pack, With(c => c with { SkillPool = [new SkillInstance(2)], SkillSlots = new Dictionary<long, long> { [2] = 0 } })));
        Assert.Contains("스킬 '9'", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, With(c => c with { SkillPool = [new SkillInstance(9)] }))).Message);
        Assert.Contains("스킬 슬롯 '9'", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack,
            With(c => c with { SkillPool = [new SkillInstance(1)], SkillSlots = new Dictionary<long, long> { [9] = 0 } }))).Message);
        Assert.Contains("번호 1", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack,
            With(c => c with { SkillPool = [new SkillInstance(1)], SkillSlots = new Dictionary<long, long> { [1] = 1 } }))).Message);
        Assert.Contains("스테이터스 '77'", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, With(c => c.WithStatus(77, 1)))).Message);
    }
}
