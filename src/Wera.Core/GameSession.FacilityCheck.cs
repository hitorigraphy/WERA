namespace Wera.Core;

// 시설 확인: 아지트의 시설을 층마다 읽기만 하는 화면. 시설마다 오퍼레이터(슬롯, 선 캐릭터, 최대 방어력), 시설 방어력, 생산 내용을 보여 준다.
// 생산 내용은 시설 흐름(서브모듈로 가는 칸 포함)을 읽어 만든 정적인 설명이다: 난수를 돌려 보지 않는다.
public sealed partial class GameSession
{
    /// <summary>아지트 화면의 [시설 확인] 항목 코드(스크롤 1000, 1001 다음 번호).</summary>
    public const string FacilityCheckEntryCode = "1002";

    /// <summary>시설 확인 화면의 한 줄: 들여쓰기 단계(0 층 제목, 1 시설 제목, 2 내용)와 글.</summary>
    public readonly record struct FacilityCheckLine(int Indent, string Text);

    /// <summary>
    /// 시설 확인 화면의 글: 아지트 화면의 순서대로(0이 맨 위 층) 층마다 기본 시설, 지은 시설 차례다. 시설이 하나도 없는 층은 "시설 없음"이다.
    /// 시설마다 오퍼레이터 슬롯별로 "슬롯: 캐릭터 (방어력 n)"(비어 있으면 "비어 있음"), "시설 방어력", "생산"이 나온다. 시설 확인 화면이 아니면 빈 목록.
    /// </summary>
    public IReadOnlyList<FacilityCheckLine> FacilityCheckLines => Screen == Screen.FacilityCheck ? BuildFacilityCheckLines() : [];

    IReadOnlyList<FacilityCheckLine> BuildFacilityCheckLines()
    {
        var lines = new List<FacilityCheckLine>();
        for (var floor = 0; floor < HideoutFloorCount; floor++)
        {
            lines.Add(new FacilityCheckLine(0, $"{floor + 1}층"));
            var any = false;
            foreach (var built in new[] { false, true })
            {
                if (FacilityOfFloor(floor, built) is not { } facility) continue;
                any = true;
                lines.Add(new FacilityCheckLine(1, $"[{(built ? "지은 시설" : "기본 시설")}] {facility.Name}"));
                var slots = Pack.OperatorSlotsOf(facility.Code);
                if (slots.Count == 0) lines.Add(new FacilityCheckLine(2, "오퍼레이터: 슬롯 없음"));
                foreach (var slot in slots)
                {
                    var id = HideoutOperatorInstanceOf(floor, built, slot.Code);
                    lines.Add(new FacilityCheckLine(2, id is not null && Characters.TryGetValue(id, out var op)
                        ? $"오퍼레이터 {slot.Name}: {CharacterName(op.CharacterCode)} (최대 방어력 {Text(OperatorDefenseOf(id, slot.Code))})"
                        : $"오퍼레이터 {slot.Name}: 비어 있음 (최대 방어력 0)"));
                }
                lines.Add(new FacilityCheckLine(2, $"시설 방어력: {Text(Pack.FacilityDefenseOf(facility.Code))}"));
                lines.Add(new FacilityCheckLine(2, $"생산: {FacilityProductionText(facility.Code)}"));
            }
            if (!any) lines.Add(new FacilityCheckLine(1, "시설 없음"));
        }
        return lines;
    }

    /// <summary>
    /// 시설의 생산 내용을 시설 흐름에서 읽은 글: 흐름(JumpToSubmodule로 가는 서브모듈의 칸 포함)의 AddInventory를 "이름 +n"으로 이어 적고,
    /// 조건이나 확률로 달라질 수 있는 점프가 있으면 "(조건·확률에 따라 달라질 수 있음)", 오퍼레이터를 보는 점프가 있으면 "(오퍼레이터에 따라 달라질 수 있음)"을 덧붙인다.
    /// 흐름이 없거나 생산하는 AddInventory가 없으면 "생산 없음". 난수를 돌려 보지 않는다.
    /// </summary>
    public string FacilityProductionText(long facilityCode)
    {
        var flows = Pack.FacilityFlowsOf(facilityCode);
        if (flows.Count == 0) return "생산 없음";
        var all = new List<FacilityFlow>();
        foreach (var flow in flows)
        {
            all.Add(flow);
            if (flow.FlowType == FacilityFlowType.JumpToSubmodule && Pack.FacilitySubmodules is { } submodules && submodules.TryGetValue(flow.Param1, out var sub))
                all.AddRange(sub);
        }
        var items = all.Where(f => f.FlowType == FacilityFlowType.AddInventory)
            .Select(f => $"{Pack.FindItem(f.Param1)?.Name ?? Text(f.Param1)} {(f.Param2 > 0 ? "+" : "")}{Text(f.Param2)}").ToList();
        var notes = new List<string>();
        if (all.Any(f => f.FlowType is FacilityFlowType.JumpOperatorExists or FacilityFlowType.JumpOperatorNotExists or FacilityFlowType.JumpOperatorHaveTag))
            notes.Add("(오퍼레이터에 따라 달라질 수 있음)");
        if (all.Any(f => GamePack.IsFacilityConditionalJump(f.FlowType)
                && f.FlowType is not (FacilityFlowType.JumpOperatorExists or FacilityFlowType.JumpOperatorNotExists or FacilityFlowType.JumpOperatorHaveTag)))
            notes.Add("(조건·확률에 따라 달라질 수 있음)");
        if (items.Count == 0) return "생산 없음";
        return string.Join(", ", items) + (notes.Count == 0 ? "" : " " + string.Join(" ", notes));
    }
}
