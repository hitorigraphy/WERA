using System.Globalization;
using System.IO.Compression;

namespace Wera.Core;

/// <summary>
/// 이벤트 흐름 한 칸이 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다.
/// 숫자 값은 정해진 번호다: None 0, Finish 1, Jump 2, Adv 3, Battle 4, SetFlag 5, ModFlag 6, AddInventory 7, JumpFlagEqual 8, JumpFlagNotEqual 9, JumpFlagGreaterThan 10, JumpFlagGreaterOrEqual 11, JumpFlagLessThan 12, JumpFlagLessOrEqual 13, JumpInventoryEqual 14, JumpInventoryNotEqual 15, JumpInventoryGreaterThan 16, JumpInventoryGreaterOrEqual 17, JumpInventoryLessThan 18, JumpInventoryLessOrEqual 19, JumpCharacterHaveTag 20, JumpUniqueItemHaveTag 21, JumpRandom 22, GameOver 23.
/// </summary>
public enum EventFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
    /// <summary>이벤트를 끝낸다.</summary>
    Finish = 1,
    /// <summary>
    /// 조건 없이 뒤쪽의 흐름 칸으로 점프한다. param_1이 점프할 flow_number이고, 같은 이벤트에 있어야 하며 자신의 flow_number보다 커야 한다
    /// (뒤로만 점프하므로 무한 반복이 생기지 않는다). 플래그를 견주어 점프하려면 JumpFlag*를 쓴다.
    /// </summary>
    Jump = 2,
    /// <summary>ADV를 재생한다. param_1이 재생할 adv_code(ADV 마스터)다.</summary>
    Adv = 3,
    /// <summary>
    /// 전투를 개시한다. param_1이 개시할 battle_code(전투 마스터)다. 전투에 나가는 파티는 플레이어(캐릭터 1, 당신)가 있는 파티로 고정이고,
    /// 승리, 패배, 도망 어느 쪽이든 끝나면 이 이벤트의 다음 칸부터 이어 간다. param_2가 0이 아니면 그 플래그(플레이어 플래그 코드)에 전투 결과를 넣는다:
    /// 승리 1, 도망 2, 패배 3(JumpFlag*로 분기한다. 0이면 결과를 남기지 않는다). 도망은 그 전투가 도망 가능(전투 마스터의 can_run)일 때만 나온다. 인자 3은 아직 쓰이지 않는다.
    /// </summary>
    Battle = 4,
    /// <summary>플레이어 플래그를 정한 값으로 바꾼다. param_1이 flag_code, param_2가 value다. 화면에는 아무것도 나오지 않고 바로 다음 칸으로 간다.</summary>
    SetFlag = 5,
    /// <summary>플레이어 플래그에 값을 더한다(음수면 뺀다). param_1이 flag_code, param_2가 더할 value다. 없던 플래그는 0에서 시작하고, 값이 long 범위를 넘으면 끝값에 멈춘다.</summary>
    ModFlag = 6,
    /// <summary>
    /// 인벤토리에 개수를 더한다(음수면 뺀다). param_1이 inventory_code(커먼, 번치, 유니크 아이템, 캐릭터가 함께 쓰는 코드), param_2가 개수다.
    /// 유니크 아이템과 캐릭터는 개수가 1 또는 -1이어야 한다(로드 검증). 화면에는 나오지 않고 바로 다음 칸으로 간다. 자세한 규칙은 <see cref="GameSession.AddInventory"/>.
    /// </summary>
    AddInventory = 7,
    /// <summary>플래그 값이 flag_value와 Equal 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagEqual = 8,
    /// <summary>플래그 값이 flag_value와 NotEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagNotEqual = 9,
    /// <summary>플래그 값이 flag_value와 GreaterThan 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagGreaterThan = 10,
    /// <summary>플래그 값이 flag_value와 GreaterOrEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagGreaterOrEqual = 11,
    /// <summary>플래그 값이 flag_value와 LessThan 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagLessThan = 12,
    /// <summary>플래그 값이 flag_value와 LessOrEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagLessOrEqual = 13,
    /// <summary>인벤토리 보유 개수가 value와 Equal 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryEqual = 14,
    /// <summary>인벤토리 보유 개수가 value와 NotEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryNotEqual = 15,
    /// <summary>인벤토리 보유 개수가 value와 GreaterThan 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryGreaterThan = 16,
    /// <summary>인벤토리 보유 개수가 value와 GreaterOrEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryGreaterOrEqual = 17,
    /// <summary>인벤토리 보유 개수가 value와 LessThan 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryLessThan = 18,
    /// <summary>인벤토리 보유 개수가 value와 LessOrEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryLessOrEqual = 19,
    /// <summary>
    /// 캐릭터가 태그를 가지고 있으면 target_flow_number로 점프하고 아니면 다음 칸으로 간다. param_1이 character_code(캐릭터 인벤토리 코드), param_2가 tag_code(태그 마스터),
    /// param_3이 target_flow_number다. 보유하지 않은 캐릭터는 태그가 없는 것으로 본다. 자세한 규칙은 <see cref="HaveTagJump"/>.
    /// </summary>
    JumpCharacterHaveTag = 20,
    /// <summary>
    /// 유니크 아이템이 태그를 가지고 있으면 target_flow_number로 점프하고 아니면 다음 칸으로 간다. param_1이 item_code(유니크 아이템 인벤토리 코드), param_2가 tag_code(태그 마스터),
    /// param_3이 target_flow_number다. 그 코드의 보유 인스턴스 중 하나라도 태그를 가지고 있으면 참이고, 보유하지 않으면 거짓이다. 자세한 규칙은 <see cref="HaveTagJump"/>.
    /// </summary>
    JumpUniqueItemHaveTag = 21,
    /// <summary>
    /// 확률로 점프한다. param_1이 확률(chance), param_2가 모수(bound), param_3이 target_flow_number다:
    /// 0 이상 bound 미만의 무작위 정수 r을 뽑아 r &lt; chance면 target_flow_number로 점프하고 아니면 다음 칸으로 간다
    /// (예: 30, 100, 5 → rand(100) &lt; 30 ? goto 5 : goto next). 모수는 1 이상이어야 한다(로드 검증). 자세한 규칙은 <see cref="RandomJump"/>.
    /// </summary>
    JumpRandom = 22,
    /// <summary>게임 오버: 이벤트를 끝내고 게임 오버 화면으로 간다(그 뒤는 로드 화면). 인자는 없다.</summary>
    GameOver = 23,
}

