using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wera.Core;

public sealed record SaveData(int Version, string PackId, string SceneId);

[JsonSerializable(typeof(SaveData))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class SaveJsonContext : JsonSerializerContext;

public sealed class SaveFormatException(string message) : Exception(message);

public sealed class GameSession
{
    public const int SaveVersion = 1;

    public GamePack Pack { get; }
    public Scene Current { get; private set; }

    GameSession(GamePack pack, Scene scene) { Pack = pack; Current = scene; }

    public static GameSession NewGame(GamePack pack) => new(pack, pack.Scenes[pack.StartSceneId]);

    public void Choose(int index)
    {
        if (index < 0 || index >= Current.Choices.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        Current = Pack.Scenes[Current.Choices[index].Next];
    }

    public string ToSaveJson() =>
        JsonSerializer.Serialize(new SaveData(SaveVersion, Pack.Id, Current.Id), SaveJsonContext.Default.SaveData);

    public static GameSession FromSaveJson(GamePack pack, string json)
    {
        SaveData? save;
        try { save = JsonSerializer.Deserialize(json, SaveJsonContext.Default.SaveData); }
        catch (JsonException) { throw new SaveFormatException("세이브 파일이 올바른 JSON이 아닙니다."); }

        if (save is null) throw new SaveFormatException("세이브 파일이 비어 있습니다.");
        if (save.Version != SaveVersion) throw new SaveFormatException($"지원하지 않는 세이브 버전입니다: {save.Version}");
        if (save.PackId != pack.Id) throw new SaveFormatException($"다른 게임팩의 세이브입니다: {save.PackId}");
        if (!pack.Scenes.TryGetValue(save.SceneId ?? "", out var scene))
            throw new SaveFormatException($"세이브의 장면 '{save.SceneId}'를 게임팩에서 찾을 수 없습니다.");
        return new GameSession(pack, scene);
    }
}
