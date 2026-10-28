using Wera.Core;

namespace Wera.Core.Tests;

// 아이템 확인 화면: 가진 커먼 아이템을 "[코드] 이름 수량 N"으로 인벤토리 마스터 순서대로 나열한다.
public class ItemListTests
{
    static GameSession Session() => GameSession.NewGame(GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        (GamePack.CommonItemInventoryMasterPath, "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n3\t약초\t99\n4\t철광석\t99\n"))));

    [Fact]
    public void Nothing_is_listed_when_no_item_is_owned()
    {
        Assert.Empty(Session().Items);
    }

    [Fact]
    public void Owned_items_are_listed_in_master_order_with_code_name_and_amount()
    {
        var s = Session();
        s.AddInventory(4, 7);
        s.AddInventory(2, 120);                      // 얻은 순서와 상관없이 마스터 순서
        Assert.Equal([("[2]", "금화", "수량 120"), ("[4]", "철광석", "수량 7")], s.Items.Select(r => (r.Code, r.Name, r.Amount)));

        s.AddInventory(4, -7);                       // 다 쓰면 목록에서 빠진다
        Assert.Equal(["금화"], s.Items.Select(r => r.Name));
    }

    [Fact]
    public void The_item_check_screen_opens_from_the_home_menu_and_goes_back()
    {
        var s = Session();
        s.Activate(GameSession.ItemCheckEntryCode);
        Assert.Equal(Screen.ItemCheck, s.Screen);
        s.Activate(GameSession.BackEntryCode);
        Assert.Equal(Screen.Home, s.Screen);
    }
}
