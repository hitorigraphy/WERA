using System.IO.Compression;

namespace Wera.Core;

/// <summary>아지트 확장 마스터의 한 행: ExpandCount번째 확장에 GroupCode 그룹이 요구하는 커먼 아이템(ItemCode) Amount개. 그룹 안은 모두(AND), 그룹끼리는 하나만(OR) 갖추면 된다.</summary>
public sealed record HideoutExpandCost(long ExpandCount, long GroupCode, long ItemCode, long Amount);

/// <summary>한 번의 확장에 쓸 수 있는 재료 한 벌(그룹): 그룹 코드와 그 재료들(파일 순서).</summary>
public sealed record HideoutExpandGroup(long GroupCode, IReadOnlyList<HideoutExpandCost> Costs);

// hideout_expand_master(expand_count, group_code, item_code; 세 열이 키, amount). 선택이다. 첫 확장이 expand_count 1이고, 다음 번호의 행이 없으면 더 확장할 수 없다.
public sealed partial class GamePack
{
    public const string HideoutExpandMasterPath = "master/hideout_expand_master_v1.tsv";

    /// <summary>아지트 확장 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<HideoutExpandCost>? HideoutExpandCosts { get; init; }

    /// <summary>expandCount번째 확장에 쓸 수 있는 재료 그룹들(그룹 코드는 파일에 처음 나온 순서). 없으면 더 확장할 수 없다.</summary>
    public IReadOnlyList<HideoutExpandGroup> HideoutExpandGroupsOf(long expandCount) =>
        (HideoutExpandCosts ?? []).Where(c => c.ExpandCount == expandCount).GroupBy(c => c.GroupCode)
            .Select(g => new HideoutExpandGroup(g.Key, g.ToList())).ToList();

    /// <summary>아지트 확장 마스터: 확장 횟수는 1 이상, 아이템은 커먼 아이템 인벤토리에 있어야 하고 개수는 1 이상이다. 세 열이 키라 같은 키를 두 번 쓸 수 없다.</summary>
    static List<HideoutExpandCost>? LoadHideoutExpandCosts(ZipArchive zip, List<ItemEntry>? items)
    {
        const string file = HideoutExpandMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (items is null) throw new PackFormatException($"{file}가 있으려면 {CommonItemInventoryMasterPath}도 필요합니다.");

        var itemCodes = items.Select(i => i.Code).ToHashSet();
        var rows = new List<HideoutExpandCost>();
        var seen = new HashSet<(long, long, long)>();
        foreach (var r in Body(file, text, "expand_count", "group_code", "item_code", "amount"))
        {
            var count = ParseLong(file, "확장 횟수", r[0]);
            var who = $"확장 {count}회째의";
            var group = ParseLong(file, $"{who} 그룹 코드", r[1]);
            var item = ParseLong(file, $"{who} 그룹 '{group}'의 아이템 코드", r[2]);
            var amount = ParseLong(file, $"{who} 그룹 '{group}' 아이템 '{item}'의 개수", r[3]);
            if (count < 1)
                throw new PackFormatException($"{file}: 확장 횟수 '{count}'는 1 이상이어야 합니다.");
            if (!itemCodes.Contains(item))
                throw new PackFormatException($"{file}: {who} 그룹 '{group}'의 아이템 '{item}'가 {CommonItemInventoryMasterPath}에 없습니다.");
            if (amount < 1)
                throw new PackFormatException($"{file}: {who} 그룹 '{group}' 아이템 '{item}'의 개수 '{amount}'는 1 이상이어야 합니다.");
            if (!seen.Add((count, group, item)))
                throw new PackFormatException($"{file}: 같은 키(확장 {count}회, 그룹 {group}, 아이템 {item})가 두 번 있습니다.");
            rows.Add(new HideoutExpandCost(count, group, item, amount));
        }
        return rows;
    }
}
