using Wera.Core;

namespace Wera.Core.Tests;

// 캐릭터 생성 후 이펙트에만 있는 종류: AddSkillToPool, AddEquipment, SetSkillToSlot, SetEquipmentToSlot.
public class CreateCircuitEffectsTests
{
    const string Head = "character_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(string effects) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        (GamePack.CharacterInventoryMasterPath,
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n7\tdurability\n"),
        (GamePack.CharacterStatusMasterPath, "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
            Rows("1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 40 0 40")),
        (GamePack.UniqueItemInventoryMasterPath, "inventory_code\titem_name\n8001\t철검\n8002\t대검\n"),
        (GamePack.UniqueItemStatusMasterPath, "item_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n" + Rows("8001 7 100 0 100", "8002 7 100 0 100")),
        (GamePack.EquipmentSlotMasterPath, "equipment_slot_code\tequipment_slot_name\n1\t무기\n2\t방어구\n"),
        (GamePack.SkillSlotMasterPath, "code\tname\n1\t스킬 슬롯 1\n2\t스킬 슬롯 2\n"),
        (GamePack.SkillMasterPath, "code\tname\n1001\t베기\n1002\t불꽃\n"),
        (GamePack.CharacterAfterCreateEffectMasterPath, Head + effects)));

    static CharacterInstance Create(GamePack pack, long code = 7001) =>
        new CharacterAfterCreateCircuit(pack, pack.NewCharacterInstance(code, "id", 0)).Execute();

    [Fact]
    public void Skills_are_added_to_the_pool_in_order_and_set_to_slots()
    {
        var pack = Pack(Rows("7001 1 AddSkillToPool 1001", "7001 2 SetSkillToSlot 1", "7001 3 AddSkillToPool 1002", "7001 4 SetSkillToSlot 2"));
        var character = Create(pack);
        Assert.Equal([1001L, 1002L], character.SkillPool.Select(s => s.SkillCode));
        Assert.Equal(new Dictionary<long, long> { [1] = 0, [2] = 1 }, character.SkillSlots);
        Assert.Empty(Create(pack, 1).SkillPool);                                  // 다른 캐릭터는 영향이 없다
    }

    [Fact]
    public void Setting_the_same_skill_to_another_slot_moves_it_and_the_pool_keeps_it()
    {
        var pack = Pack(Rows("7001 1 AddSkillToPool 1001", "7001 2 SetSkillToSlot 1", "7001 3 SetSkillToSlot 2",
            "7001 4 AddSkillToPool 1002", "7001 5 SetSkillToSlot 2"));
        var character = Create(pack);
        Assert.Equal(new Dictionary<long, long> { [2] = 1 }, character.SkillSlots);   // 슬롯 2는 새 스킬로 바뀌고 첫 스킬은 풀에만 남는다
        Assert.Equal(2, character.SkillPool.Count);
    }

    [Fact]
    public void A_skill_set_to_a_slot_without_a_focused_skill_does_nothing_and_the_focused_skill_can_be_edited()
    {
        var none = Create(Pack(Rows("7001 1 SetSkillToSlot 1")));
        Assert.Empty(none.SkillSlots);
    }

    [Fact]
    public void Equipment_is_obtained_focused_and_set_to_a_slot_of_the_circuits_unit()
    {
        var pack = Pack(Rows("7001 1 AddEquipment 8001", "7001 2 SetEquipmentToSlot 1", "7001 3 AddEquipment 8002", "7001 4 SetEquipmentToSlot 2"));
        var items = new Dictionary<string, UniqueItemInstance>();
        var circuit = new CharacterAfterCreateCircuit(pack, pack.NewCharacterInstance(7001, "enemy-3", 0), items, null);
        circuit.Execute();

        Assert.Equal(["enemy-3-item-1", "enemy-3-item-2"], items.Keys.Order());
        Assert.Equal([8001L, 8002L], items.Values.OrderBy(i => i.InstanceId).Select(i => i.ItemCode));
        Assert.Equal(EquipRef.Unique("enemy-3-item-1"), circuit.Unit.Equipment[1]);
        Assert.Equal(EquipRef.Unique("enemy-3-item-2"), circuit.Unit.Equipment[2]);
    }

    [Fact]
    public void Setting_one_item_to_another_slot_moves_it()
    {
        var pack = Pack(Rows("7001 1 AddEquipment 8001", "7001 2 SetEquipmentToSlot 1", "7001 3 SetEquipmentToSlot 2"));
        var circuit = new CharacterAfterCreateCircuit(pack, pack.NewCharacterInstance(7001, "id", 0));
        circuit.Execute();
        Assert.Equal([2L], circuit.Unit.Equipment.Keys);
    }

    [Fact]
    public void A_focused_item_can_be_edited_by_the_cache_effects_and_a_new_item_is_initialised_by_its_own_circuit()
    {
        // 철검을 얻으면 내구도(100)를 캐시의 30으로 바꾼다
        var pack = Pack(Rows("7001 1 AddEquipment 8001", "7001 2 SetCacheValue 0 30", "7001 3 CacheToFocusedItemSetValue 7 0"));
        var items = new Dictionary<string, UniqueItemInstance>();
        new CharacterAfterCreateCircuit(pack, pack.NewCharacterInstance(7001, "id", 0), items, null).Execute();
        Assert.Equal(30, items["id-item-1"].Statuses![7]);
    }

    [Fact]
    public void A_session_character_gets_the_items_as_owned_ones_and_the_skills_in_its_pool()
    {
        var pack = Pack(Rows("7001 1 AddSkillToPool 1002", "7001 2 SetSkillToSlot 2", "7001 3 AddEquipment 8001", "7001 4 SetEquipmentToSlot 1"));
        var session = GameSession.NewGame(pack);
        Assert.True(session.AddCharacter(7001));
        Assert.Equal([1002L], session.Characters[7001].SkillPool.Select(s => s.SkillCode));
        Assert.Equal(2L, session.Characters[7001].SkillSlots.Keys.Single());
        Assert.Equal([8001L], session.UniqueItemInstances.Values.Select(i => i.ItemCode));      // 보유 아이템이 되었다(끼우는 것은 파티 유닛이 있어야 한다)
    }

    [Fact]
    public void Row_arguments_must_name_codes_in_the_masters()
    {
        string Err(string rows) => Assert.Throws<PackFormatException>(() => Pack(rows)).Message;
        Assert.Contains("스킬 마스터에 없습니다", Err(Rows("7001 1 AddSkillToPool 9999")));
        Assert.Contains("유니크 아이템 마스터에 없습니다", Err(Rows("7001 1 AddEquipment 9999")));
        Assert.Contains("스킬 슬롯 마스터에 없습니다", Err(Rows("7001 1 SetSkillToSlot 9")));
        Assert.Contains("장비 슬롯 마스터에 없습니다", Err(Rows("7001 1 SetEquipmentToSlot 9")));
        Pack(Rows("7001 1 AddSkillToPool 1001", "7001 2 AddEquipment 8001", "7001 3 SetSkillToSlot 1", "7001 4 SetEquipmentToSlot 1"));
        Assert.Contains("'35'", Err(Rows("7001 1 35")));                                     // 번호는 안 된다
    }
}
