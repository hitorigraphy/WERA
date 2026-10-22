using Wera.Core;

namespace Wera.Core.Tests;

public class PartyTests
{
    static GamePack Pack() => GamePack.Load(CoreTests.Zip(
        ("pack.tsv", "id\tdemo\ntitle\t데모\nstart\ta\n"),
        ("scenes.tsv", "id\ttext\na\tx\n"),
        ("choices.tsv", "scene\tlabel\tnext\n")));

    [Fact]
    public void Home_always_shows_party_setup_103_even_without_any_master()
    {
        var s = GameSession.NewGame(Pack());
        Assert.Contains(new ScreenEntry("103", "파티 설정", Screen.PartySetup), s.Entries);
    }

    [Fact]
    public void Party_setup_opens_from_103_and_999_goes_back_home()
    {
        var s = GameSession.NewGame(Pack());
        Assert.NotNull(s.Activate("103"));
        Assert.Equal(Screen.PartySetup, s.Screen);
        Assert.Equal(["999"], s.Entries.Select(e => e.Code));
        Assert.Null(s.Activate("103"));                 // 파티 설정 화면에는 103 항목이 없다

        Assert.NotNull(s.Activate("999"));
        Assert.Equal(Screen.Home, s.Screen);
    }

    [Fact]
    public void Save_taken_on_party_setup_loads_into_home()
    {
        var pack = Pack();
        var s = GameSession.NewGame(pack);
        s.Activate("103");
        Assert.Contains("\"PartySetup\"", s.ToSaveJson());
        Assert.Equal(Screen.Home, GameSession.FromSaveJson(pack, s.ToSaveJson()).Screen);
    }
}
