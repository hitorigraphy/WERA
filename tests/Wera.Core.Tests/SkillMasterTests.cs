using Wera.Core;

namespace Wera.Core.Tests;

// 스킬 마스터: skill_slot, character_skill_slot_tag, skill, skill_tag, skill_execute_effect.
public class SkillMasterTests
{
    const string Characters =
        "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
        "1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n";
    const string Tags = "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t검\n5\t마법\n";
    const string CharacterTags = "character_code\ttag_code\n1\t1\n";
    const string SkillSlots = "code\tname\n1\t스킬 슬롯 1\n2\t스킬 슬롯 2\n";
    const string Skills = "code\tname\n1\t베기\n2\t불꽃\n";

    static (string, string)[] Base() =>
        [("pack.tsv", "id\tdemo\ntitle\t데모\n"), ("master/character_inventory_master_v1.tsv", Characters),
         (GamePack.TagMasterPath, Tags), (GamePack.CharacterTagMasterPath, CharacterTags),
         (GamePack.SkillSlotMasterPath, SkillSlots), (GamePack.SkillMasterPath, Skills)];

    // 같은 경로를 다시 주면 기본 파일 대신 그것을 쓴다
    static GamePack Load(params (string, string)[] extra) => GamePack.Load(CoreTests.PackZip(
        Base().Where(b => extra.All(e => e.Item1 != b.Item1)).Concat(extra).ToArray()));

    static void Rejects(string message, params (string, string)[] files) =>
        Assert.Contains(message, Assert.Throws<PackFormatException>(() => Load(files)).Message);

    [Fact]
    public void Slots_skills_tags_and_effects_load_and_are_found_by_key()
    {
        var pack = Load(
            (GamePack.CharacterSkillSlotTagMasterPath,
                "character_code\tskill_slot_code\tgroup_code\ttag_code\tmemo\n7001\t1\t1\t4\t검\n7001\t1\t1\t5\t\n7001\t1\t2\t5\t\n7001\t2\t1\t4\t\n1\t1\t1\t4\t\n"),
            (GamePack.SkillTagMasterPath, "skill_code\ttag_code\n1\t4\n1\t5\n2\t5\n"),
            (GamePack.SkillExecuteEffectMasterPath,
                "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n1\t2\tNone\t7\t8\t9\n1\t1\tnone\t\t\t\n2\t1\tNONE\t1\t\t\n"));

        Assert.Equal(["스킬 슬롯 1", "스킬 슬롯 2"], pack.SkillSlots!.Select(s => s.Name));
        Assert.Equal("베기", pack.FindSkill(1)!.Name);
        Assert.Equal("스킬 슬롯 2", pack.FindSkillSlot(2)!.Name);
        Assert.Null(pack.FindSkill(99));
        Assert.Null(pack.FindSkillSlot(99));

        Assert.Equal(
            [(1L, 1L, 4L), (1, 1, 5), (1, 2, 5), (2, 1, 4)],
            pack.SkillSlotTagsOf(7001).Select(t => (t.SkillSlotCode, t.GroupCode, t.TagCode)));      // 캐릭터로 찾기
        Assert.Equal([4L, 5L], pack.SkillSlotTagsOf(7001, 1, 1).Select(t => t.TagCode));              // (캐릭터, 슬롯, 그룹)으로 찾기
        Assert.Equal([5L], pack.SkillSlotTagsOf(7001, 1, 2).Select(t => t.TagCode));
        Assert.Empty(pack.SkillSlotTagsOf(7001, 2, 2));
        Assert.Single(pack.SkillSlotTagsOf(1));

        Assert.Equal([4L, 5L], pack.TagsOfSkill(1));
        Assert.Equal([5L], pack.TagsOfSkill(2));

        var effects = pack.SkillExecuteEffectsOf(1);
        Assert.Equal([1L, 2L], effects.Select(e => e.ExecuteNumber));                                   // 실행 번호 순
        Assert.All(effects, e => Assert.Equal(EffectType.None, e.EffectType));
        Assert.Equal((0L, 0L, 0L), (effects[0].Value1, effects[0].Value2, effects[0].Value3));         // 빈 값은 0
        Assert.Equal((7L, 8L, 9L), (effects[1].Value1, effects[1].Value2, effects[1].Value3));
        Assert.Single(pack.SkillExecuteEffectsOf(2));
    }

