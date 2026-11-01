using Wera.Core;

namespace Wera.Core.Tests;

// 구금된 적: 구금된 적 태그(게임 설정 CapturedEnemyTagCode)를 가진 보유 캐릭터. 보유 목록, 캐릭터 확인, 교류(파트너/어시스트)에는 나오지만
// 파티 편성, 아지트(방어 파티, 오퍼레이터), 던전, 이벤트, 전투에는 쓸 수 없다. 용사 7001은 한도 2, 구금 태그는 10(적)과 11(아군).
public class CapturedEnemyTests
{
    static GameSession Session(string settings = "CapturedEnemyTagCode\tLong\t10\t\t\nCapturedAllyTagCode\tLong\t11\t\t\n")
    {
        var pack = GamePack.Load(CoreTests.PackZip(
            ("pack.tsv", "id\tdemo\ntitle\t데모\n"),
            ("master/character_inventory_master_v1.tsv",
                "inventory_code\tcharacter_name\tcarry_limit\tadvantage_attribute_code\tdisadvantage_attribute_code\n1\t당신\t1\t1\t2\n7001\t용사\t2\t1\t2\n"),
            ("master/tag_master_v1.tsv", "tag_code\ttag_name\n1\t플레이어\n2\t리더\n3\t메인 통화\n4\t정비\n10\t구금\n11\t구금\n"),
            ("master/character_tag_master_v1.tsv", "character_code\ttag_code\n1\t1\n7001\t2\n7001\t4\n"),
            ("master/common_item_inventory_master_v1.tsv", "inventory_code\titem_name\tcarry_limit\n2\t금화\t99999\n"),
            ("master/common_item_tag_master_v1.tsv", "item_code\ttag_code\n2\t3\n"),
            ("master/facility_master_v1.tsv", "code\tname\n1\t정문\n2\t대장간\n"),
            ("master/operator_slot_master_v1.tsv", "code\tname\n1\t정비\n"),
            ("master/facility_operator_slot_master_v1.tsv", "facility_code\toperator_slot_code\n2\t1\n"),
            ("master/operator_slot_tag_master_v1.tsv", "operator_slot_code\ttag_code\n1\t4\n"),
            ("master/game_setting_master_v1.tsv", "setting_type\tvalue_type\tlong_value\tdecimal_value\ttext_value\n" + settings)));
        return GameSession.NewGame(pack);
    }

    [Fact]
    public void A_captured_enemy_is_a_new_owned_instance_with_the_captured_enemy_tag_within_the_carry_limit()
    {
        var s = Session();
        var a = s.AddCapturedEnemy(7001)!;
        Assert.True(s.IsCapturedEnemy(a));
        Assert.Contains(10L, s.CharacterTags(a));
        Assert.Contains(s.OwnedCharacters, c => c.InstanceId == a);               // 보유 목록과 캐릭터 확인에 나온다
        Assert.NotNull(s.AddCapturedEnemy(7001));                                // 한도 2
        Assert.Null(s.AddCapturedEnemy(7001));
        Assert.Equal(2, s.OwnedCountOf(7001));
        Assert.Null(Session("").AddCapturedEnemy(7001));                         // 구금 태그가 설정되지 않았다
    }

    [Fact]
    public void A_captured_enemy_cannot_be_placed_in_a_party_or_a_hideout_but_a_free_copy_still_can()
    {
        var s = Session();
        var captured = s.AddCapturedEnemy(7001)!;
        var free = s.AddCharacterInstance(7001)!;
        s.Party[0] = 7001;                                                        // 구금되지 않은 복사본이 서야 한다
        Assert.Equal(free, s.PartyInstance.Units[0]!.InstanceId);
        Assert.False(s.CanOperate(captured, 1));
        Assert.True(s.CanOperate(free, 1));
        s.Activate("105"); s.Activate("1");                                        // 파티 설정, 슬롯 1의 캐릭터 목록
        Assert.DoesNotContain(s.CharacterList, c => c.InstanceId == captured);
        Assert.Contains(s.CharacterList, c => c.InstanceId == free || c.Code == 1);
    }

    [Fact]
    public void A_captured_enemy_can_still_be_the_partner_for_communication_and_survives_the_save()
    {
        var s = Session();
        var captured = s.AddCapturedEnemy(7001)!;
        Assert.True(s.SetSupportInstance(SupportRole.Partner, captured));
        Assert.Equal(captured, s.PartnerId);
        var loaded = GameSession.FromSaveJson(s.Pack, s.ToSaveJson());
        Assert.True(loaded.IsCapturedEnemy(captured));
        Assert.Equal(captured, loaded.PartnerId);
    }

    [Fact]
    public void A_save_with_a_captured_enemy_in_a_party_is_rejected()
    {
        var s = Session();
        var captured = s.AddCapturedEnemy(7001)!;
        s.PartyInstance.Units[0] = new PartyUnit(7001, [], false, captured);
        var json = s.ToSaveJson();
        Assert.Contains("구금된 적", Assert.Throws<SaveFormatException>(() => GameSession.FromSaveJson(s.Pack, json)).Message);
    }
}
