using Wera.Core;

namespace Wera.Core.Tests;

// 디버그(400): 마스터의 캐릭터/장비/아이템 전부를 페이지당 20개씩 보여 주고 누르면 하나 추가한다.
public class DebugScreenTests
{
    static GamePack SamplePack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(zip);
    }

    // 캐릭터 1(당신)과 7001~7044, 합계 45명
    static GamePack ManyCharacters()
    {
        var rows = "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n" +
            string.Concat(Enumerable.Range(7001, 44).Select(c => $"{c}\t용사{c}\t1\t1\t2\n"));
        return GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/character_inventory_master_v1.tsv", rows)));
    }

    [Fact]
    public void The_system_menu_has_a_debug_button_that_opens_the_three_add_screens()
    {
        var s = GameSession.NewGame(SamplePack());
        Assert.Equal(["200", "300", "400"], s.HomeMenu[2].Entries.Select(e => e.Code));
        Assert.Equal(Screen.Debug, s.Activate("400")!.Target);
        Assert.Equal(["401", "402", "403", "999"], s.Entries.Select(e => e.Code));
        Assert.Equal(["캐릭터 추가", "장비 추가", "아이템 추가", "돌아가기"], s.Entries.Select(e => e.Label));
        Assert.Equal(Screen.DebugAddCharacter, s.Activate("401")!.Target);
        s.Activate("999");
        Assert.Equal(Screen.Debug, s.Screen);
        s.Activate("999");
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void The_character_list_shows_every_master_character_twenty_per_page()
    {
        var s = GameSession.NewGame(ManyCharacters());
        s.Activate("400");
        s.Activate("401");
        Assert.Equal(45, s.DebugList.Count);
        Assert.Equal(3, s.DebugPageCount);
        Assert.Equal(Enumerable.Range(1, 20).Select(i => i.ToString()), s.Entries.Where(e => e.Action == EntryAction.DebugAdd).Select(e => e.Code));
        Assert.False(s.Entries.Single(e => e.Code == "101").Enabled);            // 첫 페이지에는 이전이 없다
        Assert.True(s.Entries.Single(e => e.Code == "102").Enabled);

        s.Activate("102");
        s.Activate("102");
        Assert.Equal(2, s.DebugPage);
        Assert.Equal(5, s.Entries.Count(e => e.Action == EntryAction.DebugAdd));  // 마지막 페이지는 나머지 5개
        Assert.False(s.Entries.Single(e => e.Code == "102").Enabled);
        s.Activate("102");                                                        // 끝에서는 더 넘어가지 않는다
        Assert.Equal(2, s.DebugPage);
        s.Activate("101");
        Assert.Equal(1, s.DebugPage);

        s.Activate("999");                                                        // 다시 들어오면 첫 페이지
        s.Activate("401");
        Assert.Equal(0, s.DebugPage);
    }

    [Fact]
    public void Pressing_a_character_adds_it_once()
    {
        var s = GameSession.NewGame(ManyCharacters());
        s.Activate("400");
        s.Activate("401");
        Assert.False(s.Characters.ContainsKey(7001));
        s.Activate("2");                                                          // 첫 페이지의 둘째: 7001(당신 다음)
        Assert.True(s.Characters.ContainsKey(7001));
        Assert.Contains("캐릭터 추가", s.Message);
        Assert.Equal(Screen.DebugAddCharacter, s.Screen);
        s.Activate("2");
        Assert.Contains("이미 가지고 있는", s.Message);
        Assert.Equal(2, s.Characters.Count);
    }

    [Fact]
    public void Pressing_equipment_and_items_adds_them_and_items_stop_at_the_carry_limit()
    {
        var pack = SamplePack();
        var s = GameSession.NewGame(pack);
        s.Activate("400");
        s.Activate("402");
        Assert.Equal(pack.UniqueItems!.Select(i => i.ItemCode), s.DebugList.Select(d => d.Code));
        s.Activate("1");
        s.Activate("1");
        Assert.Equal(2, s.UniqueItemInstances.Count(kv => kv.Value.ItemCode == pack.UniqueItems![0].ItemCode));    // 장비는 몇 번이고 얻는다

        s.Activate("999");
        s.Activate("403");
        Assert.Equal(pack.ItemInventory!.Select(i => i.Code), s.DebugList.Select(d => d.Code));
        var item = pack.ItemInventory![0];
        for (var i = 0; i < item.CarryLimit + 1; i++) s.Activate("1");
        Assert.Equal(item.CarryLimit, s.InventoryItems[item.Code].Amount);
        Assert.Contains("소지 한도", s.Message);
    }
}
