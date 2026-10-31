using System.IO.Compression;
using Wera.Core;

namespace Wera.Core.Tests;

// skill_auto_target_effect_master와 자동 타겟 서킷: 후보를 서브 타겟 자리에 넣고 한 바퀴 돌려 Result로 메인 타겟을 정하고, 적절하지 못하면 랜덤 1체.
public class AutoTargetTests
{
    const string Head = "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";

    static GamePack Load(string effects, string skills = "code\tname\n1\t베기\n2\t불꽃\n") => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n"),
        (GamePack.SkillMasterPath, skills),
        (GamePack.SkillAutoTargetEffectMasterPath, Head + effects)));

    [Fact]
    public void Effects_are_read_per_skill_in_execute_number_order_and_the_master_is_optional()
    {
        var pack = Load("1\t2\tSubTargetToResult\t3\t\t\n1\t1\tNone\t\t\t\n2\t1\tsubtargettoresult\t0\t\t\n");
        Assert.Equal([1L, 2L], pack.SkillAutoTargetEffectsOf(1).Select(e => e.ExecuteNumber));
        Assert.Equal(new SkillAutoTargetEffect(1, 2, AutoTargetEffectType.SubTargetToResult, 3, 0, 0), pack.SkillAutoTargetEffectsOf(1)[1]);
        Assert.Single(pack.SkillAutoTargetEffectsOf(2));

        var bare = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Null(bare.SkillAutoTargetEffects);
        Assert.Empty(bare.SkillAutoTargetEffectsOf(1));
    }

    [Fact]
    public void Bad_rows_are_rejected_at_load()
    {
        string Err(string effects) => Assert.Throws<PackFormatException>(() => Load(effects)).Message;
        Assert.Contains("value_1(후보 번호) '-1'", Err("1\t1\tSubTargetToResult\t-1\t\t\n"));
        Assert.Contains("이펙트 종류 'CacheToResult'", Err("1\t1\tCacheToResult\t0\t\t\n"));
        Assert.Contains("이펙트 종류 '32'", Err("1\t1\t32\t0\t\t\n"));                          // 숫자는 안 된다
        Assert.Contains("스킬 코드 '9'가 스킬 마스터에 없습니다", Err("9\t1\tNone\t\t\t\n"));
        Assert.Contains("두 번 적혀 있습니다", Err("1\t1\tNone\t\t\t\n1\t1\tNone\t\t\t\n"));
        Assert.Contains("점프 대상", Err("1\t1\tJump\t1\t\t\n"));                                // 점프는 뒤로만
        Assert.Contains("skill_master_v1.tsv도 필요", Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"), (GamePack.SkillAutoTargetEffectMasterPath, Head)))).Message);
    }
}
