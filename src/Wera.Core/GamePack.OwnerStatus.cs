using System.IO.Compression;

namespace Wera.Core;

/// <summary>스킬 스테이터스 마스터와 유니크 아이템 스테이터스 마스터의 한 행: 소유자(스킬 또는 유니크 아이템)의 스테이터스 초깃값과 범위.</summary>
public sealed record OwnerStatus(long OwnerCode, long StatusCode, long InitialValue, long MinValue, long MaxValue);

// 스킬과 유니크 아이템도 스테이터스를 가진다(스테이터스 종류는 캐릭터와 같은 status_master). 레벨별 행은 없고 예약 코드도 없다.
public sealed partial class GamePack
{
    public const string SkillStatusMasterPath = "master/skill_status_master_v1.tsv";
    public const string UniqueItemStatusMasterPath = "master/unique_item_status_master_v1.tsv";

    /// <summary>스킬 스테이터스 마스터(<c>OwnerCode</c>는 스킬 코드). 마스터가 없으면 null.</summary>
    public IReadOnlyList<OwnerStatus>? SkillStatuses { get; init; }

    /// <summary>유니크 아이템 스테이터스 마스터(<c>OwnerCode</c>는 유니크 아이템 코드). 마스터가 없으면 null.</summary>
    public IReadOnlyList<OwnerStatus>? UniqueItemStatuses { get; init; }

    /// <summary>
    /// 스킬/유니크 아이템 스테이터스 마스터: 소유자는 스킬 마스터/유니크 아이템 인벤토리 마스터에, 스테이터스는 스테이터스 마스터에 있어야 하고,
    /// 복합키 (소유자, 스테이터스)를 두 번 쓸 수 없다. 최솟값은 최댓값 이하, 초깃값은 최솟값과 최댓값 사이여야 한다. 값은 모두 long이다.
    /// </summary>
    static List<OwnerStatus>? LoadOwnerStatuses(
        ZipArchive zip, string file, string ownerColumn, string ownerLabel, string ownerMasterPath,
        IEnumerable<long>? owners, List<StatusEntry>? statuses)
    {
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (owners is null) throw new PackFormatException($"{file}가 있으려면 {ownerMasterPath}도 필요합니다.");
        if (statuses is null) throw new PackFormatException($"{file}가 있으려면 {StatusMasterPath}도 필요합니다.");

        var ownerCodes = owners.ToHashSet();
        var statusCodes = statuses.Select(s => s.Code).ToHashSet();
        var rows = new List<OwnerStatus>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, ownerColumn, "status_code", "initial_value", "min_value", "max_value"))
        {
            var owner = ParseLong(file, $"{ownerLabel} 코드", r[0]);
            var status = ParseLong(file, $"{ownerLabel} '{owner}'의 스테이터스 코드", r[1]);
            var who = $"{ownerLabel} '{owner}' 스테이터스 '{status}'의";
            var initial = ParseLong(file, $"{who} 초깃값", r[2]);
            var min = ParseLong(file, $"{who} 최솟값", r[3]);
            var max = ParseLong(file, $"{who} 최댓값", r[4]);

            if (!ownerCodes.Contains(owner))
                throw new PackFormatException($"{file}: {ownerLabel} '{owner}'가 {ownerMasterPath}에 없습니다.");
            if (!statusCodes.Contains(status))
                throw new PackFormatException($"{file}: {ownerLabel} '{owner}'의 스테이터스 '{status}'가 스테이터스 마스터에 없습니다.");
            if (min > max)
                throw new PackFormatException($"{file}: {who} 최솟값 {min}이(가) 최댓값 {max}보다 큽니다.");
            if (initial < min || initial > max)
                throw new PackFormatException($"{file}: {who} 초깃값 {initial}이(가) 최솟값 {min}~최댓값 {max} 범위 밖입니다.");
            if (!seen.Add((owner, status)))
                throw new PackFormatException($"{file}: 같은 키({ownerLabel} {owner}, 스테이터스 {status})가 두 번 있습니다.");
            rows.Add(new OwnerStatus(owner, status, initial, min, max));
        }
        return rows;
    }

    /// <summary>스킬에 적힌 스테이터스 행 전부(스테이터스 코드 순). 보조키 스킬 코드로 찾는다.</summary>
    public IReadOnlyList<OwnerStatus> SkillStatusesOf(long skillCode) =>
        Masters.SkillStatusRowTable.FindBySkillCode(skillCode).OrderBy(r => r.StatusCode)
            .Select(r => new OwnerStatus(r.SkillCode, r.StatusCode, r.InitialValue, r.MinValue, r.MaxValue)).ToList();

    /// <summary>유니크 아이템에 적힌 스테이터스 행 전부(스테이터스 코드 순). 보조키 아이템 코드로 찾는다.</summary>
    public IReadOnlyList<OwnerStatus> UniqueItemStatusesOf(long itemCode) =>
        Masters.UniqueItemStatusRowTable.FindByItemCode(itemCode).OrderBy(r => r.StatusCode)
            .Select(r => new OwnerStatus(r.ItemCode, r.StatusCode, r.InitialValue, r.MinValue, r.MaxValue)).ToList();

    /// <summary>스킬 스테이터스 마스터의 초깃값(스테이터스 코드 → 값).</summary>
    public Dictionary<long, long> MasterStatusesOfSkill(long skillCode) =>
        SkillStatusesOf(skillCode).ToDictionary(r => r.StatusCode, r => r.InitialValue);

    /// <summary>유니크 아이템 스테이터스 마스터의 초깃값(스테이터스 코드 → 값).</summary>
    public Dictionary<long, long> MasterStatusesOfUniqueItem(long itemCode) =>
        UniqueItemStatusesOf(itemCode).ToDictionary(r => r.StatusCode, r => r.InitialValue);

    /// <summary>새로 익히는 스킬: 스킬 스테이터스 마스터의 초깃값을 <see cref="SkillInstance.Statuses"/>로 복사한다.</summary>
    public SkillInstance NewSkillInstance(long skillCode) => new(skillCode, MasterStatusesOfSkill(skillCode));

    /// <summary>
    /// 유니크 아이템 인스턴스를 새로 만든다: 태그와 스테이터스는 마스터에서 복사하고, 마스터에 skill_code가 있으면 그 스킬의 인스턴스(스테이터스는 스킬 스테이터스 마스터의 초깃값)를 아이템이 가진다.
    /// 생성 후 서킷은 돌리지 않는다.
    /// </summary>
    public UniqueItemInstance NewUniqueItemInstance(string instanceId, long itemCode, long orderNumber) =>
        new(instanceId, itemCode, orderNumber, MasterTagsOfUniqueItem(itemCode).ToList(), MasterStatusesOfUniqueItem(itemCode),
            FindUniqueItem(itemCode)?.SkillCode is { } skill ? NewSkillInstance(skill) : null);
}
