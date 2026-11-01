using System.IO.Compression;

namespace Wera.Core;

/// <summary>유니크 아이템 강화 스테이터스 마스터의 한 행: 이 유니크 아이템의 강화 레벨이 EnhanceLevel일 때 이 스테이터스(StatusCode)에 더하는 값(Value).</summary>
public sealed record UniqueItemEnhanceStatus(long ItemCode, long StatusCode, long EnhanceLevel, long Value);

// unique_item_강화_status_master(item_code, status_code, 강화_level, value; 앞의 세 열이 키). 선택이다.
// 강화를 영어로 어떻게 쓸지 정하기 전까지 파일 이름과 열 이름에 한글 "강화"를 그대로 쓴다.
public sealed partial class GamePack
{
    public const string UniqueItemEnhanceStatusMasterPath = "master/unique_item_강화_status_master_v1.tsv";

    /// <summary>유니크 아이템 강화 스테이터스 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<UniqueItemEnhanceStatus>? UniqueItemEnhanceStatuses { get; init; }

    /// <summary>
    /// 유니크 아이템 강화 스테이터스 마스터: 아이템은 유니크 아이템 인벤토리 마스터에, 스테이터스는 스테이터스 마스터에 있어야 하고 그 아이템의
    /// 유니크 아이템 스테이터스 마스터 행(초깃값에 더해 저장하므로)도 있어야 한다. 키 (아이템, 스테이터스, 강화 레벨)는 두 번 쓸 수 없고 강화 레벨은 0 이상이다. 값은 long이다.
    /// </summary>
    static List<UniqueItemEnhanceStatus>? LoadUniqueItemEnhanceStatuses(
        ZipArchive zip, IEnumerable<long>? uniqueItemCodes, List<StatusEntry>? statuses, List<OwnerStatus>? uniqueItemStatuses, long? levelMax)
    {
        const string file = UniqueItemEnhanceStatusMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (uniqueItemCodes is null) throw new PackFormatException($"{file}가 있으려면 {UniqueItemInventoryMasterPath}도 필요합니다.");
        if (statuses is null) throw new PackFormatException($"{file}가 있으려면 {StatusMasterPath}도 필요합니다.");
        if (uniqueItemStatuses is null) throw new PackFormatException($"{file}가 있으려면 {UniqueItemStatusMasterPath}도 필요합니다.");

        var items = uniqueItemCodes.ToHashSet();
        var statusCodes = statuses.Select(s => s.Code).ToHashSet();
        var owned = uniqueItemStatuses.Select(s => (s.OwnerCode, s.StatusCode)).ToHashSet();
        var rows = new List<UniqueItemEnhanceStatus>();
        var seen = new HashSet<(long, long, long)>();
        foreach (var r in Body(file, text, "item_code", "status_code", "강화_level", "value"))
        {
            var item = ParseLong(file, "유니크 아이템 코드", r[0]);
            var status = ParseLong(file, $"유니크 아이템 '{item}'의 스테이터스 코드", r[1]);
            var who = $"유니크 아이템 '{item}' 스테이터스 '{status}'의";
            var level = ParseLong(file, $"{who} 강화 레벨", r[2]);
            var value = ParseLong(file, $"{who} 강화 레벨 {level}의 값", r[3]);
            if (!items.Contains(item))
                throw new PackFormatException($"{file}: 유니크 아이템 '{item}'가 {UniqueItemInventoryMasterPath}에 없습니다.");
            if (!statusCodes.Contains(status))
                throw new PackFormatException($"{file}: 유니크 아이템 '{item}'의 스테이터스 '{status}'가 스테이터스 마스터에 없습니다.");
            if (!owned.Contains((item, status)))
                throw new PackFormatException($"{file}: 유니크 아이템 '{item}'의 스테이터스 '{status}'가 {UniqueItemStatusMasterPath}에 없습니다.");
            if (level < 0)
                throw new PackFormatException($"{file}: {who} 강화 레벨 '{level}'은 0 이상이어야 합니다.");
            if (!seen.Add((item, status, level)))
                throw new PackFormatException($"{file}: 같은 키(유니크 아이템 {item}, 스테이터스 {status}, 강화 레벨 {level})가 두 번 있습니다.");
            rows.Add(new UniqueItemEnhanceStatus(item, status, level, value));
        }
        // 강화 레벨 상한이 있으면 행이 있는 (아이템, 스테이터스)는 강화 레벨 1~상한이 모두 적혀 있어야 한다(빠진 레벨은 데이터 버그).
        if (levelMax is { } max)
            foreach (var group in rows.GroupBy(r => (r.ItemCode, r.StatusCode)))
            {
                var levels = group.Select(r => r.EnhanceLevel).ToHashSet();
                for (var level = 1L; level <= max; level++)
                    if (!levels.Contains(level))
                        throw new PackFormatException($"{file}: 유니크 아이템 '{group.Key.ItemCode}' 스테이터스 '{group.Key.StatusCode}'에 강화 레벨 {level}의 행이 없습니다(강화 레벨 상한 {max}까지 모두 적어야 합니다).");
            }
        return rows;
    }

    /// <summary>
    /// 강화가 끝난 뒤 강화 레벨이 바뀌었다고 보고 스테이터스를 다시 정한다: 강화 스테이터스 마스터에 이 아이템의 행이 있는 스테이터스마다
    /// (유니크 아이템 스테이터스 마스터의 초깃값) + (아이템의 강화 레벨 = 강화 횟수 스테이터스의 값에 해당하는 행의 값, 그 레벨의 행이 없으면 0)을
    /// 그 스테이터스의 최솟값~최댓값에 맞춰 인스턴스에 저장한다. 강화 레벨이 상한(<see cref="EnhanceLevelMax"/>)을 넘으면 상한으로 자른다(강화 횟수도 상한으로 저장). 자를 것도 마스터에 이 아이템의 행도 없으면 인스턴스를 그대로 돌려준다.
    /// </summary>
    public UniqueItemInstance WithEnhanceStatuses(UniqueItemInstance item)
    {
        var rows = UniqueItemEnhanceStatuses?.Where(r => r.ItemCode == item.ItemCode).ToList() ?? [];
        var statuses = new Dictionary<long, long>(item.Statuses ?? MasterStatusesOfUniqueItem(item.ItemCode));
        var level = statuses.GetValueOrDefault(EnhanceCountStatusCode);
        if (EnhanceLevelMax is { } max && level > max)
        {
            level = max;                                                      // 상한을 넘기면 상한으로 자른다
            statuses[EnhanceCountStatusCode] = max;
        }
        else if (rows.Count == 0) return item;
        var master = UniqueItemStatusesOf(item.ItemCode).ToDictionary(r => r.StatusCode);
        foreach (var status in rows.Select(r => r.StatusCode).Distinct())
        {
            var add = rows.FirstOrDefault(r => r.StatusCode == status && r.EnhanceLevel == level)?.Value ?? 0;
            var row = master[status];
            statuses[status] = Math.Clamp(checked(row.InitialValue + add), row.MinValue, row.MaxValue);
        }
        return item with { Statuses = statuses };
    }
}