/// <summary>이벤트 마스터의 한 행. 이벤트 조건 마스터의 그룹 규칙(그룹 안은 AND, 그룹끼리는 OR)으로 열리고, 조건이 없으면 늘 열린다.</summary>
/// <param name="Groups">그룹넘버 순으로 정렬된 조건 묶음.</param>
public sealed record GameEvent(long Code, string Name, IReadOnlyList<IReadOnlyList<FlagCondition>> Groups)
{
    public bool IsAvailable(IReadOnlyDictionary<long, long> playerFlags) => ConditionGroups.IsMet(Groups, playerFlags);
}

/// <summary>
/// 이벤트 흐름 마스터의 한 행. 이벤트(EventCode)의 흐름 번호(FlowNumber) 칸이 하는 일(FlowType)과 그 인자. 키는 (EventCode, FlowNumber)다.
/// Param1은 종류에 따라 점프할 흐름 번호(Jump), 플래그 코드(JumpFlag*, SetFlag, ModFlag), 인벤토리 코드(JumpInventory*), 캐릭터/유니크 아이템 코드(JumpCharacterHaveTag, JumpUniqueItemHaveTag), 재생할 adv 코드(Adv), 개시할 전투 코드(Battle), 인벤토리 코드(AddInventory)로 쓰이고,
/// Param2는 JumpFlag*, JumpInventory*, SetFlag, ModFlag, AddInventory의 값(JumpCharacterHaveTag와 JumpUniqueItemHaveTag는 태그 코드)이며, Param3은 점프하는 종류(JumpFlag*, JumpInventory*, 태그 점프, JumpRandom)의 점프할 흐름 번호다(JumpRandom의 Param1은 확률, Param2는 모수). 칸에는 조건이 없다(조건은 JumpFlag*로 건너뛰어 낸다).
/// </summary>
public sealed record EventFlow(
    long EventCode, long FlowNumber, EventFlowType FlowType, long Param1, long Param2, long Param3);

/// <summary>ADV 마스터의 한 행.</summary>
public sealed record AdvEntry(long Code, string Name);

