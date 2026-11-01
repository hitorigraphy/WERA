using Wera.Core;

namespace Wera.Core.Tests;

// 개조: 개조 슬롯 마스터, 유니크 아이템 개조 스테이터스 마스터, 개조 화면(슬롯 고르기, 아이템 끼우기), 스테이터스 반영, 세이브.
public class ModTests
{
    static GamePack SamplePack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(read);
    }

    [Fact]
    public void Slots_and_items_are_found_by_the_secondary_keys_without_duplicates()
    {
        var pack = SamplePack();
        Assert.Equal([1L, 2L], pack.ModSlotsOf(8001).Select(s => s.Code));          // (유니크 아이템)으로 슬롯을 중복 없이
        Assert.Equal([4L], pack.ModItemsOf(8001, 1));                               // (유니크 아이템, 슬롯)으로 아이템을 중복 없이
        Assert.Equal([4L, 3L], pack.ModItemsOf(8001, 2));
        Assert.Empty(pack.ModSlotsOf(8002));
    }

    static GamePack ModPack(string slots, string rows) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n40\tATK\n"),
        (GamePack.UniqueItemInventoryMasterPath, "inventory_code\titem_name\n8001\t철검\n"),
        (GamePack.UniqueItemStatusMasterPath, "item_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n8001\t40\t10\t0\t99\n"),
        (GamePack.CommonItemInventoryMasterPath, "inventory_code\titem_name\tcarry_limit\n4\t철광석\t99\n"),
        (GamePack.ModSlotMasterPath, "code\tname\n" + slots),
        (GamePack.UniqueItemModStatusMasterPath, "unique_item_code\t개조_slot_code\titem_code\tstatus_code\tvalue\n" + rows)));

    [Fact]
    public void The_mod_status_master_is_validated_at_load()
    {
        Assert.Equal(1, ModPack("1\t슬롯\n", "8001\t1\t4\t40\t3\n").UniqueItemModStatuses!.Count);
        Assert.Contains("개조 슬롯 '2'", Assert.Throws<PackFormatException>(() => ModPack("1\t슬롯\n", "8001\t2\t4\t40\t3\n")).Message);
        Assert.Contains("아이템 '9'", Assert.Throws<PackFormatException>(() => ModPack("1\t슬롯\n", "8001\t1\t9\t40\t3\n")).Message);
        Assert.Contains("스테이터스 '1'", Assert.Throws<PackFormatException>(() => ModPack("1\t슬롯\n", "8001\t1\t4\t1\t3\n")).Message);   // 유니크 아이템 스테이터스 마스터에 없음
        Assert.Contains("두 번", Assert.Throws<PackFormatException>(() => ModPack("1\t슬롯\n", "8001\t1\t4\t40\t3\n8001\t1\t4\t40\t5\n")).Message);
        Assert.Contains("8999", Assert.Throws<PackFormatException>(() => ModPack("1\t슬롯\n", "8999\t1\t4\t40\t3\n")).Message);
    }

    static GameSession ModScreen(long ore = 3, long herb = 0)
    {
        var s = GameSession.NewGame(SamplePack());
        s.AddUniqueItem(8001);
        s.AddInventory(4, ore);
        s.AddInventory(3, herb);
        s.Activate(GameSession.EquipmentEnhanceEntryCode);
        s.Activate("903");                                                                   // 개조
        return s;
    }

    static string Sword(GameSession s) => s.UniqueItemInstances.Values.Single(i => i.ItemCode == 8001).InstanceId;
    static long Atk(GameSession s) => s.UniqueItemInstances[Sword(s)].Statuses![40];

    static void PickSword(GameSession s) => s.Activate(s.Entries.First(e => e.Label.StartsWith("철검")).Code);

    [Fact]
    public void Picking_an_equipment_in_modify_mode_opens_a_detail_with_its_mod_slots()
    {
        var s = ModScreen();
        PickSword(s);
        Assert.Equal(Screen.EquipmentEnhanceTarget, s.Screen);
        Assert.Equal(["개조 슬롯 1 : 비어 있음", "개조 슬롯 2 : 비어 있음"], s.EquipmentDetail!.Parts.Select(l => $"{l.Label} : {l.Value}"));
        Assert.Equal(["1", "2", "999"], s.Entries.Select(e => e.Code));
        s.Activate("999");
        Assert.Equal(Screen.EquipmentEnhance, s.Screen);
    }

    [Fact]
    public void A_slot_lists_only_its_items_and_only_owned_ones_are_active()
    {
        var s = ModScreen(ore: 3, herb: 0);
        PickSword(s);
        s.Activate("2");                                                                     // 개조 슬롯 2
        Assert.Equal(Screen.EquipmentModSlot, s.Screen);
        Assert.Equal([("4", true), ("3", false), ("998", false), ("999", true)], s.Entries.Select(e => (e.Code, e.Enabled)));   // 약초는 없어서 회색
        s.Activate("999");
        Assert.Equal(Screen.EquipmentEnhanceTarget, s.Screen);
    }

    [Fact]
    public void Installing_spends_one_item_adds_its_status_bonus_and_replacing_returns_the_old_one()
    {
        var s = ModScreen(ore: 3, herb: 1);
        Assert.Equal(10, Atk(s));
        PickSword(s);
        s.Activate("2");
        s.Activate("4");                                                                     // 슬롯 2에 철광석: +1
        Assert.Equal(Screen.EquipmentEnhanceTarget, s.Screen);
        Assert.Equal("개조했습니다: 철검 개조 슬롯 2 철광석", s.Message);
        Assert.Equal(2, s.CountInventory(4));
        Assert.Equal(11, Atk(s));
        Assert.Equal("철광석", s.EquipmentDetail!.Parts[1].Value);

        s.Activate("1");
        s.Activate("4");                                                                     // 슬롯 1에 철광석: +3 (두 슬롯의 합)
        Assert.Equal(14, Atk(s));

        s.Activate("2");
        Assert.False(s.Entries.First(e => e.Code == "4").Enabled);                           // 낀 아이템은 회색
        Assert.Equal("철광석 (장착 중)", s.Entries.First(e => e.Code == "4").Label);
        s.Activate("3");                                                                     // 슬롯 2를 약초로 바꾼다: 철광석은 돌아오고 +2
        Assert.Equal(2, s.CountInventory(4) - 0);                                            // 1개 돌려받아 1+1
        Assert.Equal(0, s.CountInventory(3));
        Assert.Equal(15, Atk(s));
    }

    [Fact]
    public void Enhancing_keeps_the_mod_bonus_and_the_mods_survive_the_save()
    {
        var s = ModScreen();
        PickSword(s);
        s.Activate("1");
        s.Activate("4");
        var id = Sword(s);
        s.UniqueItemInstances[id] = SamplePackEnhanced(s, s.UniqueItemInstances[id]);
        var enhanced = s.UniqueItemInstances[id].Statuses![40];
        Assert.Equal(10 + 5 + 3, enhanced);                                                  // 초깃값 + 강화 1(+5) + 개조 +3

        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal(4, loaded.UniqueItemInstances[id].Mods![1]);
        Assert.Equal(enhanced, loaded.UniqueItemInstances[id].Statuses![40]);
    }

    static UniqueItemInstance SamplePackEnhanced(GameSession s, UniqueItemInstance item) =>
        s.Pack.WithEnhanceStatuses(item with { Statuses = new Dictionary<long, long>(item.Statuses!) { [GamePack.EnhanceCountStatusCode] = 1 } });

    [Fact]
    public void Unknown_mod_slots_and_items_in_a_save_are_rejected()
    {
        var s = ModScreen();
        var id = Sword(s);
        s.UniqueItemInstances[id] = s.UniqueItemInstances[id] with { Mods = new Dictionary<long, long> { [9] = 4 } };
        Assert.Contains("개조 슬롯", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(s.Pack, s.ToSaveJson())).Message);
        s.UniqueItemInstances[id] = s.UniqueItemInstances[id] with { Mods = new Dictionary<long, long> { [1] = 999 } };
        Assert.Contains("개조 아이템", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(s.Pack, s.ToSaveJson())).Message);
    }

    [Fact]
    public void Removing_returns_the_item_and_takes_its_bonus_back()
    {
        var s = ModScreen(ore: 3);
        PickSword(s);
        s.Activate("1");
        Assert.False(s.Entries.First(e => e.Code == GameSession.RemoveModEntryCode).Enabled);   // 낀 것이 없으면 회색
        s.Activate("4");
        Assert.Equal(13, Atk(s));
        s.Activate("1");
        s.Activate(GameSession.RemoveModEntryCode);
        Assert.Equal(Screen.EquipmentEnhanceTarget, s.Screen);
        Assert.Equal("뺐습니다: 철검 개조 슬롯 1 철광석", s.Message);
        Assert.Equal(3, s.CountInventory(4));
        Assert.Equal(10, Atk(s));
        Assert.Equal("비어 있음", s.EquipmentDetail!.Parts[0].Value);
    }
}
