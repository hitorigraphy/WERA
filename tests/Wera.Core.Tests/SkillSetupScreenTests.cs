using Wera.Core;

namespace Wera.Core.Tests;

// 스킬 설정: 캐릭터의 스킬 슬롯을 순서대로, 구분선 아래에 스킬 풀. 슬롯을 고르면 넣을 수 있는 풀의 스킬이 나오고 고르면 장착한다.
public class SkillSetupScreenTests
{
    static GamePack SamplePack()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var zip = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GamePack.Load(zip);
    }

    // 용사(7001)를 첫 칸에 두고 스킬 풀에 베기(1001, 검), 불꽃(1002, 마법), 베기(1001)를 익힌 상태로 스킬 설정 화면의 첫 칸을 연다.
    static GameSession Opened()
    {
        var s = GameSession.NewGame(SamplePack());
        s.AddCharacter(7001);
        s.Activate("105"); s.Activate("1"); s.Activate("7001");                 // 첫 칸에 용사
        s.Characters[7001] = s.Characters[7001] with
        {
            SkillPool = [new SkillInstance(1001), new SkillInstance(1002), new SkillInstance(1001)],
            SkillSlots = [],                                                    // 생성 후 이펙트가 장착한 스킬은 비운다
        };
        s.Activate("103");
        Assert.Equal(Screen.SkillCharacter, s.Activate("1")!.Target);
        return s;
    }

    [Fact]
    public void The_character_screen_lists_the_skill_slots_in_order_then_the_skill_pool_button()
    {
        var s = Opened();
        Assert.Equal(["1", "2", "0", "999"], s.Entries.Select(e => e.Code));
        Assert.Equal(["스킬 슬롯 1 : 비어 있음", "스킬 슬롯 2 : 비어 있음", "스킬 풀", "돌아가기"], s.Entries.Select(e => e.Label));
        Assert.Equal(Screen.SkillPoolList, s.Entries.Single(e => e.Code == GameSession.SkillPoolEntryCode).Target);
        s.Activate("999");
        Assert.Equal(Screen.SkillSetup, s.Screen);
    }

    [Fact]
    public void A_slot_shows_only_the_pool_skills_that_fit_and_choosing_one_equips_it()
    {
        var s = Opened();
        Assert.Equal(Screen.SkillSlotSelect, s.Activate("1")!.Target);          // 슬롯 1은 검 스킬만
        Assert.Equal(["1", "3", "999"], s.Entries.Select(e => e.Code));          // 풀 번호 1, 3(베기); 불꽃은 없다
        Assert.Equal(["베기", "베기"], s.Entries.Where(e => e.Action == EntryAction.EquipSkill).Select(e => e.Label));

        s.Activate("3");                                                         // 풀의 세 번째 베기
        Assert.Equal(Screen.SkillCharacter, s.Screen);
        Assert.Equal(2, s.Characters[7001].SkillSlots[1]);
        Assert.Equal("스킬 슬롯 1 : 베기", s.Entries.First().Label);
        Assert.Contains("장착", s.Message);

        s.Activate("1");                                                         // 같은 슬롯을 다시: 비우기가 맨 위에 생긴다
        Assert.Equal("0", s.Entries.First().Code);
        s.Activate("0");
        Assert.Empty(s.Characters[7001].SkillSlots);
        Assert.Equal(Screen.SkillCharacter, s.Screen);
    }

    [Fact]
    public void A_skill_sits_in_one_slot_at_a_time_and_the_character_stays_the_same()
    {
        var s = Opened();
        s.Activate("2"); s.Activate("2");                                        // 슬롯 2에 불꽃(풀 번호 1)
        Assert.Equal(1, s.Characters[7001].SkillSlots[2]);
        s.Activate("1"); s.Activate("1");                                        // 슬롯 1에 풀 번호 0의 베기
        Assert.Equal(0, s.Characters[7001].SkillSlots[1]);
        Assert.Equal(2, s.Characters[7001].SkillSlots.Count);
        Assert.Equal("용사", s.CurrentSlotCharacterName);
    }

    [Fact]
    public void The_skill_pool_lists_what_has_been_learned_with_the_slot_it_is_in()
    {
        var s = Opened();
        s.Activate("1"); s.Activate("1");                                        // 슬롯 1에 풀 번호 0
        Assert.Equal(Screen.SkillPoolList, s.Activate("0")!.Target);
        Assert.Equal(["베기", "불꽃", "베기"], s.SkillPoolRows.Select(r => r.Name));
        Assert.Equal(["스킬 슬롯 1", null, null], s.SkillPoolRows.Select(r => r.EquippedSlotName));
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));
        s.Activate("999");
        Assert.Equal(Screen.SkillCharacter, s.Screen);
        Assert.Equal("용사", s.CurrentSlotCharacterName);
    }
}
