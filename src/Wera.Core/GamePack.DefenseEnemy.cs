using System.IO.Compression;

namespace Wera.Core;

/// <summary>
/// 방어전 적 파티 마스터의 한 행: 방어전에 쳐들어올 적 파티(프리셋 파티)와 등장 규칙.
/// 위협도가 MinThreat 이상이어야 후보가 되고, 등장하면 위협도를 ConsumeThreat만큼 잃는다.
/// Priority가 작은 행부터 검사하고, 후보여도 Chance(%)의 확률을 통과해야 등장한다.
/// </summary>
/// <param name="HideoutCode">이 행을 쓰는 아지트(null이면 모든 아지트가 함께 쓴다).</param>
public sealed record DefenseEnemy(long Code, long PresetPartyCode, long MinThreat, long ConsumeThreat, long Priority, long Chance, long? HideoutCode = null);

// hideout_defense_enemy_master(code, preset_party_code, min_threat, consume_threat, priority, chance, 선택 열 hideout_code; (hideout_code, code)가 키). 선택이다.
// 위협도는 게임 설정 ThreatFlagCode가 가리키는 플레이어 플래그의 값이다(이벤트·ADV의 SetFlag/ModFlag로 올린다. 위협도는 아지트와 상관없이 하나다).
// priority는 아지트가 실제로 쓰는 행들(공용 행 + 그 아지트 전용 행) 안에서 겹칠 수 없다. 전용 행이 같은 code를 적으면 그 아지트에서는 전용 행이 공용 행을 대신한다.
public sealed partial class GamePack
{
    public const string DefenseEnemyMasterPath = "master/hideout_defense_enemy_master_v1.tsv";

    /// <summary>방어전 적 파티 마스터(게임팩 파일 순서). 마스터가 없으면 null.</summary>
    public IReadOnlyList<DefenseEnemy>? DefenseEnemies { get; init; }

    /// <summary>이 아지트의 방어전 적 파티 행들(파일 순서): 공용 행과 그 아지트 전용 행(같은 code는 전용 행이 대신한다).</summary>
    public IReadOnlyList<DefenseEnemy> DefenseEnemiesOf(long hideoutCode) =>
        ForHideout(DefenseEnemies ?? [], hideoutCode, e => e.HideoutCode, e => e.Code);

    /// <summary>방어전 적 파티 마스터: 프리셋 파티는 프리셋 파티 마스터에 있어야 하고, 게임 설정 ThreatFlagCode가 있어야 한다.
    /// min_threat·consume_threat는 0 이상, chance는 0~100(%), priority는 아지트마다 쓰는 행들 안에서 겹칠 수 없다.</summary>
    static List<DefenseEnemy>? LoadDefenseEnemies(ZipArchive zip, List<PresetParty>? parties, List<GameSettingEntry>? settings, IReadOnlySet<long>? hideoutCodes)
    {
        const string file = DefenseEnemyMasterPath;
        var text = TryReadEntry(zip, file);
        if (text is null) return null;
        if (parties is null) throw new PackFormatException($"{file}가 있으려면 {PresetPartyMasterPath}도 필요합니다.");
        if (settings?.Any(s => s.SettingType == GameSettingType.ThreatFlagCode) != true)
            throw new PackFormatException($"{file}가 있으려면 {GameSettingMasterPath}에 ThreatFlagCode(위협도 플래그)가 필요합니다.");

        var partyCodes = parties.Select(p => p.Code).ToHashSet();
        var rows = new List<DefenseEnemy>();
        var codes = new HashSet<(long?, long)>();
        foreach (var (r, hideout) in HideoutBody(file, text, hideoutCodes, "code", "preset_party_code", "min_threat", "consume_threat", "priority", "chance"))
        {
            var code = ParseLong(file, "코드", r[0]);
            var who = $"방어전 적 '{code}'의";
            var party = ParseLong(file, $"{who} 프리셋 파티 코드", r[1]);
            var min = ParseLong(file, $"{who} min_threat", r[2]);
            var consume = ParseLong(file, $"{who} consume_threat", r[3]);
            var priority = ParseLong(file, $"{who} priority", r[4]);
            var chance = ParseLong(file, $"{who} chance", r[5]);
            if (!codes.Add((hideout, code)))
                throw new PackFormatException($"{file}: {HideoutLabel(hideout)}코드 '{code}'가 두 번 있습니다.");
            if (!partyCodes.Contains(party))
                throw new PackFormatException($"{file}: {who} 프리셋 파티 '{party}'가 {PresetPartyMasterPath}에 없습니다.");
            if (min < 0) throw new PackFormatException($"{file}: {who} min_threat '{min}'은(는) 0 이상이어야 합니다.");
            if (consume < 0) throw new PackFormatException($"{file}: {who} consume_threat '{consume}'은(는) 0 이상이어야 합니다.");
            if (chance is < 0 or > 100) throw new PackFormatException($"{file}: {who} chance '{chance}'은(는) 0~100(%)이어야 합니다.");
            rows.Add(new DefenseEnemy(code, party, min, consume, priority, chance, hideout));
        }

        // priority는 아지트가 실제로 쓰는 행들(공용 + 그 아지트 전용) 안에서 겹칠 수 없다. 아지트 마스터가 없으면 아지트 하나가 공용 행만 쓴다.
        foreach (var hideout in hideoutCodes ?? (IEnumerable<long>)[DefaultHideoutCode])
        {
            var priorities = new HashSet<long>();
            foreach (var e in ForHideout(rows, hideout, row => row.HideoutCode, row => row.Code))
                if (!priorities.Add(e.Priority))
                    throw new PackFormatException($"{file}: 방어전 적 '{e.Code}'의 priority '{e.Priority}'가 두 번 있습니다(priority는 겹칠 수 없습니다)."
                        + (hideoutCodes is null ? "" : $" (아지트 '{hideout}')"));
        }
        return rows;
    }
}
