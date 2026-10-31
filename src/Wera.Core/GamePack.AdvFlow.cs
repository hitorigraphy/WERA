using System.Globalization;
using System.IO.Compression;

namespace Wera.Core;

/// <summary>
/// ADV 흐름 한 칸이 하는 일의 종류. TSV에는 이름(대소문자 무시)으로 적는다.
/// 숫자 값은 정해진 번호다: None 0, Finish 1, Jump 2, TextMe 3, Choice 4, TextYou 5, TextPlain 6, SetFlag 7, ModFlag 8, AddInventory 9, JumpFlagEqual 10, JumpFlagNotEqual 11, JumpFlagGreaterThan 12, JumpFlagGreaterOrEqual 13, JumpFlagLessThan 14, JumpFlagLessOrEqual 15, JumpInventoryEqual 16, JumpInventoryNotEqual 17, JumpInventoryGreaterThan 18, JumpInventoryGreaterOrEqual 19, JumpInventoryLessThan 20, JumpInventoryLessOrEqual 21, JumpCharacterHaveTag 22, JumpUniqueItemHaveTag 23, JumpRandom 24, TextLog 25.
/// </summary>
public enum AdvFlowType
{
    /// <summary>아무 일도 하지 않는다.</summary>
    None = 0,
    /// <summary>ADV를 끝낸다.</summary>
    Finish = 1,
    /// <summary>조건 없이 뒤쪽의 흐름 칸으로 점프한다. param_1이 점프할 flow_number이고, 같은 ADV에 있어야 하며 자신의 flow_number보다 커야 한다. 플래그를 견주어 점프하려면 JumpFlag*를 쓴다.</summary>
    Jump = 2,
    /// <summary>나(플레이어 쪽)의 대사. 보여 줄 문장은 text 열이고 비워 둘 수 없다. param_1이 말하는 캐릭터 코드, param_2가 그 캐릭터의 variation_number다.</summary>
    TextMe = 3,
    /// <summary>
    /// 선택지 하나. param_1이 선택지 그룹 아이디(같은 그룹의 행이 한 번의 선택지 묶음이고, flow_number 순으로 연속해야 한다),
    /// param_2가 이 선택지를 고르면 점프할 flow_number다(같은 ADV에 있어야 하며 자신의 flow_number보다 커야 한다).
    /// </summary>
    Choice = 4,
    /// <summary>상대(말 거는 쪽)의 대사. 보여 줄 문장은 text 열이고 비워 둘 수 없다. param_1이 말하는 캐릭터 코드, param_2가 그 캐릭터의 variation_number다.</summary>
    TextYou = 5,
    /// <summary>
    /// 화자가 없는 상자 텍스트. 아이콘 자리를 포함한 가로 전체에, 아이콘과 같은 높이의 상자를 그리고 그 안에 text를 보여 준다(이름 자리는 없다).
    /// 보여 줄 문장은 text 열이고 비워 둘 수 없으며 상자(<see cref="AdvLayout.PlainBoxColumns"/>칸 × <see cref="AdvLayout.BoxLines"/>줄)에 들어가야 한다. 인자는 아직 쓰이지 않는다.
    /// </summary>
    TextPlain = 6,
    /// <summary>플레이어 플래그를 정한 값으로 바꾼다. param_1이 flag_code, param_2가 value다. 화면에는 아무것도 나오지 않고 바로 다음 칸으로 간다.</summary>
    SetFlag = 7,
    /// <summary>플레이어 플래그에 값을 더한다(음수면 뺀다). param_1이 flag_code, param_2가 더할 value다. 없던 플래그는 0에서 시작하고, 값이 long 범위를 넘으면 끝값에 멈춘다.</summary>
    ModFlag = 8,
    /// <summary>
    /// 인벤토리에 개수를 더한다(음수면 뺀다). param_1이 inventory_code(커먼, 번치, 유니크 아이템, 캐릭터가 함께 쓰는 코드), param_2가 개수다.
    /// 유니크 아이템과 캐릭터는 개수가 1 또는 -1이어야 한다(로드 검증). 화면에는 나오지 않고 바로 다음 칸으로 간다. 자세한 규칙은 <see cref="GameSession.AddInventory"/>.
    /// </summary>
    AddInventory = 9,
    /// <summary>플래그 값이 flag_value와 Equal 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagEqual = 10,
    /// <summary>플래그 값이 flag_value와 NotEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagNotEqual = 11,
    /// <summary>플래그 값이 flag_value와 GreaterThan 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagGreaterThan = 12,
    /// <summary>플래그 값이 flag_value와 GreaterOrEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagGreaterOrEqual = 13,
    /// <summary>플래그 값이 flag_value와 LessThan 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagLessThan = 14,
    /// <summary>플래그 값이 flag_value와 LessOrEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="FlagJump"/>).</summary>
    JumpFlagLessOrEqual = 15,
    /// <summary>인벤토리 보유 개수가 value와 Equal 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryEqual = 16,
    /// <summary>인벤토리 보유 개수가 value와 NotEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryNotEqual = 17,
    /// <summary>인벤토리 보유 개수가 value와 GreaterThan 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryGreaterThan = 18,
    /// <summary>인벤토리 보유 개수가 value와 GreaterOrEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryGreaterOrEqual = 19,
    /// <summary>인벤토리 보유 개수가 value와 LessThan 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryLessThan = 20,
    /// <summary>인벤토리 보유 개수가 value와 LessOrEqual 비교가 참이면 target_flow_number로 점프하고 아니면 다음 칸으로 간다(자세한 규칙은 <see cref="InventoryJump"/>).</summary>
    JumpInventoryLessOrEqual = 21,
    /// <summary>
    /// 캐릭터가 태그를 가지고 있으면 target_flow_number로 점프하고 아니면 다음 칸으로 간다. param_1이 character_code(캐릭터 인벤토리 코드), param_2가 tag_code(태그 마스터),
    /// param_3이 target_flow_number다. 보유하지 않은 캐릭터는 태그가 없는 것으로 본다. 자세한 규칙은 <see cref="HaveTagJump"/>.
    /// </summary>
    JumpCharacterHaveTag = 22,
    /// <summary>
    /// 유니크 아이템이 태그를 가지고 있으면 target_flow_number로 점프하고 아니면 다음 칸으로 간다. param_1이 item_code(유니크 아이템 인벤토리 코드), param_2가 tag_code(태그 마스터),
    /// param_3이 target_flow_number다. 그 코드의 보유 인스턴스 중 하나라도 태그를 가지고 있으면 참이고, 보유하지 않으면 거짓이다. 자세한 규칙은 <see cref="HaveTagJump"/>.
    /// </summary>
    JumpUniqueItemHaveTag = 23,
    /// <summary>
    /// 확률로 점프한다. param_1이 확률(chance), param_2가 모수(bound), param_3이 target_flow_number다:
    /// 0 이상 bound 미만의 무작위 정수 r을 뽑아 r &lt; chance면 target_flow_number로 점프하고 아니면 다음 칸으로 간다
    /// (예: 30, 100, 5 → rand(100) &lt; 30 ? goto 5 : goto next). 모수는 1 이상이어야 한다(로드 검증). 자세한 규칙은 <see cref="RandomJump"/>.
    /// </summary>
    JumpRandom = 24,
    /// <summary>
    /// 화자가 없는, 상자 없이 글만 나오는 텍스트(로그). 보여 줄 문장은 text 열이고 비워 둘 수 없으며 꾸밈 문법은 맞아야 하지만 줄 수 제한은 없다. 인자는 아직 쓰이지 않는다.
    /// </summary>
    TextLog = 25,
}

