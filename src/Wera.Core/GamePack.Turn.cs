using System.IO.Compression;

namespace Wera.Core;

/// <summary>시간대 마스터의 한 행(예: 아침, 낮, 저녁, 밤).</summary>
public sealed record TimeSlotEntry(long Code, string Name);

/// <summary>
/// 턴 마스터의 한 행: 이 턴(TurnCount)의 게임상 날짜(Year, Month, Day)와 시간대(TimeSlotCode).
/// 게임은 가장 작은 턴에서 시작하고, 턴이 넘어가면 다음으로 큰 TurnCount의 날짜와 시간대로 바뀐다.
/// </summary>
public sealed record TurnEntry(long TurnCount, long Year, long Month, long Day, long TimeSlotCode);

// 턴과 시간대: 시간대 마스터(time_slot_master)와 턴 마스터(turn_master).
public sealed partial class GamePack
{
    public const string TimeSlotMasterPath = "master/time_slot_master_v1.tsv";
    public const string TurnMasterPath = "master/turn_master_v1.tsv";

    /// <summary>시간대 마스터. 필수 마스터라 로드된 게임팩에는 늘 있다.</summary>
    public IReadOnlyList<TimeSlotEntry>? TimeSlots { get; init; }

    /// <summary>턴 마스터(게임팩 파일 순서). 필수 마스터라 로드된 게임팩에는 늘 한 행 이상 있다.</summary>
    public IReadOnlyList<TurnEntry>? Turns { get; init; }

    static List<TimeSlotEntry>? LoadTimeSlots(ZipArchive zip) =>
        LoadCodeNames(zip, TimeSlotMasterPath, "code", "name", "시간대", (code, name) => new TimeSlotEntry(code, name));

    /// <summary>
    /// 턴 마스터: 턴 카운트가 키라 같은 턴을 두 번 쓸 수 없고, 시간대는 시간대 마스터에 있어야 하며, 한 행 이상 있어야 한다
    /// (게임이 가장 작은 턴에서 시작한다). 날짜는 게임팩이 정하는 달력이라 범위를 검증하지 않는다.
    /// </summary>
    static List<TurnEntry>? LoadTurns(ZipArchive zip, List<TimeSlotEntry>? timeSlots)
    {
        const string file = TurnMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (timeSlots is null) throw new PackFormatException($"{file}가 있으려면 {TimeSlotMasterPath}도 필요합니다.");

        var slotCodes = timeSlots.Select(s => s.Code).ToHashSet();
        var rows = new List<TurnEntry>();
        var seen = new HashSet<long>();
        foreach (var r in Body(file, text, "turn_count", "year", "month", "day", "time_slot_code"))
        {
            var turn = ParseLong(file, "턴 카운트", r[0]);
            var who = $"턴 '{turn}'의";
            var year = ParseLong(file, $"{who} 연도", r[1]);
            var month = ParseLong(file, $"{who} 월", r[2]);
            var day = ParseLong(file, $"{who} 일", r[3]);
            var slot = ParseLong(file, $"{who} 시간대 코드", r[4]);
            if (!seen.Add(turn))
                throw new PackFormatException($"{file}: 턴 카운트 '{turn}'가 중복됩니다.");
            if (!slotCodes.Contains(slot))
                throw new PackFormatException($"{file}: {who} 시간대 '{slot}'가 시간대 마스터에 없습니다.");
            rows.Add(new TurnEntry(turn, year, month, day, slot));
        }
        if (rows.Count == 0)
            throw new PackFormatException($"{file}에 턴이 한 개도 없습니다(게임은 가장 작은 턴에서 시작합니다).");
        return rows;
    }

    // ---- 키로 찾기 ----

    public TimeSlotEntry? FindTimeSlot(long code) =>
        Masters.TimeSlotRowTable.TryFindByCode(code, out var r) ? new TimeSlotEntry(r.Code, r.Name) : null;

    public TurnEntry? FindTurn(long turnCount) =>
        Masters.TurnRowTable.TryFindByTurnCount(turnCount, out var r)
            ? new TurnEntry(r.TurnCount, r.Year, r.Month, r.Day, r.TimeSlotCode) : null;

    /// <summary>게임이 시작하는 턴: 가장 작은 턴 카운트.</summary>
    public TurnEntry FirstTurn
    {
        get
        {
            var r = Masters.TurnRowTable.All.First();       // 기본키(턴 카운트) 순서의 첫 행
            return new TurnEntry(r.TurnCount, r.Year, r.Month, r.Day, r.TimeSlotCode);
        }
    }

    /// <summary>이 턴 다음 턴(다음으로 큰 턴 카운트). 마지막 턴이면 null.</summary>
    public TurnEntry? NextTurnAfter(long turnCount)
    {
        foreach (var r in Masters.TurnRowTable.All)
            if (r.TurnCount > turnCount)
                return new TurnEntry(r.TurnCount, r.Year, r.Month, r.Day, r.TimeSlotCode);
        return null;
    }

    /// <summary>"1년 1월 1일 아침" 같은 표시. 시간대가 마스터에 없으면 코드 그대로.</summary>
    public string DateText(TurnEntry turn) =>
        $"{turn.Year}년 {turn.Month}월 {turn.Day}일 {FindTimeSlot(turn.TimeSlotCode)?.Name ?? CodeText(turn.TimeSlotCode)}";
}
