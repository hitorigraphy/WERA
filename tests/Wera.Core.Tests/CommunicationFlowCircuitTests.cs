using Wera.Core;

namespace Wera.Core.Tests;

// 교류 흐름 실행기: ADV와 따로의 실행기이고 교류 흐름 종류(Text 등)를 직접 읽는다.
public class CommunicationFlowCircuitTests
{
    static GamePack Pack() => GamePack.Load(CoreTests.PackZip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
        ("master/character_inventory_master_v1.tsv",
            "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n")));

    static CommunicationFlowStep Step(long number, CommunicationFlowType type, long p1 = 0, long p2 = 0, long p3 = 0, string text = "") =>
        new(number, type, p1, p2, p3, text);

    static CommunicationFlowCircuit Circuit(long speaker, Dictionary<long, long> flags, params CommunicationFlowStep[] flows) =>
        new(Pack(), 1, flows, speaker, flags);

    [Fact]
    public void Text_is_the_partner_speaking_and_an_empty_slot_is_unknown()
    {
        var known = Circuit(7001, [], Step(1, CommunicationFlowType.Text, text: "안녕")).Advance();
        Assert.Equal((AdvStepKind.TextYou, "용사", 7001L, "안녕"), (known.Kind, known.SpeakerName, known.CharacterCode, known.Text));

        var unknown = Circuit(0, [], Step(1, CommunicationFlowType.Text, text: "안녕")).Advance();
        Assert.Equal(("???", 0L), (unknown.SpeakerName, unknown.CharacterCode));
    }

    [Fact]
    public void Flags_jumps_and_finish_follow_the_same_rules_as_the_adv_circuit()
    {
        var flags = new Dictionary<long, long>();
        var c = Circuit(7001, flags,
            Step(1, CommunicationFlowType.SetFlag, 5, 3),
            Step(2, CommunicationFlowType.JumpFlagEqual, 5, 3, 4),
            Step(3, CommunicationFlowType.TextPlain, text: "건너뜀"),
            Step(4, CommunicationFlowType.TextLog, text: "도착"),
            Step(5, CommunicationFlowType.Finish),
            Step(6, CommunicationFlowType.TextPlain, text: "안 나옴"));
        var step = c.Advance();
        Assert.Equal((AdvStepKind.TextLog, "도착"), (step.Kind, step.Text));
        Assert.Equal(3, flags[5]);
        Assert.Equal(AdvStepKind.Finished, c.Advance().Kind);
        Assert.True(c.IsFinished);
    }

    [Fact]
    public void A_choice_waits_and_choosing_jumps_to_its_flow_number()
    {
        var c = Circuit(7001, [],
            Step(1, CommunicationFlowType.Choice, 1, 3, text: "가"),
            Step(2, CommunicationFlowType.Choice, 1, 4, text: "나"),
            Step(3, CommunicationFlowType.TextPlain, text: "가 선택"),
            Step(4, CommunicationFlowType.TextPlain, text: "나 선택"));
        var choice = c.Advance();
        Assert.Equal(["가", "나"], choice.Choices.Select(x => x.Text));
        Assert.Equal(AdvStepKind.Choice, c.Advance().Kind);            // 고르기 전에는 같은 묶음
        Assert.False(c.Choose(5));
        Assert.True(c.Choose(1));
        Assert.Equal("나 선택", c.Advance().Text);
    }
}
