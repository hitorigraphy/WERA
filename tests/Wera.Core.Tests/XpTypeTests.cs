using Wera.Core;

namespace Wera.Core.Tests;

// xp_type_master, 캐릭터의 xp_type_code, xpp_changed_effect_master와 XPP 변경 서킷.
// XP는 MP, SP, TP처럼 캐릭터가 스킬을 쓸 때 쓰는 자원들의 통칭이고, 각 XP는 XPP(XP로 바꾸기 전 단계의 값)를 바탕으로 계산한다.
public class XpTypeTests
{
    const string XpTypes = "xp_type_code\txp_type_name\n1\tMP\n2\tTP\n";
    const string EffectHead = "xp_type_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
    const long Xpp = GamePack.XppStatusCode;
    const long Mana = 6;

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static string Characters(string xpType7001 = "1") =>
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\txp_type_code\n" +
        $"1\t당신\t1\t1\t2\t\n7001\t용사\t1\t1\t2\t{xpType7001}\n7002\t마법사\t1\t2\t1\t\n";

    const string Statuses = "status_code\tstatus_name\n1\tlevel\n2\thp\n3\txpp\n4\tmax_hp\n5\txpp_max\n6\tmana\n";
    static readonly string CharacterStatuses = "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" + Rows(
        "1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 25 0 40", "7001 3 0 10 0 999", "7001 6 0 0 0 9999",
        "7002 1 0 1 1 99", "7002 2 0 20 0 20", "7002 3 0 10 0 999", "7002 6 0 0 0 9999");

    // XPP × 2를 마나로: cache[1] = 2, cache[0] = XPP, cache[2] = cache[0] × cache[1], 마나 = cache[2]
    static readonly string DoubleToMana = EffectHead + Rows(
        "1 1 SetCacheValue 1 2 0", "1 2 FocusedCharacterValueToCache 3 0 0", "1 3 CacheMultiply 2 0 1", "1 4 CacheToFocusedCharacterSetValue 6 2 0");

    static GamePack Pack(string? xpTypes = XpTypes, string? effects = null, string? characters = null)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv", characters ?? Characters()),
            (GamePack.StatusMasterPath, Statuses),
            (GamePack.CharacterStatusMasterPath, CharacterStatuses),
        };
        if (xpTypes is not null) files.Add((GamePack.XpTypeMasterPath, xpTypes));
        if (effects is not null) files.Add((GamePack.XppChangedEffectMasterPath, effects));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    [Fact]
    public void The_xp_type_master_lists_the_kinds_of_xp_and_is_optional()
    {
        var pack = Pack();
        Assert.Equal([new XpType(1, "MP"), new XpType(2, "TP")], pack.XpTypes);
        Assert.Equal("TP", pack.FindXpType(2)!.Name);
        Assert.Null(pack.FindXpType(3));
        Assert.Null(Pack(null, characters: Characters("")).XpTypes);
    }

    [Fact]
    public void A_character_has_an_optional_xp_type_code_and_no_code_means_no_xp()
    {
        var pack = Pack();
        Assert.Equal(1, pack.FindCharacter(7001)!.XpTypeCode);
        Assert.Equal(0, pack.FindCharacter(7002)!.XpTypeCode);
        Assert.Equal(0, pack.FindCharacter(1)!.XpTypeCode);
    }

    [Fact]
    public void The_description_and_xp_type_columns_can_come_in_either_order()
    {
        var characters =
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\txp_type_code\tdescription\n" +
            "1\t당신\t1\t1\t2\t\t주인공\n7001\t용사\t1\t1\t2\t2\t검사\n7002\t마법사\t1\t2\t1\t\t\n";
        var pack = Pack(characters: characters);
        Assert.Equal((2L, "검사"), (pack.FindCharacter(7001)!.XpTypeCode, pack.FindCharacter(7001)!.Description));
    }

    [Fact]
    public void Bad_xp_masters_are_rejected()
    {
        Assert.Contains("중복", Assert.Throws<PackFormatException>(() => Pack("xp_type_code\txp_type_name\n1\tMP\n1\tTP\n")).Message);
        Assert.Contains("이름이 비어", Assert.Throws<PackFormatException>(() => Pack("xp_type_code\txp_type_name\n1\t\n")).Message);
        Assert.Contains("xp_type_code '9'", Assert.Throws<PackFormatException>(() => Pack(characters: Characters("9"))).Message);
        Assert.Contains("xp_type_code '1'", Assert.Throws<PackFormatException>(() => Pack(null, characters: Characters("1"))).Message);   // XP 종류 마스터가 없으면 코드를 쓸 수 없다
        Assert.Contains("XP 종류 코드 '9'", Assert.Throws<PackFormatException>(() => Pack(effects: EffectHead + Rows("9 1 None"))).Message);
        Assert.Contains("xp_type_master_v1.tsv도 필요", Assert.Throws<PackFormatException>(() => Pack(null, EffectHead + Rows("1 1 None"), Characters(""))).Message);
        Assert.Contains("두 번", Assert.Throws<PackFormatException>(() => Pack(effects: EffectHead + Rows("1 1 None", "1 1 None"))).Message);
        Assert.Contains("'Explode'", Assert.Throws<PackFormatException>(() => Pack(effects: EffectHead + Rows("1 1 Explode"))).Message);
        Assert.Contains("같은 XP 종류에 없습니다", Assert.Throws<PackFormatException>(() => Pack(effects: EffectHead + Rows("1 1 Jump 5", "2 5 None"))).Message);
    }

    [Fact]
    public void The_circuit_computes_each_xp_from_the_xpp_of_the_character()
    {
        var pack = Pack(effects: DoubleToMana);
        Assert.Equal([1L, 2L, 3L, 4L], pack.XppChangedEffectsOf(1).Select(e => e.ExecuteNumber));
        Assert.Empty(pack.XppChangedEffectsOf(2));

        var character = pack.NewCharacterInstance(7001);
        Assert.Equal(0, character.Statuses[Mana]);
        var computed = new XppChangedCircuit(pack, character).Execute();
        Assert.Equal(20, computed.Statuses[Mana]);                                         // XPP 10 × 2
    }

    [Fact]
    public void The_session_runs_the_circuit_when_a_character_is_added_or_the_xpp_changes()
    {
        var s = GameSession.NewGame(Pack(effects: DoubleToMana));
        Assert.True(s.AddCharacter(7001));
        Assert.Equal(20, s.Characters[7001].Statuses[Mana]);                               // 얻을 때 처음 XPP로 계산한다

        s.Characters[7001] = s.Characters[7001].WithStatus(Xpp, 50);
        Assert.True(s.RunXppChanged(7001));
        Assert.Equal(100, s.Characters[7001].Statuses[Mana]);

        Assert.True(s.AddCharacter(7002));
        Assert.False(s.RunXppChanged(7002));                                               // XP 종류가 없는 캐릭터는 아무 일도 없다
        Assert.False(s.RunXppChanged(9999));                                               // 보유하지 않은 캐릭터
        Assert.Equal(0, s.Characters[7002].Statuses[Mana]);
    }
}
