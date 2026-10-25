using Wera.Core;

namespace Wera.Core.Tests;

// 캐릭터와 유니크 아이템은 인스턴스가 자기 태그를 가진다(마스터에서 복사, 게임 중 추가/소실). 커먼 아이템은 늘 마스터의 태그.
//  캐릭터 7001: 마스터 태그 없음. 슬롯 1 규칙: 검 AND 변신. 슬롯 2 규칙: 소환수.
//  유니크 아이템 8001 철검(검), 8002 변신 검(검, 변신). 커먼 아이템 3 뼈 조각(소환수).
//  캐릭터 7002 강시(소환수).
public class TagInstanceTests
{
    static GamePack Pack() => GamePack.Load(CoreTests.Zip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
        ("master/common_item_inventory_master_v1.tsv", "item_code\titem_name\tcarry_limit\n3\t뼈 조각\t99\n"),
        ("master/character_inventory_master_v1.tsv",
            "character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
            "1\t당신\t1\t1\t2\n7001\t변신술사\t1\t1\t2\n7002\t강시\t1\t1\t2\n"),
        ("master/equipment_slot_master_v1.tsv", "equipment_slot_code\tequipment_slot_name\n1\t무기\n2\t소환\n"),
        ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t검\n7\t변신\n8\t소환수\n"),
        ("master/unique_item_inventory_master_v1.tsv", "item_code\titem_name\n8001\t철검\n8002\t변신 검\n"),
        ("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n8001\t3\n8002\t3\n"),
        ("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n3\t8\n"),
        ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7002\t8\n"),
        ("master/equipment_slot_tag_master_v1.tsv",
            "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n" +
            "7001\t1\t3\t1\ttrue\n7001\t1\t7\t1\ttrue\n7001\t2\t8\t1\ttrue\n")));

    static GameSession Session()
    {
        var s = GameSession.NewGame(Pack());
        s.Characters[7001] = new CharacterState(5, 36, 40, 3, 30);
        s.Characters[7002] = new CharacterState(1, 10, 10, 0, 30);
        return s;
    }

    // ---- 유니크 아이템 인스턴스 ----

    [Fact]
    public void A_unique_item_instance_copies_the_master_tags_and_gets_its_own_id()
    {
        var s = Session();
        var a = s.AddUniqueItem(8001)!;
        var b = s.AddUniqueItem(8001)!;
        Assert.NotEqual(a, b);
        Assert.Equal([3L], s.TagsOf(EquipRef.Unique(a)));
        Assert.Equal([a, b], s.InstancesOf(8001));
        Assert.Null(s.AddUniqueItem(99999));                       // 마스터에 없다
    }

    [Fact]
    public void Changing_one_instances_tags_does_not_touch_its_twin_or_the_master()
    {
        var s = Session();
        var a = s.AddUniqueItem(8001)!;
        var b = s.AddUniqueItem(8001)!;

        Assert.True(s.AddUniqueItemTag(a, 7));
        Assert.Equal([3L, 7L], s.TagsOf(EquipRef.Unique(a)));
        Assert.Equal([3L], s.TagsOf(EquipRef.Unique(b)));
        Assert.Equal([3L], s.Pack.MasterTagsOfUniqueItem(8001));
        Assert.False(s.AddUniqueItemTag(a, 7));                    // 이미 있다
        Assert.False(s.AddUniqueItemTag(a, 99));                   // 태그 마스터에 없다
        Assert.True(s.RemoveUniqueItemTag(a, 3));
        Assert.False(s.RemoveUniqueItemTag(a, 3));
        Assert.False(s.RemoveUniqueItemTag("없는 ID", 3));
    }

    [Fact]
    public void Gaining_a_tag_can_make_an_item_equippable_and_losing_it_unequips_it()
    {
        // 변신 검이 아닌 철검도 "변신" 태그를 얻으면 낄 수 있다. 캐릭터가 변신을 잃으면 도로 빠진다.
        var s = Session();
        var sword = EquipRef.Unique(s.AddUniqueItem(8001)!);

        Assert.False(s.Equip(7001, 1, sword));                     // 캐릭터에 변신 태그가 없다
        s.AddCharacterTag(7001, 7);
        Assert.True(s.Equip(7001, 1, sword));
        Assert.Equal(sword, s.EquippedItemOf(7001, 1));

        s.RemoveCharacterTag(7001, 7);                             // 변신이 풀리면
        Assert.Null(s.EquippedItemOf(7001, 1));                    // 장비도 빠진다
        Assert.Equal(1, s.FreeCopies(8001));
    }

    [Fact]
    public void An_item_losing_its_own_tag_is_unequipped()
    {
        var s = Session();
        s.AddCharacterTag(7001, 7);
        var sword = EquipRef.Unique(s.AddUniqueItem(8001)!);
        s.Equip(7001, 1, sword);

        s.RemoveUniqueItemTag(sword.Instance!, 3);                      // 검 태그가 사라지면
        Assert.Null(s.EquippedItemOf(7001, 1));
    }

    [Fact]
    public void Equip_by_item_code_takes_a_fitting_instance_and_skips_unfit_ones()
    {
        var s = Session();
        s.AddCharacterTag(7001, 7);
        var first = s.AddUniqueItem(8001)!;
        var second = s.AddUniqueItem(8001)!;
        s.RemoveUniqueItemTag(first, 3);                           // 첫 번째는 검이 아니게 되었다

        Assert.True(s.Equip(7001, 1, 8001));
        Assert.Equal(EquipRef.Unique(second), s.EquippedItemOf(7001, 1));
    }

    [Fact]
    public void Removing_an_instance_unequips_it()
    {
        var s = Session();
        s.AddCharacterTag(7001, 7);
        var id = s.AddUniqueItem(8001)!;
        s.Equip(7001, 1, 8001);
        Assert.True(s.RemoveUniqueItem(id));
        Assert.Empty(s.EquippedItems);
        Assert.False(s.RemoveUniqueItem(id));
    }

    // ---- 캐릭터 태그 인스턴스 ----

    [Fact]
    public void A_character_starts_with_the_master_tags_and_changes_are_its_own()
    {
        var s = Session();
        Assert.Equal([8L], s.CharacterTags(7002));
        Assert.Empty(s.CharacterTags(7001));

        Assert.True(s.RemoveCharacterTag(7002, 8));
        Assert.Empty(s.CharacterTags(7002));
        Assert.Equal([8L], s.Pack.MasterTagsOfCharacter(7002));    // 마스터는 그대로
        Assert.False(s.AddCharacterTag(9999, 3));                  // 보유하지 않은 캐릭터
        Assert.False(s.AddCharacterTag(7001, 99));
    }

    [Fact]
    public void A_pet_that_loses_its_tag_leaves_the_slot_and_regains_fit_with_it()
    {
        var s = Session();
        Assert.True(s.Equip(7001, 2, EquipRef.Char(7002)));
        s.RemoveCharacterTag(7002, 8);                             // 강시가 소환수가 아니게 되었다
        Assert.Null(s.EquippedItemOf(7001, 2));
        Assert.False(s.Equip(7001, 2, EquipRef.Char(7002)));
        s.AddCharacterTag(7002, 8);
        Assert.True(s.Equip(7001, 2, EquipRef.Char(7002)));
    }

    [Fact]
    public void Common_items_always_use_the_master_tags()
    {
        var s = Session();
        s.InventoryItems[3] = new InventoryItem(1);
        Assert.Equal([8L], s.TagsOf(EquipRef.Common(3)));
        Assert.True(s.Equip(7001, 2, EquipRef.Common(3)));
    }

    // ---- 화면 ----

    [Fact]
    public void The_select_screen_lists_instances_and_each_can_be_chosen()
    {
        var s = Session();
        s.AddCharacterTag(7001, 7);
        s.AddUniqueItem(8001);
        s.AddUniqueItem(8002);
        s.Party[0] = 7001;
        s.Activate("103"); s.Activate("11"); s.Activate("1"); s.Activate("1");
        Assert.Equal(["철검", "변신 검", "돌아가기"], s.Entries.Select(e => e.Label));

        s.Activate("2");
        Assert.Equal("장착: 변신술사의 무기 ← 변신 검", s.Message);
        Assert.Equal(8002, s.EquippedUniqueCode(7001, 1));
    }

    // ---- 세이브 ----

    [Fact]
    public void Tags_round_trip_through_the_save_and_survive_master_copies()
    {
        var s = Session();
        s.AddCharacterTag(7001, 7);
        s.RemoveCharacterTag(7002, 8);
        var id = s.AddUniqueItem(8001)!;
        s.AddUniqueItemTag(id, 7);
        s.Equip(7001, 1, EquipRef.Unique(id));

        var json = s.ToSaveJson();
        Assert.Contains("\"CharacterTags\"", json);
        var loaded = GameSession.FromSaveJson(Pack(), json);
        Assert.Equal([7L], loaded.CharacterTags(7001));
        Assert.Empty(loaded.CharacterTags(7002));                  // 잃은 태그는 마스터로 되돌아가지 않는다
        Assert.Equal([3L, 7L], loaded.TagsOf(EquipRef.Unique(id)));
        Assert.Equal(EquipRef.Unique(id), loaded.EquippedItemOf(7001, 1));
    }

    const string Head = "{\"Version\":1,\"PackId\":\"demo\",";
    const string Chars =
        "\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}," +
        "\"7002\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xp\":0,\"XpMax\":10}},";

    [Fact]
    public void Missing_tags_in_a_save_are_copied_from_the_masters()
    {
        var s = GameSession.FromSaveJson(Pack(), Head + Chars + "\"UniqueItems\":{\"01H00000000000000000000004\":{\"ItemCode\":8002}}}");
        Assert.Equal([3L], s.TagsOf(EquipRef.Unique("01H00000000000000000000004")));
        Assert.Equal([8L], s.CharacterTags(7002));
        Assert.Empty(s.CharacterTags(7001));
        Assert.True(string.CompareOrdinal(s.AddUniqueItem(8001), "01H00000000000000000000004") > 0);   // 새 인스턴스 ID는 가장 큰 ID보다 뒤
    }

    [Fact]
    public void An_equipped_item_that_no_longer_fits_its_tags_in_a_save_is_rejected()
    {
        // 철검(검)만으로는 슬롯 1(검 AND 변신)에 맞지 않는다: 캐릭터 태그에 변신이 없다.
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(), Head + Chars +
            "\"UniqueItems\":{\"01H00000000000000000000001\":{\"ItemCode\":8001}}," +
            "\"EquippedItems\":{\"7001\":{\"1\":{\"Kind\":\"UniqueItem\",\"Instance\":\"01H00000000000000000000001\"}}}}"));

        var ok = GameSession.FromSaveJson(Pack(), Head + Chars +
            "\"UniqueItems\":{\"01H00000000000000000000001\":{\"ItemCode\":8001}}," +
            "\"CharacterTags\":{\"7001\":[7]}," +
            "\"EquippedItems\":{\"7001\":{\"1\":{\"Kind\":\"UniqueItem\",\"Instance\":\"01H00000000000000000000001\"}}}}");
        Assert.Equal(8001, ok.EquippedUniqueCode(7001, 1));
    }

    [Fact]
    public void The_shipped_sample_save_loads_with_the_shipped_sample_pack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        var wwwroot = Path.Combine(root, "src", "Wera.Web", "wwwroot");

        using var zip = File.OpenRead(Path.Combine(wwwroot, "sample-pack.zip"));
        var pack = GamePack.Load(zip);
        var s = GameSession.FromSaveJson(pack, File.ReadAllText(Path.Combine(wwwroot, "sample-tutorial-clear.sav")));

        Assert.Equal(7, s.UniqueItemInstances.Count);
        Assert.Equal(8001, s.EquippedUniqueCode(7001, 1));
        Assert.Equal(8004, s.EquippedUniqueCode(7001, 2));
        Assert.Equal(8001, s.EquippedUniqueCode(7003, 1));
    }
}
