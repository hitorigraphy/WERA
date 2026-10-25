using Wera.Core;

namespace Wera.Core.Tests;

public class CharacterStatusTests
{
    const string Pack0 = "id\tdemo\ntitle\t데모\n";
    const string Attributes = "attribute_code\tattribute_name\n1\t불\n2\t물\n";
    const string Characters =
        "character_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n";
    const string Statuses = "status_code\tstatus_name\n3\t경험치 상한\n1\t레벨\n2\t최대 HP\n";
    const string Initials =
        "character_code\tstatus_code\tinitial_value\n7001\t2\t40\n7001\t1\t5\n7002\t1\t3\n7001\t3\t30\n";

    static GamePack Load(string? statuses, string? initials, string characters = Characters)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", Pack0),
            ("master/attribute_master_v1.tsv", Attributes),
            ("master/character_inventory_master_v1.tsv", characters),
        };
        if (statuses is not null) files.Add(("master/character_status_master_v1.tsv", statuses));
        if (initials is not null) files.Add(("master/character_initial_status_master_v1.tsv", initials));
        return GamePack.Load(CoreTests.Zip(files.ToArray()));
    }

    [Fact]
    public void Statuses_are_loaded_in_file_order_and_found_by_code()
    {
        var pack = Load(Statuses, null);
        Assert.Equal([3L, 1L, 2L], pack.CharacterStatuses!.Select(s => s.Code));
        Assert.Equal(new CharacterStatusEntry(2, "최대 HP"), pack.FindCharacterStatus(2));
        Assert.Null(pack.FindCharacterStatus(99));
        Assert.Equal(3, pack.Masters.CharacterStatusRowTable.Count);
    }

    [Fact]
    public void Initial_statuses_are_found_by_composite_key_and_by_character_in_file_order()
    {
        var pack = Load(Statuses, Initials);
        Assert.Equal(40, pack.InitialStatusOf(7001, 2));
        Assert.Equal(3, pack.InitialStatusOf(7002, 1));
        Assert.Null(pack.InitialStatusOf(7002, 2));                  // 정해 두지 않은 스테이터스
        Assert.Null(pack.InitialStatusOf(9999, 1));

        Assert.Equal([2L, 1L, 3L], pack.InitialStatusesOf(7001).Select(s => s.StatusCode));   // 파일 순서
        Assert.Equal(new CharacterInitialStatus(7002, 1, 3), Assert.Single(pack.InitialStatusesOf(7002)));
        Assert.Empty(pack.InitialStatusesOf(9999));
        Assert.Equal(4, pack.Masters.CharacterInitialStatusRowTable.Count);
        Assert.False(pack.Masters.Validate().IsValidationFailed);
    }

    [Fact]
    public void Initial_values_may_be_zero_or_negative()
    {
        var pack = Load(Statuses, "character_code\tstatus_code\tinitial_value\n7001\t1\t0\n7001\t2\t-5\n");
        Assert.Equal(0, pack.InitialStatusOf(7001, 1));
        Assert.Equal(-5, pack.InitialStatusOf(7001, 2));
    }

    [Fact]
    public void Both_masters_are_optional()
    {
        var pack = Load(null, null);
        Assert.Null(pack.CharacterStatuses);
        Assert.Null(pack.CharacterInitialStatuses);
        Assert.Null(pack.InitialStatusOf(7001, 1));
        Assert.Empty(pack.InitialStatusesOf(7001));
    }

    [Theory]
    [InlineData("status_code\tstatus_name\n1\t레벨\n1\t또 레벨\n", "중복")]
    [InlineData("status_code\tstatus_name\nx\t레벨\n", "정수")]
    public void Status_master_rejects_duplicates_and_bad_codes(string statuses, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(statuses, null)).Message);

    [Theory]
    [InlineData("character_code\tstatus_code\tinitial_value\n9999\t1\t5\n", "캐릭터 '9999'")]
    [InlineData("character_code\tstatus_code\tinitial_value\n7001\t99\t5\n", "스테이터스 '99'")]
    [InlineData("character_code\tstatus_code\tinitial_value\n7001\t1\t5\n7001\t1\t6\n", "두 번")]
    [InlineData("character_code\tstatus_code\tinitial_value\n7001\t1\tabc\n", "초깃값")]
    [InlineData("character_code\tstatus_code\tinitial_value\n7001\t1\t\n", "초깃값")]
    public void Initial_status_master_checks_foreign_keys_duplicates_and_values(string initials, string message) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(Statuses, initials)).Message);

    [Fact]
    public void Initial_status_master_needs_the_character_and_status_masters()
    {
        Assert.Throws<PackFormatException>(() => Load(null, Initials));
        Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.Zip(
            ("pack.tsv", Pack0),
            ("master/character_status_master_v1.tsv", Statuses),
            ("master/character_initial_status_master_v1.tsv", Initials))));
    }

    [Fact]
    public void Wrong_headers_are_rejected()
    {
        Assert.Throws<PackFormatException>(() => Load("code\tname\n1\t레벨\n", null));
        Assert.Throws<PackFormatException>(() => Load(Statuses, "character_code\tstatus_code\n7001\t1\n"));
    }
}