    [Fact]
    public void The_skill_masters_are_optional()
    {
        var pack = GamePack.Load(CoreTests.PackZip(("pack.tsv", "id\tdemo\ntitle\t데모\n")));
        Assert.Null(pack.SkillSlots);
        Assert.Null(pack.CharacterSkillSlotTags);
        Assert.Null(pack.Skills);
        Assert.Null(pack.SkillTags);
        Assert.Null(pack.SkillExecuteEffects);
        Assert.Null(pack.FindSkill(1));
        Assert.Empty(pack.SkillExecuteEffectsOf(1));
    }

    [Fact]
    public void A_skill_without_effects_or_tags_is_fine()
    {
        var pack = Load();
        Assert.Empty(pack.SkillExecuteEffectsOf(1));
        Assert.Empty(pack.TagsOfSkill(1));
    }

    [Fact]
    public void Duplicate_codes_are_rejected()
    {
        Rejects("스킬 슬롯 코드 '1'", (GamePack.SkillSlotMasterPath, "code\tname\n1\ta\n1\tb\n"));
        Rejects("스킬 코드 '1'", (GamePack.SkillMasterPath, "code\tname\n1\ta\n1\tb\n"));
    }

    [Fact]
    public void Slot_tags_must_point_at_known_characters_slots_and_tags_and_keys_cannot_repeat()
    {
        const string head = "character_code\tskill_slot_code\tgroup_code\ttag_code\n";
        Rejects("캐릭터 '9'가 캐릭터 인벤토리 마스터에 없습니다", (GamePack.CharacterSkillSlotTagMasterPath, head + "9\t1\t1\t4\n"));
        Rejects("스킬 슬롯 '9'가 스킬 슬롯 마스터에 없습니다", (GamePack.CharacterSkillSlotTagMasterPath, head + "7001\t9\t1\t4\n"));
        Rejects("태그 '9'가 태그 마스터에 없습니다", (GamePack.CharacterSkillSlotTagMasterPath, head + "7001\t1\t1\t9\n"));
        Rejects("같은 키", (GamePack.CharacterSkillSlotTagMasterPath, head + "7001\t1\t1\t4\n7001\t1\t1\t4\n"));
        Rejects("그룹 코드 'x'", (GamePack.CharacterSkillSlotTagMasterPath, head + "7001\t1\tx\t4\n"));
    }

    [Fact]
    public void Skill_tags_must_point_at_known_skills_and_tags_and_cannot_repeat()
    {
        const string head = "skill_code\ttag_code\n";
        Rejects("스킬 코드 '9'가 스킬 마스터에 없습니다", (GamePack.SkillTagMasterPath, head + "9\t4\n"));
        Rejects("태그 코드 '9'가 태그 마스터에 없습니다", (GamePack.SkillTagMasterPath, head + "1\t9\n"));
        Rejects("두 번 적혀 있습니다", (GamePack.SkillTagMasterPath, head + "1\t4\n1\t4\n"));
    }

    [Fact]
    public void Skill_effects_need_a_known_skill_a_known_type_and_a_unique_execute_number()
    {
        const string head = "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
        Rejects("스킬 코드 '9'가 스킬 마스터에 없습니다", (GamePack.SkillExecuteEffectMasterPath, head + "9\t1\tNone\t\t\t\n"));
        Rejects("이펙트 종류 'Fly'을(를) 알 수 없습니다", (GamePack.SkillExecuteEffectMasterPath, head + "1\t1\tFly\t\t\t\n"));
        Rejects("이펙트 종류 '0'을(를) 알 수 없습니다", (GamePack.SkillExecuteEffectMasterPath, head + "1\t1\t0\t\t\t\n"));       // 숫자는 안 된다
        Rejects("실행 번호 '1'가 두 번", (GamePack.SkillExecuteEffectMasterPath, head + "1\t1\tNone\t\t\t\n1\t1\tNone\t\t\t\n"));
        Rejects("value_2", (GamePack.SkillExecuteEffectMasterPath, head + "1\t1\tNone\t\tx\t\n"));
    }

    [Fact]
    public void A_child_master_needs_its_parent_master()
    {
        var noSlots = Base().Where(b => b.Item1 != GamePack.SkillSlotMasterPath).Append(
            (GamePack.CharacterSkillSlotTagMasterPath, "character_code\tskill_slot_code\tgroup_code\ttag_code\n")).ToArray();
        Assert.Contains(GamePack.SkillSlotMasterPath, Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(noSlots))).Message);

        var noSkills = Base().Where(b => b.Item1 != GamePack.SkillMasterPath).Append(
            (GamePack.SkillExecuteEffectMasterPath, "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n")).ToArray();
        Assert.Contains(GamePack.SkillMasterPath, Assert.Throws<PackFormatException>(() => GamePack.Load(CoreTests.PackZip(noSkills))).Message);
    }
}
