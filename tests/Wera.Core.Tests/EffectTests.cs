using Wera.Core;

namespace Wera.Core.Tests;

public class EffectTests
{
    // ---- 테스트용 임베디드 이펙트 (구동기의 하드코딩 이펙트와 같은 방식: 생성자에서 타겟과 밸류를 받고, Execute()는 파라미터 없음) ----

    // 1번은 구동기의 실제 HealEffect를 쓴다.

    /// <summary>2: XP를 Value1, 레벨을 Value2, 최대 HP를 Value3만큼 늘린다.</summary>
    sealed class GrowEffect(Character target, long v1, long v2, long v3) : EmbeddedEffect(target, v1, v2, v3)
    {
        public override long EffectCode => 2;
        public override void Execute() => Target.State = Target.State with
        {
            Xp = Target.State.Xp + Value1,
            Level = Target.State.Level + Value2,
            MaxHp = Target.State.MaxHp + Value3,
        };
    }

    /// <summary>3: 실행될 때 저장해 둔 값을 기록한다.</summary>
    sealed class RecordingEffect(Character target, long v1, long v2, long v3, List<(long, string, long, long, long)> log)
        : EmbeddedEffect(target, v1, v2, v3)
    {
        public override long EffectCode => 3;
        public override void Execute() => log.Add((Target.Code, Target.Name, Value1, Value2, Value3));
    }

    /// <summary>등록한 코드와 다른 EffectCode를 돌려주는 잘못된 이펙트.</summary>
    sealed class MismatchedEffect(Character target, long v1, long v2, long v3) : EmbeddedEffect(target, v1, v2, v3)
    {
        public override long EffectCode => 12345;
        public override void Execute() { }
    }

    static EmbeddedEffectRegistry Registry(List<(long, string, long, long, long)>? log = null)
    {
        log ??= [];
        return new EmbeddedEffectRegistry(
        [
            new(1, (t, a, b, c) => new HealEffect(t, a, b, c)),
            new(2, (t, a, b, c) => new GrowEffect(t, a, b, c)),
            new(3, (t, a, b, c) => new RecordingEffect(t, a, b, c, log)),
        ]);
    }

    static Character Hero(long hp = 30) =>
        new(new CharacterEntry(7001, "용사", 1, 4, 5), new CharacterState(5, hp, 40, 3, 30));

    // ---- 임베디드 이펙트 (추상 클래스) ----