/// <summary>전투 마스터의 한 행.</summary>
/// <param name="CanRun">도망(전투 화면의 [106])을 칠 수 있는가. 전투 마스터의 can_run 열(없거나 비우면 true).</param>
public sealed record BattleEntry(long Code, string Name, bool CanRun = true);

// 이벤트, 이벤트 조건, 이벤트 흐름, ADV, 전투 마스터. 이 마스터들은 선택이고, 아직 게임 진행에 쓰이지 않는다.
public sealed partial class GamePack
{
    public const string EventMasterPath = "master/event_master_v1.tsv";
    public const string EventConditionMasterPath = "master/event_condition_master_v1.tsv";
    public const string EventFlowMasterPath = "master/event_flow_master_v1.tsv";
    public const string EventFlowConditionMasterPath = "master/event_flow_condition_master_v1.tsv";
    public const string AdvMasterPath = "master/adv_master_v1.tsv";
    public const string BattleMasterPath = "master/battle_master_v1.tsv";

    /// <summary>이벤트 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<GameEvent>? Events { get; init; }

    /// <summary>이벤트 흐름 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<EventFlow>? EventFlows { get; init; }

    /// <summary>ADV 마스터가 없으면 null.</summary>
    public IReadOnlyList<AdvEntry>? Advs { get; init; }

    /// <summary>전투 마스터가 없으면 null.</summary>
    public IReadOnlyList<BattleEntry>? Battles { get; init; }

    static List<AdvEntry>? LoadAdvs(ZipArchive zip) =>
        LoadCodeNames(zip, AdvMasterPath, "code", "name", "ADV", (code, name) => new AdvEntry(code, name));

    /// <summary>전투 마스터: code(키), name, 그리고 선택 열 can_run(도망 가능 여부, true/false 또는 1/0, 열이 없거나 칸이 비면 true).</summary>
    static List<BattleEntry>? LoadBattles(ZipArchive zip)
    {
        var text = TryReadEntry(zip, BattleMasterPath);
        if (text is null) return null;
        var rows = Tsv.WithoutMemo(Tsv.Parse(text));
        var withRun = rows.Count > 0 && rows[0].Length > 2 && rows[0][2] == "can_run";
        string[] header = ["code", "name", .. withRun ? new[] { "can_run" } : []];
        var battles = new List<BattleEntry>();
        var seen = new HashSet<long>();
        foreach (var r in Body(BattleMasterPath, text, header))
        {
            var code = ParseLong(BattleMasterPath, "전투 코드", r[0]);
            if (!seen.Add(code))
                throw new PackFormatException($"{BattleMasterPath}: 전투 코드 '{code}'가 중복됩니다.");
            var canRun = !withRun || r[2].Trim() == "" || ParseBool(BattleMasterPath, $"전투 '{code}'의 can_run", r[2]);
            battles.Add(new BattleEntry(code, r[1], canRun));
        }
        return battles;
    }

