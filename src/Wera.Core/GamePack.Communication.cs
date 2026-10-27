using System.IO.Compression;

namespace Wera.Core;

/// <summary>교류 그룹 마스터의 한 행(예: 대화). 교류 화면 왼쪽에 "[코드×100] 이름"으로 나열된다.</summary>
public sealed record CommunicationGroupEntry(long Code, string Name);

/// <summary>
/// 교류 마스터의 한 행(예: 대화 그룹의 인사하기). GroupCode는 교류 그룹 마스터의 코드다.
/// IsAssist가 참이면 이 교류는 파트너 대신 어시스트 캐릭터를 상대로 하고, 흐름을 찾을 때 파트너와 어시스트 자리를 뒤바꾼다.
/// </summary>
public sealed record CommunicationEntry(long Code, long GroupCode, string Name, bool IsAssist);

/// <summary>
/// 교류 흐름 한 칸이 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다.
/// 숫자 값은 정해진 번호다: None 0, Finish 1, Jump 2, Text 3, Choice 4, TextPlain 5, SetFlag 6, ModFlag 7, AddInventory 8,
/// JumpFlagEqual 9, JumpFlagNotEqual 10, JumpFlagGreaterThan 11, JumpFlagGreaterOrEqual 12, JumpFlagLessThan 13, JumpFlagLessOrEqual 14,
/// JumpInventoryEqual 15, JumpInventoryNotEqual 16, JumpInventoryGreaterThan 17, JumpInventoryGreaterOrEqual 18, JumpInventoryLessThan 19, JumpInventoryLessOrEqual 20,
/// JumpCharacterHaveTag 21, JumpUniqueItemHaveTag 22, JumpRandom 23.
/// 점프하는 칸의 규칙과 인자는 ADV 흐름(<see cref="AdvFlowType"/>)과 같고, 점프할 흐름 번호는 같은 흐름 묶음 안에 있고 자신보다 커야 한다.
/// </summary>
public enum CommunicationFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
    /// <summary>교류를 끝낸다.</summary>
    Finish = 1,
    /// <summary>조건 없이 뒤쪽의 흐름 칸으로 점프한다. param_1이 점프할 flow_number다.</summary>
    Jump = 2,
    /// <summary>대사. 보여 줄 문장은 text 열이고 비워 둘 수 없다(꾸밈 문법 가능). 인자는 아직 쓰이지 않는다.</summary>
    Text = 3,
    /// <summary>
    /// 선택지 하나. param_1이 선택지 그룹 아이디(같은 그룹의 행은 flow_number 순으로 연속해야 한다), param_2가 고르면 점프할 flow_number다.
    /// 문구는 text 열이고 비워 둘 수 없으며 한 줄이어야 한다.
    /// </summary>
    Choice = 4,
    /// <summary>화자가 없는 일반 텍스트. 보여 줄 문장은 text 열이고 비워 둘 수 없다. 인자는 아직 쓰이지 않는다.</summary>
    TextPlain = 5,
    /// <summary>플레이어 플래그를 정한 값으로 바꾼다. param_1이 flag_code, param_2가 value다.</summary>
    SetFlag = 6,
    /// <summary>플레이어 플래그에 값을 더한다(음수면 뺀다). param_1이 flag_code, param_2가 더할 value다.</summary>
    ModFlag = 7,
    /// <summary>인벤토리에 개수를 더한다(음수면 뺀다). param_1이 inventory_code, param_2가 개수다. 유니크 아이템과 캐릭터는 1 또는 -1이어야 한다.</summary>
    AddInventory = 8,
    /// <summary>플래그 값이 flag_value와 Equal 비교가 참이면 target_flow_number로 점프한다. param_1 flag_code, param_2 flag_value, param_3 target_flow_number.</summary>
    JumpFlagEqual = 9,
    JumpFlagNotEqual = 10,
    JumpFlagGreaterThan = 11,
    JumpFlagGreaterOrEqual = 12,
    JumpFlagLessThan = 13,
    JumpFlagLessOrEqual = 14,
    /// <summary>인벤토리 보유 개수가 value와 Equal 비교가 참이면 target_flow_number로 점프한다. param_1 inventory_code, param_2 count, param_3 target_flow_number.</summary>
    JumpInventoryEqual = 15,
    JumpInventoryNotEqual = 16,
    JumpInventoryGreaterThan = 17,
    JumpInventoryGreaterOrEqual = 18,
    JumpInventoryLessThan = 19,
    JumpInventoryLessOrEqual = 20,
    /// <summary>캐릭터가 태그를 가지면 점프한다. param_1 캐릭터 코드, param_2 tag_code, param_3 target_flow_number.</summary>
    JumpCharacterHaveTag = 21,
    /// <summary>유니크 아이템이 태그를 가지면 점프한다. param_1 유니크 아이템 코드, param_2 tag_code, param_3 target_flow_number.</summary>
    JumpUniqueItemHaveTag = 22,
    /// <summary>rand(param_2) &lt; param_1이면 param_3으로 점프한다(모수 param_2는 1 이상).</summary>
    JumpRandom = 23,
}

