using Wera.Core;

namespace Wera.Core.Tests;

// status_master: 스테이터스 종류. level(1)과 hp(2)는 엔진이 예약한 코드라 반드시 있어야 하고, xpp(3)는 선택이다.
// character_status_master: 캐릭터 × 스테이터스 × 레벨별 (초깃값, 최솟값, 최댓값). 레벨 0 행은 폴백이자 캐릭터 생성 시의 값.
public class CharacterStatusTests
{
    const string Pack0 = "id\tdemo\ntitle\t데모\n";
    const string Attributes = "attribute_code\tattribute_name\n1\t불\n2\t물\n";
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n";
    const string Statuses = "status_code\tstatus_name\n3\txp\n1\tlevel\n2\thp\n4\texp\n";
    const string Header = "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n";

    // 모든 캐릭터는 level과 hp의 레벨 0 행이 있어야 한다.
    const string Base =
        Header +
        "7001\t2\t0\t40\t0\t40\n" +      // 용사 hp: 기본값(레벨 0)
        "7001\t1\t0\t1\t1\t99\n" +       // 용사 level
        "7002\t2\t0\t20\t0\t20\n" +      // 마법사 hp
        "7002\t1\t0\t1\t1\t99\n" +       // 마법사 level
        "1\t2\t0\t10\t0\t10\n" +          // 플레이어(캐릭터 1, 시험 도우미가 캐릭터 마스터 끝에 붙인다) hp
        "1\t1\t0\t1\t1\t99\n";           // 플레이어 level

