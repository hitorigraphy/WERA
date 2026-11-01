using Wera.Core;

namespace Wera.Core.Tests;

// 부품 슬롯 마스터, 부품 슬롯 태그 마스터, 장비 상세 화면(부품 슬롯, 강화 횟수, 설명문).
public class PartSlotTests
{
    static GamePack SamplePack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(read);
    }

    static GamePack Pack(string? slots, string? tags, string? uniques = "inventory_code\titem_name\n8001\t철검\n")
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n9\t부품\n"),
        };
        if (uniques is not null) files.Add((GamePack.UniqueItemInventoryMasterPath, uniques));
        if (slots is not null) files.Add((GamePack.PartSlotMasterPath, slots));
        if (tags is not null) files.Add((GamePack.PartSlotTagMasterPath, tags));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    const string Slots = "code\tname\n1\t코어\n2\t날개\n";
    const string TagHead = "item_code\tpart_slot_code\tgroup_number\ttag_code\tis_positive\n";

    [Fact]
    public void The_part_slot_masters_are_optional_and_every_item_has_every_slot()
    {
        Assert.Null(Pack(null, null).PartSlots);
        var pack = Pack(Slots, TagHead + "8001\t1\t1\t9\ttrue\n");
        Assert.Equal([new PartSlotEntry(1, "코어"), new PartSlotEntry(2, "날개")], pack.PartSlots);
        Assert.Equal(["코어", "날개"], pack.PartSlotsOf(8001).Select(s => s.Name));
        Assert.Equal(["코어", "날개"], pack.PartSlotsOf(12345).Select(s => s.Name));          // 태그 행이 없어도 슬롯은 모두 있다
        Assert.Equal([new UniqueItemPartSlotTag(8001, 1, 1, 9, true)], pack.PartSlotTags);
    }

    [Theory]
    [InlineData("8001\t1\t1\t9\ttrue\n8001\t1\t1\t9\ttrue\n")]       // 같은 키 두 번
    [InlineData("9999\t1\t1\t9\ttrue\n")]                            // 없는 유니크 아이템
    [InlineData("8001\t7\t1\t9\ttrue\n")]                            // 없는 부품 슬롯
    [InlineData("8001\t1\t1\t77\ttrue\n")]                           // 없는 태그
    [InlineData("8001\t1\tx\t9\ttrue\n")]                            // 그룹넘버가 정수 아님
    [InlineData("8001\t1\t1\t9\tmaybe\n")]                           // is_positive가 참/거짓 아님
    public void Bad_part_slot_tag_rows_are_rejected(string rows)
        => Assert.Throws<PackFormatException>(() => Pack(Slots, TagHead + rows));

    [Fact]
    public void The_tag_master_needs_the_slot_master_and_the_unique_items_and_codes_must_be_unique()
    {
        Assert.Throws<PackFormatException>(() => Pack(null, TagHead));
        Assert.Throws<PackFormatException>(() => Pack(Slots, TagHead, uniques: null));
        Assert.Throws<PackFormatException>(() => Pack("code\tname\n1\t하나\n1\t중복\n", null));
    }

    [Fact]
    public void A_common_item_fits_a_part_slot_when_the_combined_tags_match_the_rules()
    {
        var pack = SamplePack();
        Assert.True(pack.CanInstallPart(8001, 1, 4));         // 철광석(부품 태그)은 철검의 부품 슬롯 1에
        Assert.False(pack.CanInstallPart(8001, 2, 4));        // 슬롯 2에는 규칙이 없어 아무것도 못 낀다
        Assert.False(pack.CanInstallPart(8001, 1, 3));        // 약초는 부품 태그가 없다
        Assert.False(pack.CanInstallPart(8003, 1, 4));        // 규칙이 없는 장비
    }

    [Fact]
    public void The_unique_item_description_column_is_optional()
    {
        var pack = Pack(null, null, "inventory_code\titem_name\tdescription\n8001\t철검\t날카롭다\n8002\t대검\t\n");
        Assert.Equal("날카롭다", pack.FindUniqueItem(8001)!.Description);
        Assert.Equal("", pack.FindUniqueItem(8002)!.Description);
        Assert.Equal("", Pack(null, null).FindUniqueItem(8001)!.Description);
        var both = Pack(null, null, "inventory_code\titem_name\tdescription\tskill_code\n8001\t철검\t설명\t\n");     // 순서는 자유
        Assert.Equal(("설명", (long?)null), (both.FindUniqueItem(8001)!.Description, both.FindUniqueItem(8001)!.SkillCode));
    }

    [Fact]
    public void The_common_item_description_column_is_optional_and_may_follow_skill_code_in_either_order()
    {
        static GamePack Items(string rows) => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n"),
            (GamePack.CommonItemInventoryMasterPath, rows)));
        Assert.Equal("", Items("inventory_code\titem_name\tcarry_limit\n3\t약초\t9\n").FindItem(3)!.Description);
        Assert.Equal("쓴맛", Items("inventory_code\titem_name\tcarry_limit\tdescription\n3\t약초\t9\t쓴맛\n").FindItem(3)!.Description);
    }

    [Fact]
    public void The_skill_master_optional_columns_come_in_any_order()
    {
        static GamePack Skills(string rows) => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            (GamePack.SkillMasterPath, rows)));
        Assert.Equal("", Skills("code\tname\n1\t베기\n").FindSkill(1)!.Description);
        var skill = Skills("code\tname\tdescription\tmain_target_type\n1\t치유\t살린다\tAlly\n").FindSkill(1)!;
        Assert.Equal(("살린다", SkillMainTargetType.Ally), (skill.Description, skill.MainTargetType));
    }

    static (GameSession Session, string Id) SessionWithWornSword()
    {
        var s = GameSession.NewGame(SamplePack());
        s.AddCharacter(7001);
        s.Party[0] = 7001;
        var id = s.AddUniqueItem(8001)!;
        Assert.True(s.Equip(7001, 1, EquipRef.Unique(id)));
        s.Pick("202");
        s.Pick(s.DataEntries.First(e => e.Label == "철검").Code);
        return (s, id);
    }

    [Fact]
    public void The_equipment_detail_shows_name_enhance_count_shared_hp_xp_parts_skill_and_description()
    {
        var (s, id) = SessionWithWornSword();
        var d = s.EquipmentDetail!;
        Assert.Equal("철검", d.Name);
        Assert.Equal("+0", d.EnhanceText);                                                  // 강화 횟수(스테이터스 8)
        Assert.Equal(GameSession.CharacterDetailExpWidth, d.Hp.Text.Length);
        Assert.Contains("40/40", d.Hp.Text);                                                // 낀 용사의 HP를 공유한다
        Assert.Equal(["부품 슬롯 1 : 비어 있음", "부품 슬롯 2 : 비어 있음"], d.Parts.Select(l => $"{l.Label} : {l.Value}"));
        Assert.Equal("베기", d.Skills.Single().Value);                                      // 장비가 가진 스킬
        Assert.Equal(s.Pack.FindUniqueItem(8001)!.Description, d.Description);
        Assert.NotEqual("", d.Description);

        s.UniqueItemInstances[id] = s.UniqueItemInstances[id] with
        {
            Statuses = new Dictionary<long, long>(s.UniqueItemInstances[id].Statuses!) { [GamePack.EnhanceCountStatusCode] = 3 },
            Parts = new Dictionary<long, long> { [1] = 4 },
        };
        Assert.Equal("+3", s.EquipmentDetail!.EnhanceText);
        Assert.Equal("철광석", s.EquipmentDetail!.Parts[0].Value);
    }

    [Fact]
    public void An_unworn_equipment_has_empty_gray_hp_and_xp_and_the_rows_open_item_and_skill_details()
    {
        var s = GameSession.NewGame(SamplePack());
        var id = s.AddUniqueItem(8001)!;
        s.UniqueItemInstances[id] = s.UniqueItemInstances[id] with { Parts = new Dictionary<long, long> { [1] = 4 } };
        s.Pick("202");
        s.Pick(s.DataEntries.First(e => e.Label == "철검").Code);
        var d = s.EquipmentDetail!;
        Assert.Equal(GaugeColor.Gray, d.Hp.Color);
        Assert.Equal(GaugeColor.Gray, d.Xp.Color);

        Assert.True(s.DataEntries.First(e => e.Code == "1").Enabled);
        Assert.False(s.DataEntries.First(e => e.Code == "2").Enabled);                         // 빈 부품 슬롯은 고를 수 없다
        Assert.Equal(Screen.ItemDetail, s.Pick("1")!.Target);
        Assert.Equal(4, s.DetailItemCode);
        Assert.Equal(Screen.EquipmentDetail, s.Pick("999")!.Target);                   // 보던 장비 상세로
        Assert.Equal(id, s.DetailEquipmentId);
        Assert.Equal(Screen.SkillDetail, s.Pick("101")!.Target);
        Assert.Equal("베기", s.DetailHeading!.Value.Name);
        Assert.Equal(Screen.EquipmentDetail, s.Pick("999")!.Target);
        Assert.Equal(Screen.EquipmentCheck, s.Pick("999")!.Target);
    }

    [Fact]
    public void Parts_and_the_item_skill_survive_the_save_and_unknown_parts_are_rejected()
    {
        var pack = SamplePack();
        var s = GameSession.NewGame(pack);
        var id = s.AddUniqueItem(8001)!;
        s.UniqueItemInstances[id] = s.UniqueItemInstances[id] with { Parts = new Dictionary<long, long> { [1] = 4 } };
        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(s.UniqueItemInstances[id], loaded.UniqueItemInstances[id]);
        Assert.Equal(4, loaded.UniqueItemInstances[id].Parts![1]);
        Assert.Equal(1001, loaded.UniqueItemInstances[id].Skill!.SkillCode);

        s.UniqueItemInstances[id] = s.UniqueItemInstances[id] with { Parts = new Dictionary<long, long> { [9] = 4 } };
        Assert.Contains("부품 슬롯 '9'", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, s.ToSaveJson())).Message);
        s.UniqueItemInstances[id] = s.UniqueItemInstances[id] with { Parts = new Dictionary<long, long> { [1] = 777 } };
        Assert.Contains("부품 '777'", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(pack, s.ToSaveJson())).Message);
    }

    [Fact]
    public void The_equipment_enhance_screen_needs_a_mode_first_then_a_picked_equipment_proceeds()
    {
        var s = GameSession.NewGame(SamplePack());
        var id = s.AddUniqueItem(8001)!;
        s.Pick(GameSession.EquipmentEnhanceEntryCode);
        Assert.Equal(["순례자의 은단검", "철검", "합성", "제련", "개조", "돌아가기"], s.DataEntries.Select(e => e.Label));
        Assert.Equal(["901", "902", "903"], s.DataEntries.Where(e => e.Action == EntryAction.SelectEnhanceMode).Select(e => e.Code));
        Assert.Null(s.EnhanceMode);
        var sword = s.DataEntries.First(e => e.Label == "철검");
        Assert.False(sword.Enabled);                                                        // 작업을 고르기 전에는 장비를 못 누른다
        s.Pick(sword.Code);
        Assert.Equal(Screen.EquipmentEnhance, s.Screen);

        s.Pick("902");
        Assert.Equal(EnhanceMode.Smelt, s.EnhanceMode);
        s.Pick("903");
        Assert.Equal(EnhanceMode.Modify, s.EnhanceMode);                                    // 다른 쪽을 누르면 바뀐다
        Assert.True(s.DataEntries.First(e => e.Label == "철검").Enabled);
        s.Pick(sword.Code);
        Assert.Equal(Screen.EquipmentEnhanceTarget, s.Screen);
        Assert.Equal(id, s.EnhanceTargetId);
        Assert.Equal("철검", s.EquipmentName(s.EnhanceTargetId));
        Assert.Equal(Screen.EquipmentEnhance, s.Pick("999")!.Target);
        Assert.Equal(EnhanceMode.Modify, s.EnhanceMode);                                    // 돌아와도 고른 작업은 그대로
        s.Pick("999");
        s.Pick(GameSession.EquipmentEnhanceEntryCode);
        Assert.Null(s.EnhanceMode);                                                         // 새로 들어오면 비어 있다
    }

    [Fact]
    public void Synthesis_is_gray_for_equipment_without_enough_other_copies_of_the_same_item()
    {
        var s = GameSession.NewGame(SamplePack());                                          // 제물 수 기본 1
        s.AddUniqueItem(8001);
        s.Pick(GameSession.EquipmentEnhanceEntryCode);
        s.Pick("901");
        Assert.All(s.DataEntries.Where(e => e.Action == EntryAction.PickEnhanceEquipment), e => Assert.False(e.Enabled));   // 같은 코드가 하나뿐

        s.AddUniqueItem(8001);
        var swords = s.DataEntries.Where(e => e.Label == "철검").ToList();
        Assert.Equal(2, swords.Count);
        Assert.All(swords, e => Assert.True(e.Enabled));                                    // 서로가 제물이 된다
        Assert.False(s.DataEntries.First(e => e.Label != "철검" && e.Action == EntryAction.PickEnhanceEquipment).Enabled);   // 다른 코드는 그대로 회색

        var worn = s.UniqueItemInstances.Values.Where(i => i.ItemCode == 8001).Select(i => i.InstanceId).ToList();
        s.AddCharacter(7001);
        s.Party[0] = 7001;
        Assert.True(s.Equip(7001, 1, EquipRef.Unique(worn[0])));                            // 한 개를 장착하면 그 개체는 제물이 못 된다
        var swordsNow = s.DataEntries.Where(e => e.Label == "철검").ToList();
        Assert.Equal([true, false], swordsNow.Select(e => e.Enabled).OrderByDescending(x => x));   // 장착한 개체는 합성할 수 있고(제물은 안 낀 쪽), 안 낀 쪽은 제물이 없어 회색
        s.AddUniqueItem(8001);
        Assert.All(s.DataEntries.Where(e => e.Label == "철검"), e => Assert.True(e.Enabled));   // 안 낀 개체가 둘이면 모두 가능

        s.Pick("902");                                                                  // 제련은 제물 대신 제련석이 필요하다
        Assert.All(s.DataEntries.Where(e => e.Action == EntryAction.PickEnhanceEquipment), e => Assert.False(e.Enabled));
        s.AddInventory(5, 1);
        Assert.All(s.DataEntries.Where(e => e.Action == EntryAction.PickEnhanceEquipment), e => Assert.True(e.Enabled));
    }
}