/// <summary>흐름의 한 칸. 세 흐름 마스터가 모두 이 모양의 칸을 가진다.</summary>
public sealed record CommunicationFlowStep(
    long FlowNumber, CommunicationFlowType FlowType, long Param1, long Param2, long Param3, string Text);

/// <summary>교류 솔로 흐름의 한 행: 교류(CommunicationCode)와 상대 캐릭터(CharacterCode)가 정하는 흐름 묶음의 한 칸.</summary>
public sealed record CommunicationSoloFlow(
    long CommunicationCode, long CharacterCode, long FlowNumber, CommunicationFlowType FlowType,
    long Param1, long Param2, long Param3, string Text);

/// <summary>교류 듀오 흐름의 한 행: 교류와 파트너 캐릭터(PartnerCode), 어시스트 캐릭터(AssistCode)의 짝이 정하는 흐름 묶음의 한 칸.</summary>
public sealed record CommunicationDuoFlow(
    long CommunicationCode, long PartnerCode, long AssistCode, long FlowNumber, CommunicationFlowType FlowType,
    long Param1, long Param2, long Param3, string Text);

/// <summary>교류 기본 흐름의 한 행: 교류만으로 정해지는 흐름 묶음의 한 칸(캐릭터와 상관없다).</summary>
public sealed record CommunicationDefaultFlow(
    long CommunicationCode, long FlowNumber, CommunicationFlowType FlowType,
    long Param1, long Param2, long Param3, string Text);

// 교류: communication_group_master(code, name; code가 키), communication_master(code, group_code, name, is_assist; code가 키),
// communication_solo_flow_master(communication_code, character_code, flow_number, flow_type, param_1~3, text; 앞의 세 열이 키),
// communication_duo_flow_master(communication_code, partner_code, assist_code, flow_number, ...; 앞의 네 열이 키),
// communication_default_flow_master(communication_code, flow_number, ...; 앞의 두 열이 키).
// 흐름은 duo → solo → default 순으로 찾는다(FindCommunicationFlow).
public sealed partial class GamePack
{
    public const string CommunicationGroupMasterPath = "master/communication_group_master_v1.tsv";
    public const string CommunicationMasterPath = "master/communication_master_v1.tsv";
    public const string CommunicationSoloFlowMasterPath = "master/communication_solo_flow_master_v1.tsv";
    public const string CommunicationDuoFlowMasterPath = "master/communication_duo_flow_master_v1.tsv";
    public const string CommunicationDefaultFlowMasterPath = "master/communication_default_flow_master_v1.tsv";

    /// <summary>communication_solo_flow_master로 이름이 바뀐 이전 파일 이름.</summary>
    const string OldCommunicationFlowMasterPath = "master/communication_flow_master_v1.tsv";

    /// <summary>교류 그룹 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CommunicationGroupEntry>? CommunicationGroups { get; init; }

    /// <summary>교류 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CommunicationEntry>? Communications { get; init; }

    /// <summary>교류 솔로 흐름 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CommunicationSoloFlow>? CommunicationSoloFlows { get; init; }

    /// <summary>교류 듀오 흐름 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CommunicationDuoFlow>? CommunicationDuoFlows { get; init; }

    /// <summary>교류 기본 흐름 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<CommunicationDefaultFlow>? CommunicationDefaultFlows { get; init; }

    /// <summary>교류 화면의 입력 코드는 그룹 코드×100이라, 1 이상이고 100배가 long을 넘지 않아야 한다.</summary>
    static List<CommunicationGroupEntry>? LoadCommunicationGroups(ZipArchive zip)
    {
        var groups = LoadCodeNames(zip, CommunicationGroupMasterPath, "code", "name", "교류 그룹",
            (code, name) => new CommunicationGroupEntry(code, name));
        foreach (var g in groups ?? [])
            if (g.Code < 1 || g.Code > long.MaxValue / 100)
                throw new PackFormatException(
                    $"{CommunicationGroupMasterPath}: 교류 그룹 코드 '{g.Code}'는 1 이상 {long.MaxValue / 100} 이하여야 합니다(화면의 코드는 그룹 코드×100).");
        return groups;
    }

