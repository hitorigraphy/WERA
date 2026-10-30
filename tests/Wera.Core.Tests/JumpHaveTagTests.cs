using Wera.Core;

namespace Wera.Core.Tests;

// JumpCharacterHaveTag / JumpUniqueItemHaveTag: param_1 코드, param_2 태그 코드, param_3 target_flow_number
public class JumpHaveTagTests
{
    const string TagMaster = "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n9\t강함\n10\t날카로움\n";

    static (string, string)[] Files(bool viaAdv, string type, long code, long tag)
    {
        var eventRows = viaAdv
            ? "3001\t1\tAdv\t4001\t0\t0\n"
            : $"3001\t1\t{type}\t{code}\t{tag}\t3\n3001\t2\tAdv\t4001\t0\t0\n3001\t3\tAdv\t4002\t0\t0\n";
        var advRows = viaAdv
            ? $"4001\t1\t{type}\t{code}\t{tag}\t3\t\t\n4001\t2\tTextPlain\t0\t0\t0\t건너뛴 칸\t\n4001\t3\tTextPlain\t0\t0\t0\t도착한 칸\t\n"
            : "";
        return
        [
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/tag_master_v1.tsv", TagMaster),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n8002\t대검\n"),
            ("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n8001\t10\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t1\t1\t2\n7002\t마법사\t1\t1\t2\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7001\t9\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" + eventRows),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" + advRows),
        ];
    }

    static GameSession Session(bool viaAdv, string type, long code, long tag) =>
        GameSession.NewGame(GamePack.Load(CoreTests.PackZip(Files(viaAdv, type, code, tag))));

    /// <summary>점프했으면 true. 세션이 알려 주는 태그로 판단한다.</summary>
    static bool Jumped(GameSession s, bool viaAdv)
    {
        if (viaAdv)
            return new AdvFlowCircuit(s.Pack, 4001, s.PlayerFlags, s.AddInventory, s.CountInventory, s.CharacterHasTag, s.UniqueItemHasTag)
                .Advance().Text == "도착한 칸";
        return s.StartEvent(3001)!.Advance().Code == 4002;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_character_jumps_when_it_is_owned_and_has_the_tag_now(bool viaAdv)
    {
        var s = Session(viaAdv, "JumpCharacterHaveTag", 7001, 9);
        Assert.False(Jumped(s, viaAdv));                                            // 보유하지 않은 캐릭터는 태그가 없는 것으로 본다

        s.AddInventory(7001, 1);                                                    // 용사는 마스터 태그 9(강함)를 복사해 온다
        Assert.True(Jumped(s, viaAdv));

        s.RemoveCharacterTag(7001, 9);                                              // 게임 중에 태그를 잃으면 거짓
        Assert.False(Jumped(s, viaAdv));
        s.AddCharacterTag(7001, 9);
        Assert.True(Jumped(s, viaAdv));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_character_without_the_tag_does_not_jump_and_the_player_has_the_player_tag(bool viaAdv)
    {
        var other = Session(viaAdv, "JumpCharacterHaveTag", 7002, 9);
        other.AddInventory(7002, 1);
        Assert.False(Jumped(other, viaAdv));                                        // 마법사는 태그 9가 없다

        Assert.True(Jumped(Session(viaAdv, "JumpCharacterHaveTag", 1, 1), viaAdv)); // 플레이어(캐릭터 1)는 플레이어 태그를 가진다
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_unique_item_jumps_when_any_owned_instance_has_the_tag_now(bool viaAdv)
    {
        var s = Session(viaAdv, "JumpUniqueItemHaveTag", 8001, 10);
        Assert.False(Jumped(s, viaAdv));                                            // 보유하지 않으면 거짓

        s.AddInventory(8001, 1);                                                    // 인스턴스는 마스터 태그 10(날카로움)을 복사해 온다
        Assert.True(Jumped(s, viaAdv));

        var id = s.InstancesOf(8001).Single();
        s.RemoveUniqueItemTag(id, 10);                                              // 인스턴스가 태그를 잃으면 거짓
        Assert.False(Jumped(s, viaAdv));

        s.AddInventory(8001, 1);                                                    // 새 인스턴스는 마스터 태그를 가지니 하나라도 있으면 참
        Assert.True(Jumped(s, viaAdv));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_item_gained_inside_the_flow_is_seen_by_the_tag_jump_that_follows(bool viaAdv)
    {
        var files = new (string, string)[]
        {
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/tag_master_v1.tsv", TagMaster),
            ("master/unique_item_inventory_master_v1.tsv", "inventory_code\titem_name\n8001\t철검\n"),
            ("master/unique_item_tag_master_v1.tsv", "item_code\ttag_code\n8001\t10\n"),
            ("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"),
            ("master/adv_master_v1.tsv", "code\tname\n4001\tA\n4002\tB\n"),
            ("master/event_flow_master_v1.tsv", "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n" +
                (viaAdv
                    ? "3001\t1\tAdv\t4001\t0\t0\n"
                    : "3001\t1\tAddInventory\t8001\t1\t0\n3001\t2\tJumpUniqueItemHaveTag\t8001\t10\t4\n3001\t3\tAdv\t4001\t0\t0\n3001\t4\tAdv\t4002\t0\t0\n")),
            ("master/adv_flow_master_v1.tsv", "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" +
                (viaAdv
                    ? "4001\t1\tAddInventory\t8001\t1\t0\t\t\n4001\t2\tJumpUniqueItemHaveTag\t8001\t10\t4\t\t\n4001\t3\tTextPlain\t0\t0\t0\t건너뛴 칸\t\n4001\t4\tTextPlain\t0\t0\t0\t도착한 칸\t\n"
                    : "")),
        };
        var s = GameSession.NewGame(GamePack.Load(CoreTests.PackZip(files)));
        if (viaAdv)
            Assert.Equal("도착한 칸", new AdvFlowCircuit(s.Pack, 4001, s.PlayerFlags, s.AddInventory, s.CountInventory, s.CharacterHasTag, s.UniqueItemHasTag).Advance().Text);
        else
            Assert.Equal(4002, s.StartEvent(3001)!.Advance().Code);
    }

    [Fact]
    public void Without_a_session_the_tag_jumps_never_jump()
    {
        var s = Session(true, "JumpCharacterHaveTag", 1, 1);
        Assert.Equal("건너뛴 칸", new AdvFlowCircuit(s.Pack, 4001, s.PlayerFlags).Advance().Text);
    }

    // ---- 로드 검증 ----

    static string Error(bool viaAdv, string type, long code, long tag, long target)
    {
        var f = Files(viaAdv, type, code, tag).ToList();
        string Rows(string kind, long owner) => viaAdv
            ? $"4001\t1\t{type}\t{code}\t{tag}\t{target}\t\t\n4001\t5\tFinish\t0\t0\t0\t\t\n4002\t7\tFinish\t0\t0\t0\t\t\n"
            : $"3001\t1\t{type}\t{code}\t{tag}\t{target}\n3001\t5\tFinish\t0\t0\t0\n3001\t7\tFinish\t0\t0\t0\n";
        var flowFile = viaAdv ? "master/adv_flow_master_v1.tsv" : "master/event_flow_master_v1.tsv";
        var header = viaAdv ? "adv_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\ttext\tforce_name\n" : "event_code\tflow_number\tflow_type\tparam_1\tparam_2\tparam_3\n";
        var i = f.FindIndex(x => x.Item1 == flowFile);
        f[i] = (flowFile, header + Rows(type, 0));
        if (!viaAdv) f.Add(("master/event_master_v1.tsv", "code\tname\n3001\t이벤트\n"));
        try
        {
            GamePack.Load(CoreTests.PackZip(f.Where((x, idx) => idx == f.FindLastIndex(y => y.Item1 == x.Item1)).ToArray()));
            return "";
        }
        catch (PackFormatException ex) { return ex.Message; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_code_must_match_the_kind_the_tag_must_exist_and_the_target_must_be_larger(bool viaAdv)
    {
        Assert.Equal("", Error(viaAdv, "JumpCharacterHaveTag", 7001, 9, 5));
        Assert.Equal("", Error(viaAdv, "JumpUniqueItemHaveTag", 8001, 10, 5));

        Assert.Contains("캐릭터 인벤토리 마스터에 없습니다", Error(viaAdv, "JumpCharacterHaveTag", 8001, 9, 5));    // 유니크 아이템 코드를 줌
        Assert.Contains("캐릭터 인벤토리 마스터에 없습니다", Error(viaAdv, "JumpCharacterHaveTag", 12345, 9, 5));
        Assert.Contains("유니크 아이템 인벤토리 마스터에 없습니다", Error(viaAdv, "JumpUniqueItemHaveTag", 7001, 10, 5));   // 캐릭터 코드를 줌
        Assert.Contains("태그 코드 '77'가 태그 마스터에 없습니다", Error(viaAdv, "JumpCharacterHaveTag", 7001, 77, 5));
        Assert.Contains("태그 코드 '77'가 태그 마스터에 없습니다", Error(viaAdv, "JumpUniqueItemHaveTag", 8001, 77, 5));
        Assert.Contains("보다 커야", Error(viaAdv, "JumpCharacterHaveTag", 7001, 9, 1));
        Assert.Contains("없습니다", Error(viaAdv, "JumpUniqueItemHaveTag", 8001, 10, 9));
    }

    [Fact]
    public void The_enum_numbers_are_fixed()
    {
        Assert.Equal(20, (int)EventFlowType.JumpCharacterHaveTag);
        Assert.Equal(21, (int)EventFlowType.JumpUniqueItemHaveTag);
        Assert.Equal(22, (int)AdvFlowType.JumpCharacterHaveTag);
        Assert.Equal(23, (int)AdvFlowType.JumpUniqueItemHaveTag);
        Assert.True(HaveTagJump.IsConditionalJump("JumpFlagEqual"));
        Assert.True(HaveTagJump.IsConditionalJump("JumpInventoryLessThan"));
        Assert.True(HaveTagJump.IsConditionalJump("JumpCharacterHaveTag"));
        Assert.False(HaveTagJump.IsConditionalJump("Jump"));
        Assert.False(HaveTagJump.IsConditionalJump("Choice"));
    }
}
