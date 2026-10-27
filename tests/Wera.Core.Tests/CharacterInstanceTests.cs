using Wera.Core;

namespace Wera.Core.Tests;

static class CharacterInstanceTestExtensions
{
    /// <summary>인스턴스 ID, 마스터 코드, 순서를 지우고 상태만 남긴다(상태만 견줄 때).</summary>
    public static CharacterInstance StatsOnly(this CharacterInstance c) => c with { InstanceId = "", CharacterCode = 0, OrderNumber = 0 };
}

// 캐릭터와 유니크 아이템 인스턴스는 인스턴스 ID와 마스터 코드를 구조체 안에 함께 가진다.
// 캐릭터 인스턴스는 OrderNumber가 있고 동료 확인은 OrderNumber순, 같으면 InstanceId순으로 보여 준다.
public class CharacterInstanceTests
{
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
        "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n7003\t전사\t1\t1\t2\n";

    static GamePack Pack() => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/character_inventory_master_v1.tsv", Characters)));

    [Fact]
    public void A_new_character_instance_carries_its_id_master_code_and_order()
    {
        var s = GameSession.NewGame(Pack());
        s.AddCharacter(7002);
        s.AddCharacter(7001);
        var player = s.Characters[1];
        var wizard = s.Characters[7002];
        var hero = s.Characters[7001];
        Assert.Equal([1L, 7002L, 7001L], new[] { player.CharacterCode, wizard.CharacterCode, hero.CharacterCode });
        Assert.All(new[] { player, wizard, hero }, c => Assert.True(Ulid.TryParse(c.InstanceId, out _)));
        Assert.Equal(3, new[] { player, wizard, hero }.Select(c => c.InstanceId).Distinct().Count());
        Assert.Equal([1L, 2L, 3L], new[] { player.OrderNumber, wizard.OrderNumber, hero.OrderNumber });   // 소지 캐릭터 수 + 1: 1, 2, 3
        Assert.True(string.CompareOrdinal(player.InstanceId, wizard.InstanceId) < 0);                     // ID도 만든 순서대로 늘어난다
        Assert.True(string.CompareOrdinal(wizard.InstanceId, hero.InstanceId) < 0);
    }

    [Fact]
    public void The_owned_list_is_sorted_by_order_number_then_instance_id()
    {
        var s = GameSession.NewGame(Pack());
        s.AddCharacter(7001);
        s.AddCharacter(7002);
        s.AddCharacter(7003);
        Assert.Equal([1L, 7001L, 7002L, 7003L], s.OwnedCharacters.Select(c => c.CharacterCode));

        // 순서 번호를 바꾸면 그 순서로, 같은 순서 번호면 인스턴스 ID 순서로
        s.Characters[7003] = s.Characters[7003] with { OrderNumber = 0 };
        s.Characters[1] = s.Characters[1] with { OrderNumber = 0 };
        Assert.Equal([1L, 7003L, 7001L, 7002L], s.OwnedCharacters.Select(c => c.CharacterCode));            // 둘 다 0: 플레이어의 ID가 더 이르다
        s.Characters[7002] = s.Characters[7002] with { OrderNumber = -5 };
        Assert.Equal([7002L, 1L, 7003L, 7001L], s.OwnedCharacters.Select(c => c.CharacterCode));
    }

    [Fact]
    public void The_companion_list_has_every_owned_character_including_the_player_in_that_order()
    {
        var s = GameSession.NewGame(Pack());
        Assert.Equal(["당신"], s.CompanionList.Select(c => c.Name));
        s.AddCharacter(7003);
        s.AddCharacter(7001);
        Assert.Equal(["당신", "전사", "용사"], s.CompanionList.Select(c => c.Name));
        Assert.Equal([1L, 7003L, 7001L], s.CompanionList.Select(c => c.CharacterCode!.Value));
    }

