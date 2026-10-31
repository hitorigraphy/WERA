using Wera.Core;

namespace Wera.Core.Tests;

// xp_type_master, 캐릭터의 xp_type_code, xp_calculate_effect_master와 XPP 변경 서킷.
// XP는 MP, SP, TP처럼 캐릭터가 스킬을 쓸 때 쓰는 자원들의 통칭이고, 각 XP는 XPP(XP로 바꾸기 전 단계의 값)를 바탕으로 계산한다.
public class XpTypeTests
{
    const string XpTypes = "xp_type_code\txp_type_name\n1\tMP\n2\tTP\n";
    const string EffectHead = "xp_type_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
    const long Xpp = GamePack.XppStatusCode;

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static string Characters(string xpType7001 = "1") =>
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\txp_type_code\n" +
        $"1\t당신\t1\t1\t2\t\n7001\t용사\t1\t1\t2\t{xpType7001}\n7002\t마법사\t1\t2\t1\t\n";

    const string Statuses = "status_code\tstatus_name\n1\tlevel\n2\thp\n3\txpp\n4\tmax_hp\n5\txpp_max\n6\tmana\n";
    static readonly string CharacterStatuses = "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" + Rows(
        "1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 25 0 40", "7001 3 0 10 0 999", "7001 6 0 0 0 9999",
        "7002 1 0 1 1 99", "7002 2 0 20 0 20", "7002 3 0 10 0 999", "7002 6 0 0 0 9999");

    // 결과(XP 값 = 캐시 0번, 최댓값 = 캐시 1번): XPP × 2가 XP 값, 최댓값은 100
    static readonly string DoubleXpp = EffectHead + Rows(
        "1 1 SetCacheValue 2 2 0", "1 2 FocusedCharacterValueToCache 3 3 0", "1 3 CacheMultiply 0 3 2", "1 4 SetCacheValue 1 100 0");

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
        if (effects is not null) files.Add((GamePack.XpCalculateEffectMasterPath, effects));
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
    public void The_circuit_computes_the_xp_result_from_the_xpp_of_the_character()
    {
        var pack = Pack(effects: DoubleXpp);
        Assert.Equal([1L, 2L, 3L, 4L], pack.XpCalculateEffectsOf(1).Select(e => e.ExecuteNumber));
        Assert.Empty(pack.XpCalculateEffectsOf(2));

        var character = pack.NewCharacterInstance(7001);
        Assert.Equal(new XpResult(20, 100), new XpCalculateCircuit(pack, character).Execute());   // XPP 10 × 2
        Assert.Equal(new XpResult(100, 100), new XpCalculateCircuit(pack, character.WithStatus(Xpp, 50)).Execute());
        Assert.Null(new XpCalculateCircuit(pack, pack.NewCharacterInstance(7002)).Execute());      // XP 종류가 없는 캐릭터
        Assert.Equal(new XpResult(0, 0), new XpCalculateCircuit(Pack(), character).Execute());     // 이펙트가 없으면 0/0
    }

    [Fact]
    public void The_slot_shows_the_xp_name_and_gauge_from_the_result_and_follows_the_xpp()
    {
        var s = GameSession.NewGame(Pack(effects: DoubleXpp));
        s.AddCharacter(7001); s.AddCharacter(7002);
        s.Party[0] = 7001;
        s.Party[1] = 7002;
        var v = s.PartySlotViews[0];
        Assert.Equal("MP", v.XpName);
        Assert.Equal(Gauge.Render(20, 100, gray: false), v.Xp);                             // 결과 20/100
        s.Characters[7001] = s.Characters[7001].WithStatus(Xpp, 40);                       // XPP가 바뀌면 다음에 보일 때 다시 계산된다
        Assert.Equal(Gauge.Render(80, 100, gray: false), s.PartySlotViews[0].Xp);

        Assert.Equal("", s.PartySlotViews[1].XpName);                                       // XP 종류가 없는 캐릭터
        Assert.Equal(Gauge.Render(0, 0, gray: true), s.PartySlotViews[1].Xp);
        Assert.Equal("MP  : ", GameSession.XpLabel("MP"));
        Assert.Equal("마나 : ", GameSession.XpLabel("마나"));
    }
}