/// <summary>
/// ADV 흐름 마스터의 한 행. ADV(AdvCode)의 흐름 번호(FlowNumber) 칸이 하는 일(FlowType)과 그 인자. 키는 (AdvCode, FlowNumber)다.
/// Text는 TextMe/TextYou/TextPlain/TextLog 칸이 보여 줄 문장이고(Choice는 선택지 문구로 쓸 수 있다), 그 외 종류는 비워 둔다.
/// TextMe와 TextYou의 Param1은 말하는 캐릭터 코드, Param2는 그 캐릭터 이미지의 variation_number다(0이거나 없는 캐릭터면 이름 ???와 No Image).
/// ForceName은 화자 이름을 강제하는 특별 연출용 열로, 비어 있으면 캐릭터 이름을 쓴다. 칸에는 조건이 없다(조건은 JumpFlag*로 건너뛰어 낸다).
/// </summary>
public sealed record AdvFlow(
    long AdvCode, long FlowNumber, AdvFlowType FlowType, long Param1, long Param2, long Param3, string Text, string ForceName);

// ADV 흐름 마스터. 이 마스터는 선택이다.
public sealed partial class GamePack
{
    public const string AdvFlowMasterPath = "master/adv_flow_master_v1.tsv";
    public const string AdvFlowConditionMasterPath = "master/adv_flow_condition_master_v1.tsv";