    /// <summary>
    /// 이벤트 흐름 마스터와 흐름 조건 마스터를 읽는다.
    /// 흐름: 이벤트는 이벤트 마스터에 있어야 하고, 키 (이벤트, 흐름 번호)를 두 번 쓸 수 없다. 흐름 번호와 인자는 정수(long, 인자는 비우면 0),
    /// 종류는 이름으로 적는다. 종류별 인자 검증: Jump의 param_1과 JumpFlag*와 JumpInventory*의 param_3(점프할 흐름 번호)은 자신의 흐름 번호보다 큰 같은 이벤트의 흐름 번호(뒤로만 점프해 무한 반복이 없다), Adv의 param_1은 ADV 마스터의 코드,
    /// Battle의 param_1은 전투 마스터의 코드여야 한다(Adv와 Battle은 그 마스터가 있어야 한다). Finish와 None은 인자를 보지 않고, SetFlag와 ModFlag의 param_1(플래그 코드)과 param_2(값)는 어떤 정수든 되고, AddInventory는 param_1이 인벤토리 마스터에 있는 코드여야 하며 유니크 아이템과 캐릭터는 param_2가 1 또는 -1이어야 한다.
    /// 흐름 조건 마스터(event_flow_condition_master)는 없어졌다. 그 파일이 게임팩에 있으면 로드 오류다.
    /// </summary>
    static List<EventFlow>? LoadEventFlows(
        ZipArchive zip, List<GameEvent>? events, List<AdvEntry>? advs, List<BattleEntry>? battles, IReadOnlyList<TagEntry>? tags, IReadOnlyDictionary<long, InventoryEntry> inventory)
    {
        const string file = EventFlowMasterPath;
        const string conditionFile = EventFlowConditionMasterPath;
        var text = TryReadEntry(zip, file);
        // 흐름 조건 마스터는 없어졌다: 조건에 따라 칸을 건너뛰려면 JumpFlag*로 뛰어넘는다. 조용히 무시하면 조건이 사라지므로 오류로 알린다.
        if (FindEntry(zip, conditionFile) is not null)
            throw new PackFormatException($"{conditionFile}는 더 이상 쓰이지 않습니다(조건은 JumpFlag* 칸으로 건너뛰어 냅니다). 이 파일을 지워 주세요.");
        if (text is null) return null;
        if (events is null) throw new PackFormatException($"{file}가 있으려면 {EventMasterPath}도 필요합니다.");

        var eventCodes = events.Select(e => e.Code).ToHashSet();
        var rows = new List<(long Event, long Flow, EventFlowType Type, long P1, long P2, long P3)>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "event_code", "flow_number", "flow_type", "param_1", "param_2", "param_3"))
        {
            var eventCode = ParseLong(file, "이벤트 코드", r[0]);
            var who = $"이벤트 '{eventCode}'의";
            var flow = ParseLong(file, $"{who} 흐름 번호", r[1]);
            var where = $"{who} 흐름 {flow}의";
            // 숫자("1")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<EventFlowType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<EventFlowType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 흐름 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<EventFlowType>())})");

            if (!eventCodes.Contains(eventCode))
                throw new PackFormatException($"{file}: 이벤트 '{eventCode}'가 이벤트 마스터에 없습니다.");
            if (!seen.Add((eventCode, flow)))
                throw new PackFormatException($"{file}: 같은 키(이벤트 {eventCode}, 흐름 {flow})가 두 번 있습니다.");
            rows.Add((eventCode, flow, type,
                OptionalLong(file, $"{where} 인자 1", r[3]),
                OptionalLong(file, $"{where} 인자 2", r[4]),
                OptionalLong(file, $"{where} 인자 3", r[5])));
        }

        // 종류별 인자 검증(param_2, param_3은 쓰지 않는다)
        var advCodes = advs?.Select(a => a.Code).ToHashSet();
        var battleCodes = battles?.Select(b => b.Code).ToHashSet();
        foreach (var (eventCode, flow, type, p1, p2, p3) in rows)
        {
            var where = $"{file}: 이벤트 '{eventCode}'의 흐름 {flow}의";
            if (type == EventFlowType.AddInventory) CheckAddInventory(where, p1, p2, inventory);
            if (RandomJump.Is(type.ToString()) && p2 < 1)
                throw new PackFormatException($"{where} JumpRandom의 모수 '{p2}'는 1 이상이어야 합니다.");
            if (HaveTagJump.Is(type.ToString())) CheckHaveTag(where, type.ToString(), p1, p2, inventory, tags);
            if (InventoryJump.Of(type) is not null && !inventory.ContainsKey(p1))
                throw new PackFormatException($"{where} {type}의 인벤토리 코드 '{p1}'가 인벤토리 마스터에 없습니다.");
            switch (type)
            {
                case EventFlowType.Jump when p1 <= flow:
                    throw new PackFormatException(
                        $"{where} 점프할 흐름 번호 '{p1}'는 자신의 흐름 번호 {flow}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                case EventFlowType.Jump when !seen.Contains((eventCode, p1)):
                    throw new PackFormatException($"{where} 점프할 흐름 번호 '{p1}'가 같은 이벤트의 흐름에 없습니다.");
                case var jump when HaveTagJump.IsConditionalJump(jump.ToString()) && p3 <= flow:
                    throw new PackFormatException(
                        $"{where} 점프할 흐름 번호 '{p3}'는 자신의 흐름 번호 {flow}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                case var jump when HaveTagJump.IsConditionalJump(jump.ToString()) && !seen.Contains((eventCode, p3)):
                    throw new PackFormatException($"{where} 점프할 흐름 번호 '{p3}'가 같은 이벤트의 흐름에 없습니다.");
                case EventFlowType.Adv when advCodes is null:
                    throw new PackFormatException($"{where} Adv가 있으려면 {AdvMasterPath}도 필요합니다.");
                case EventFlowType.Adv when !advCodes!.Contains(p1):
                    throw new PackFormatException($"{where} 재생할 ADV '{p1}'가 ADV 마스터에 없습니다.");
                case EventFlowType.Battle when battleCodes is null:
                    throw new PackFormatException($"{where} Battle이 있으려면 {BattleMasterPath}도 필요합니다.");
                case EventFlowType.Battle when !battleCodes!.Contains(p1):
                    throw new PackFormatException($"{where} 개시할 전투 '{p1}'가 전투 마스터에 없습니다.");
            }
        }

        return rows.Select(r => new EventFlow(r.Event, r.Flow, r.Type, r.P1, r.P2, r.P3)).ToList();
    }

    /// <summary>
    /// AddInventory 칸(이벤트, ADV 공통)의 인자 검증: param_1은 인벤토리 코드 공간에 있어야 하고, 유니크 아이템과 캐릭터는 개수가 1 또는 -1이어야 한다.
    /// 플레이어 캐릭터(1)는 내보낼 수 없으므로 -1도 안 된다.
    /// </summary>
    static void CheckAddInventory(string where, long code, long amount, IReadOnlyDictionary<long, InventoryEntry> inventory)
    {
        if (!inventory.TryGetValue(code, out var entry))
            throw new PackFormatException($"{where} AddInventory의 인벤토리 코드 '{code}'가 인벤토리 마스터에 없습니다.");
        if (entry.Kind is InventoryKind.UniqueItem or InventoryKind.Character && amount is not (1 or -1))
            throw new PackFormatException(
                $"{where} AddInventory의 {entry.Kind} '{code}' 개수는 1 또는 -1이어야 합니다(받은 값 {amount}).");
        if (entry.Kind == InventoryKind.Character && code == PlayerCharacterCode && amount == -1)
            throw new PackFormatException($"{where} AddInventory로 플레이어 캐릭터 '{code}'를 내보낼 수 없습니다.");
    }

    /// <summary>
    /// 태그 점프 칸(이벤트, ADV 공통)의 인자 검증: param_1은 JumpCharacterHaveTag면 캐릭터 인벤토리, JumpUniqueItemHaveTag면 유니크 아이템 인벤토리의 코드여야 하고,
    /// param_2는 태그 마스터에 있는 태그 코드여야 한다.
    /// </summary>
    static void CheckHaveTag(
        string where, string type, long code, long tagCode, IReadOnlyDictionary<long, InventoryEntry> inventory, IReadOnlyList<TagEntry>? tags)
    {
        var expected = HaveTagJump.IsCharacter(type) ? InventoryKind.Character : InventoryKind.UniqueItem;
        if (!inventory.TryGetValue(code, out var entry) || entry.Kind != expected)
            throw new PackFormatException(
                $"{where} {type}의 코드 '{code}'가 {(expected == InventoryKind.Character ? "캐릭터" : "유니크 아이템")} 인벤토리 마스터에 없습니다.");
        if (tags is null || !tags.Any(t => t.Code == tagCode))
            throw new PackFormatException($"{where} {type}의 태그 코드 '{tagCode}'가 태그 마스터에 없습니다.");
    }

    // ---- 키로 찾기 ----

    public GameEvent? FindEvent(long code) => Events?.FirstOrDefault(e => e.Code == code);

    public AdvEntry? FindAdv(long code) =>
        Masters.AdvRowTable.TryFindByCode(code, out var r) ? new AdvEntry(r.Code, r.Name) : null;

    public BattleEntry? FindBattle(long code) =>
        Masters.BattleRowTable.TryFindByCode(code, out var r) ? new BattleEntry(r.Code, r.Name, r.CanRun) : null;

    /// <summary>이벤트의 흐름 칸 하나(조건 포함). 없으면 null.</summary>
    public EventFlow? FindEventFlow(long eventCode, long flowNumber) =>
        EventFlows?.FirstOrDefault(f => f.EventCode == eventCode && f.FlowNumber == flowNumber);

    /// <summary>한 이벤트의 흐름 칸들(키 순서: 흐름 번호).</summary>
    public IReadOnlyList<EventFlow> FlowsOf(long eventCode) =>
        (EventFlows ?? []).Where(f => f.EventCode == eventCode).OrderBy(f => f.FlowNumber).ToList();
}