    [Fact]
    public void Ids_and_order_numbers_survive_a_save_and_load()
    {
        var s = GameSession.NewGame(Pack());
        s.AddCharacter(7002);
        s.AddCharacter(7001);
        var loaded = GameSession.FromSaveJson(Pack(), s.ToSaveJson());
        Assert.Equal(s.Characters.OrderBy(kv => kv.Key), loaded.Characters.OrderBy(kv => kv.Key));
        Assert.Equal(s.OwnedCharacters.Select(c => c.InstanceId), loaded.OwnedCharacters.Select(c => c.InstanceId));
    }

    [Fact]
    public void An_old_save_without_ids_gets_new_ones_in_code_order()
    {
        var json = "{\"Version\":1,\"PackId\":\"demo\",\"Characters\":{" +
            "\"7002\":{\"Level\":3,\"Hp\":10,\"MaxHp\":20,\"Xp\":0,\"XpMax\":30}," +
            "\"7001\":{\"Level\":2,\"Hp\":10,\"MaxHp\":20,\"Xp\":0,\"XpMax\":30}}}";
        var s = GameSession.FromSaveJson(Pack(), json);
        Assert.Equal([1L, 7001L, 7002L], s.Characters.Keys.Order());
        Assert.All(s.Characters, kv =>
        {
            Assert.True(Ulid.TryParse(kv.Value.InstanceId, out _));
            Assert.Equal(kv.Key, kv.Value.CharacterCode);
        });
        Assert.Equal(3, s.Characters.Values.Select(c => c.InstanceId).Distinct().Count());
        Assert.Equal(3L, s.Characters[7002].Level);
    }

    [Fact]
    public void A_save_with_a_wrong_master_code_or_a_bad_or_repeated_id_is_rejected()
    {
        var id = Ulid.NewUlid().ToString();
        string Save(string a, string b) => "{\"Version\":1,\"PackId\":\"demo\",\"Characters\":{" +
            $"\"7001\":{{{a},\"Level\":1,\"Hp\":1,\"MaxHp\":1,\"Xp\":0,\"XpMax\":0}}," +
            $"\"7002\":{{{b},\"Level\":1,\"Hp\":1,\"MaxHp\":1,\"Xp\":0,\"XpMax\":0}}}}}}";
        Assert.Contains("키와 다릅니다", Assert.Throws<SaveFormatException>(() =>
            GameSession.FromSaveJson(Pack(), Save("\"CharacterCode\":7002", "\"CharacterCode\":7002"))).Message);
        Assert.Contains("ULID가 아닙니다", Assert.Throws<SaveFormatException>(() =>
            GameSession.FromSaveJson(Pack(), Save("\"InstanceId\":\"nope\"", "\"CharacterCode\":7002"))).Message);
        Assert.Contains("두 번 이상", Assert.Throws<SaveFormatException>(() =>
            GameSession.FromSaveJson(Pack(), Save($"\"InstanceId\":\"{id}\"", $"\"InstanceId\":\"{id}\""))).Message);
    }

    [Fact]
    public void A_unique_item_instance_carries_its_id_and_master_code()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t검\n")));
        var s = GameSession.NewGame(pack);
        var id = s.AddUniqueItem(8001)!;
        var instance = s.UniqueItemInstances[id];
        Assert.Equal((id, 8001L), (instance.InstanceId, instance.ItemCode));

        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal((id, 8001L), (loaded.UniqueItemInstances[id].InstanceId, loaded.UniqueItemInstances[id].ItemCode));
    }

    [Fact]
    public void An_old_unique_item_save_without_the_id_inside_still_loads()
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t검\n")));
        var id = Ulid.NewUlid().ToString();
        var s = GameSession.FromSaveJson(pack, $"{{\"Version\":1,\"PackId\":\"demo\",\"UniqueItems\":{{\"{id}\":{{\"ItemCode\":8001}}}}}}");
        Assert.Equal(id, s.UniqueItemInstances[id].InstanceId);
    }
}
