using Wera.Core;

namespace Wera.Core.Tests;

// 스킬의 서브 타겟 범위(skill_master의 sub_target_type)와 그 계산.
public class SubTargetTests
{
    // 슬롯 0~8: 전위 0 1 2 / 중위 3 4 5 / 후위 6 7 8
    static readonly Func<int, bool> All9 = _ => true;
    static List<int> Pick(SkillSubTargetType type, int main, Func<int, bool>? present = null, Func<int, int>? random = null) =>
        SubTargetPicker.Pick(type, main, present ?? All9, random ?? (_ => 0));

    [Fact]
    public void Row_column_and_adjacent_ranges_exclude_the_main_target()
    {
        Assert.Equal([3, 5], Pick(SkillSubTargetType.SameRow, 4));
        Assert.Equal([1, 7], Pick(SkillSubTargetType.SameColumn, 4));
        Assert.Equal([5], Pick(SkillSubTargetType.Right, 4));
        Assert.Equal([3], Pick(SkillSubTargetType.Left, 4));
        Assert.Equal([7], Pick(SkillSubTargetType.Back, 4));
        Assert.Equal([1], Pick(SkillSubTargetType.Front, 4));
        Assert.Equal([1, 3, 5, 7], Pick(SkillSubTargetType.Cross, 4));
        Assert.Equal([0, 1, 2, 3, 5, 6, 7, 8], Pick(SkillSubTargetType.All, 4));
        Assert.Equal([], Pick(SkillSubTargetType.None, 4));
    }

    [Fact]
    public void Ranges_stop_at_the_edges_and_skip_empty_or_fallen_slots()
    {
        Assert.Equal([], Pick(SkillSubTargetType.Left, 3));                          // 왼쪽 끝
        Assert.Equal([], Pick(SkillSubTargetType.Right, 5));                         // 오른쪽 끝
        Assert.Equal([], Pick(SkillSubTargetType.Front, 1));                         // 맨 앞 줄
        Assert.Equal([], Pick(SkillSubTargetType.Back, 7));                          // 맨 뒤 줄
        Assert.Equal([1, 5, 7], Pick(SkillSubTargetType.Cross, 4, i => i != 3));     // 슬롯 3은 비어 있다
        Assert.Equal([1, 2], Pick(SkillSubTargetType.SameRow, 0));
        Assert.Equal([2], Pick(SkillSubTargetType.SameRow, 0, i => i != 1));
        Assert.Equal([4, 6], Pick(SkillSubTargetType.All, 0, i => i is 0 or 4 or 6));
    }

    [Fact]
    public void Random_picks_distinct_units_and_takes_as_many_as_possible()
    {
        var next = 0;
        Assert.Equal([1], Pick(SkillSubTargetType.Random1, 0, random: n => { next = n; return 0; }));
        Assert.Equal(8, next);                                                       // 메인을 뺀 8칸에서 고른다
        Assert.Equal([1, 2, 3], Pick(SkillSubTargetType.Random3, 0));                // 항상 맨 앞을 뽑으면 앞에서부터 중복 없이
        Assert.Equal([2, 5], Pick(SkillSubTargetType.Random2, 0, i => i is 0 or 2 or 5 or 8, n => n - 1 == 2 ? 1 : 0));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], Pick(SkillSubTargetType.Random7, 0));
        Assert.Equal([2, 5], Pick(SkillSubTargetType.Random7, 0, i => i is 0 or 2 or 5));    // 가능한 수보다 많으면 가능한 만큼
        Assert.Equal([], Pick(SkillSubTargetType.Random3, 0, i => i == 0));
    }

    [Fact]
    public void Diagonal_ranges_go_one_or_two_steps_toward_the_back_or_front()
    {
        Assert.Equal([6, 8], Pick(SkillSubTargetType.BackDiagonals1, 4));
        Assert.Equal([6], Pick(SkillSubTargetType.BackLeftDiagonal1, 4));
        Assert.Equal([8], Pick(SkillSubTargetType.BackRightDiagonal1, 4));
        Assert.Equal([4, 6], Pick(SkillSubTargetType.BackLeftDiagonal2, 2));         // 슬롯 2 → 4 → 6
        Assert.Equal([4], Pick(SkillSubTargetType.BackLeftDiagonal1, 2));
        Assert.Equal([4, 6], Pick(SkillSubTargetType.BackDiagonals2, 2));
        Assert.Equal([0, 2], Pick(SkillSubTargetType.FrontDiagonals1, 4));
        Assert.Equal([0], Pick(SkillSubTargetType.FrontLeftDiagonal1, 4));
        Assert.Equal([2], Pick(SkillSubTargetType.FrontRightDiagonal1, 4));
        Assert.Equal([0, 4], Pick(SkillSubTargetType.FrontLeftDiagonal2, 8));        // 슬롯 8 → 4 → 0
        Assert.Equal([], Pick(SkillSubTargetType.BackDiagonals1, 7));                // 맨 뒤 줄
    }

    static GamePack Load(string skillMaster) => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.SkillMasterPath, skillMaster)));

    [Fact]
    public void The_sub_target_type_column_is_optional_and_read_by_name()
    {
        Assert.Equal(SkillSubTargetType.None, Load("code\tname\n1\t베기\n").FindSkill(1)!.SubTargetType);
        Assert.Equal(SkillSubTargetType.None, Load("code\tname\tmain_target_type\n1\t베기\tEnemy\n").FindSkill(1)!.SubTargetType);
        var pack = Load("code\tname\tmain_target_type\tsub_target_type\tmemo\n1\t베기\tEnemy\tsamerow\t\n2\t기본\tAlly\t\t\n3\t난사\tAny\tRandom7\t\n");
        Assert.Equal([SkillSubTargetType.SameRow, SkillSubTargetType.None, SkillSubTargetType.Random7], new long[] { 1, 2, 3 }.Select(c => pack.FindSkill(c)!.SubTargetType));
        Assert.Equal(SkillMainTargetType.Any, pack.FindSkill(3)!.MainTargetType);

        var only = Load("code\tname\tsub_target_type\n1\t베기\tCross\n").FindSkill(1)!;               // 메인 열 없이 서브 열만
        Assert.Equal((SkillMainTargetType.Enemy, SkillSubTargetType.Cross), (only.MainTargetType, only.SubTargetType));

        var error = Assert.Throws<PackFormatException>(() => Load("code\tname\tmain_target_type\tsub_target_type\n1\t베기\tEnemy\tRandom8\n"));
        Assert.Contains("sub_target_type 'Random8'", error.Message);
        Assert.Throws<PackFormatException>(() => Load("code\tname\tsub_target_type\n1\t베기\t3\n"));      // 숫자는 안 된다
    }
}
