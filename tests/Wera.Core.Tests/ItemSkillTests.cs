using Wera.Core;

namespace Wera.Core.Tests;

// 커먼/유니크 아이템 인벤토리 마스터의 선택 열 skill_code와 유니크 아이템 인스턴스가 가진 스킬 인스턴스.
public class ItemSkillTests
{
    const string Tags = "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n";

    static GamePack Pack(string common, string unique, string skills = "code\tname\n1001\t베기\n1002\t불꽃\n") => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        (GamePack.TagMasterPath, Tags),
        (GamePack.CommonItemInventoryMasterPath, common),
        (GamePack.CommonItemTagMasterPath, "item_code\ttag_code\n2\t3\n"),
        (GamePack.UniqueItemInventoryMasterPath, unique),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n7\tdurability\n"),
        (GamePack.SkillStatusMasterPath, "skill_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n1001\t7\t50\t0\t99\n"),
        (GamePack.SkillMasterPath, skills)));

    const string Common = "inventory_code\titem_name\tcarry_limit\tskill_code\n2\t금화\t99999\t\n3\t약초\t99\t1002\n";
    const string Unique = "inventory_code\titem_name\tskill_code\n8001\t철검\t1001\n8002\t대검\t\n";

    [Fact]
    public void Skill_code_is_optional_and_read_by_item()
    {
        var pack = Pack(Common, Unique);
        Assert.Equal((null, 1002L), (pack.FindItem(2)!.SkillCode, pack.FindItem(3)!.SkillCode));
        Assert.Equal((1001L, null), (pack.FindUniqueItem(8001)!.SkillCode, pack.FindUniqueItem(8002)!.SkillCode));

        var old = Pack("inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n", "inventory_code\titem_name\n8001\t철검\n");   // 열이 없는 예전 마스터
        Assert.Null(old.FindItem(2)!.SkillCode);
        Assert.Null(old.FindUniqueItem(8001)!.SkillCode);
    }

    [Fact]
    public void Skill_codes_must_be_in_the_skill_master()
    {
        string Err(string common, string unique, string skills = "code\tname\n1001\t베기\n1002\t불꽃\n") =>
            Assert.Throws<PackFormatException>(() => Pack(common, unique, skills)).Message;
        Assert.Contains("아이템 '3'의 skill_code '9999'가 스킬 마스터에 없습니다", Err("inventory_code\titem_name\tcarry_limit\tskill_code\n2\t금화\t99999\t\n3\t약초\t99\t9999\n", Unique));
        Assert.Contains("유니크 아이템 '8001'의 skill_code '9999'가 스킬 마스터에 없습니다", Err(Common, "inventory_code\titem_name\tskill_code\n8001\t철검\t9999\n"));
        Assert.Contains("skill_code가 있으려면", Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.TagMasterPath, Tags),
            (GamePack.CommonItemInventoryMasterPath, Common), (GamePack.CommonItemTagMasterPath, "item_code\ttag_code\n2\t3\n")))).Message);
    }

    [Fact]
    public void A_new_unique_item_instance_holds_a_skill_instance_with_the_master_statuses()
    {
        var pack = Pack(Common, Unique);
        var sword = pack.NewUniqueItemInstance("a", 8001, 0);
        Assert.Equal(1001, sword.Skill!.SkillCode);
        Assert.Equal(new Dictionary<long, long> { [7] = 50 }, sword.Skill.Statuses);
        Assert.Null(pack.NewUniqueItemInstance("b", 8002, 0).Skill);                  // skill_code가 없는 아이템

        var session = GameSession.NewGame(pack);
        var id = session.AddUniqueItem(8001)!;
        Assert.Equal(1001, session.UniqueItemInstances[id].Skill!.SkillCode);
    }

    [Fact]
    public void The_skill_instance_takes_part_in_instance_equality()
    {
        var a = new UniqueItemInstance("a", 8001, 0, null, null, new SkillInstance(1001));
        Assert.Equal(a, new UniqueItemInstance("a", 8001, 0, null, null, new SkillInstance(1001)));
        Assert.NotEqual(a, new UniqueItemInstance("a", 8001, 0, null, null, new SkillInstance(1002)));
        Assert.NotEqual(a, new UniqueItemInstance("a", 8001, 0, null, null, null));
    }
}
