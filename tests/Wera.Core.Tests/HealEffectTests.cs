using Wera.Core;

namespace Wera.Core.Tests;

public class HealEffectTests
{
    static Character Hero(long hp = 30, long maxHp = 40)
        => new(new CharacterEntry(7001, "용사", 1, 4, 5), CharacterInstance.Stats(5, hp, maxHp, 3, 30));

    // ---- 임베디드 이펙트 1번: HP 회복/감소 ----

    [Fact]
    public void Heal_is_embedded_effect_1_and_registered_in_the_default_registry()
    {
        var registry = EmbeddedEffectRegistry.Default;
        Assert.True(registry.Contains(1));
        var effect = registry.Create(1, Hero(), 5, 3, 0);
        Assert.IsType<HealEffect>(effect);
        Assert.Equal(1, effect!.EffectCode);
    }

    [Fact]
    public void Positive_value1_heals_and_negative_value1_damages()
    {
        var healed = Hero(hp: 10);
        new HealEffect(healed, 7, 0, 0).Execute();
        Assert.Equal(17, healed.State.Hp);

        var hurt = Hero(hp: 10);
        new HealEffect(hurt, -4, 0, 0).Execute();
        Assert.Equal(6, hurt.State.Hp);
    }

    [Fact]
    public void Hp_stays_between_zero_and_max_hp()
    {
        var overheal = Hero(hp: 38);
        new HealEffect(overheal, 100, 0, 0).Execute();
        Assert.Equal(40, overheal.State.Hp);

        var overkill = Hero(hp: 5);
        new HealEffect(overkill, -100, 0, 0).Execute();
        Assert.Equal(0, overkill.State.Hp);
        Assert.True(overkill.State.IsDead);
    }

    [Fact]
    public void Huge_values_do_not_overflow()
    {
        var a = Hero(hp: long.MaxValue - 1, maxHp: long.MaxValue);
        new HealEffect(a, long.MaxValue, 0, 0).Execute();
        Assert.Equal(long.MaxValue, a.State.Hp);

        var b = Hero(hp: 1, maxHp: long.MaxValue);
        new HealEffect(b, long.MinValue, 0, 0).Execute();
        Assert.Equal(0, b.State.Hp);
    }

    [Fact]
    public void Only_hp_changes_and_the_other_values_are_not_used()
    {
        var target = Hero(hp: 10);
        var before = target.State;
        new HealEffect(target, 5, 99, 12345).Execute();
        Assert.Equal(before.WithStatus(GamePack.HpStatusCode, 15), target.State);
    }

    [Theory]
    [InlineData(8)]       // 회복도
    [InlineData(-8)]      // 감소도
    [InlineData(0)]
    public void A_dead_target_is_left_unchanged_and_the_effect_invalidates_itself_at_once(long value1)
    {
        var dead = Hero(hp: 0);
        var before = dead.State;
        var effect = new HealEffect(dead, value1, 5, 0);

        effect.Execute();

        Assert.Equal(before, dead.State);                        // 되살아나지 않는다
        Assert.True(dead.State.IsDead);
        Assert.False(effect.IsValid);                            // 바로 무효
    }

    [Fact]
    public void A_living_target_keeps_the_effect_valid()
    {
        var effect = new HealEffect(Hero(hp: 10), 5, 5, 0);
        effect.Execute();
        Assert.True(effect.IsValid);
    }

    [Fact]
    public void Killing_the_target_ends_that_run_normally_and_the_next_run_invalidates_the_effect()
    {
        var target = Hero(hp: 4);
        var effect = new HealEffect(target, -10, 5, 0);          // 한 번에 죽는 피해

        effect.Execute();
        Assert.Equal(0, target.State.Hp);
        Assert.True(effect.IsValid);                             // 이번 실행으로는 아직 유효

        effect.Execute();                                        // 다음 실행: 사망 → 무효
        Assert.False(effect.IsValid);
        Assert.Equal(0, target.State.Hp);
    }

    [Fact]
    public void Executor_discards_the_effect_of_a_dead_target_without_waiting_for_value2()
    {
        var target = Hero(hp: 0);
        var effect = new HealEffect(target, 5, 10, 0);           // 밸류 2는 10번이지만
        var executor = new EffectExecutor();
        executor.Add(effect);

        executor.Run();

        Assert.False(effect.IsValid);
        Assert.Empty(executor.Effects);                          // 첫 실행 뒤 바로 폐기
        Assert.Equal(0, target.State.Hp);
    }

    [Fact]
    public void Poison_that_kills_the_target_stops_on_the_following_run()
    {
        var target = Hero(hp: 5);
        var executor = new EffectExecutor();
        executor.Add(new HealEffect(target, -3, 10, 0));

        executor.Run();                                          // 5 → 2
        executor.Run();                                          // 2 → 0 (사망)
        Assert.Equal(0, target.State.Hp);
        Assert.Single(executor.Effects);                         // 아직 유효

        executor.Run();                                          // 사망 확인 → 무효, 폐기
        Assert.Empty(executor.Effects);
        Assert.Equal(0, target.State.Hp);
    }

    // ---- 카운터와 유효 플래그 ----

    [Fact]
    public void A_new_effect_is_valid_and_its_counter_is_zero()
    {
        var effect = new HealEffect(Hero(), 1, 3, 0);
        Assert.Equal(0, effect.Counter);
        Assert.True(effect.IsValid);
    }

