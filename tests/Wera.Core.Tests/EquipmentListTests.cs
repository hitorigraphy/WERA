using Wera.Core;

namespace Wera.Core.Tests;

// 유니크 아이템 인스턴스의 OrderNumber와 장비 확인 화면의 "[순서 번호] 장비 이름" 목록, pack.tsv의 디버깅용 초기 부여.
public class EquipmentListTests
{
    static GamePack Pack(string packTsv = "id\tdemo\ntitle\t데모\n") => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", packTsv),
        ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n8002\t대검\n8007\t순례자의 은단검\n")));

    [Fact]
    public void A_new_unique_item_gets_the_owned_count_as_its_order_number_starting_at_zero()
    {
        var s = GameSession.NewGame(Pack());
        var a = s.AddUniqueItem(8001)!;
        var b = s.AddUniqueItem(8002)!;
        var c = s.AddUniqueItem(8001)!;
        Assert.Equal([0L, 1L, 2L], new[] { a, b, c }.Select(id => s.UniqueItemInstances[id].OrderNumber));
    }

    [Fact]
    public void The_equipment_list_is_sorted_by_order_number_then_instance_id_and_shows_order_and_name()
    {
        var s = GameSession.NewGame(Pack());
        var sword = s.AddUniqueItem(8001)!;
        var great = s.AddUniqueItem(8002)!;
        var dagger = s.AddUniqueItem(8007)!;
        Assert.Equal([("[0]", "철검"), ("[1]", "대검"), ("[2]", "순례자의 은단검")], s.Equipments.Select(e => (e.Order, e.Name)));

        // 순서 번호를 바꾸면 그 순서로, 같으면 인스턴스 ID 순서로
        s.UniqueItemInstances[dagger] = s.UniqueItemInstances[dagger] with { OrderNumber = 0 };
        Assert.Equal(["철검", "순례자의 은단검", "대검"], s.Equipments.Select(e => e.Name));         // 둘 다 0: 철검의 ID가 더 이르다
        s.UniqueItemInstances[great] = s.UniqueItemInstances[great] with { OrderNumber = -1 };
        Assert.Equal(["대검", "철검", "순례자의 은단검"], s.Equipments.Select(e => e.Name));
        Assert.Equal(["[-1]", "[0]", "[0]"], s.Equipments.Select(e => e.Order));
        _ = sword;
    }

    [Fact]
    public void Order_numbers_survive_a_save_and_an_old_save_without_them_loads_with_zero()
    {
        var pack = Pack();
        var s = GameSession.NewGame(pack);
        s.AddUniqueItem(8001);
        s.AddUniqueItem(8002);
        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(s.Equipments.Select(e => (e.Order, e.Name)), loaded.Equipments.Select(e => (e.Order, e.Name)));

        var id = Ulid.NewUlid().ToString();
        var old = GameSession.FromSaveJson(pack, $"{{\"Version\":1,\"PackId\":\"demo\",\"UniqueItems\":{{\"{id}\":{{\"ItemCode\":8001}}}}}}");
        Assert.Equal(0, old.UniqueItemInstances[id].OrderNumber);
    }

    [Fact]
    public void The_debug_start_unique_items_are_granted_when_a_new_game_starts()
    {
        var s = GameSession.NewGame(Pack("id\tdemo\ntitle\t데모\ndebug_start_unique_items\t8007, 8001\n"));
        Assert.Equal(["순례자의 은단검", "철검"], s.Equipments.Select(e => e.Name));
        Assert.Equal(["[0]", "[1]"], s.Equipments.Select(e => e.Order));

        Assert.Empty(GameSession.NewGame(Pack()).Equipments);                                      // 항목이 없으면 아무것도 주지 않는다
        // 불러온 게임은 새로 주지 않는다
        var loaded = GameSession.FromSaveJson(Pack(), "{\"Version\":1,\"PackId\":\"demo\"}");
        Assert.Empty(loaded.Equipments);
    }

    [Theory]
    [InlineData("9999", "유니크 아이템 마스터에 없습니다")]
    [InlineData("8001,x", "정수")]
    public void Bad_debug_start_unique_items_are_rejected(string value, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() =>
            Pack($"id\tdemo\ntitle\t데모\ndebug_start_unique_items\t{value}\n")).Message);

    [Fact]
    public void The_sample_pack_has_the_pilgrims_silver_dagger_from_the_start()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Directory.GetParent(root)!.FullName;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        var s = GameSession.NewGame(GamePack.Load(zip));
        Assert.Equal([("[0]", "순례자의 은단검")], s.Equipments.Select(e => (e.Order, e.Name)));
    }
}
