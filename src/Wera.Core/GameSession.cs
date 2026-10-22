using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wera.Core;

[JsonConverter(typeof(JsonStringEnumConverter<Screen>))]
public enum Screen
{
    /// <summary>scenes.tsv 기반 장면 진행.</summary>
    Scene,
    Home,
    DungeonSelect,
}

/// <summary>화면에 나오는 클릭 가능한 텍스트. 클릭하거나 Code를 입력하면 Target 화면으로 이동한다.</summary>
public sealed record ScreenEntry(string Code, string Label, Screen Target);

/// <param name="Version">1: 장면만 저장. 2: 화면과 플래그 추가.</param>
public sealed record SaveData(
    int Version,
    string PackId,
    string SceneId,
    Screen Screen = Screen.Scene,
    Dictionary<string, int>? Flags = null);

[JsonSerializable(typeof(SaveData))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class SaveJsonContext : JsonSerializerContext;

public sealed class SaveFormatException(string message) : Exception(message);

public sealed class GameSession
{
    public const int SaveVersion = 2;

    public const string DungeonEntryCode = "101";
    public const string BackEntryCode = "0";

    public GamePack Pack { get; }
    public Scene Current { get; private set; }
    public Screen Screen { get; private set; }
    public Dictionary<string, int> Flags { get; }

    GameSession(GamePack pack, Scene scene, Screen screen, Dictionary<string, int> flags)
    {
        Pack = pack;
        Current = scene;
        Screen = screen;
        Flags = flags;
    }

    /// <summary>튜토리얼이 구현될 때까지 새 게임은 홈 화면으로 직행한다.</summary>
    public static GameSession NewGame(GamePack pack) =>
        new(pack, pack.Scenes[pack.StartSceneId], Screen.Home, []);

    /// <summary>장면 화면으로 들어간다.</summary>
    public void EnterScene(string sceneId)
    {
        Current = Pack.Scenes.TryGetValue(sceneId, out var scene)
            ? scene
            : throw new ArgumentException($"알 수 없는 장면: {sceneId}", nameof(sceneId));
        Screen = Screen.Scene;
    }

    /// <summary>입장 조건을 하나라도 만족하는 던전.</summary>
    public IReadOnlyList<Dungeon> AvailableDungeons =>
        Pack.Dungeons?.Where(d => d.IsAvailable(Flags)).ToList() ?? [];

    /// <summary>현재 화면에서 클릭하거나 코드를 입력할 수 있는 텍스트들.</summary>
    public IReadOnlyList<ScreenEntry> Entries
    {
        get
        {
            switch (Screen)
            {
                case Screen.Home:
                    var list = new List<ScreenEntry>();
                    if (AvailableDungeons.Count > 0)
                        list.Add(new ScreenEntry(DungeonEntryCode, "던전", Screen.DungeonSelect));
                    return list;
                case Screen.DungeonSelect:
                    return [new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home)];
                default:
                    return [];
            }
        }
    }

    /// <summary>코드(공백 무시)에 해당하는 항목으로 이동한다. 없으면 false.</summary>
    public bool Activate(string code)
    {
        code = code.Trim();
        var entry = Entries.FirstOrDefault(e => e.Code == code);
        if (entry is null) return false;
        Screen = entry.Target;
        return true;
    }

    public void Choose(int index)
    {
        if (Screen != Screen.Scene) throw new InvalidOperationException("장면 화면이 아닙니다.");
        if (index < 0 || index >= Current.Choices.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        Current = Pack.Scenes[Current.Choices[index].Next];
    }

    public string ToSaveJson() =>
        JsonSerializer.Serialize(
            new SaveData(SaveVersion, Pack.Id, Current.Id, Screen, new Dictionary<string, int>(Flags)),
            SaveJsonContext.Default.SaveData);

    public static GameSession FromSaveJson(GamePack pack, string json)
    {
        SaveData? save;
        try { save = JsonSerializer.Deserialize(json, SaveJsonContext.Default.SaveData); }
        catch (JsonException) { throw new SaveFormatException("세이브 파일이 올바른 JSON이 아닙니다."); }

        if (save is null) throw new SaveFormatException("세이브 파일이 비어 있습니다.");
        if (save.Version is < 1 or > SaveVersion) throw new SaveFormatException($"지원하지 않는 세이브 버전입니다: {save.Version}");
        if (save.PackId != pack.Id) throw new SaveFormatException($"다른 게임팩의 세이브입니다: {save.PackId}");
        if (!pack.Scenes.TryGetValue(save.SceneId ?? "", out var scene))
            throw new SaveFormatException($"세이브의 장면 '{save.SceneId}'를 게임팩에서 찾을 수 없습니다.");
        // 불러오기는 저장된 화면과 상관없이 항상 홈으로 들어간다.
        return new GameSession(pack, scene, Screen.Home, save.Flags is null ? [] : new Dictionary<string, int>(save.Flags));
    }
}