    [Fact]
    public void Executing_directly_does_not_touch_the_counter_only_the_executor_does()
    {
        var effect = new HealEffect(Hero(hp: 10), 1, 3, 0);
        effect.Execute();
        Assert.Equal(0, effect.Counter);
        Assert.True(effect.IsValid);
    }

    // ---- 익스큐터 ----

    [Fact]
    public void Executor_runs_the_effect_raises_the_counter_and_invalidates_at_value2()
    {
        var target = Hero(hp: 10);
        var effect = new HealEffect(target, 2, 3, 0);            // 밸류 2 = 3번
        var executor = new EffectExecutor();
        executor.Add(effect);

        executor.Run();
        Assert.Equal((1L, true, 12L), (effect.Counter, effect.IsValid, target.State.Hp));
        executor.Run();
        Assert.Equal((2L, true, 14L), (effect.Counter, effect.IsValid, target.State.Hp));
        Assert.Single(executor.Effects);

        executor.Run();                                          // 3번째: 카운터 3 >= 밸류 2(3) → 무효
        Assert.Equal((3L, false, 16L), (effect.Counter, effect.IsValid, target.State.Hp));
        Assert.Empty(executor.Effects);                          // 폐기됨
    }

    [Fact]
    public void Discarded_effect_does_not_run_again()
    {
        var target = Hero(hp: 10);
        var executor = new EffectExecutor();
        executor.Add(new HealEffect(target, 2, 2, 0));

        executor.Run();
        executor.Run();
        executor.Run();
        executor.Run();

        Assert.Equal(14, target.State.Hp);                       // 2번만 돌았다
        Assert.Empty(executor.Effects);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]          // 밸류 2가 0 이하여도 한 번은 돌고(카운터 1 >= 밸류 2) 곧바로 무효가 된다
    [InlineData(-5)]
    public void Value2_of_one_or_less_runs_exactly_once(long value2)
    {
        var target = Hero(hp: 10);
        var executor = new EffectExecutor();
        executor.Add(new HealEffect(target, 3, value2, 0));

        executor.Run();
        executor.Run();

        Assert.Equal(13, target.State.Hp);
        Assert.Empty(executor.Effects);
    }

    [Fact]
    public void Effects_with_different_value2_expire_independently()
    {
        var target = Hero(hp: 1, maxHp: 1000);
        var executor = new EffectExecutor();
        var shortLived = new HealEffect(target, 1, 1, 0);
        var longLived = new HealEffect(target, 10, 3, 0);
        executor.Add(shortLived);
        executor.Add(longLived);

        executor.Run();
        Assert.Equal([longLived], executor.Effects);             // shortLived는 폐기, longLived만 남음
        executor.Run();
        executor.Run();
        Assert.Empty(executor.Effects);
        Assert.Equal(1 + (1 + 10) + 10 + 10, target.State.Hp);
    }

    [Fact]
    public void Effects_run_in_the_order_they_were_added()
    {
        var log = new List<long>();
        var executor = new EffectExecutor();
        foreach (var id in new long[] { 3, 1, 2 })
            executor.Add(new OrderEffect(Hero(), id, log));
        executor.Run();
        Assert.Equal([3L, 1L, 2L], log);
    }

    [Fact]
    public void An_already_invalid_effect_is_skipped_and_discarded()
    {
        var target = Hero(hp: 10);
        var effect = new HealEffect(target, 5, 3, 0) { IsValid = false };
        var executor = new EffectExecutor();
        executor.Add(effect);

        executor.Run();

        Assert.Equal(10, target.State.Hp);                       // 실행되지 않았다
        Assert.Equal(0, effect.Counter);
        Assert.Empty(executor.Effects);
    }

    [Fact]
    public void An_effect_added_during_a_run_starts_with_the_next_run()
    {
        var target = Hero(hp: 10);
        var executor = new EffectExecutor();
        var late = new HealEffect(target, 100, 5, 0);
        executor.Add(new AddingEffect(Hero(), executor, late));

        executor.Run();
        Assert.Equal(10, target.State.Hp);                       // late는 아직 돌지 않았다
        Assert.Contains(late, executor.Effects);

        executor.Run();
        Assert.Equal(40, target.State.Hp);
    }

    [Fact]
    public void Executor_works_with_effects_made_by_the_registry()
    {
        var target = Hero(hp: 10);
        var executor = new EffectExecutor();
        executor.Add(EmbeddedEffectRegistry.Default.Create(1, target, 4, 2, 0)!);
        executor.Run();
        executor.Run();
        executor.Run();
        Assert.Equal(18, target.State.Hp);
        Assert.Empty(executor.Effects);
    }

    // ---- 테스트용 이펙트 ----

    sealed class OrderEffect(Character target, long id, List<long> log) : EmbeddedEffect(target, id, 2, 0)
    {
        public override long EffectCode => 100;
        public override void Execute() => log.Add(Value1);
    }

    sealed class AddingEffect(Character target, EffectExecutor executor, EmbeddedEffect other) : EmbeddedEffect(target, 0, 1, 0)
    {
        public override long EffectCode => 101;
        public override void Execute() => executor.Add(other);
    }
}