    /// <summary>ADV 흐름 마스터가 없으면 null. 게임팩 파일 순서.</summary>
    public IReadOnlyList<AdvFlow>? AdvFlows { get; init; }

    /// <summary>
    /// ADV 흐름 마스터를 읽는다.
    /// 흐름: ADV는 ADV 마스터에 있어야 하고, 키 (ADV, 흐름 번호)를 두 번 쓸 수 없다. 흐름 번호와 인자는 정수(long, 인자는 비우면 0), 종류는 이름으로 적는다.
    /// 종류별 인자 검증: Jump의 param_1과 JumpFlag*, JumpInventory*, 태그 점프(JumpCharacterHaveTag, JumpUniqueItemHaveTag), JumpRandom의 param_3(점프할 흐름 번호), Choice의 param_2는 자신의 흐름 번호보다 큰 같은 ADV의 흐름 번호(뒤로만 점프해 무한 반복이 없다).
    /// 같은 ADV에서 Choice의 param_1(선택지 그룹 아이디)이 같은 행들은 흐름 번호 순으로 연속해야 한다(다른 칸이 끼어들 수 없다).
    /// Finish, None, 대사 종류(TextMe, TextYou, TextPlain, TextLog)는 인자를 검증하지 않는다(TextMe와 TextYou의 param_1이 캐릭터가 아니어도 오류가 아니라 이름 ???). SetFlag와 ModFlag의 param_1(플래그 코드)과 param_2(값)는 어떤 정수든 되고, AddInventory는 param_1이 인벤토리 마스터에 있는 코드여야 하며 유니크 아이템과 캐릭터는 param_2가 1 또는 -1이어야 한다. 대사 종류는 text 열이 비어 있으면 안 된다.
    /// 흐름 조건 마스터(adv_flow_condition_master)는 없어졌다. 그 파일이 게임팩에 있으면 로드 오류다.
    /// </summary>
    static List<AdvFlow>? LoadAdvFlows(
        ZipArchive zip, List<AdvEntry>? advs, IReadOnlyDictionary<long, InventoryEntry> inventory, IReadOnlyList<TagEntry>? tags)
    {
        const string file = AdvFlowMasterPath;
        const string conditionFile = AdvFlowConditionMasterPath;
        var text = TryReadEntry(zip, file);
        // 흐름 조건 마스터는 없어졌다: 조건에 따라 칸이나 선택지를 숨기려면 JumpFlag*로 다른 칸/선택지 묶음으로 뛴다. 조용히 무시하면 조건이 사라지므로 오류로 알린다.
        if (FindEntry(zip, conditionFile) is not null)
            throw new PackFormatException($"{conditionFile}는 더 이상 쓰이지 않습니다(조건은 JumpFlag* 칸으로 건너뛰거나 다른 선택지 묶음으로 뛰어 냅니다). 이 파일을 지워 주세요.");
        if (text is null) return null;
        if (advs is null) throw new PackFormatException($"{file}가 있으려면 {AdvMasterPath}도 필요합니다.");

        var advCodes = advs.Select(a => a.Code).ToHashSet();
        var rows = new List<(long Adv, long Flow, AdvFlowType Type, long P1, long P2, long P3, string Text, string ForceName)>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "adv_code", "flow_number", "flow_type", "param_1", "param_2", "param_3", "text", "force_name"))
        {
            var advCode = ParseLong(file, "ADV 코드", r[0]);
            var who = $"ADV '{advCode}'의";
            var flow = ParseLong(file, $"{who} 흐름 번호", r[1]);
            var where = $"{who} 흐름 {flow}의";
            // 숫자("1")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<AdvFlowType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<AdvFlowType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 흐름 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<AdvFlowType>())})");

            if (!advCodes.Contains(advCode))
                throw new PackFormatException($"{file}: ADV '{advCode}'가 ADV 마스터에 없습니다.");
            if (!seen.Add((advCode, flow)))
                throw new PackFormatException($"{file}: 같은 키(ADV {advCode}, 흐름 {flow})가 두 번 있습니다.");
            rows.Add((advCode, flow, type,
                OptionalLong(file, $"{where} 인자 1", r[3]),
                OptionalLong(file, $"{where} 인자 2", r[4]),
                OptionalLong(file, $"{where} 인자 3", r[5]), r[6], r[7]));
        }

        // 종류별 인자 검증
        foreach (var (advCode, flow, type, p1, p2, p3, line, _) in rows)
        {
            var where = $"{file}: ADV '{advCode}'의 흐름 {flow}의";
            if (type == AdvFlowType.Choice) CheckChoiceText(where, line);
            if (type == AdvFlowType.AddInventory) CheckAddInventory(where, p1, p2, inventory);
            if (RandomJump.Is(type.ToString()) && p2 < 1)
                throw new PackFormatException($"{where} JumpRandom의 모수 '{p2}'는 1 이상이어야 합니다.");
            if (HaveTagJump.Is(type.ToString())) CheckHaveTag(where, type.ToString(), p1, p2, inventory, tags);
            if (InventoryJump.Of(type) is not null && !inventory.ContainsKey(p1))
                throw new PackFormatException($"{where} {type}의 인벤토리 코드 '{p1}'가 인벤토리 마스터에 없습니다.");
            switch (type)
            {
                case AdvFlowType.TextMe or AdvFlowType.TextYou or AdvFlowType.TextPlain or AdvFlowType.TextLog when string.IsNullOrWhiteSpace(line):
                    throw new PackFormatException($"{where} {type}에는 text가 있어야 합니다.");
                case AdvFlowType.TextMe or AdvFlowType.TextYou or AdvFlowType.TextPlain or AdvFlowType.TextLog:
                    CheckLine(where, type, line);
                    break;
                case AdvFlowType.Jump when p1 <= flow:
                    throw new PackFormatException(
                        $"{where} 점프할 흐름 번호 '{p1}'는 자신의 흐름 번호 {flow}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                case AdvFlowType.Jump when !seen.Contains((advCode, p1)):
                    throw new PackFormatException($"{where} 점프할 흐름 번호 '{p1}'가 같은 ADV의 흐름에 없습니다.");
                case var jump when HaveTagJump.IsConditionalJump(jump.ToString()) && p3 <= flow:
                    throw new PackFormatException(
                        $"{where} 점프할 흐름 번호 '{p3}'는 자신의 흐름 번호 {flow}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                case var jump when HaveTagJump.IsConditionalJump(jump.ToString()) && !seen.Contains((advCode, p3)):
                    throw new PackFormatException($"{where} 점프할 흐름 번호 '{p3}'가 같은 ADV의 흐름에 없습니다.");
                case AdvFlowType.Choice when p2 <= flow:
                    throw new PackFormatException(
                        $"{where} 선택지가 점프할 흐름 번호 '{p2}'는 자신의 흐름 번호 {flow}보다 커야 합니다(뒤로만 점프할 수 있어 무한 반복이 생기지 않습니다).");
                case AdvFlowType.Choice when !seen.Contains((advCode, p2)):
                    throw new PackFormatException($"{where} 선택지가 점프할 흐름 번호 '{p2}'가 같은 ADV의 흐름에 없습니다.");
            }
        }

        // 같은 선택지 그룹(param_1)의 Choice 행은 흐름 번호 순으로 연속해야 한다.
        foreach (var byAdv in rows.GroupBy(r => r.Adv))
        {
            var started = new HashSet<long>();
            long? last = null;                          // 바로 앞 칸이 Choice일 때 그 선택지 그룹
            foreach (var (advCode, flow, type, p1, _, _, _, _) in byAdv.OrderBy(r => r.Flow))
            {
                if (type != AdvFlowType.Choice) { last = null; continue; }
                if (last != p1 && !started.Add(p1))
                    throw new PackFormatException(
                        $"{file}: ADV '{advCode}'의 흐름 {flow}의 선택지 그룹 '{p1}'가 연속하지 않습니다(같은 그룹의 선택지 사이에 다른 칸이 있습니다).");
                last = p1;
            }
        }

        return rows.Select(r => new AdvFlow(r.Adv, r.Flow, r.Type, r.P1, r.P2, r.P3, r.Text, r.ForceName)).ToList();
    }

    /// <summary>
    /// 대사의 꾸밈 문법(<see cref="RichText"/>)이 맞는지, 그리고 상자가 있는 종류는 상자(<see cref="AdvLayout"/>)에 들어가는지 본다:
    /// 줄 수가 <see cref="AdvLayout.BoxLines"/>를 넘으면 게임팩 로드가 실패한다(TextMe/TextYou는 <see cref="AdvLayout.BoxColumns"/>칸,
    /// 가로 전체 상자인 TextPlain은 <see cref="AdvLayout.PlainBoxColumns"/>칸 기준). TextLog는 상자가 없어 문법만 본다.
    /// </summary>
    static void CheckLine(string where, AdvFlowType type, string line)
    {
        var rich = RichText.TryParse(line, out var error);
        if (rich is null) throw new PackFormatException($"{where} text가 올바르지 않습니다: {error}");
        if (type == AdvFlowType.TextLog) return;
        var columns = type == AdvFlowType.TextPlain ? AdvLayout.PlainBoxColumns : AdvLayout.BoxColumns;
        var lines = rich.CountLines(columns);
        if (lines > AdvLayout.BoxLines)
            throw new PackFormatException(
                $"{where} text가 대사 상자에 들어가지 않습니다: {lines}줄입니다(최대 {AdvLayout.BoxLines}줄, 한 줄 {columns}칸, 한글은 2칸).");
    }

    /// <summary>선택지 문구도 꾸밈 문법(<see cref="RichText"/>)을 쓸 수 있다. 목록의 한 줄로 나오므로 줄바꿈(\n)은 쓸 수 없다.</summary>
    static void CheckChoiceText(string where, string text)
    {
        var rich = RichText.TryParse(text, out var error);
        if (rich is null) throw new PackFormatException($"{where} 선택지 text가 올바르지 않습니다: {error}");
        if (rich.Lines.Count > 1) throw new PackFormatException($"{where} 선택지 text에는 줄바꿈(\\n)을 쓸 수 없습니다.");
    }

    /// <summary>ADV의 흐름 칸 하나. 없으면 null.</summary>
    public AdvFlow? FindAdvFlow(long advCode, long flowNumber) =>
        AdvFlows?.FirstOrDefault(f => f.AdvCode == advCode && f.FlowNumber == flowNumber);

    /// <summary>한 ADV의 흐름 칸들(키 순서: 흐름 번호).</summary>
    public IReadOnlyList<AdvFlow> AdvFlowsOf(long advCode) =>
        (AdvFlows ?? []).Where(f => f.AdvCode == advCode).OrderBy(f => f.FlowNumber).ToList();
}
