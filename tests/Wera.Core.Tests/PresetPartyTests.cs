using Wera.Core;

namespace Wera.Core.Tests;

// 전투 편성 마스터: preset_unit, preset_unit_equip, preset_unit_drop, preset_party, battle_enemy.
public class PresetPartyTests
{
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
        "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n";
    const string Slots = "equipment_slot_code\tequipment_slot_name\n1\t무기\n";
    const string Tags = "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t검\n";
    const string CharacterTags = "character_code\ttag_code\n1\t1\n";
    const string SlotTags = "character_code\tequipment_slot_code\ttag_code\tgroup_number\tis_positive\n7001\t1\t4\t1\ttrue\n";
    const string UniqueItems = "inventory_code\titem_name\n8001\t철검\n8002\t지팡이\n";
    const string UniqueItemTags = "item_code\ttag_code\n8001\t4\n";
    const string Units = "code\tcharacter_code\n1\t7001\n2\t7002\n";
    const string PartyHeader =
        "code\tfront_left_unit_code\tfront_center_unit_code\tfront_right_unit_code\tmiddle_left_unit_code\tmiddle_center_unit_code\t" +
        "middle_right_unit_code\tback_left_unit_code\tback_center_unit_code\tback_right_unit_code\n";

    static (string, string)[] Base() =>
        [("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/character_inventory_master_v1.tsv", Characters),
         (GamePack.EquipmentSlotMasterPath, Slots), (GamePack.TagMasterPath, Tags), (GamePack.CharacterTagMasterPath, CharacterTags),
         (GamePack.EquipmentSlotTagMasterPath, SlotTags), (GamePack.UniqueItemInventoryMasterPath, UniqueItems),
         (GamePack.UniqueItemTagMasterPath, UniqueItemTags), (GamePack.PresetUnitMasterPath, Units)];

    // 같은 경로를 다시 주면 기본 파일 대신 그것을 쓴다
    static GamePack Load(params (string, string)[] extra) => GamePack.Load(CoreTests.PackZip(
        Base().Where(b => extra.All(e => e.Item1 != b.Item1)).Concat(extra).ToArray()));

