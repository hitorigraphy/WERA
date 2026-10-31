using Wera.Core;

namespace Wera.Core.Tests;

// 서킷의 포커스: 시전자, 메인 타겟, 서브 타겟의 캐릭터, 장비(유니크 아이템), 스킬을 포커스하고 포커스한 것과 캐시가 값을 주고받는다.
public class CircuitFocusTests
{
    const long Hp = GamePack.HpStatusCode;
    const long Mana = 20;
    const long Durability = 21;
    const long Power = 22;
    const long Weapon = 1;
    const long Armor = 2;
    const long SwordSlot = 1;

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(string effects, bool slots = true)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            (GamePack.CharacterInventoryMasterPath,
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t1\t2\n"),
            (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n20\tmana\n21\tdurability\n22\tpower\n"),
            (GamePack.CharacterStatusMasterPath, "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
                Rows("1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 40 0 40", "7001 20 0 10 0 20", "7002 1 0 1 1 99", "7002 2 0 30 0 30", "7002 20 0 8 0 20")),
            (GamePack.UniqueItemInventoryMasterPath, "inventory_code\titem_name\n8001\t철검\n8002\t대검\n"),
            (GamePack.UniqueItemStatusMasterPath, "item_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n" +
                Rows("8001 21 100 0 100", "8001 22 5 0 50", "8002 21 80 0 100")),
            (GamePack.SkillMasterPath, "code\tname\n1\t베기\n"),
            (GamePack.SkillStatusMasterPath, "skill_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n" + Rows("1 22 5 1 10")),
            (GamePack.SkillExecuteEffectMasterPath, "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n" + Rows(effects.Split('\n').Select(r => "1 " + r.Trim()).ToArray())),
        };
        if (slots)
        {
            files.Add((GamePack.EquipmentSlotMasterPath, "equipment_slot_code\tequipment_slot_name\n1\t무기\n2\t방어구\n"));
            files.Add((GamePack.SkillSlotMasterPath, "code\tname\n1\t스킬 슬롯 1\n"));
        }
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    // 시전자 당신(1), 메인 타겟 용사(7001), 서브 타겟 마법사(7002). 용사는 철검(8001)을 무기 슬롯에 끼고 베기를 스킬 슬롯 1에 장착했다.
    static (SkillExecuteCircuit Circuit, CircuitField Field, string Sword) Start(GamePack pack, bool withSub = true)
    {
        var field = new CircuitField(pack, 1, 7001, 7002);
        var sword = field.Equip(7001, Weapon, 8001);
        field.Learn(7001, SwordSlot, 1);
        var c = new SkillExecuteCircuit(pack, field.Store, 1, field.Unit(1), field.Unit(7001), withSub ? [field.Unit(7002)] : null);
        return (c, field, sword);
    }

    [Fact]
    public void Equipment_focus_reads_and_writes_the_unique_item_within_its_status_range()
    {
        var pack = Pack("1 FocusMainTargetEquipment 1\n2 FocusedItemValueToCache 21 0\n3 SetCacheValue 1 -30\n4 CacheToFocusedItemModValue 21 1\n" +
                        "5 SetCacheValue 2 99\n6 CacheToFocusedItemModValue 22 2");
        var (c, field, sword) = Start(pack);
        c.Execute();
        Assert.Equal(100m, c.CacheValues[0]);
        Assert.Equal((70, 50), (field.Items[sword].Statuses![Durability], field.Items[sword].Statuses![Power]));       // 5+99는 최댓값 50
        Assert.Equal(sword, c.FocusedUniqueItemInstance!.InstanceId);
    }

    [Fact]
    public void An_empty_or_foreign_slot_clears_the_focus_so_later_writes_do_nothing()
    {
        // 방어구 슬롯은 비었다: 포커스가 비워져 mod가 철검에 닿지 않는다(포커스가 이전 것으로 남지 않는다).
        var pack = Pack("1 FocusMainTargetEquipment 1\n2 FocusMainTargetEquipment 2\n3 SetCacheValue 0 -50\n4 CacheToFocusedItemModValue 21 0");
        var (c, field, sword) = Start(pack);
        c.Execute();
        Assert.Null(c.FocusedUniqueItemInstance);
        Assert.Equal(100, field.Items[sword].Statuses![Durability]);
    }

    [Fact]
    public void A_common_item_or_character_in_the_slot_is_not_a_unique_item_and_is_not_focused()
    {
        var pack = Pack("1 FocusMainTargetEquipment 2");
        var (c, field, _) = Start(pack);
        field.Unit(7001).Equipment[Armor] = EquipRef.Common(1);
        c.Execute();
        Assert.Null(c.FocusedUniqueItemInstance);
    }

    [Fact]
    public void Skill_focus_reads_and_writes_the_skill_status_and_the_character_keeps_its_other_changes()
    {
        // 용사의 mana(10)에 3을 더하고, 스킬 슬롯 1의 스킬 power(5)에 7을 더한다(최댓값 10). 둘 다 같은 용사 인스턴스에 반영된다.
        var pack = Pack("1 SetCacheValue 0 3\n2 SetCacheValue 1 7\n3 FocusMainTargetCharacter\n4 CacheToFocusedCharacterModValue 20 0\n" +
                        "5 FocusMainTargetSkill 1\n6 FocusedSkillValueToCache 22 2\n7 CacheToFocusedSkillModValue 22 1");
        var (c, field, _) = Start(pack);
        c.Execute();
        Assert.Equal(5m, c.CacheValues[2]);
        var hero = field.Characters[7001];
        Assert.Equal(13, hero.Statuses[Mana]);
        Assert.Equal(10, hero.SkillPool[0].Statuses![Power]);
        Assert.Equal(1, hero.SkillPool[0].SkillCode);
        Assert.Equal(10, c.FocusedSkill!.Statuses![Power]);
    }

    [Fact]
    public void A_skill_slot_without_a_skill_clears_the_focus()
    {
        var pack = Pack("1 FocusMainTargetSkill 1\n2 FocusCasterSkill 1\n3 SetCacheValue 0 1\n4 CacheToFocusedSkillModValue 22 0");
        var (c, field, _) = Start(pack);
        c.Execute();                                                       // 시전자(당신)는 스킬이 없다
        Assert.Null(c.FocusedSkill);
        Assert.Equal(5, field.Characters[7001].SkillPool[0].Statuses![Power]);
    }

    [Fact]
    public void Sub_targets_are_chosen_by_number_and_a_missing_number_clears_the_focus()
    {
        var pack = Pack("1 FocusSubTargetCharacter 0\n2 SetCacheValue 0 2\n3 CacheToFocusedCharacterModValue 20 0\n" +
                        "4 FocusSubTargetCharacter 1\n5 CacheToFocusedCharacterModValue 20 0");
        var (c, field, _) = Start(pack);
        c.Execute();
        Assert.Null(c.FocusedCharacterInstance);                           // 서브 타겟 1번은 없다
        Assert.Equal(10, field.Characters[7002].Statuses[Mana]);           // 8 + 2
        Assert.Equal(10, field.Characters[7001].Statuses[Mana]);
    }

    [Fact]
    public void Sub_target_equipment_and_skill_take_the_number_first_and_the_slot_second()
    {
        var pack = Pack("1 FocusSubTargetEquipment 0 1\n2 FocusSubTargetSkill 0 1");
        var field = new CircuitField(pack, 1, 7001, 7002);
        var sword = field.Equip(7002, Weapon, 8002);
        field.Learn(7002, SwordSlot, 1);
        var c = new SkillExecuteCircuit(pack, field.Store, 1, field.Unit(1), field.Unit(7001), [field.Unit(7002)]);
        c.Execute();
        Assert.Equal(sword, c.FocusedUniqueItemInstance!.InstanceId);
        Assert.Equal(1, c.FocusedSkill!.SkillCode);
    }

    [Fact]
    public void Focusing_the_same_character_twice_edits_one_copy_and_other_units_stay_as_they_were()
    {
        // 시전자와 메인 타겟이 같은 용사: 두 번 포커스해 더해도 하나의 사본에 쌓인다(10+2+3=15).
        var pack = Pack("1 FocusCasterCharacter\n2 SetCacheValue 0 2\n3 CacheToFocusedCharacterModValue 20 0\n4 FocusMainTargetCharacter\n" +
                        "5 SetCacheValue 0 3\n6 CacheToFocusedCharacterModValue 20 0");
        var field = new CircuitField(pack, 1, 7001, 7002);
        var c = new SkillExecuteCircuit(pack, field.Store, 1, field.Unit(7001), field.Unit(7001));
        c.Execute();
        Assert.Equal(15, field.Characters[7001].Statuses[Mana]);
        Assert.Equal(8, field.Characters[7002].Statuses[Mana]);
    }

    [Fact]
    public void Nothing_is_written_back_when_no_value_changed()
    {
        var pack = Pack("1 FocusMainTargetCharacter\n2 FocusedCharacterValueToCache 20 0\n3 FocusMainTargetEquipment 1\n4 FocusedItemValueToCache 21 1");
        var (c, field, sword) = Start(pack);
        var hero = field.Characters[7001];
        var item = field.Items[sword];
        c.Execute();
        Assert.Same(hero, field.Characters[7001]);
        Assert.Same(item, field.Items[sword]);
    }

    [Fact]
    public void A_circuit_without_units_focuses_nothing()
    {
        var pack = Pack("1 FocusCasterCharacter\n2 FocusMainTargetEquipment 1\n3 FocusCasterSkill 1\n4 FocusSubTargetCharacter 0");
        var field = new CircuitField(pack, 1, 7001);
        var c = new SkillExecuteCircuit(pack, field.Store, 1, null, null);
        c.Execute();
        Assert.Null(c.FocusedCharacterInstance);
        Assert.Null(c.FocusedUniqueItemInstance);
        Assert.Null(c.FocusedSkill);
    }

    [Fact]
    public void The_loader_checks_the_sub_target_number_and_the_slot_codes()
    {
        string Err(string effects, bool slots = true) => Assert.Throws<PackFormatException>(() => Pack(effects, slots)).Message;
        Assert.Contains("value_1(서브 타겟 번호) '-1'", Err("1 FocusSubTargetCharacter -1"));
        Assert.Contains("value_1(서브 타겟 번호) '-1'", Err("1 FocusSubTargetEquipment -1 1"));
        Assert.Contains("value_2(장비 슬롯 코드) '9'", Err("1 FocusSubTargetEquipment 0 9"));
        Assert.Contains("value_1(장비 슬롯 코드) '9'", Err("1 FocusCasterEquipment 9"));
        Assert.Contains("value_1(장비 슬롯 코드) '9'", Err("1 FocusMainTargetEquipment 9"));
        Assert.Contains("value_2(스킬 슬롯 코드) '9'", Err("1 FocusSubTargetSkill 0 9"));
        Assert.Contains("value_1(스킬 슬롯 코드) '9'", Err("1 FocusCasterSkill 9"));
        Assert.Contains(GamePack.EquipmentSlotMasterPath, Err("1 FocusCasterEquipment 1", slots: false));
        Assert.Contains(GamePack.SkillSlotMasterPath, Err("1 FocusMainTargetSkill 1", slots: false));
        Assert.NotNull(Pack("1 FocusCasterCharacter\n2 FocusMainTargetCharacter\n3 FocusSubTargetCharacter 5", slots: false));    // 슬롯이 없는 종류는 슬롯 마스터가 필요 없다
    }
}
