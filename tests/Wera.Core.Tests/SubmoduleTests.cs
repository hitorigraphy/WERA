using Wera.Core;

namespace Wera.Core.Tests;

// 서브모듈: 서킷 종류마다 공통 동작을 서브모듈 마스터에 한 번 정의하고 JumpToSubmodule(공통 번호 32)로 부른다.
// 서브모듈은 부른 서킷의 캐시, 포커스, 시전자와 타겟을 그대로 쓰고, 끝나면 호출 다음 이펙트로 돌아온다. 서브모듈 안의 Finish는 서브모듈만 끝낸다.
public class SubmoduleTests
{
    const long Hp = GamePack.HpStatusCode;
    const long Mana = 20;
    const string Header = "execute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";
    const string SubHeader = "submodule_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n";

    // 공백으로 구분해 쓴 행들을 TSV 본문으로 바꾼다
    static string Rows(params string[] rows) => string.Concat(rows.Select(r => r.Replace(' ', '\t') + "\n"));

    static GamePack Pack(params (string Path, string Text)[] extra)
    {
        var files = new List<(string, string)>
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            (GamePack.CharacterInventoryMasterPath,
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n"),
            (GamePack.StatusMasterPath, "status_code\tstatus_name\n1\tlevel\n2\thp\n20\tmana\n"),
            (GamePack.CharacterStatusMasterPath, "character_code\tstatus_code\tlevel\tinitial_value\tmin_value\tmax_value\n" +
                Rows("1 1 0 1 1 99", "1 2 0 40 0 40", "7001 1 0 1 1 99", "7001 2 0 40 0 40", "7001 20 0 10 0 20")),
            (GamePack.SkillMasterPath, "code\tname\n1\t베기\n2\t찌르기\n"),
            (GamePack.SkillSlotMasterPath, "code\tname\n1\t스킬 슬롯 1\n"),
        };
        files.AddRange(extra.Select(e => (e.Path, e.Text)));
        return GamePack.Load(CoreTests.PackZip(files.ToArray()));
    }

    static string SkillRows(params string[] rows) =>
        "skill_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n" + Rows(rows.Select(r => "1 " + r).ToArray());

    static string SkillSubRows(params string[] rows) => SubHeader + Rows(rows);

    static CharacterInstance Run(GamePack pack, long target = 7001)
    {
        var field = new CircuitField(pack, 1, 7001);
        new SkillExecuteCircuit(pack, field.Store, 1, field.Unit(1), field.Unit(target)).Execute();
        return field.Characters[target];
    }

    [Fact]
    public void A_submodule_runs_on_the_callers_cache_and_focus_then_the_caller_continues_after_the_call()
    {
        // 서브모듈 5: 캐시 1에 -3, 캐시 0(-10)을 hp에 더함, 여기서 끝(뒤의 -100은 실행되지 않는다). 돌아와서 캐시 1(-3)을 hp에 더한다.
        var pack = Pack(
            (GamePack.SkillExecuteEffectMasterPath, SkillRows("1 SetCacheValue 0 -10", "2 FocusMainTargetCharacter", "3 JumpToSubmodule 5", "4 CacheToFocusedCharacterModValue 2 1")),
            (GamePack.SkillExecuteSubmoduleMasterPath, SkillSubRows("5 1 SetCacheValue 1 -3", "5 2 CacheToFocusedCharacterModValue 2 0", "5 3 Finish", "5 4 SetCacheValue 1 -100")));
        Assert.Equal(27, Run(pack).Statuses[Hp]);                                   // 40 - 10 - 3: Finish는 서브모듈만 끝냈다
    }

    [Fact]
    public void Finish_in_the_main_master_still_ends_the_whole_circuit_and_jumps_inside_a_submodule_stay_inside()
    {
        var pack = Pack(
            (GamePack.SkillExecuteEffectMasterPath, SkillRows("1 FocusMainTargetCharacter", "2 JumpToSubmodule 5", "3 Finish", "4 SetCacheValue 0 -30", "5 CacheToFocusedCharacterModValue 2 0")),
            (GamePack.SkillExecuteSubmoduleMasterPath, SkillSubRows("5 1 Jump 3", "5 2 SetCacheValue 0 -100", "5 3 SetCacheValue 0 -2", "5 4 CacheToFocusedCharacterModValue 2 0")));
        Assert.Equal(38, Run(pack).Statuses[Hp]);                                   // 서브모듈이 -2를 hp에 더하고 돌아오면 3번 Finish
    }

    [Fact]
    public void One_submodule_serves_several_callers()
    {
        var rows = SkillRows("1 FocusMainTargetCharacter", "2 JumpToSubmodule 5", "3 SetCacheValue 0 -1", "4 CacheToFocusedCharacterModValue 2 0");
        var sub = SkillSubRows("5 1 SetCacheValue 0 -5", "5 2 CacheToFocusedCharacterModValue 2 0");
        var pack = Pack((GamePack.SkillExecuteEffectMasterPath, rows + Rows("2 1 FocusMainTargetCharacter", "2 2 JumpToSubmodule 5")),
            (GamePack.SkillExecuteSubmoduleMasterPath, sub));
        Assert.Equal(34, Run(pack).Statuses[Hp]);                                   // 40 - 5 - 1
        var field = new CircuitField(pack, 1, 7001);
        new SkillExecuteCircuit(pack, field.Store, 2, field.Unit(1), field.Unit(7001)).Execute();
        Assert.Equal(35, field.Characters[7001].Statuses[Hp]);                       // 다른 스킬도 같은 서브모듈(5)을 부른다
    }