    static void Rejects(string message, params (string, string)[] files) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(files)).Message);

    [Fact]
    public void Units_equipment_drops_parties_and_battle_enemies_load_and_are_found_by_key()
    {
        var pack = Load(
            (GamePack.PresetUnitEquipMasterPath, "preset_unit_code\tequipment_slot_code\titem_code\tmemo\n1\t1\t8001\t검\n"),
            (GamePack.PresetUnitDropMasterPath, "preset_unit_code\titem_code\tamount\n1\t2\t5\n1\t8002\t1\n"),
            (GamePack.PresetPartyMasterPath, PartyHeader + "1\t1\t\t2\t\t\t\t\t\t\n2\t\t\t\t\t2\t\t\t\t\n"),
            (GamePack.BattleMasterPath, "code\tname\n5001\t전투\n"),
            (GamePack.BattleEnemyMasterPath, "battle_code\tpreset_party_code\tweight\n5001\t1\t1\n5001\t2\t3\n"));

        Assert.Equal(new PresetUnit(2, 7002), pack.FindPresetUnit(2));
        Assert.Null(pack.FindPresetUnit(9));
        Assert.Equal([new PresetUnitEquip(1, 1, 8001)], pack.PresetUnitEquipsOf(1));
        Assert.Empty(pack.PresetUnitEquipsOf(2));
        Assert.Equal([new PresetUnitDrop(1, 2, 5), new PresetUnitDrop(1, 8002, 1)], pack.PresetUnitDropsOf(1));

        // 진형 아홉 칸: 전위(왼, 가운데, 오른), 중위, 후위 순서. 비어 있으면 null
        Assert.Equal<long?>([1, null, 2, null, null, null, null, null, null], pack.FindPresetParty(1)!.UnitCodes);
        Assert.Equal<long?>([null, null, null, null, 2, null, null, null, null], pack.FindPresetParty(2)!.UnitCodes);
        Assert.Equal([new BattleEnemy(5001, 1, 1), new BattleEnemy(5001, 2, 3)], pack.BattleEnemiesOf(5001));
        Assert.Empty(pack.BattleEnemiesOf(9999));
    }

    [Fact]
    public void Without_the_files_everything_is_null_and_empty()
    {
        var pack = Load();
        Assert.Null(pack.PresetUnitEquips);
        Assert.Null(pack.PresetParties);
        Assert.Null(pack.BattleEnemies);
        Assert.Empty(pack.PresetUnitDropsOf(1));
    }

    [Fact]
    public void A_unit_needs_a_known_character_and_a_unique_code()
    {
        Rejects("캐릭터 코드 '9'", (GamePack.PresetUnitMasterPath, "code\tcharacter_code\n1\t9\n"));
        Rejects("중복", (GamePack.PresetUnitMasterPath, "code\tcharacter_code\n1\t7001\n1\t7002\n"));
    }

    [Fact]
    public void Equipment_must_name_known_codes_and_fit_the_slot_rules()
    {
        const string header = "preset_unit_code\tequipment_slot_code\titem_code\n";
        Rejects("프리셋 유닛 코드 '9'", (GamePack.PresetUnitEquipMasterPath, header + "9\t1\t8001\n"));
        Rejects("장비 슬롯 코드 '9'", (GamePack.PresetUnitEquipMasterPath, header + "1\t9\t8001\n"));
        Rejects("아이템 코드 '9999'", (GamePack.PresetUnitEquipMasterPath, header + "1\t1\t9999\n"));
        Rejects("두 번", (GamePack.PresetUnitEquipMasterPath, header + "1\t1\t8001\n1\t1\t8001\n"));
        Rejects("낄 수 없습니다", (GamePack.PresetUnitEquipMasterPath, header + "1\t1\t8002\n"));            // 지팡이는 검 태그가 없다
        Rejects("낄 수 없습니다", (GamePack.PresetUnitEquipMasterPath, header + "2\t1\t8001\n"));            // 마법사의 무기 슬롯 규칙이 없다
    }

    [Fact]
    public void A_unit_cannot_equip_its_own_character()
    {
        Rejects("자기 자신", (GamePack.PresetUnitEquipMasterPath, "preset_unit_code\tequipment_slot_code\titem_code\n1\t1\t7001\n"));
    }

    [Fact]
    public void Drops_need_known_items_and_sensible_amounts()
    {
        const string header = "preset_unit_code\titem_code\tamount\n";
        Rejects("프리셋 유닛 코드 '9'", (GamePack.PresetUnitDropMasterPath, header + "9\t2\t1\n"));
        Rejects("아이템 코드 '9999'", (GamePack.PresetUnitDropMasterPath, header + "1\t9999\t1\n"));
        Rejects("1 이상", (GamePack.PresetUnitDropMasterPath, header + "1\t2\t0\n"));
        Rejects("개수가 1이어야", (GamePack.PresetUnitDropMasterPath, header + "1\t8001\t2\n"));
        Rejects("두 번", (GamePack.PresetUnitDropMasterPath, header + "1\t2\t1\n1\t2\t2\n"));
    }

    [Fact]
    public void Party_cells_are_known_units_or_blank_and_the_party_is_not_empty()
    {
        Rejects("'9'가 프리셋 유닛 마스터에 없습니다", (GamePack.PresetPartyMasterPath, PartyHeader + "1\t9\t\t\t\t\t\t\t\t\n"));
        Rejects("유닛이 하나도 없습니다", (GamePack.PresetPartyMasterPath, PartyHeader + "1\t\t\t\t\t\t\t\t\t\n"));
        Rejects("중복", (GamePack.PresetPartyMasterPath, PartyHeader + "1\t1\t\t\t\t\t\t\t\t\n1\t2\t\t\t\t\t\t\t\t\n"));
        Rejects("헤더", (GamePack.PresetPartyMasterPath, "code\tfront_left_unit_code\n1\t1\n"));
    }

    [Fact]
    public void Battle_enemies_need_a_known_battle_a_known_party_and_a_positive_weight()
    {
        var battle = (GamePack.BattleMasterPath, "code\tname\n5001\t전투\n");
        var party = (GamePack.PresetPartyMasterPath, PartyHeader + "1\t1\t\t\t\t\t\t\t\t\n");
        const string header = "battle_code\tpreset_party_code\tweight\n";
        Rejects("전투 코드 '9'", battle, party, (GamePack.BattleEnemyMasterPath, header + "9\t1\t1\n"));
        Rejects("프리셋 파티 코드 '9'", battle, party, (GamePack.BattleEnemyMasterPath, header + "5001\t9\t1\n"));
        Rejects("1 이상", battle, party, (GamePack.BattleEnemyMasterPath, header + "5001\t1\t0\n"));
        Rejects("두 번", battle, party, (GamePack.BattleEnemyMasterPath, header + "5001\t1\t1\n5001\t1\t2\n"));
        Rejects("preset_party_master_v1.tsv도 필요", battle, (GamePack.BattleEnemyMasterPath, header + "5001\t1\t1\n"));
        Rejects("battle_master_v1.tsv도 필요", party, (GamePack.BattleEnemyMasterPath, header + "5001\t1\t1\n"));
    }

    [Fact]
    public void The_sample_pack_loads_with_its_battle_setup()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Directory.GetParent(root)!.FullName;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var pack = GamePack.Load(zip);
        Assert.Equal(2, pack.BattleEnemiesOf(5001).Count);
        Assert.Equal([8001L, 8004L], pack.PresetUnitEquipsOf(1).Select(e => e.ItemCode));
    }
}