    /// <summary>교류 마스터: 그룹 코드는 교류 그룹 마스터에 있어야 한다(그래서 교류 그룹 마스터가 있어야 한다).</summary>
    static List<CommunicationEntry>? LoadCommunications(ZipArchive zip, List<CommunicationGroupEntry>? groups)
    {
        const string file = CommunicationMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (groups is null) throw new PackFormatException($"{file}가 있으려면 {CommunicationGroupMasterPath}도 필요합니다.");

        var groupCodes = groups.Select(g => g.Code).ToHashSet();
        var rows = new List<CommunicationEntry>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, "code", "group_code", "name", "is_assist"))
        {
            var code = ParseLong(file, "교류 코드", r[0]);
            var groupCode = ParseLong(file, $"교류 '{code}'의 그룹 코드", r[1]);
            var isAssist = ParseBool(file, $"교류 '{code}'의 is_assist", r[3]);
            if (!seen.Add(code))
                throw new PackFormatException($"{file}: 교류 코드 '{code}'가 중복됩니다.");
            if (!groupCodes.Contains(groupCode))
                throw new PackFormatException($"{file}: 교류 '{code}'의 그룹 코드 '{groupCode}'가 교류 그룹 마스터에 없습니다.");
            rows.Add(new CommunicationEntry(code, groupCode, r[2], isAssist));
        }
        return rows;
    }

    static void CheckOldCommunicationFlowMaster(ZipArchive zip)
    {
        if (FindEntry(zip, OldCommunicationFlowMasterPath) is not null)
            throw new PackFormatException(
                $"{OldCommunicationFlowMasterPath}는 {CommunicationSoloFlowMasterPath}로 이름이 바뀌었습니다. 파일 이름을 바꿔 주세요.");
    }

    static List<CommunicationSoloFlow>? LoadCommunicationSoloFlows(
        ZipArchive zip, List<CommunicationEntry>? communications, IReadOnlyDictionary<long, InventoryEntry> inventory, IReadOnlyList<TagEntry>? tags)
    {
        CheckOldCommunicationFlowMaster(zip);
        return LoadCommunicationFlowFile(zip, CommunicationSoloFlowMasterPath, ["character_code"], communications, inventory, tags)
            ?.Select(r => new CommunicationSoloFlow(r.Keys[0], r.Keys[1], r.Step.FlowNumber, r.Step.FlowType,
                r.Step.Param1, r.Step.Param2, r.Step.Param3, r.Step.Text)).ToList();
    }

    static List<CommunicationDuoFlow>? LoadCommunicationDuoFlows(
        ZipArchive zip, List<CommunicationEntry>? communications, IReadOnlyDictionary<long, InventoryEntry> inventory, IReadOnlyList<TagEntry>? tags) =>
        LoadCommunicationFlowFile(zip, CommunicationDuoFlowMasterPath, ["partner_code", "assist_code"], communications, inventory, tags)
            ?.Select(r => new CommunicationDuoFlow(r.Keys[0], r.Keys[1], r.Keys[2], r.Step.FlowNumber, r.Step.FlowType,
                r.Step.Param1, r.Step.Param2, r.Step.Param3, r.Step.Text)).ToList();

    static List<CommunicationDefaultFlow>? LoadCommunicationDefaultFlows(
        ZipArchive zip, List<CommunicationEntry>? communications, IReadOnlyDictionary<long, InventoryEntry> inventory, IReadOnlyList<TagEntry>? tags) =>
        LoadCommunicationFlowFile(zip, CommunicationDefaultFlowMasterPath, [], communications, inventory, tags)
            ?.Select(r => new CommunicationDefaultFlow(r.Keys[0], r.Step.FlowNumber, r.Step.FlowType,
                r.Step.Param1, r.Step.Param2, r.Step.Param3, r.Step.Text)).ToList();

    /// <summary>
    /// 세 흐름 마스터가 함께 쓰는 로더. 열은 communication_code, (캐릭터 열들), flow_number, flow_type, param_1~3, text다.
    /// 교류는 교류 마스터에, 캐릭터 열은 캐릭터 인벤토리에 있어야 하고 키(교류, 캐릭터 열들, 흐름 번호)는 겹칠 수 없다.
    /// 칸의 인자는 종류별로 검증한다(점프 대상, 선택지 그룹의 연속, 인벤토리/태그 코드 등).
    /// 같은 (교류, 캐릭터 열들)의 칸들이 한 흐름 묶음이고 점프는 그 안에서만 한다.
    /// </summary>
    static List<(long[] Keys, CommunicationFlowStep Step)>? LoadCommunicationFlowFile(
        ZipArchive zip, string file, string[] characterColumns, List<CommunicationEntry>? communications,
        IReadOnlyDictionary<long, InventoryEntry> inventory, IReadOnlyList<TagEntry>? tags)
    {
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (communications is null) throw new PackFormatException($"{file}가 있으려면 {CommunicationMasterPath}도 필요합니다.");

        var codes = communications.Select(c => c.Code).ToHashSet();
        string[] header = ["communication_code", .. characterColumns, "flow_number", "flow_type", "param_1", "param_2", "param_3", "text"];
        var n = characterColumns.Length;
        var rows = new List<(long[] Keys, CommunicationFlowStep Step)>();
        var seen = new HashSet<string>();
        foreach (var r in Body(file, text, header))
        {
            var code = ParseLong(file, "교류 코드", r[0]);
            var who = $"교류 '{code}'의";
            var keys = new long[1 + n];
            keys[0] = code;
            for (var i = 0; i < n; i++)
            {
                keys[1 + i] = ParseLong(file, $"{who} {characterColumns[i]}", r[1 + i]);
                who += $" {characterColumns[i]} '{keys[1 + i]}'의";
            }
            var flow = ParseLong(file, $"{who} 흐름 번호", r[1 + n]);
            var where = $"{who} 흐름 {flow}의";
            var typeText = r[2 + n];
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<CommunicationFlowType>().Contains(typeText, StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<CommunicationFlowType>(typeText, ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 흐름 종류 '{typeText}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<CommunicationFlowType>())})");

            if (!codes.Contains(code))
                throw new PackFormatException($"{file}: 교류 '{code}'가 교류 마스터에 없습니다.");
            for (var i = 0; i < n; i++)
                if (!inventory.TryGetValue(keys[1 + i], out var entry) || entry.Kind != InventoryKind.Character)
                    throw new PackFormatException($"{file}: {characterColumns[i]} '{keys[1 + i]}'가 캐릭터 인벤토리 마스터에 없습니다.");
            if (!seen.Add(string.Join(",", keys) + $",{flow}"))
                throw new PackFormatException($"{file}: 같은 키({string.Join(", ", keys)}, 흐름 {flow})가 두 번 있습니다.");
            rows.Add((keys, new CommunicationFlowStep(flow, type,
                OptionalLong(file, $"{where} 인자 1", r[3 + n]),
                OptionalLong(file, $"{where} 인자 2", r[4 + n]),
                OptionalLong(file, $"{where} 인자 3", r[5 + n]), r[6 + n])));
        }

        foreach (var set in rows.GroupBy(r => string.Join(",", r.Keys)))
            CheckCommunicationFlowSet(file, set.OrderBy(r => r.Step.FlowNumber).Select(r => (r.Keys, r.Step)).ToList(), inventory, tags);
        return rows;
    }

    /// <summary>한 흐름 묶음(같은 교류와 캐릭터 열들)의 칸들을 종류별로 검증한다. steps는 흐름 번호 순이다.</summary>
    static void CheckCommunicationFlowSet(
        string file, List<(long[] Keys, CommunicationFlowStep Step)> steps,
        IReadOnlyDictionary<long, InventoryEntry> inventory, IReadOnlyList<TagEntry>? tags)
    {
        var numbers = steps.Select(s => s.Step.FlowNumber).ToHashSet();
        var started = new HashSet<long>();
        long? lastGroup = null;                          // 바로 앞 칸이 Choice일 때 그 선택지 그룹
        foreach (var (keys, s) in steps)
        {
            var where = $"{file}: 교류 '{keys[0]}'의 키 [{string.Join(", ", keys.Skip(1))}]의 흐름 {s.FlowNumber}의";
            var (type, p1, p2, p3) = (s.FlowType, s.Param1, s.Param2, s.Param3);
            var name = type.ToString();

            if (type is CommunicationFlowType.Text or CommunicationFlowType.TextPlain or CommunicationFlowType.Choice
                && string.IsNullOrWhiteSpace(s.Text))
                throw new PackFormatException($"{where} {type}에는 text가 있어야 합니다.");
            if (type is CommunicationFlowType.Text or CommunicationFlowType.TextPlain)
            {
                if (RichText.TryParse(s.Text, out var error) is null)
                    throw new PackFormatException($"{where} text가 올바르지 않습니다: {error}");
            }
            if (type == CommunicationFlowType.Choice) CheckChoiceText(where, s.Text);
            if (type == CommunicationFlowType.AddInventory) CheckAddInventory(where, p1, p2, inventory);
            if (RandomJump.Is(name) && p2 < 1)
                throw new PackFormatException($"{where} JumpRandom의 모수 '{p2}'는 1 이상이어야 합니다.");
            if (HaveTagJump.Is(name)) CheckHaveTag(where, name, p1, p2, inventory, tags);
            if (InventoryJump.Of(name) is not null && !inventory.ContainsKey(p1))
                throw new PackFormatException($"{where} {type}의 인벤토리 코드 '{p1}'가 인벤토리 마스터에 없습니다.");

            // 점프 대상(Jump는 param_1, Choice는 param_2, 조건 점프는 param_3): 같은 묶음에 있고 자신보다 커야 한다.
            long? target = type == CommunicationFlowType.Jump ? p1
                : type == CommunicationFlowType.Choice ? p2
                : HaveTagJump.IsConditionalJump(name) ? p3
                : null;
            if (target is { } t)
            {
                if (t <= s.FlowNumber)
                    throw new PackFormatException(
                        $"{where} 점프할 흐름 번호 '{t}'는 자신의 흐름 번호 {s.FlowNumber}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                if (!numbers.Contains(t))
                    throw new PackFormatException($"{where} 점프할 흐름 번호 '{t}'가 같은 흐름 묶음에 없습니다.");
            }

            // 같은 선택지 그룹(param_1)의 Choice 행은 흐름 번호 순으로 연속해야 한다.
            if (type != CommunicationFlowType.Choice) { lastGroup = null; continue; }
            if (lastGroup != p1 && !started.Add(p1))
                throw new PackFormatException($"{where} 선택지 그룹 '{p1}'가 연속하지 않습니다(같은 그룹의 선택지 사이에 다른 칸이 있습니다).");
            lastGroup = p1;
        }
    }

    /// <summary>
    /// 교류의 흐름을 찾는다: 듀오 → 솔로 → 기본 순으로 찾아 흐름이 있는 첫 마스터의 칸들(흐름 번호 순)을 돌려준다(칸 하나씩이 아니라 묶음 단위로 폴백한다).
    /// 교류가 IsAssist면 솔로는 파트너 대신 어시스트 캐릭터로 찾고, 듀오는 파트너와 어시스트 자리를 뒤바꿔 찾는다
    /// (partner_code에 어시스트 캐릭터, assist_code에 파트너 캐릭터를 넣는다). 필요한 캐릭터가 슬롯에 없으면 그 단계는 건너뛴다.
    /// 교류 코드가 마스터에 없거나 어느 마스터에도 흐름이 없으면 빈 목록.
    /// </summary>
    public IReadOnlyList<CommunicationFlowStep> FindCommunicationFlow(long communicationCode, long? partnerCode, long? assistCode)
    {
        var communication = Communications?.FirstOrDefault(c => c.Code == communicationCode);
        if (communication is null) return [];
        var (first, second) = communication.IsAssist ? (assistCode, partnerCode) : (partnerCode, assistCode);

        if (first is { } a && second is { } b
            && Steps(CommunicationDuoFlows?.Where(f => f.CommunicationCode == communicationCode && f.PartnerCode == a && f.AssistCode == b)
                .Select(f => new CommunicationFlowStep(f.FlowNumber, f.FlowType, f.Param1, f.Param2, f.Param3, f.Text))) is { Count: > 0 } duo)
            return duo;
        if (first is { } c
            && Steps(CommunicationSoloFlows?.Where(f => f.CommunicationCode == communicationCode && f.CharacterCode == c)
                .Select(f => new CommunicationFlowStep(f.FlowNumber, f.FlowType, f.Param1, f.Param2, f.Param3, f.Text))) is { Count: > 0 } solo)
            return solo;
        return Steps(CommunicationDefaultFlows?.Where(f => f.CommunicationCode == communicationCode)
            .Select(f => new CommunicationFlowStep(f.FlowNumber, f.FlowType, f.Param1, f.Param2, f.Param3, f.Text)));

        static List<CommunicationFlowStep> Steps(IEnumerable<CommunicationFlowStep>? steps) =>
            (steps ?? []).OrderBy(s => s.FlowNumber).ToList();
    }
}
