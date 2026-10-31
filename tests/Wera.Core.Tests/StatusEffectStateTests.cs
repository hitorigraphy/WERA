using Wera.Core;

namespace Wera.Core.Tests;

public class StatusEffectStateTests
{
    const string EffectHeader =
        "status_effect_code\ttrigger_type\tpriority\teffect_code\teffect_value_1\teffect_value_2\teffect_value_3\n";

    // 독(1)은 이펙트 행이 둘, 화상(2)과 재생(3)은 하나, 표식(4)은 이펙트가 없다.
    const string DefaultEffects = EffectHeader +
        "1\tAfterTurnEnd\t1\t1\t-3\t3\t0\n" +
        "1\tBeforeAttack\t2\t1\t-1\t2\t9\n" +
        "2\tAfterTurnEnd\t1\t1\t-5\t2\t0\n" +
        "3\tAfterTurnStart\t1\t1\t5\t5\t0\n";

    static GamePack Pack(string effects = DefaultEffects, EmbeddedEffectRegistry? registry = null)
        => GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
                "7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n"),
            ("master/status_effect_master_v1.tsv", "status_effect_code\tstatus_effect_name\n1\t독\n2\t화상\n3\t재생\n4\t표식\n"),
            ("master/status_effect_effect_master_v1.tsv", effects)), registry);

    static GameSession Session(GamePack? pack = null)
    {
        var s = GameSession.NewGame(pack ?? Pack());
        s.Characters[7001] = CharacterInstance.Stats(5, 36, 40, 3, 30);
        return s;
    }

    static ActiveEffect Poison1 => new(TriggerType.AfterTurnEnd, 1, 1, -3, 3, 0);
    static ActiveEffect Poison2 => new(TriggerType.BeforeAttack, 2, 1, -1, 2, 9);

    // ---- 걸기 / 풀기 ----

    [Fact]
    public void Applying_a_status_effect_copies_the_master_rows_with_counter_zero_and_valid()
    {
        var s = Session();
        Assert.True(s.ApplyStatusEffect(7001, 1));

        var status = Assert.Single(s.StatusEffectsOf(7001));
        Assert.Equal(1, status.StatusEffectCode);
        Assert.Equal([Poison2, Poison1], status.EffectList);          // 키 순서: 트리거(BeforeAttack, AfterTurnEnd)
        Assert.All(status.EffectList, e =>
        {
            Assert.Equal(0, e.Counter);
            Assert.True(e.IsValid);
        });
    }

    [Fact]
    public void A_status_effect_without_effect_rows_is_still_applied_with_no_effects()
    {
        var s = Session();
        Assert.True(s.ApplyStatusEffect(7001, 4));
        var status = Assert.Single(s.StatusEffectsOf(7001));
        Assert.Equal(4, status.StatusEffectCode);
        Assert.Empty(status.EffectList);
    }

    [Fact]
    public void Unowned_character_or_unknown_status_is_refused()
    {
        var s = Session();
        Assert.False(s.ApplyStatusEffect(7002, 1));            // 마스터에 있지만 보유하지 않음
        Assert.False(s.ApplyStatusEffect(9999, 1));            // 마스터에 없는 캐릭터
        Assert.False(s.ApplyStatusEffect(7001, 99));           // 없는 상태이상
        Assert.Empty(s.CharacterStatusEffects);
    }

    [Fact]
    public void Statuses_stack_by_code_and_applying_the_same_one_again_refreshes_it()
    {
        var s = Session();
        s.ApplyStatusEffect(7001, 1);
        s.ApplyStatusEffect(7001, 3);
        Assert.Equal([1L, 3L], s.StatusEffectsOf(7001).Select(x => x.StatusEffectCode));

        // 진행된 카운터가 있는 독을 다시 걸면 새로 시작한다.
        s.CharacterStatusEffects[7001][0] = new ActiveStatusEffect(1, [Poison1 with { Counter = 2 }]);
        s.ApplyStatusEffect(7001, 1);
        Assert.Equal([3L, 1L], s.StatusEffectsOf(7001).Select(x => x.StatusEffectCode));
        Assert.All(s.StatusEffectsOf(7001).Single(x => x.StatusEffectCode == 1).EffectList, e => Assert.Equal(0, e.Counter));
    }

    [Fact]
    public void Removing_a_status_effect_and_cleaning_up_empty_characters()
    {
        var s = Session();
        s.ApplyStatusEffect(7001, 1);
        s.ApplyStatusEffect(7001, 2);

        Assert.True(s.RemoveStatusEffect(7001, 1));
        Assert.Equal([2L], s.StatusEffectsOf(7001).Select(x => x.StatusEffectCode));
        Assert.False(s.RemoveStatusEffect(7001, 1));           // 이미 풀림
        Assert.True(s.RemoveStatusEffect(7001, 2));
        Assert.False(s.CharacterStatusEffects.ContainsKey(7001));   // 비면 키도 사라진다
        Assert.False(s.RemoveStatusEffect(7002, 1));
        Assert.Empty(s.StatusEffectsOf(7001));
    }

    // ---- 세이브: 복구 가능한 값 ----

    [Fact]
    public void Save_roundtrip_restores_every_value_including_counter_and_valid_flag()
    {
        var pack = Pack();
        var s = Session(pack);
        s.CharacterStatusEffects[7001] =
        [
            new ActiveStatusEffect(1,
            [
                new ActiveEffect(TriggerType.AfterTurnEnd, -4, 1, long.MinValue, 7, long.MaxValue, Counter: 2, IsValid: true),
                new ActiveEffect(TriggerType.AfterMove, 5000000000, 1, 1, 2, 3, Counter: 9000000000000, IsValid: false),
            ]),
            new ActiveStatusEffect(4),
        ];

        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());

        var original = s.StatusEffectsOf(7001);
        var restored = loaded.StatusEffectsOf(7001);
        Assert.Equal(2, restored.Count);
        Assert.Equal(original[0].EffectList, restored[0].EffectList);     // 값, 카운터, 유효 플래그까지 같다
        Assert.Equal([1L, 4L], restored.Select(x => x.StatusEffectCode));  // 순서도 그대로
        Assert.Empty(restored[1].EffectList);                              // 이펙트가 없는 상태이상도 유지
    }

    [Fact]
    public void Save_json_stores_the_trigger_by_name_and_all_values()
    {
        var s = Session();
        s.ApplyStatusEffect(7001, 2);
        s.CharacterStatusEffects[7001][0].Effects![0] = s.CharacterStatusEffects[7001][0].EffectList[0] with { Counter = 1 };
        var json = s.ToSaveJson();

        Assert.Contains("\"StatusEffects\"", json);
        Assert.Contains("\"7001\"", json);
        Assert.Contains("\"StatusEffectCode\": 2", json);
        Assert.Contains("\"Trigger\": \"AfterTurnEnd\"", json);
        Assert.Contains("\"EffectCode\": 1", json);
        Assert.Contains("\"Value1\": -5", json);
        Assert.Contains("\"Value2\": 2", json);
        Assert.Contains("\"Counter\": 1", json);
        Assert.Contains("\"IsValid\": true", json);
    }

    [Fact]
    public void Saved_values_survive_a_later_change_of_the_master()
    {
        var s = Session();
        s.ApplyStatusEffect(7001, 2);                          // 화상: -5, 2번
        var json = s.ToSaveJson();

        // 같은 게임팩인데 마스터의 화상이 -50, 20번으로 바뀌었다.
        var changed = Pack(EffectHeader + "2\tAfterTurnEnd\t1\t1\t-50\t20\t0\n");
        var loaded = GameSession.FromSaveJson(changed, json);

        var effect = Assert.Single(loaded.StatusEffectsOf(7001).Single().EffectList);
        Assert.Equal(-5, effect.Value1);
        Assert.Equal(2, effect.Value2);
    }

    [Fact]
    public void Save_without_status_effects_loads_as_empty_and_empty_characters_are_not_saved()
    {
        var pack = Pack();
        Assert.Empty(GameSession.FromSaveJson(pack, "{\"Version\":1,\"PackId\":\"demo\"}").CharacterStatusEffects);

        var s = Session(pack);
        s.CharacterStatusEffects[7001] = [];                    // 빈 목록은 저장하지 않는다
        Assert.Empty(GameSession.FromSaveJson(pack, s.ToSaveJson()).CharacterStatusEffects);
    }

    [Fact]
    public void Saving_copies_the_data_so_later_changes_do_not_leak_into_the_json()
    {
        var s = Session();
        s.ApplyStatusEffect(7001, 2);
        var json = s.ToSaveJson();
        s.RemoveStatusEffect(7001, 2);
        s.ApplyStatusEffect(7001, 3);
        Assert.Contains("\"StatusEffectCode\": 2", json);
        Assert.DoesNotContain("\"StatusEffectCode\": 3", json);
    }

    // ---- 이펙트 인스턴스로 복구 ----

    [Fact]
    public void Restore_recreates_the_embedded_effect_with_values_counter_and_valid_flag()
    {
        var target = new Character(new CharacterEntry(7001, "용사", 1, 1, 2), CharacterInstance.Stats(5, 30, 40, 3, 30));
        var saved = new ActiveEffect(TriggerType.AfterTurnEnd, 1, 1, -3, 3, 0, Counter: 2, IsValid: true);

        var effect = saved.Restore(EmbeddedEffectRegistry.Default, target)!;

        Assert.IsType<HealEffect>(effect);
        Assert.Same(target, effect.Target);
        Assert.Equal((-3L, 3L, 0L), (effect.Value1, effect.Value2, effect.Value3));
        Assert.Equal(2, effect.Counter);
        Assert.True(effect.IsValid);
    }

    [Fact]
    public void Restore_returns_null_for_a_code_the_registry_does_not_have()
    {
        var target = new Character(new CharacterEntry(7001, "용사", 1, 1, 2), CharacterInstance.Stats(5, 30, 40, 3, 30));
        Assert.Null(new ActiveEffect(TriggerType.None, 0, 99, 0, 0, 0).Restore(EmbeddedEffectRegistry.Default, target));
    }

    [Fact]
    public void Capture_and_restore_let_a_running_effect_continue_where_it_stopped()
    {
        // 독 -3을 3번: 한 번 돌리고 저장 → 복구해서 이어 돌리면 처음부터 계속 돌린 것과 같다.
        Character NewTarget() => new(new CharacterEntry(7001, "용사", 1, 1, 2), CharacterInstance.Stats(5, 30, 40, 3, 30));
        var straight = NewTarget();
        var straightExecutor = new EffectExecutor();
        straightExecutor.Add(new HealEffect(straight, -3, 3, 0));
        for (var i = 0; i < 3; i++) straightExecutor.Run();

        var target = NewTarget();
        var first = new HealEffect(target, -3, 3, 0);
        var executor = new EffectExecutor();
        executor.Add(first);
        executor.Run();                                         // 1번 실행
        var saved = ActiveEffect.Capture(TriggerType.AfterTurnEnd, 1, first);
        Assert.Equal((1L, true), (saved.Counter, saved.IsValid));

        var resumedExecutor = new EffectExecutor();             // 복구해서 이어서
        resumedExecutor.Add(saved.Restore(EmbeddedEffectRegistry.Default, target)!);
        resumedExecutor.Run();
        resumedExecutor.Run();
        resumedExecutor.Run();                                  // 이미 한 번 돌았으므로 2번만 더 돌고 끝난다

        Assert.Equal(straight.State.Hp, target.State.Hp);       // 30 - 9
        Assert.Empty(resumedExecutor.Effects);
    }

    [Fact]
    public void An_effect_saved_as_invalid_stays_invalid_when_restored_and_is_discarded_by_the_executor()
    {
        var target = new Character(new CharacterEntry(7001, "용사", 1, 1, 2), CharacterInstance.Stats(5, 30, 40, 3, 30));
        var effect = new ActiveEffect(TriggerType.None, 0, 1, -3, 3, 0, Counter: 1, IsValid: false)
            .Restore(EmbeddedEffectRegistry.Default, target)!;
        Assert.False(effect.IsValid);

        var executor = new EffectExecutor();
        executor.Add(effect);
        executor.Run();
        Assert.Equal(30, target.State.Hp);
        Assert.Empty(executor.Effects);
    }

    // ---- 로드 검증 ----

    const string Head = "{\"Version\":1,\"PackId\":\"demo\",";
    const string Owned7001 = "\"Characters\":{\"7001\":{\"Level\":1,\"Hp\":5,\"MaxHp\":5,\"Xpp\":0,\"XppMax\":10}},";
    const string Eff = "{\"Trigger\":\"AfterTurnEnd\",\"Priority\":1,\"EffectCode\":1,\"Value1\":-3,\"Value2\":3,\"Value3\":0}";

    static string Status(string statusJson, string characterKey = "7001")
        => Head + Owned7001 + "\"StatusEffects\":{\"" + characterKey + "\":[" + statusJson + "]}}";

    [Fact]
    public void A_minimal_valid_status_effect_save_loads()
    {
        var s = GameSession.FromSaveJson(Pack(), Status("{\"StatusEffectCode\":1,\"Effects\":[" + Eff + "]}"));
        var effect = Assert.Single(Assert.Single(s.StatusEffectsOf(7001)).EffectList);
        Assert.Equal((0L, true), (effect.Counter, effect.IsValid));    // 생략한 카운터/유효는 기본값
        Assert.Equal(TriggerType.AfterTurnEnd, effect.Trigger);
    }

    [Theory]
    [InlineData("{\"StatusEffectCode\":99}")]                                                      // 상태이상 마스터에 없음
    [InlineData("{\"StatusEffectCode\":1},{\"StatusEffectCode\":1}")]                              // 같은 상태이상 두 번
    [InlineData("{\"StatusEffectCode\":1,\"Effects\":[{\"Trigger\":\"AfterTurnEnd\",\"Priority\":1,\"EffectCode\":99,\"Value1\":0,\"Value2\":0,\"Value3\":0}]}")]   // 없는 이펙트 코드
    [InlineData("{\"StatusEffectCode\":1,\"Effects\":[{\"Trigger\":\"Bogus\",\"Priority\":1,\"EffectCode\":1,\"Value1\":0,\"Value2\":0,\"Value3\":0}]}")]          // 모르는 트리거 이름
    [InlineData("{\"StatusEffectCode\":1,\"Effects\":[{\"Trigger\":99,\"Priority\":1,\"EffectCode\":1,\"Value1\":0,\"Value2\":0,\"Value3\":0}]}")]                // 범위 밖 트리거 숫자
    [InlineData("{\"StatusEffectCode\":1,\"Effects\":[{\"Trigger\":\"None\",\"Priority\":1,\"EffectCode\":1,\"Value1\":0,\"Value2\":0,\"Value3\":0,\"Counter\":-1}]}")]  // 음수 카운터
    [InlineData("{\"StatusEffectCode\":1,\"Effects\":[null]}")]                                    // 빈 이펙트 항목
    [InlineData("null")]                                                                           // 빈 상태이상 항목
    [InlineData("{\"StatusEffectCode\":\"독\"}")]                                                  // 코드가 숫자가 아님
    public void Invalid_status_effects_in_a_save_are_rejected(string statusJson)
        => Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(Pack(), Status(statusJson)));

    [Fact]
    public void Status_effects_on_an_unowned_or_unknown_character_are_rejected()
    {
        Assert.Throws<SaveFormatException>(() =>
            GameSession.FromSaveJson(Pack(), Status("{\"StatusEffectCode\":1}", characterKey: "7002")));   // 보유하지 않음
        Assert.Throws<SaveFormatException>(() =>
            GameSession.FromSaveJson(Pack(), Status("{\"StatusEffectCode\":1}", characterKey: "abc")));    // 숫자가 아닌 키
    }

    [Fact]
    public void An_effect_code_must_exist_in_the_registry_of_the_pack_that_loads_the_save()
    {
        var withHeal = Pack();
        var json = Status("{\"StatusEffectCode\":1,\"Effects\":[" + Eff + "]}");
        Assert.NotNull(GameSession.FromSaveJson(withHeal, json));

        // 이펙트 1번이 없는 등록부로 읽은 게임팩(이펙트 행 없이)에서는 같은 세이브가 거부된다.
        var noHeal = Pack(EffectHeader, new EmbeddedEffectRegistry([]));
        Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(noHeal, json));
    }

    // ---- 파티 화면의 상태이상 아이콘 ----

    [Fact]
    public void Party_slot_shows_status_badges_in_the_order_applied()
    {
        var s = Session();
        s.Party[0] = 7001;
        Assert.Empty(s.PartySlotViews[0].StatusBadges);

        s.ApplyStatusEffect(7001, 3);                           // 재생
        s.ApplyStatusEffect(7001, 1);                           // 독
        Assert.Equal(
            [new StatusBadge(3, "재생"), new StatusBadge(1, "독")],
            s.PartySlotViews[0].StatusBadges);
        Assert.Equal(2, s.PartySlotViews[0].Badges.Count);       // 속성 배지는 그대로
        Assert.All(s.PartySlotViews.Skip(1), v => Assert.Empty(v.StatusBadges));
    }

    [Fact]
    public void Status_badges_survive_save_and_load()
    {
        var pack = Pack();
        var s = Session(pack);
        s.Party[2] = 7001;
        s.ApplyStatusEffect(7001, 2);

        var loaded = GameSession.FromSaveJson(pack, s.ToSaveJson());
        Assert.Equal(["화상"], loaded.PartySlotViews[2].StatusBadges.Select(b => b.Name));
    }
}
