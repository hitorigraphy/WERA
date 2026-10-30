using Wera.Core;

namespace Wera.Core.Tests;

// 스킬 스테이터스 마스터, 유니크 아이템 스테이터스 마스터와 SkillInstance/UniqueItemInstance의 Statuses.
public class OwnerStatusTests
{
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
        "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n";
    const string Statuses = "status_code\tstatus_name\n1\tlevel\n2\thp\n6\tskill_level\n7\tdurability\n";
    const string SkillHead = "skill_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n";
    const string ItemHead = "item_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n";

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(string? skillStatus = null, string? itemStatus = null) =>
        GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv", Characters),
            (GamePack.StatusMasterPath, Statuses),
            (GamePack.CharacterStatusMasterPath, "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
                Rows("1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 25 0 40")),
            (GamePack.SkillMasterPath, "code\tname\n1\t베기\n2\t불꽃\n"),
            (GamePack.UniqueItemInventoryMasterPath, "inventory_code\titem_name\n8001\t철검\n8002\t대검\n"),
            (GamePack.SkillStatusMasterPath, skillStatus ?? SkillHead + Rows("1 6 1 1 10")),
            (GamePack.UniqueItemStatusMasterPath, itemStatus ?? ItemHead + Rows("8001 7 100 0 100"))));

    static string LoadError(string skillStatus, string itemStatus) =>
        Assert.Throws<PackFormatException>(() => Pack(skillStatus, itemStatus)).Message;

    [Fact]
    public void The_masters_are_looked_up_by_owner()
    {
        var pack = Pack(SkillHead + Rows("1 6 1 1 10", "1 7 5 0 9", "2 6 3 1 10"), ItemHead + Rows("8001 7 100 0 100", "8001 6 2 0 5"));
        Assert.Equal([6L, 7L], pack.SkillStatusesOf(1).Select(r => r.StatusCode));
        Assert.Equal(new OwnerStatus(1, 7, 5, 0, 9), pack.SkillStatusesOf(1)[1]);
        Assert.Equal([6L], pack.SkillStatusesOf(2).Select(r => r.StatusCode));
        Assert.Equal([6L, 7L], pack.UniqueItemStatusesOf(8001).Select(r => r.StatusCode));
        Assert.Empty(pack.UniqueItemStatusesOf(8002));
        Assert.Equal(new Dictionary<long, long> { [6] = 1, [7] = 5 }.OrderBy(kv => kv.Key), pack.MasterStatusesOfSkill(1).OrderBy(kv => kv.Key));
        Assert.Equal(100, pack.MasterStatusesOfUniqueItem(8001)[7]);
        Assert.Equal(3, pack.NewSkillInstance(2).Statuses![6]);
        Assert.Equal(2, pack.SkillStatuses!.Count(r => r.OwnerCode == 1));
        Assert.Equal(2, pack.UniqueItemStatuses!.Count);
    }

    [Fact]
    public void The_masters_are_optional_and_null_without_the_file()
    {
        var pack = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Null(pack.SkillStatuses);
        Assert.Null(pack.UniqueItemStatuses);
        Assert.Empty(pack.MasterStatusesOfSkill(1));
    }

    [Fact]
    public void Invalid_rows_are_rejected_at_load()
    {
        Assert.Contains("스킬 '9'", LoadError(SkillHead + Rows("9 6 1 1 10"), ItemHead));
        Assert.Contains("스테이터스 '99'", LoadError(SkillHead + Rows("1 99 1 1 10"), ItemHead));
        Assert.Contains("범위 밖", LoadError(SkillHead + Rows("1 6 11 1 10"), ItemHead));
        Assert.Contains("최솟값", LoadError(SkillHead + Rows("1 6 5 8 3"), ItemHead));
        Assert.Contains("두 번", LoadError(SkillHead + Rows("1 6 1 1 10", "1 6 2 1 10"), ItemHead));
        Assert.Contains("유니크 아이템 '9'", LoadError(SkillHead, ItemHead + Rows("9 7 1 0 1")));
        Assert.Contains("스테이터스 '99'", LoadError(SkillHead, ItemHead + Rows("8001 99 1 0 1")));
        Assert.Contains("두 번", LoadError(SkillHead, ItemHead + Rows("8001 7 1 0 1", "8001 7 1 0 1")));
    }

    [Fact]
    public void A_master_needs_its_owner_master_and_the_status_master()
    {
        var noSkills = Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.StatusMasterPath, Statuses),
            (GamePack.SkillStatusMasterPath, SkillHead))));
        Assert.Contains(GamePack.SkillMasterPath, noSkills.Message);
    }

    [Fact]
    public void A_new_unique_item_copies_the_master_statuses_and_changing_them_leaves_the_master()
    {
        var pack = Pack();
        var s = GameSession.NewGame(pack);
        var id = s.AddUniqueItem(8001)!;
        Assert.Equal(100, s.UniqueItemInstances[id].Statuses![7]);
        Assert.Empty(s.UniqueItemInstances[s.AddUniqueItem(8002)!].Statuses!);       // 행이 없는 아이템은 빈 Statuses
        s.UniqueItemInstances[id].Statuses![7] = 40;
        Assert.Equal(100, pack.MasterStatusesOfUniqueItem(8001)[7]);
    }

    [Fact]
    public void Skill_and_unique_item_statuses_survive_the_save()
    {
        var pack = Pack();
        var s = GameSession.NewGame(pack);
        var id = s.AddUniqueItem(8001)!;
        s.UniqueItemInstances[id].Statuses![7] = 40;
        s.AddCharacter(7001);
        var skill = pack.NewSkillInstance(1);
        skill.Statuses![6] = 4;
        s.Characters[7001] = s.Characters[7001] with { SkillPool = [skill] };

        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(40, loaded.UniqueItemInstances[id].Statuses![7]);
        Assert.Equal(4, loaded.Characters[7001].SkillPool[0].Statuses![6]);
        Assert.Equal(s.UniqueItemInstances[id], loaded.UniqueItemInstances[id]);
        Assert.Equal(s.Characters[7001], loaded.Characters[7001]);
    }

    [Fact]
    public void A_save_without_statuses_copies_the_master_and_an_unknown_status_is_rejected()
    {
        var pack = Pack();
        var s = GameSession.NewGame(pack);
        var id = s.AddUniqueItem(8001)!;
        s.AddCharacter(7001);
        s.Characters[7001] = s.Characters[7001] with { SkillPool = [new SkillInstance(1)] };       // Statuses 없음(null)

        var json = System.Text.Json.Nodes.JsonNode.Parse(s.ToSaveJson())!;
        json["UniqueItems"]![id]!.AsObject().Remove("Statuses");
        var loaded = GameSession.FromSaveJson(pack, json.ToJsonString());
        Assert.Equal(100, loaded.UniqueItemInstances[id].Statuses![7]);
        Assert.Equal(1, loaded.Characters[7001].SkillPool[0].Statuses![6]);

        s.UniqueItemInstances[id].Statuses![77] = 1;
        Assert.Contains("스테이터스 '77'", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, s.ToSaveJson())).Message);
        s.UniqueItemInstances[id].Statuses!.Remove(77);
        s.Characters[7001] = s.Characters[7001] with { SkillPool = [new SkillInstance(1, new Dictionary<long, long> { [77] = 1 })] };
        Assert.Contains("스테이터스 '77'", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, s.ToSaveJson())).Message);
    }
}
