using System.Globalization;

namespace Wera.Core;

/// <summary>던전 맵 한 칸이 어떻게 보이는가: 그 칸 타일의 표시용 효과들(<see cref="TileEffectType.Wall"/>, <see cref="TileEffectType.Stairs"/> 등). 화면이 색을 고르는 데 쓴다.</summary>
[Flags]
public enum DungeonCellLook
{
    None = 0,
    Wall = 1,
    Stairs = 2,
    Exit = 4,
    /// <summary>닿으면 전투가 벌어질 수 있는 칸(FixedBattle).</summary>
    Battle = 8,
    Adv = 16,
    Warp = 32,
    Impassable = 64,
}

/// <summary>던전 화면의 한 칸. Text는 맵 TSV의 글자(맵 밖이면 ""), IsPlayer면 화면은 그 자리에 플레이어 표시를 그린다.</summary>
public readonly record struct DungeonCellView(string Text, DungeonCellLook Look, bool IsPlayer);

public sealed partial class GameSession
{
    /// <summary>던전 선택 화면에서 고른 던전(파티 선택 화면에서 쓴다).</summary>
    long? selectedDungeonCode;

    /// <summary>지금 들어와 있는 던전의 코드. 던전 이동 화면이 아니면 null(세이브에는 담기지 않는다).</summary>
    public long? CurrentDungeonCode { get; private set; }

    /// <summary>지금 서 있는 칸의 맵 번호(layer_number)와 위치.</summary>
    public long DungeonLayer { get; private set; }

    public long DungeonX { get; private set; }

    public long DungeonY { get; private set; }

    /// <summary>방금 닿은 칸의 효과 가운데 아직 실행하지 못한 것들(전투나 ADV가 끝나면 이어서 실행한다).</summary>
    readonly Queue<TileEffect> pendingTileEffects = new();

