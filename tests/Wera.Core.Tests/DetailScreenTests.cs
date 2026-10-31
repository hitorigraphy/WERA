using Wera.Core;

namespace Wera.Core.Tests;

// 상세 화면(캐릭터, 장비, 아이템)은 스테이터스 확인 슬롯과 동료/장비/아이템 확인의 줄에서 들어간다. 내용은 아직 정의되지 않았다.
public class DetailScreenTests
{
    static GameSession SampleSession()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WERA.slnx"))) root = Path.GetDirectoryName(root)!;
        using var read = File.OpenRead(Path.Combine(root, "src", "Wera.Web", "wwwroot", "sample-pack.wra"));
        return GameSession.NewGame(GamePack.Load(read));
    }

    [Fact]
    public void A_companion_row_opens_the_character_detail_and_back_returns_to_the_list()
    {
        var s = SampleSession();
        s.Activate("201");
        var rows = s.Entries.Where(e => e.Code != "999").ToList();
        Assert.Equal(s.Companions.Count, rows.Count);
        Assert.All(rows, r => Assert.Equal(EntryAction.OpenDetail, r.Action));
        Assert.Equal(Screen.CharacterDetail, s.Activate(rows[0].Code)!.Target);
        Assert.NotNull(s.DetailCharacterCode);
        Assert.Equal("캐릭터 상세", s.DetailHeading!.Value.Title);
        Assert.Equal(Screen.CompanionCheck, s.Activate("999")!.Target);
    }

    [Fact]
    public void An_equipment_row_opens_the_equipment_detail_and_an_item_row_opens_the_item_detail()
    {
        var s = SampleSession();
        var unique = s.Pack.Inventory.Values.First(i => i.Kind == InventoryKind.UniqueItem).Code;
        var common = s.Pack.ItemInventory!.First().Code;
        s.AddInventory(unique, 1);
        s.AddInventory(common, 1);

        s.Activate("202");
        var equipment = s.Entries.First(e => e.Code != "999");
        Assert.Equal(Screen.EquipmentDetail, s.Activate(equipment.Code)!.Target);
        Assert.NotNull(s.DetailEquipmentId);
        Assert.Equal("장비 상세", s.DetailHeading!.Value.Title);
        Assert.Equal(Screen.EquipmentCheck, s.Activate("999")!.Target);
        s.Activate("999");

        s.Activate("203");
        Assert.Equal(Screen.ItemDetail, s.Activate(common.ToString())!.Target);
        Assert.Equal(common, s.DetailItemCode);
        Assert.Equal("아이템 상세", s.DetailHeading!.Value.Title);
        Assert.Equal(Screen.ItemCheck, s.Activate("999")!.Target);
        Assert.Equal(Screen.Home, s.Activate("999")!.Target);
    }

    [Fact]
    public void The_status_check_shows_the_party_slots_and_only_occupied_slots_open_the_character_detail()
    {
        var s = SampleSession();
        s.Activate("105"); s.Activate("1"); s.Activate("1"); s.Activate("999");     // 파티-A의 1번 슬롯에 첫 캐릭터
        s.Activate("101"); s.Activate("1999"); s.Activate("11");                     // 던전에 들어가
        Assert.Equal(Screen.StatusCheck, s.Activate("201")!.Target);                 // 오른쪽 패널의 스테이터스 확인
        Assert.Equal(Enumerable.Range(1, 9).Select(i => i.ToString()).Append("999"), s.Entries.Select(e => e.Code));
        Assert.True(s.Entries.First(e => e.Code == "1").Enabled);
        Assert.False(s.Entries.First(e => e.Code == "9").Enabled);
        s.Activate("9");                                                 // 빈 슬롯은 눌러도 그대로
        Assert.Equal(Screen.StatusCheck, s.Screen);
        Assert.Equal(Screen.CharacterDetail, s.Activate("1")!.Target);
        Assert.Equal(Screen.StatusCheck, s.Activate("999")!.Target);
        Assert.Equal(Screen.DungeonMove, s.Activate("999")!.Target);                 // 던전에서 연 화면은 던전으로
    }

    [Fact]
    public void The_character_detail_shows_name_level_bars_equipment_and_skill_slots_and_the_description()
    {
        var s = SampleSession();
        s.Activate("201");
        s.Activate(s.Entries.First(e => e.Code != "999").Code);
        var d = s.CharacterDetail!;
        Assert.Equal(s.DetailHeading!.Value.Name, d.Name);
        Assert.StartsWith("Lv.", d.LevelText);
        Assert.Equal(GameSession.CharacterDetailExpWidth, d.Exp.Text.Length);             // Exp, HP, XP 바는 모두 같은 길이
        Assert.Equal(GameSession.CharacterDetailExpWidth, d.Hp.Text.Length);
        Assert.Equal(GameSession.CharacterDetailExpWidth, d.Xp.Text.Length);
        Assert.Equal("이야기의 주인공. 아직 아무것도 알려지지 않았다.", d.Description);        // 샘플 팩의 캐릭터 1
        Assert.All(d.Equipment.Concat(d.Skills), l => Assert.NotEqual("", l.Value));
        s.Activate("999");
        Assert.Null(s.CharacterDetail);
    }

    [Fact]
    public void The_exp_bar_shows_the_exp_status_not_the_xpp()
    {
        var s = SampleSession();
        var player = GamePack.PlayerCharacterCode;
        s.Characters[player] = s.Characters[player].WithStatus(GamePack.ExpStatusCode, 30).WithStatus(GamePack.ExpMaxStatusCode, 60).WithStatus(GamePack.XppStatusCode, 5);
        s.Activate("201");
        s.Activate(s.Entries.First(e => e.Code != "999").Code);
        Assert.Equal(Gauge.Render(30, 60, gray: false, GameSession.CharacterDetailExpWidth), s.CharacterDetail!.Exp);
        Assert.Equal((6L, 7L), (GamePack.ExpStatusCode, GamePack.ExpMaxStatusCode));
        s.Characters[player] = s.Characters[player].WithStatus(GamePack.ExpStatusCode, 5).WithStatus(GamePack.HpStatusCode, 0);   // 사망이어도, 25% 미만이어도
        Assert.Equal(GaugeColor.White, s.CharacterDetail!.Exp.Color);
        Assert.Equal(100, SampleSession().Characters[player].ExpMax);               // 샘플 팩: 새 캐릭터의 필요 경험치 100
    }
}