    [Fact]
    public void Embedded_effect_is_abstract_with_a_constructor_taking_target_and_values()
    {
        Assert.True(typeof(EmbeddedEffect).IsAbstract);
        Assert.NotNull(typeof(EmbeddedEffect).GetProperty(nameof(EmbeddedEffect.EffectCode)));

        var constructor = typeof(EmbeddedEffect).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Single();
        Assert.Equal([typeof(Character), typeof(long), typeof(long), typeof(long)], constructor.GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public void Execute_takes_no_parameters()
    {
        var execute = typeof(EmbeddedEffect).GetMethod(nameof(EmbeddedEffect.Execute))!;
        Assert.True(execute.IsAbstract);
        Assert.Empty(execute.GetParameters());
    }

    [Fact]
    public void Constructor_stores_the_target_and_values_without_executing()
    {
        var log = new List<(long, string, long, long, long)>();
        var target = Hero();

        var effect = Registry(log).Create(3, target, 10, -20, long.MaxValue)!;

        Assert.Same(target, effect.Target);
        Assert.Equal(10, effect.Value1);
        Assert.Equal(-20, effect.Value2);
        Assert.Equal(long.MaxValue, effect.Value3);
        Assert.Empty(log);                                    // 만들기만 했고 아직 실행하지 않았다
    }

    [Fact]
    public void Execute_runs_with_the_stored_target_and_values()
    {
        var log = new List<(long, string, long, long, long)>();
        var effect = Registry(log).Create(3, Hero(), 1, 2, 3)!;

        effect.Execute();

        Assert.Equal([(7001L, "용사", 1L, 2L, 3L)], log);
    }

    [Fact]
    public void Execute_can_change_the_target_state_and_can_run_more_than_once()
    {
        var target = Hero(hp: 10);
        var effect = Registry().Create(1, target, 8)!;

        effect.Execute();
        Assert.Equal(18, target.State.Hp);
        effect.Execute();
        Assert.Equal(26, target.State.Hp);
    }

    [Fact]
    public void Character_exposes_entry_name_and_code()
    {
        var c = Hero();
        Assert.Equal(7001, c.Code);
        Assert.Equal("용사", c.Name);
        Assert.Equal(4, c.Entry.AdvantageAttributeCode);
    }

    // ---- 임베디드 이펙트 등록부 ----

    [Fact]
    public void Master_creates_effects_by_code_and_knows_which_codes_exist()
    {
        var master = Registry();
        Assert.True(master.Contains(1));
        Assert.IsType<GrowEffect>(master.Create(2, Hero()));
        Assert.False(master.Contains(99));
        Assert.Null(master.Create(99, Hero()));
        Assert.Equal(3, master.All.Count);
    }

    [Fact]
    public void Each_create_makes_a_new_independent_effect()
    {
        var master = Registry();
        var a = master.Create(1, Hero(), 1)!;
        var b = master.Create(1, Hero(), 2)!;
        Assert.NotSame(a, b);
        Assert.Equal(1, a.Value1);
        Assert.Equal(2, b.Value1);
    }

    [Fact]
    public void Master_rejects_duplicate_effect_codes()
        => Assert.Throws<ArgumentException>(() => new EmbeddedEffectRegistry(
        [
            new(1, (t, a, b, c) => new HealEffect(t, a, b, c)),
            new(1, (t, a, b, c) => new HealEffect(t, a, b, c)),
        ]));

    [Fact]
    public void Effect_whose_code_differs_from_its_registration_is_reported()
    {
        var master = new EmbeddedEffectRegistry([new(7, (t, a, b, c) => new MismatchedEffect(t, a, b, c))]);
        var error = Assert.Throws<InvalidOperationException>(() => master.Create(7, Hero()));
        Assert.Contains("12345", error.Message);
    }

    [Fact]
    public void Default_master_is_hardcoded_in_the_engine_and_each_definition_matches_its_class()
    {
        var codes = EmbeddedEffectRegistry.Default.All.Select(d => d.EffectCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Same(EmbeddedEffectRegistry.Default, EmbeddedEffectRegistry.Default);
        foreach (var code in codes)                           // 등록한 코드와 클래스의 EffectCode가 같아야 한다
            Assert.Equal(code, EmbeddedEffectRegistry.Default.Create(code, Hero())!.EffectCode);
    }

    // ---- 게임팩: 상태이상 마스터, 상태이상 이펙트 마스터 ----

    const string StatusHeader = "status_effect_code\tstatus_effect_name\n";
    const string EffectHeader =
        "status_effect_code\ttrigger_type\tpriority\teffect_code\teffect_value_1\teffect_value_2\teffect_value_3\n";
    const string Statuses = StatusHeader + "1\t독\n2\t화상\n3\t재생\n";

    static GamePack Pack(
        string? statuses = Statuses, string? effects = null, EmbeddedEffectRegistry? embedded = null,
        bool withCharacters = false)
    {
        var files = new List<(string, string)> { ("pack.tsv", "id\tdemo\ntitle\t데모\n") };
        if (statuses is not null) files.Add(("master/status_effect_master_v1.tsv", statuses));
        if (effects is not null) files.Add(("master/status_effect_effect_master_v1.tsv", effects));
        if (withCharacters)
        {
            files.Add(("master/attribute_master_v1.tsv", "attribute_code\tattribute_name\n1\t불\n2\t물\n"));
            files.Add(("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n" +
                "7001\t용사\t1\t1\t2\n7002\t마법사\t1\t2\t1\n"));
        }
        return GamePack.Load(CoreTests.PackZip(files.ToArray()), embedded);
    }

    [Fact]
    public void Status_effect_masters_are_optional()
    {
        var pack = Pack(null);
        Assert.Null(pack.StatusEffects);
        Assert.Null(pack.StatusEffectEffects);
    }

    [Fact]
    public void Status_effect_master_loads_code_and_name_in_order()
        => Assert.Equal(
            [new StatusEffectEntry(1, "독"), new StatusEffectEntry(2, "화상"), new StatusEffectEntry(3, "재생")],
            Pack().StatusEffects);

    [Theory]
    [InlineData("1\ta\n1\tb\n")]       // 중복 코드
    [InlineData("\ta\n")]               // 빈 코드
    [InlineData("abc\ta\n")]            // 정수 아님
    public void Bad_status_effect_rows_are_rejected(string rows)
        => Assert.Throws<PackFormatException>(() => Pack(StatusHeader + rows));

    [Theory]
    [InlineData("code\tname\n1\ta\n")]
    [InlineData("status_effect_code\n1\n")]
    public void Status_effect_master_header_is_checked(string tsv)
        => Assert.Throws<PackFormatException>(() => Pack(tsv));

    [Fact]
    public void Status_effect_effects_load_all_columns_in_file_order()
    {
        var pack = Pack(effects: EffectHeader +
            "1\tNone\t10\t1\t5\t6\t7\n" +
            "1\tnone\t-3\t2\t\t\t\n" +                    // 트리거는 대소문자 무시, 밸류 비우면 0, 우선도 음수 허용
            "2\tNone\t5000000000\t3\t9000000000000\t-1\t0\n", embedded: Registry());

        Assert.Equal(
            [
                new StatusEffectEffect(1, TriggerType.None, 10, 1, 5, 6, 7),
                new StatusEffectEffect(1, TriggerType.None, -3, 2, 0, 0, 0),
                new StatusEffectEffect(2, TriggerType.None, 5000000000, 3, 9000000000000, -1, 0),
            ],
            pack.StatusEffectEffects);
    }

    [Fact]
    public void Effect_codes_are_checked_against_the_embedded_effect_master_given_to_load()
    {
        var master = Registry();
        var pack = Pack(effects: EffectHeader + "1\tNone\t1\t1\t5\t0\t0\n", embedded: master);
        Assert.Same(master, pack.EmbeddedEffects);

        // 기본(하드코딩) 마스터에는 코드 1이 없으므로 같은 게임팩이 거부된다.
        Assert.Throws<PackFormatException>(() => Pack(effects: EffectHeader + "1\tNone\t1\t99\t0\t0\t0\n", embedded: master));
        Assert.Same(EmbeddedEffectRegistry.Default, Pack().EmbeddedEffects);
    }

    [Fact]
    public void Status_effect_effects_need_the_status_effect_master()
        => Assert.Throws<PackFormatException>(() =>
            Pack(statuses: null, effects: EffectHeader + "1\tNone\t1\t1\t0\t0\t0\n", embedded: Registry()));

    [Theory]
    [InlineData("9\tNone\t1\t1\t0\t0\t0\n")]       // 상태이상 마스터에 없는 코드
    [InlineData("abc\tNone\t1\t1\t0\t0\t0\n")]     // 상태이상 코드가 정수 아님
    [InlineData("1\tBogus\t1\t1\t0\t0\t0\n")]      // 모르는 트리거 타입
    [InlineData("1\t0\t1\t1\t0\t0\t0\n")]          // 트리거 타입에 숫자는 불가
    [InlineData("1\t\t1\t1\t0\t0\t0\n")]           // 트리거 타입 비어 있음
    [InlineData("1\tNone\t\t1\t0\t0\t0\n")]        // 우선도 비어 있음
    [InlineData("1\tNone\tx\t1\t0\t0\t0\n")]       // 우선도가 정수 아님
    [InlineData("1\tNone\t1.5\t1\t0\t0\t0\n")]     // 우선도가 소수
    [InlineData("1\tNone\t1\t\t0\t0\t0\n")]        // 이펙트 코드 비어 있음
    [InlineData("1\tNone\t1\tx\t0\t0\t0\n")]       // 이펙트 코드가 정수 아님
    [InlineData("1\tNone\t1\t99\t0\t0\t0\n")]      // 임베디드 이펙트에 없는 코드
    [InlineData("1\tNone\t1\t1\tx\t0\t0\n")]       // 밸류 1이 정수 아님
    [InlineData("1\tNone\t1\t1\t0\ty\t0\n")]       // 밸류 2가 정수 아님
    [InlineData("1\tNone\t1\t1\t0\t0\t1.5\n")]     // 밸류 3이 정수 아님
    public void Bad_status_effect_effect_rows_are_rejected(string row)
        => Assert.Throws<PackFormatException>(() => Pack(effects: EffectHeader + row, embedded: Registry()));

    [Theory]
    [InlineData("status_effect_code\ttrigger_type\tpriority\teffect_code\n1\tNone\t1\t1\n")]
    [InlineData("code\ttrigger\tpriority\teffect_code\teffect_value_1\teffect_value_2\teffect_value_3\n1\tNone\t1\t1\t0\t0\t0\n")]
    public void Status_effect_effect_header_is_checked(string tsv)
        => Assert.Throws<PackFormatException>(() => Pack(effects: tsv, embedded: Registry()));

    [Fact]
    public void Trigger_types_are_the_agreed_list_with_none_as_zero()
    {
        Assert.Equal(0, (int)TriggerType.None);
        Assert.Equal(
            [
                "None", "BeforeAttack", "AfterAttack", "BeforeAttacked", "AfterAttacked",
                "AfterTurnStart", "AfterTurnEnd", "AfterBattleStart", "AfterBattleEnd", "AfterMove",
            ],
            Enum.GetNames<TriggerType>());
    }

    [Theory]
    [InlineData("None", TriggerType.None)]
    [InlineData("BeforeAttack", TriggerType.BeforeAttack)]
    [InlineData("AfterAttack", TriggerType.AfterAttack)]
    [InlineData("BeforeAttacked", TriggerType.BeforeAttacked)]
    [InlineData("AfterAttacked", TriggerType.AfterAttacked)]
    [InlineData("AfterTurnStart", TriggerType.AfterTurnStart)]
    [InlineData("AfterTurnEnd", TriggerType.AfterTurnEnd)]
    [InlineData("AfterBattleStart", TriggerType.AfterBattleStart)]
    [InlineData("AfterBattleEnd", TriggerType.AfterBattleEnd)]
    [InlineData("AfterMove", TriggerType.AfterMove)]
    [InlineData("afterturnend", TriggerType.AfterTurnEnd)]        // 대소문자 무시
    public void Every_trigger_type_can_be_written_by_name_in_the_tsv(string name, TriggerType expected)
    {
        var pack = Pack(effects: EffectHeader + $"1\t{name}\t1\t1\t0\t0\t0\n", embedded: Registry());
        Assert.Equal(expected, Assert.Single(pack.StatusEffectEffects!).Trigger);
    }

    [Theory]
    [InlineData("BeforeTurnStart")]       // 개시 전은 만들지 않았다
    [InlineData("BeforeBattleStart")]
    [InlineData("BeforeMove")]
    [InlineData("1")]                      // 숫자는 불가
    public void Trigger_types_outside_the_list_are_rejected(string name)
        => Assert.Throws<PackFormatException>(() =>
            Pack(effects: EffectHeader + $"1\t{name}\t1\t1\t0\t0\t0\n", embedded: Registry()));

    // ---- 세션: 보유 캐릭터에게 이펙트 실행 ----

    static GameSession Session(List<(long, string, long, long, long)>? log = null)
    {
        var pack = Pack(withCharacters: true, embedded: Registry(log));
        var s = GameSession.NewGame(pack);
        s.Characters[7001] = new CharacterState(5, 30, 40, 3, 30);
        return s;
    }

    [Fact]
    public void Applying_an_effect_changes_the_owned_character()
    {
        var s = Session();
        Assert.True(s.ApplyEffect(1, 7001, 6));
        Assert.Equal(36, s.Characters[7001].Hp);
    }

    [Fact]
    public void Applied_state_is_clamped_to_what_a_save_accepts()
    {
        var s = Session();
        s.ApplyEffect(1, 7001, 1000);                          // 회복이 최대 HP를 넘으면 최대 HP까지
        Assert.Equal(40, s.Characters[7001].Hp);

        s.ApplyEffect(1, 7001, -1000);                         // 음수 HP는 0(사망)
        Assert.Equal(0, s.Characters[7001].Hp);
        Assert.True(s.Characters[7001].IsDead);

        s.ApplyEffect(2, 7001, 100, -100, -100);               // XP 넘침, 레벨 1 미만, 최대 HP 0 미만
        var c = s.Characters[7001];
        Assert.Equal(30, c.Xp);                                // 필요 XP 30까지
        Assert.Equal(1, c.Level);
        Assert.Equal(0, c.MaxHp);
    }

    [Fact]
    public void Values_reach_the_effect_unchanged()
    {
        var log = new List<(long, string, long, long, long)>();
        var s = Session(log);
        s.ApplyEffect(3, 7001, 1, 2, 3);
        Assert.Equal([(7001L, "용사", 1L, 2L, 3L)], log);
    }

    [Fact]
    public void Unknown_effect_or_unowned_or_unknown_character_does_nothing()
    {
        var log = new List<(long, string, long, long, long)>();
        var s = Session(log);
        Assert.False(s.ApplyEffect(99, 7001, 5));              // 없는 이펙트
        Assert.False(s.ApplyEffect(1, 7002, 5));               // 마스터에 있지만 보유하지 않음
        Assert.False(s.ApplyEffect(1, 9999, 5));               // 마스터에 없는 캐릭터
        Assert.False(s.ApplyEffect(3, 7002, 5));
        Assert.Empty(log);
        Assert.Equal(30, s.Characters[7001].Hp);
    }

    [Fact]
    public void State_after_an_effect_still_saves_and_loads()
    {
        var s = Session();
        s.ApplyEffect(1, 7001, 500);
        s.ApplyEffect(2, 7001, 500, 3, 10);

        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.Equal(s.Characters[7001], loaded.Characters[7001]);
    }
}