    static GamePack Load(string? statuses, string? characterStatuses, string characters = Characters)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", Pack0),
            ("master/attribute_master_v1.tsv", Attributes),
            ("master/character_inventory_master_v1.tsv", characters),
        };
        if (statuses is not null) files.Add(("master/status_master_v1.tsv", statuses));
        if (characterStatuses is not null) files.Add(("master/character_status_master_v1.tsv", characterStatuses));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    // ---- 스테이터스 마스터 ----

    [Fact]
    public void Statuses_are_loaded_in_file_order_and_found_by_code()
    {
        var pack = Load(Statuses, null);
        Assert.Equal([3L, 1L, 2L, 4L], pack.Statuses!.Select(s => s.Code));
        Assert.Equal(new StatusEntry(2, "hp"), pack.FindStatus(2));
        Assert.Null(pack.FindStatus(99));
        Assert.Equal(4, pack.Masters.StatusRowTable.Count);
    }

    [Theory]
    [InlineData("status_code\tstatus_name\n1\tlevel\n1\t또 level\n2\thp\n", "중복")]
    [InlineData("status_code\tstatus_name\nx\tlevel\n2\thp\n", "정수")]
    public void Status_master_rejects_duplicates_and_bad_codes(string statuses, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(statuses, null)).Message);

    [Theory]
    [InlineData("status_code\tstatus_name\n2\thp\n3\txp\n", "1(level)")]     // level이 없다
    [InlineData("status_code\tstatus_name\n1\tlevel\n3\txp\n", "2(hp)")]      // hp가 없다
    [InlineData("status_code\tstatus_name\n3\txp\n", "예약")]
    public void Status_master_must_define_the_reserved_level_and_hp_codes(string statuses, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(statuses, null)).Message);

    [Fact]
    public void The_reserved_codes_may_carry_any_name_and_xp_is_optional()
    {
        var pack = Load("status_code\tstatus_name\n1\t레벨\n2\t체력\n", null);
        Assert.Equal("체력", pack.FindStatus(GamePack.HpStatusCode)!.Name);
        Assert.Null(pack.FindStatus(GamePack.XppStatusCode));
    }

    // ---- 캐릭터 스테이터스 마스터: 키와 폴백 ----

    [Fact]
    public void The_key_is_character_status_and_level()
    {
        var pack = Load(Statuses, Base + "7001\t2\t5\t80\t0\t80\n");
        var table = pack.Masters.CharacterStatusRowTable;
        Assert.Equal(80, table.FindByCharacterCodeAndStatusCodeAndLevel((7001, 2, 5)).InitialValue);
        Assert.Equal(40, table.FindByCharacterCodeAndStatusCodeAndLevel((7001, 2, 0)).InitialValue);
        Assert.False(table.TryFindByCharacterCodeAndStatusCodeAndLevel((7001, 2, 6), out _));
        Assert.Equal(7, table.Count);
        Assert.Equal(3, table.FindByCharacterCode(7001).Count);
        Assert.False(pack.Masters.Validate().IsValidationFailed);
    }

    [Fact]
    public void A_level_with_a_row_uses_it_and_other_levels_fall_back_to_level_zero()
    {
        var pack = Load(Statuses, Base + "7001\t2\t5\t80\t0\t80\n");
        Assert.Equal(new CharacterStatus(7001, 2, 5, 80, 0, 80), pack.CharacterStatusAt(7001, 2, 5));
        Assert.Equal(new CharacterStatus(7001, 2, 0, 40, 0, 40), pack.CharacterStatusAt(7001, 2, 3));     // 레벨 3 행이 없다 → 레벨 0
        Assert.Equal(new CharacterStatus(7001, 2, 0, 40, 0, 40), pack.CharacterStatusAt(7001, 2, 99));
        Assert.Equal(new CharacterStatus(7001, 2, 0, 40, 0, 40), pack.CharacterStatusAt(7001, 2, 0));
        Assert.Equal(20, pack.CharacterStatusAt(7002, 2, 5)!.InitialValue);                               // 캐릭터마다 따로
    }

    [Fact]
    public void A_status_without_any_row_has_no_value_at_any_level()
    {
        var pack = Load(Statuses, Base);
        Assert.Null(pack.CharacterStatusAt(7001, 3, 5));          // xpp 행이 없다
        Assert.Null(pack.CharacterStatusAt(7001, 3, 0));
        Assert.Null(pack.CharacterStatusAt(9999, 2, 5));
    }

    [Fact]
    public void Creating_a_character_reads_level_zero()
    {
        var pack = Load(Statuses, Base + "7001\t2\t5\t80\t0\t80\n");
        Assert.Equal(40, pack.InitialCharacterStatus(7001, 2)!.InitialValue);        // 레벨 5 행(80)이 아니라 레벨 0
        Assert.Equal(1, pack.InitialCharacterStatus(7001, 1)!.InitialValue);
        Assert.Null(pack.InitialCharacterStatus(7002, 3));
    }

    [Fact]
    public void A_characters_rows_are_listed_in_key_order()
    {
        var pack = Load(Statuses, Base + "7001\t2\t5\t80\t0\t80\n");
        Assert.Equal([(1L, 0L), (2L, 0L), (2L, 5L)], pack.CharacterStatusesOf(7001).Select(s => (s.StatusCode, s.Level)));
        Assert.Empty(pack.CharacterStatusesOf(9999));
    }

    [Fact]
    public void Values_may_be_zero_or_negative_within_the_range()
    {
        var pack = Load(Statuses, Base + "7001\t4\t0\t-5\t-10\t0\n7001\t3\t0\t0\t0\t0\n");
        Assert.Equal(-5, pack.InitialCharacterStatus(7001, 4)!.InitialValue);
        Assert.Equal(-10, pack.InitialCharacterStatus(7001, 4)!.MinValue);
    }

    [Fact]
    public void Both_masters_get_helper_defaults_and_the_defaults_make_a_minimal_living_player()
    {
        var pack = Load(null, null);                                 // 시험 도우미가 기본값을 채운다(실제 게임팩은 직접 적어야 한다)
        Assert.Equal([1L, 2L, 3L], pack.Statuses!.Select(s => s.Code));
        Assert.Equal(CharacterInstance.Stats(1, 1, 1, 0, 0), pack.NewCharacterInstance(7001).StatsOnly());
        Assert.Empty(pack.CharacterStatusesOf(9999));
    }

    [Theory]
    [InlineData("master/status_master_v1.tsv")]
    [InlineData("master/character_status_master_v1.tsv")]
    public void Both_masters_are_required_when_the_pack_is_loaded(string path) =>
        Assert.Contains(path, Assert.Throws<PackFormatException>(() =>
            GamePack.Load(CoreTests.PackZipWithout(path, ("pack.tsv", Pack0)))).Message);

    // ---- 검증 ----

    [Theory]
    [InlineData("9999\t1\t0\t1\t0\t5\n", "캐릭터 '9999'")]
    [InlineData("7001\t99\t0\t1\t0\t5\n", "스테이터스 '99'")]
    [InlineData("7001\t4\t-1\t1\t0\t5\n", "음수")]
    [InlineData("7001\t4\t0\t1\t0\t5\n7001\t4\t0\t2\t0\t5\n", "같은 키")]
    [InlineData("7001\t4\t0\t1\t5\t0\n", "최솟값 5")]
    [InlineData("7001\t4\t0\t9\t0\t5\n", "범위 밖")]
    [InlineData("7001\t4\t0\t-1\t0\t5\n", "범위 밖")]
    [InlineData("7001\t4\t0\tabc\t0\t5\n", "초깃값")]
    [InlineData("7001\t4\t0\t1\t\t5\n", "최솟값")]
    [InlineData("7001\t4\t0\t1\t0\t\n", "최댓값")]
    [InlineData("7001\t4\tx\t1\t0\t5\n", "레벨")]
    public void Character_status_master_checks_keys_ranges_and_values(string row, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(Statuses, Base + row)).Message);

    [Fact]
    public void The_same_status_may_repeat_across_levels_and_characters()
    {
        var pack = Load(Statuses, Base + "7001\t1\t1\t2\t0\t5\n7001\t3\t0\t1\t0\t5\n7002\t3\t0\t1\t0\t5\n");
        Assert.Equal(9, pack.Masters.CharacterStatusRowTable.Count);
    }

    [Theory]
    [InlineData("7001\t2\t0\t40\t0\t40\n7002\t2\t0\t20\t0\t20\n7002\t1\t0\t1\t1\t99\n", "캐릭터 '7001'에 level(1)")]    // 용사의 level 0 행이 없다
    [InlineData("7001\t1\t0\t1\t1\t99\n7002\t2\t0\t20\t0\t20\n7002\t1\t0\t1\t1\t99\n", "캐릭터 '7001'에 hp(2)")]        // 용사의 hp 0 행이 없다
    [InlineData("7001\t1\t0\t1\t1\t99\n7001\t2\t0\t40\t0\t40\n7002\t1\t0\t1\t1\t99\n7002\t2\t3\t20\t0\t20\n", "캐릭터 '7002'에 hp(2)")]  // 마법사의 hp는 레벨 3 행뿐이다
    public void Every_character_needs_level_and_hp_rows_at_level_zero(string rows, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(Statuses, Header + rows)).Message);

    [Fact]
    public void Character_status_master_needs_the_character_and_status_masters_to_check_against()
    {
        // 두 마스터 모두 필수라 어느 쪽이 없어도 로드 단계에서 거부된다.
        Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZipWithout(GamePack.CharacterInventoryMasterPath,
            ("pack.tsv", Pack0), ("master/character_status_master_v1.tsv", Base))));
        Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZipWithout(GamePack.StatusMasterPath,
            ("pack.tsv", Pack0), ("master/character_status_master_v1.tsv", Base))));
    }

    [Fact]
    public void Wrong_headers_are_rejected()
    {
        Assert.Throws<PackFormatException>(() => Load("code\tname\n1\tlevel\n", null));
        Assert.Throws<PackFormatException>(() =>
            Load(Statuses, "character_code\tstatus_code\tinitial_value\tmin_value\tmax_value\n7001\t1\t1\t0\t5\n"));   // level 칼럼이 없다
    }

    // ---- CharacterInstance 채우기 ----

    [Fact]
    public void A_new_character_state_is_filled_from_the_level_zero_rows()
    {
        // 용사: hp 0행 40/40, 레벨 5행은 80 — 생성 때는 레벨 0만 읽는다. xp는 0/30.
        var pack = Load(Statuses, Base + "7001\t2\t5\t80\t0\t80\n7001\t3\t0\t0\t0\t30\n");
        Assert.Equal(CharacterInstance.Stats(1, 40, 40, 0, 30), pack.NewCharacterInstance(7001).StatsOnly());
    }

    [Fact]
    public void A_character_may_start_hurt_or_with_experience()
    {
        var pack = Load(Statuses,
            Header + "7001\t1\t0\t3\t1\t99\n7001\t2\t0\t10\t0\t40\n7001\t3\t0\t12\t0\t30\n7002\t1\t0\t1\t1\t99\n7002\t2\t0\t20\t0\t20\n1\t1\t0\t1\t1\t99\n1\t2\t0\t10\t0\t10\n");
        Assert.Equal(CharacterInstance.Stats(3, 10, 40, 12, 30), pack.NewCharacterInstance(7001).StatsOnly());
    }

    [Fact]
    public void A_character_without_xp_rows_starts_with_zero_experience()
    {
        var pack = Load(Statuses, Base);
        Assert.Equal(CharacterInstance.Stats(1, 40, 40, 0, 0), pack.NewCharacterInstance(7001).StatsOnly());
        Assert.Equal(CharacterInstance.Stats(1, 20, 20, 0, 0), pack.NewCharacterInstance(7002).StatsOnly());
    }

    [Fact]
    public void A_pack_without_status_masters_gives_a_minimal_living_character()
    {
        Assert.Equal(CharacterInstance.Stats(1, 1, 1, 0, 0), Load(null, null).NewCharacterInstance(7001).StatsOnly());
    }

    [Fact]
    public void Adding_a_character_creates_its_state_and_copies_its_tags()
    {
        var pack = Load(Statuses, Base + "7001\t3\t0\t0\t0\t30\n");
        var s = GameSession.NewGame(pack);

        Assert.True(s.AddCharacter(7001));
        Assert.Equal(CharacterInstance.Stats(1, 40, 40, 0, 30), s.Characters[7001].StatsOnly());
        Assert.False(s.AddCharacter(7001));                          // 이미 보유
        Assert.False(s.AddCharacter(9999));                          // 마스터에 없다
        Assert.True(s.AddCharacter(7002));
        Assert.Equal(CharacterInstance.Stats(1, 20, 20, 0, 0), s.Characters[7002].StatsOnly());

        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());   // 만든 상태는 세이브가 받아들이는 범위다
        Assert.Equal(s.Characters[7001], loaded.Characters[7001]);
        Assert.Equal(s.Characters[7002], loaded.Characters[7002]);
    }

    [Fact]
    public void The_optional_character_display_order_column_defaults_to_zero_and_must_not_be_negative()
    {
        static GamePack Load(string statuses) => GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.StatusMasterPath, statuses)));
        var pack = Load("status_code\tstatus_name\tcharacter_display_order\n1\tlevel\t0\n2\thp\t\n7\tSTR\t3\n");
        Assert.Equal([0L, 0L, 3L], pack.Statuses!.Select(x => x.CharacterDisplayOrder));
        Assert.Equal(3, pack.FindStatus(7)!.CharacterDisplayOrder);
        Assert.Equal(0, Load("status_code\tstatus_name\n1\tlevel\n2\thp\n").FindStatus(1)!.CharacterDisplayOrder);   // 열이 없어도 된다
        Assert.Contains("character_display_order", Assert.Throws<PackFormatException>(() => Load("status_code\tstatus_name\tcharacter_display_order\n1\tlevel\t-1\n2\thp\t0\n")).Message);
        Assert.Throws<PackFormatException>(() => Load("status_code\tstatus_name\tcharacter_display_order\n1\tlevel\tx\n2\thp\t0\n"));
    }
}
