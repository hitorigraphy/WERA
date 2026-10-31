using Wera.Core;

namespace Wera.Core.Tests;

// character_after_create_effect_master / unique_item_after_create_effect_master와 생성 후 서킷: 새로 얻을 때 그 캐릭터·아이템을 초기화한다.
public class AfterCreateEffectTests
{
    const string CharacterHead = "character_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
    const string ItemHead = "item_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
    const long Mana = 6;
    const long Durability = 7;

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(string characterEffects, string itemEffects) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        (GamePack.CharacterInventoryMasterPath,
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t1\t2\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n6\tmana\n7\tdurability\n"),
        (GamePack.CharacterStatusMasterPath, "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
            Rows("1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 40 0 40", "7001 6 0 10 0 20", "7002 1 0 1 1 99", "7002 2 0 40 0 40", "7002 6 0 10 0 20")),
        (GamePack.UniqueItemInventoryMasterPath, "inventory_code\titem_name\n8001\t철검\n8002\t대검\n"),
        (GamePack.UniqueItemStatusMasterPath, "item_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n" + Rows("8001 7 100 0 100", "8002 7 100 0 100")),
        (GamePack.CharacterAfterCreateEffectMasterPath, CharacterHead + characterEffects),
        (GamePack.UniqueItemAfterCreateEffectMasterPath, ItemHead + itemEffects)));

    [Fact]
    public void Rows_are_grouped_by_owner_and_read_in_execute_number_order_and_the_masters_are_optional()
    {
        var pack = Pack(Rows("7001 20 None 1 2 3", "7001 10 none", "7002 5 None"), Rows("8001 2 None", "8001 1 None"));
        Assert.Equal([10L, 20L], pack.CharacterAfterCreateEffectsOf(7001).Select(e => e.ExecuteNumber));
        Assert.Equal(new CharacterAfterCreateEffect(7001, 20, CharacterAfterCreateEffectType.None, 1, 2, 3), pack.CharacterAfterCreateEffectsOf(7001)[1]);
        Assert.Equal([5L], pack.CharacterAfterCreateEffectsOf(7002).Select(e => e.ExecuteNumber));
        Assert.Empty(pack.CharacterAfterCreateEffectsOf(1));
        Assert.Equal([1L, 2L], pack.UniqueItemAfterCreateEffectsOf(8001).Select(e => e.ExecuteNumber));
        Assert.Empty(pack.UniqueItemAfterCreateEffectsOf(8002));

        var bare = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Null(bare.CharacterAfterCreateEffects);
        Assert.Null(bare.UniqueItemAfterCreateEffects);
        Assert.Empty(bare.CharacterAfterCreateEffectsOf(1));
        Assert.Empty(bare.UniqueItemAfterCreateEffectsOf(8001));
    }

    [Fact]
    public void Owner_codes_must_exist_and_the_key_cannot_repeat_and_types_must_be_names()
    {
        string CharErr(string rows) => Assert.Throws<PackFormatException>(() => Pack(rows, "")).Message;
        string ItemErr(string rows) => Assert.Throws<PackFormatException>(() => Pack("", rows)).Message;
        Assert.Contains("캐릭터 인벤토리 마스터에 없습니다", CharErr(Rows("9999 1 None")));
        Assert.Contains("두 번", CharErr(Rows("7001 1 None", "7001 1 None")));
        Pack(Rows("7001 1 None", "7002 1 None"), "");                    // 다른 캐릭터면 같은 번호여도 된다
        Assert.Contains("'Explode'", CharErr(Rows("7001 1 Explode")));
        Assert.Contains("'0'", CharErr(Rows("7001 1 0")));
        Assert.Contains("유니크 아이템 인벤토리 마스터에 없습니다", ItemErr(Rows("9999 1 None")));
        Assert.Contains("두 번", ItemErr(Rows("8001 1 None", "8001 1 None")));
    }

    [Fact]
    public void Row_arguments_are_checked_and_jumps_stay_inside_the_same_owner()
    {
        string Err(string rows) => Assert.Throws<PackFormatException>(() => Pack(rows, "")).Message;
        Assert.Contains("스테이터스 마스터에 없습니다", Err(Rows("7001 1 FocusedCharacterValueToCache 99 0")));
        Assert.Contains("cache_index", Err(Rows("7001 1 FocusedCharacterValueToCache 6 256")));
        Assert.Contains("value_3(소수 자릿수)", Err(Rows("7001 1 SetCacheValue 0 1 29")));
        Assert.Contains("자신의 실행 번호보다 커야", Err(Rows("7001 5 Jump 5")));
        Assert.Contains("같은 캐릭터에 없습니다", Err(Rows("7001 1 Jump 2", "7002 2 None")));     // 다른 캐릭터의 번호로는 갈 수 없다
        Assert.Contains("같은 유니크 아이템에 없습니다", Assert.Throws<PackFormatException>(() => Pack("", Rows("8001 1 Jump 2", "8002 2 None"))).Message);
    }

    [Fact]
    public void A_new_character_is_initialised_by_its_own_effects_only()
    {
        // 용사: mana(10)에 5를 더하고 hp를 캐시의 25.5로 바꾼다(25로 버림). 마법사는 아무 일도 없다.
        var pack = Pack(Rows("7001 1 SetCacheValue 0 5", "7001 2 CacheToFocusedCharacterModValue 6 0", "7001 3 SetCacheValue 1 255 1", "7001 4 CacheToFocusedCharacterSetValue 2 1"), "");
        var s = GameSession.NewGame(pack);
        Assert.Equal(40, s.Characters[GamePack.PlayerCharacterCode].Hp);       // 당신에게는 행이 없다
        Assert.True(s.AddCharacter(7001));
        Assert.True(s.AddCharacter(7002));
        Assert.Equal((15, 25), (s.Characters[7001].Statuses[Mana], s.Characters[7001].Hp));
        Assert.Equal((10, 40), (s.Characters[7002].Statuses[Mana], s.Characters[7002].Hp));
    }

    [Fact]
    public void The_character_circuit_clamps_to_the_master_range_and_finish_stops_it()
    {
        var pack = Pack(Rows("7001 1 SetCacheValue 0 50", "7001 2 CacheToFocusedCharacterModValue 6 0", "7001 3 Finish", "7001 4 CacheToFocusedCharacterSetValue 2 0"), "");
        var c = new CharacterAfterCreateCircuit(pack, pack.NewCharacterInstance(7001)).Execute();
        Assert.Equal(20, c.Statuses[Mana]);                                     // 10+50이 최댓값 20으로 맞춰졌다
        Assert.Equal(40, c.Hp);                                                 // Finish 뒤는 실행하지 않았다
    }

    [Fact]
    public void A_new_unique_item_is_initialised_by_its_own_effects_and_each_copy_starts_from_the_master()
    {
        var pack = Pack("", Rows("8001 1 FocusedItemValueToCache 7 0", "8001 2 SetCacheValue 1 30", "8001 3 CacheSubtract 2 0 1", "8001 4 CacheToFocusedItemSetValue 7 2"));
        var s = GameSession.NewGame(pack);
        var a = s.AddUniqueItem(8001)!;
        var b = s.AddUniqueItem(8001)!;
        var other = s.AddUniqueItem(8002)!;
        Assert.Equal(70, s.UniqueItemInstances[a].Statuses![Durability]);
        Assert.Equal(70, s.UniqueItemInstances[b].Statuses![Durability]);       // 마스터의 100에서 시작해 70이 된다(누적되지 않는다)
        Assert.Equal(100, s.UniqueItemInstances[other].Statuses![Durability]);
    }

    [Fact]
    public void A_load_does_not_run_the_create_effects_again()
    {
        var pack = Pack(Rows("7001 1 SetCacheValue 0 5", "7001 2 CacheToFocusedCharacterModValue 6 0"), Rows("8001 1 SetCacheValue 0 30", "8001 2 CacheToFocusedItemSetValue 7 0"));
        var s = GameSession.NewGame(pack);
        s.AddCharacter(7001);
        var id = s.AddUniqueItem(8001)!;
        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(15, loaded.Characters[7001].Statuses[Mana]);
        Assert.Equal(30, loaded.UniqueItemInstances[id].Statuses![Durability]);
    }
}