    [Fact]
    public void After_battle_win_and_lose_circuits_call_submodules_and_circuit_specific_effects_work_inside_them()
    {
        var pack = Pack(
            (GamePack.AfterBattleWinEffectMasterPath, Header + Rows("1 SetCacheValue 0 4 0", "2 JumpToSubmodule 2")),
            (GamePack.AfterBattleWinSubmoduleMasterPath, SubHeader + Rows("2 1 CacheToFocusedCharacterModValue 20 0")),
            (GamePack.AfterBattleLoseEffectMasterPath, Header + Rows("1 JumpToSubmodule 1")),
            (GamePack.AfterBattleLoseSubmoduleMasterPath, SubHeader + Rows("1 1 JumpNotInDungeon 3", "1 2 CaptureFocusedCharacter", "1 3 Finish")));
        var win = new AfterBattleWinCircuit(pack, pack.NewCharacterInstance(7001, "c1", 0)).Execute();
        Assert.Equal(14, win.Statuses[Mana]);                                       // 10 + 4

        var character = pack.NewCharacterInstance(7001, "c1", 0);
        var captured = new List<string>();
        new AfterBattleLoseCircuit(pack, UnitStore.Of(character), new PartyUnit(7001), inDungeon: true, capture: c => captured.Add(c.InstanceId)).Execute();
        Assert.Equal(["c1"], captured);                                              // 서브모듈 안의 CaptureFocusedCharacter(고유 이펙트)
        new AfterBattleLoseCircuit(pack, UnitStore.Of(character), new PartyUnit(7001), inDungeon: false, capture: c => captured.Add(c.InstanceId)).Execute();
        Assert.Single(captured);                                                     // 서브모듈 안의 JumpNotInDungeon은 서브모듈 안으로 점프한다
    }

    [Fact]
    public void A_keyed_kind_character_after_create_calls_a_submodule_shared_by_all_characters()
    {
        var pack = Pack(
            (GamePack.CharacterAfterCreateEffectMasterPath, "character_code\texecute_number\teffect_type\tvalue_1\tvalue_2\tvalue_3\n" + Rows("7001 1 JumpToSubmodule 9")),
            (GamePack.CharacterAfterCreateSubmoduleMasterPath, SubHeader + Rows("9 1 AddSkillToPool 1", "9 2 SetSkillToSlot 1")));
        var created = new CharacterAfterCreateCircuit(pack, pack.NewCharacterInstance(7001, "c1", 0)).Execute();
        Assert.Contains(created.SkillPool, s => s.SkillCode == 1);
    }

    static string Message(params (string Path, string Text)[] files) => Assert.Throws<PackFormatException>(() => Pack(files)).Message;

    [Fact]
    public void Calls_must_name_a_submodule_of_the_same_kind_and_the_kind_needs_a_submodule_master()
    {
        var win = (GamePack.AfterBattleWinEffectMasterPath, Header + Rows("1 JumpToSubmodule 2"));
        Assert.Contains(GamePack.AfterBattleWinSubmoduleMasterPath, Message(win));                                                     // 마스터가 없다
        Assert.Contains("서브모듈 '2'", Message(win, (GamePack.AfterBattleWinSubmoduleMasterPath, SubHeader + Rows("3 1 None"))));    // 코드가 없다
        Assert.Contains(GamePack.AfterBattleWinSubmoduleMasterPath, Message(win, (GamePack.AfterBattleLoseSubmoduleMasterPath, SubHeader + Rows("2 1 None"))));   // 다른 종류의 서브모듈은 부를 수 없다
    }

    [Fact]
    public void A_submodule_master_rejects_nesting_bad_jumps_unknown_types_duplicates_and_bad_arguments()
    {
        string Sub(params string[] rows) => Message((GamePack.AfterBattleWinSubmoduleMasterPath, SubHeader + Rows(rows)));
        Assert.Contains("JumpToSubmodule을 쓸 수 없습니다", Sub("1 1 JumpToSubmodule 1"));                  // 서브모듈이 서브모듈을 부를 수 없다
        Assert.Contains("점프 대상", Sub("1 1 Jump 1"));                                                        // 뒤로(같은 번호)는 안 된다
        Assert.Contains("점프 대상", Sub("1 1 Jump 5", "1 2 None"));                                            // 없는 번호
        Assert.Contains("점프 대상", Sub("1 1 Jump 2", "2 2 None"));                                            // 다른 서브모듈의 번호
        Assert.Contains("알 수 없습니다", Sub("1 1 Nope"));
        Assert.Contains("두 번", Sub("1 1 None", "1 1 Finish"));
        Assert.Contains("cache_index", Sub("1 1 SetCacheValue 999 1 0"));                                       // 종류별 인자 검증은 이펙트 마스터와 같다
        Assert.Contains("점프 대상", Message((GamePack.AfterBattleLoseSubmoduleMasterPath, SubHeader + Rows("1 1 JumpNotInDungeon 1"))));   // 서킷 고유 점프도 같은 규칙
    }
}