    /// <summary>던전 파티 선택 화면의 파티 버튼 코드는 [11] 파티-A부터다(파티 설정 화면과 같다).</summary>
    IReadOnlyList<ScreenEntry> DungeonSelectEntries() =>
    [
        .. AvailableDungeons.Select(d => new ScreenEntry(Text(d.Code), d.Name, Screen.DungeonPartySelect, EntryAction.SelectDungeon,
            Enabled: d.Entry is not null)),                  // 맵이 없는 던전(도입 지점이 없음)은 들어갈 수 없다
        new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home),
    ];

    /// <summary>던전에 데려갈 파티 고르기: 파견할 수 있는 파티(리더가 있는 파티)만 나온다. 리더가 없는 등 출격할 수 없는 파티는 표시하지 않는다.</summary>
    IReadOnlyList<ScreenEntry> DungeonPartySelectEntries() =>
    [
        .. Parties.Select((p, i) => (p, i)).Where(x => IsDispatchable(x.i))
            .Select(x => new ScreenEntry((PartySelectCodeBase + x.i).ToString(CultureInfo.InvariantCulture), x.p.Name, Screen.DungeonMove,
                EntryAction.EnterDungeon, x.i.ToString(CultureInfo.InvariantCulture))),
        new ScreenEntry(BackEntryCode, "돌아가기", Screen.DungeonSelect),
    ];

    /// <summary>던전 이동 화면에는 목록이 없다: W A S D(또는 방향키)로 움직이고 Enter로 지금 서 있는 칸과 상호작용한다(<see cref="PressDungeonKey"/>, <see cref="InteractDungeon"/>).</summary>
    IReadOnlyList<ScreenEntry> DungeonMoveEntries() => [];

    /// <summary>고른 파티로 던전에 들어간다: 던전 마스터의 도입 지점에 선다(도입 지점 칸의 효과는 실행하지 않는다).</summary>
    void EnterDungeon(int partyIndex)
    {
        if (selectedDungeonCode is not { } code || Pack.Dungeons?.FirstOrDefault(d => d.Code == code) is not { Entry: { } entry }) return;
        CurrentPartyIndex = partyIndex;
        CurrentDungeonCode = code;
        (DungeonLayer, DungeonX, DungeonY) = (entry.LayerNumber, entry.PosX, entry.PosY);
        pendingTileEffects.Clear();
        encounterPending = false;
        Screen = Screen.DungeonMove;
    }

    /// <summary>던전에서 나가 홈 화면으로 돌아간다.</summary>
    void LeaveDungeon()
    {
        CurrentDungeonCode = null;
        selectedDungeonCode = null;
        pendingTileEffects.Clear();
        encounterPending = false;
        Screen = Screen.Home;
    }

    /// <summary>던전 전투에서 졌다: 던전에 들어온 파티에 주인공(캐릭터 1)이 있으면 게임 오버, 없으면 홈으로 돌아온다. (도망은 이동 화면으로 돌아와 계속한다.)</summary>
    void EndDungeonByDefeat()
    {
        var leaderLost = Parties[CurrentPartyIndex].IndexOf(GamePack.PlayerCharacterCode) >= 0;
        LeaveDungeon();
        if (!leaderLost) return;
        gameOver = true;
        Screen = Screen.GameOver;
    }

    /// <summary>게임 오버가 났다: 이 세션은 로드 화면에서 세이브를 불러오는 것밖에 할 수 없다(돌아가기 없음).</summary>
    bool gameOver;

    static (int dx, int dy) Direction(char dir) => dir switch { 'U' => (0, -1), 'D' => (0, 1), 'L' => (-1, 0), _ => (1, 0) };

    /// <summary>지금 던전 맵의 (x, y) 칸의 글자. 맵 밖(범위 밖이거나 빈 칸)이면 null.</summary>
    string? DungeonCellText(long layer, long x, long y)
    {
        if (CurrentDungeonCode is not { } code || x < 0 || x >= GamePack.DungeonMapWidth) return null;
        var line = Pack.DungeonMapOf(code, layer).FirstOrDefault(l => l.PosY == y);
        return line is null || line.Cells[(int)x] == "" ? null : line.Cells[(int)x];
    }

    IReadOnlyList<TileEffect> DungeonCellEffects(string text) =>
        Pack.DungeonTileCodeOf(CurrentDungeonCode!.Value, text) is { } tile ? Pack.TileEffectsOf(tile) : [];

    /// <summary>그 방향으로 한 칸 갈 수 있는가: 맵 안의 칸이고 그 칸의 타일에 이동불가(<see cref="TileEffectType.Impassable"/>)가 없어야 한다.</summary>
    bool CanStep(char dir)
    {
        if (CurrentDungeonCode is null) return false;
        var (dx, dy) = Direction(dir);
        return DungeonCellText(DungeonLayer, DungeonX + dx, DungeonY + dy) is { } text
            && DungeonCellEffects(text).All(e => e.EffectType != TileEffectType.Impassable);
    }

    /// <summary>한 칸 움직이고 닿은 칸의 효과를 실행한다. 갈 수 없으면 아무 일도 없다.</summary>
    void StepDungeon(char dir)
    {
        if (!CanStep(dir)) return;
        var (dx, dy) = Direction(dir);
        (DungeonX, DungeonY) = (DungeonX + dx, DungeonY + dy);
        pendingTileEffects.Clear();
        var effects = DungeonCellEffects(DungeonCellText(DungeonLayer, DungeonX, DungeonY)!);
        if (!effects.Any(e => e.EffectType == TileEffectType.Interact))      // Interact 칸은 닿아도 발동하지 않고 Enter를 눌러야 한다
            foreach (var effect in effects.OrderBy(e => (int)e.EffectType))
                pendingTileEffects.Enqueue(effect);
        encounterPending = true;
        ContinueTileEffects();
    }

    /// <summary>방금 한 걸음의 인카운트 판정이 남았다(칸의 효과를 모두 실행한 뒤에 한 번만 한다).</summary>
    bool encounterPending;

    /// <summary>
    /// 던전 이동 키 입력: W 위, S 아래, A 왼쪽, D 오른쪽(대소문자 무시, 방향키도 된다). 던전 이동 화면이 아니거나 다른 키면 아무 일도 없다.
    /// </summary>
    public void PressDungeonKey(char key)
    {
        if (Screen != Screen.DungeonMove) return;
        Message = null;
        switch (char.ToLowerInvariant(key))
        {
            case 'w': StepDungeon('U'); break;
            case 's': StepDungeon('D'); break;
            case 'a': StepDungeon('L'); break;
            case 'd': StepDungeon('R'); break;
        }
    }

    /// <summary>
    /// 지금 서 있는 칸에서 Enter로 할 수 있는 상호작용이 있는가: 출구(Exit) 칸이거나, 상호작용(Interact) 칸에 전투, ADV, 워프 효과가 있을 때.
    /// </summary>
    public bool CanInteractDungeon =>
        Screen == Screen.DungeonMove && DungeonCellText(DungeonLayer, DungeonX, DungeonY) is { } text && InteractEffects(DungeonCellEffects(text)).Any();

    /// <summary>Enter로 발동하는 효과들(효과 번호 순): 출구, 그리고 Interact 칸의 전투, ADV, 워프.</summary>
    static IEnumerable<TileEffect> InteractEffects(IReadOnlyList<TileEffect> effects) =>
        effects.Where(e => e.EffectType == TileEffectType.Exit
                || effects.Any(i => i.EffectType == TileEffectType.Interact)
                && e.EffectType is TileEffectType.FixedBattle or TileEffectType.Adv or TileEffectType.Warp)
            .OrderBy(e => (int)e.EffectType);

    /// <summary>
    /// 지금 서 있는 칸의 상호작용(Enter): 출구 칸이면 던전에서 나간다(던전에서 나가는 곳은 출구뿐이다). Interact 칸이면 겹친 전투, ADV, 워프 효과를 실행한다.
    /// 할 일이 없으면 아무 일도 없다. 인카운트 판정은 하지 않는다(걸음이 아니므로).
    /// </summary>
    public void InteractDungeon()
    {
        if (!CanInteractDungeon) return;
        Message = null;
        var effects = InteractEffects(DungeonCellEffects(DungeonCellText(DungeonLayer, DungeonX, DungeonY)!)).ToList();
        if (effects.Any(e => e.EffectType == TileEffectType.Exit))
        {
            LeaveDungeon();
            return;
        }
        pendingTileEffects.Clear();
        foreach (var effect in effects) pendingTileEffects.Enqueue(effect);
        encounterPending = false;
        ContinueTileEffects();
    }


    /// <summary>
    /// 닿은 칸의 효과들을 효과 번호 순서대로 하나씩 실행한다. Exit는 던전에서 나가고, FixedBattle과 RandomBattle(확률에 맞으면)은 전투 화면으로,
    /// Adv는 ADV 재생 화면으로 가며 그것이 끝나면 남은 효과를 이어서 실행한다. Warp는 같은 던전의 도착 칸으로 옮겨 서며 도착 칸의 효과는 실행하지 않는다(왕복 계단이 무한히 돌지 않게).
    /// 표시용(Wall, Stairs)이나 이동불가는 닿았을 때 하는 일이 없다.
    /// </summary>
    void ContinueTileEffects()
    {
        while (pendingTileEffects.Count > 0)
        {
            var effect = pendingTileEffects.Dequeue();
            switch (effect.EffectType)
            {
                case TileEffectType.FixedBattle:
                    StartDungeonBattle(effect.Value1);
                    return;
                case TileEffectType.Adv:
                    StartDungeonAdv(effect.Value1);
                    return;
                case TileEffectType.Warp:
                    (DungeonLayer, DungeonX, DungeonY) = (effect.Value1, effect.Value2, effect.Value3);
                    break;
            }
        }
        if (!encounterPending) return;
        encounterPending = false;
        CheckEncounter();
    }

    /// <summary>
    /// 인카운트 판정: 던전 마스터의 인카운트률(%)로 0~99의 난수가 그보다 작으면 전투가 벌어지고, 지금 맵의 던전 인카운트 마스터에서 가중치로 전투를 하나 고른다.
    /// 지금 맵에 설정된 전투가 없으면 인카운트하지 않는다(난수도 뽑지 않는다).
    /// </summary>
    void CheckEncounter()
    {
        if (CurrentDungeonCode is not { } code || Pack.Dungeons?.FirstOrDefault(d => d.Code == code) is not { EncounterRate: > 0 } dungeon) return;
        var candidates = Pack.DungeonEncountersOf(code, DungeonLayer);
        if (candidates.Count == 0 || !RandomJump.Hit(dungeon.EncounterRate, RandomJump.Roll(100, NextRandom))) return;
        var roll = NextRandom(candidates.Sum(c => c.Weight));
        foreach (var candidate in candidates)
        {
            if (roll < candidate.Weight) { StartDungeonBattle(candidate.BattleCode); return; }
            roll -= candidate.Weight;
        }
    }

    /// <summary>던전의 전투: 던전에 들어온 파티가 싸우고, 끝나면 [999]가 던전 이동 화면으로 돌아와 남은 효과를 이어 간다.</summary>
    void StartDungeonBattle(long battleCode)
    {
        encounterPending = false;                      // 이 걸음은 이미 전투를 하므로 또 인카운트 판정을 하지 않는다
        battleReturnScreen = Screen.DungeonMove;
        battlePartyBefore = null;
        battleResultFlag = 0;
        BeginBattle(battleCode);
        Screen = Screen.Battle;
    }

    /// <summary>던전의 ADV: 이벤트 없이 ADV 하나를 재생하고, 끝나면 던전 이동 화면으로 돌아와 남은 효과를 이어 간다.</summary>
    void StartDungeonAdv(long advCode)
    {
        encounterPending = false;                      // ADV를 연 걸음에는 인카운트 판정을 하지 않는다
        CurrentEvent = null;
        CurrentCommunicationFlow = null;
        CurrentStep = new EventStep(EventStepKind.Adv, 0, advCode);
        CurrentAdv = new AdvFlowCircuit(Pack, advCode, PlayerFlags, AddInventory, CountInventory, CharacterHasTag, UniqueItemHasTag, NextRandom);
        CurrentAdvStep = null;
        advLog.Clear();
        Screen = Screen.EventPlay;
        ShowNextAdvStep();
    }

    /// <summary>ADV(던전에서 연 것)가 끝나 던전 이동 화면으로 돌아오고 남은 효과를 이어 실행한다.</summary>
    void ReturnToDungeonAfterAdv()
    {
        CurrentAdv = null;
        CurrentStep = null;
        CurrentAdvStep = null;
        advLog.Clear();
        Screen = Screen.DungeonMove;
        ContinueTileEffects();
    }

    /// <summary>던전 이동 화면: 지금 던전의 지금 맵(<see cref="DungeonLayer"/>)을 위에서부터 줄마다 그린 것. 오른쪽 끝의 빈 칸 열은 잘라 낸다. 던전에 있지 않으면 빈 목록.</summary>
    public IReadOnlyList<IReadOnlyList<DungeonCellView>> DungeonMapRows
    {
        get
        {
            if (CurrentDungeonCode is not { } code) return [];
            var lines = Pack.DungeonMapOf(code, DungeonLayer);
            var width = lines.Count == 0 ? 0 : lines.Max(l => l.Cells.ToList().FindLastIndex(c => c != "") + 1);
            var rows = new List<IReadOnlyList<DungeonCellView>>();
            for (var y = 0L; y <= (lines.Count == 0 ? -1 : lines.Max(l => l.PosY)); y++)
            {
                var line = lines.FirstOrDefault(l => l.PosY == y);
                rows.Add(Enumerable.Range(0, width).Select(x =>
                {
                    var text = line is null ? "" : line.Cells[x];
                    return new DungeonCellView(text, text == "" ? DungeonCellLook.None : LookOf(text), x == DungeonX && y == DungeonY);
                }).ToList());
            }
            return rows;
        }
    }

    DungeonCellLook LookOf(string text)
    {
        var look = DungeonCellLook.None;
        foreach (var e in DungeonCellEffects(text))
            look |= e.EffectType switch
            {
                TileEffectType.Wall => DungeonCellLook.Wall,
                TileEffectType.Stairs => DungeonCellLook.Stairs,
                TileEffectType.Exit => DungeonCellLook.Exit,
                TileEffectType.FixedBattle => DungeonCellLook.Battle,
                TileEffectType.Adv => DungeonCellLook.Adv,
                TileEffectType.Warp => DungeonCellLook.Warp,
                TileEffectType.Impassable => DungeonCellLook.Impassable,
                _ => DungeonCellLook.None,
            };
        return look;
    }

    /// <summary>던전 이동 화면 위에 보여 줄 던전 이름과 맵 번호. 던전에 있지 않으면 null.</summary>
    public string? DungeonTitle => CurrentDungeonCode is { } code
        ? $"{Pack.Dungeons?.FirstOrDefault(d => d.Code == code)?.Name ?? Text(code)} (맵 {DungeonLayer})" : null;
}
