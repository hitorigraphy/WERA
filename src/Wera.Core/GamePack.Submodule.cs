using System.IO.Compression;

namespace Wera.Core;

// 서브모듈: 서킷 종류마다 공통 동작을 한 번 정의해 두고 JumpToSubmodule 이펙트(공통 번호 32)로 불러 쓰는 함수 같은 것이다.
// 서킷 종류마다 서브모듈 마스터가 따로 있다(선택): master/<이펙트(흐름) 마스터 이름에서 _effect_master / _flow_master를 뗀 것>_submodule_master_v1.tsv.
// 열은 submodule_code, execute_number, effect_type(흐름 종류), value_1, value_2, value_3이고 키는 (submodule_code, execute_number)다.
// 종류와 인자 검증은 그 서킷 종류의 이펙트 마스터와 같다. 다만 서브모듈 안에서는 JumpToSubmodule을 쓸 수 없고(서브모듈이 서브모듈을 부를 수 없어 재귀가 없다),
// 점프는 같은 서브모듈 안의 자신보다 큰 execute_number로만 가며, Finish는 서브모듈만 끝내고 부른 곳으로 돌아간다.
// 서브모듈은 부른 서킷의 캐시, 포커스, 시전자와 타겟을 그대로 쓰므로 값은 캐시로 주고받는다(어느 칸을 쓸지는 게임팩이 약속한다).
public sealed partial class GamePack
{
    public const string AfterAttackSubmoduleMasterPath = "master/after_attack_submodule_master_v1.tsv";
    public const string AfterBattleLoseSubmoduleMasterPath = "master/after_battle_lose_submodule_master_v1.tsv";
    public const string AfterBattleRunSubmoduleMasterPath = "master/after_battle_run_submodule_master_v1.tsv";
    public const string AfterBattleWinSubmoduleMasterPath = "master/after_battle_win_submodule_master_v1.tsv";
    public const string AfterHitSubmoduleMasterPath = "master/after_hit_submodule_master_v1.tsv";
    public const string AfterLoadCharacterSubmoduleMasterPath = "master/after_load_character_submodule_master_v1.tsv";
    public const string AfterLoadUniqueItemSubmoduleMasterPath = "master/after_load_unique_item_submodule_master_v1.tsv";
    public const string CharacterAfterCreateSubmoduleMasterPath = "master/character_after_create_submodule_master_v1.tsv";
    public const string SkillExecuteSubmoduleMasterPath = "master/skill_execute_submodule_master_v1.tsv";
    public const string SkillAutoTargetSubmoduleMasterPath = "master/skill_auto_target_submodule_master_v1.tsv";
    public const string SkillOrderSubmoduleMasterPath = "master/skill_order_submodule_master_v1.tsv";
    public const string SmeltSubmoduleMasterPath = "master/smelt_submodule_master_v1.tsv";
    public const string SynthesisSubmoduleMasterPath = "master/synthesis_submodule_master_v1.tsv";
    public const string UniqueItemAfterCreateSubmoduleMasterPath = "master/unique_item_after_create_submodule_master_v1.tsv";
    public const string XpCalculateSubmoduleMasterPath = "master/xp_calculate_submodule_master_v1.tsv";

    /// <summary>서브모듈 마스터 경로 → (서브모듈 코드 → 이펙트들, 실행 번호 순). 마스터가 있는 서킷 종류만 들어 있다.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<long, IReadOnlyList<CircuitEffect>>> Submodules { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<long, IReadOnlyList<CircuitEffect>>>();

    /// <summary>서킷 종류의 서브모듈 마스터(경로)에 정의된 서브모듈들. 마스터가 없으면 null.</summary>
    public IReadOnlyDictionary<long, IReadOnlyList<CircuitEffect>>? SubmodulesOf(string masterPath) => Submodules.GetValueOrDefault(masterPath);

    /// <summary>
    /// 서브모듈 마스터 하나를 읽는다(없으면 null). 종류는 이름으로 적고(JumpToSubmodule은 쓸 수 없다) 값 셋은 비우면 0이다.
    /// <paramref name="checkRow"/>는 그 서킷 종류의 이펙트 마스터와 같은 종류별 인자 검증이고 <paramref name="jumpTarget"/>은 점프하는 종류의 점프 대상(아니면 null)이다.
    /// 점프 대상은 같은 서브모듈의, 자신보다 큰 execute_number여야 한다.
    /// </summary>
    static Dictionary<long, IReadOnlyList<CircuitEffect>>? LoadSubmodules<TType>(
        ZipArchive zip, string file, Action<string, string, TType, long, long, long> checkRow, Func<TType, long, long, long?> jumpTarget)
        where TType : struct, Enum
    {
        var text = TryReadEntry(zip, file);
        if (text is null) return null;

        var rows = new List<(long Code, CircuitEffect Effect, TType Type)>();
        var seen = new HashSet<(long, long)>();
        foreach (var r in Body(file, text, "submodule_code", "execute_number", "effect_type", "value_1", "value_2", "value_3"))
        {
            var code = ParseLong(file, "서브모듈 코드", r[0]);
            var who = $"서브모듈 '{code}'의";
            var number = ParseLong(file, $"{who} 실행 번호", r[1]);
            var where = $"{who} 실행 {number}의";
            // 숫자("0")도 Enum.TryParse를 통과하므로 이름 목록으로 먼저 거른다.
            if (!Enum.GetNames<TType>().Contains(r[2], StringComparer.OrdinalIgnoreCase)
                || !Enum.TryParse<TType>(r[2], ignoreCase: true, out var type))
                throw new PackFormatException(
                    $"{file}: {where} 이펙트 종류 '{r[2]}'을(를) 알 수 없습니다. ({string.Join(", ", Enum.GetNames<TType>())})");
            var value1 = r[3].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_1", r[3]);
            var value2 = r[4].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_2", r[4]);
            var value3 = r[5].Trim().Length == 0 ? 0 : ParseLong(file, $"{where} value_3", r[5]);
            if (!seen.Add((code, number)))
                throw new PackFormatException($"{file}: 서브모듈 '{code}'의 실행 번호 '{number}'가 두 번 적혀 있습니다.");
            if (Convert.ToInt32(type) == (int)EffectType.JumpToSubmodule)
                throw new PackFormatException($"{file}: {where} 서브모듈 안에서는 JumpToSubmodule을 쓸 수 없습니다(서브모듈이 서브모듈을 부를 수 없습니다).");
            checkRow(file, where, type, value1, value2, value3);
            rows.Add((code, new CircuitEffect(number, Convert.ToInt32(type), value1, value2, value3), type));
        }

        // 점프는 같은 서브모듈에 있는, 자신보다 큰 execute_number로만 갈 수 있다.
        foreach (var (code, e, type) in rows)
            if (jumpTarget(type, e.Value1, e.Value3) is { } target)
            {
                var where = $"서브모듈 '{code}'의 실행 {e.ExecuteNumber}의";
                if (target <= e.ExecuteNumber)
                    throw new PackFormatException($"{file}: {where} 점프 대상 '{target}'는 자신의 실행 번호보다 커야 합니다.");
                if (!seen.Contains((code, target)))
                    throw new PackFormatException($"{file}: {where} 점프 대상 '{target}'가 같은 서브모듈에 없습니다.");
            }
        return rows.GroupBy(r => r.Code).ToDictionary(g => g.Key,
            g => (IReadOnlyList<CircuitEffect>)g.Select(r => r.Effect).OrderBy(e => e.ExecuteNumber).ToList());
    }

    /// <summary>
    /// 이펙트 마스터의 JumpToSubmodule 행(value_1이 서브모듈 코드)이 가리키는 서브모듈이 그 서킷 종류의 서브모듈 마스터에 있는지 본다.
    /// 마스터가 아예 없으면 부를 곳이 없으므로 오류다.
    /// </summary>
    static void CheckSubmoduleCalls(string mainFile, string submoduleFile, IReadOnlyDictionary<long, IReadOnlyList<CircuitEffect>>? catalog,
        IEnumerable<(string Where, int Type, long Value1)>? rows)
    {
        foreach (var (where, type, code) in rows ?? [])
        {
            if (type != (int)EffectType.JumpToSubmodule) continue;
            if (catalog is null) throw new PackFormatException($"{mainFile}: {where} JumpToSubmodule에는 {submoduleFile}가 필요합니다.");
            if (!catalog.ContainsKey(code))
                throw new PackFormatException($"{mainFile}: {where} JumpToSubmodule의 서브모듈 '{code}'가 {submoduleFile}에 없습니다.");
        }
    }

    static Func<TType, long, long, long?> DefaultJump<TType>() where TType : struct, Enum =>
        (type, v1, v3) => EffectArgs.JumpTargetOf((EffectType)Convert.ToInt32(type), v1, v3);

    /// <summary>모든 서킷 종류의 서브모듈 마스터를 읽고, 이펙트 마스터의 JumpToSubmodule 호출이 가리키는 서브모듈이 있는지 확인한다.</summary>
    static Dictionary<string, IReadOnlyDictionary<long, IReadOnlyList<CircuitEffect>>> LoadAllSubmodules(
        ZipArchive zip, EffectCheck check,
        List<AfterAttackEffect>? afterAttack, List<AfterBattleLoseEffect>? afterBattleLose, List<AfterBattleRunEffect>? afterBattleRun,
        List<AfterBattleWinEffect>? afterBattleWin, List<AfterHitEffect>? afterHit, List<LoadCharacterFlow>? afterLoadCharacter,
        List<LoadUniqueItemFlow>? afterLoadUniqueItem, List<CharacterAfterCreateEffect>? characterAfterCreate,
        List<SkillExecuteEffect>? skillExecute, List<SkillAutoTargetEffect>? skillAutoTarget, List<SkillOrderEffect>? skillOrder,
        List<SmeltFlow>? smelt, List<SynthesisFlow>? synthesis, List<UniqueItemAfterCreateEffect>? uniqueItemAfterCreate,
        List<XpCalculateEffect>? xpCalculate)
    {
        var all = new Dictionary<string, IReadOnlyDictionary<long, IReadOnlyList<CircuitEffect>>>();

        // 종류마다 (서브모듈 마스터, 이펙트 마스터의 호출 행)을 읽어 등록하고 호출을 확인한다.
        void Kind<TType>(string path, string mainFile, Action<string, string, TType, long, long, long> checkRow, Func<TType, long, long, long?> jump,
            IEnumerable<(string Where, int Type, long Value1)>? calls) where TType : struct, Enum
        {
            var catalog = LoadSubmodules(zip, path, checkRow, jump);
            if (catalog is not null) all[path] = catalog;
            CheckSubmoduleCalls(mainFile, path, catalog, calls);
        }

        void CheckStandard(string file, string where, EffectType type, long v1, long v2, long v3) => EffectArgs.Check(file, where, type, v1, v2, v3, check);

        Kind<AfterAttackEffectType>(AfterAttackSubmoduleMasterPath, AfterAttackEffectMasterPath,
            (f, w, t, a, b, c) => CheckStandard(f, w, (EffectType)(int)t, a, b, c), DefaultJump<AfterAttackEffectType>(),
            afterAttack?.Select(e => ($"실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<AfterBattleLoseEffectType>(AfterBattleLoseSubmoduleMasterPath, AfterBattleLoseEffectMasterPath,
            (f, w, t, a, b, c) => { if ((int)t < 33) CheckStandard(f, w, (EffectType)(int)t, a, b, c); },
            (t, a, c) => t == AfterBattleLoseEffectType.JumpNotInDungeon ? a : EffectArgs.JumpTargetOf((EffectType)(int)t, a, c),
            afterBattleLose?.Select(e => ($"실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<AfterBattleRunEffectType>(AfterBattleRunSubmoduleMasterPath, AfterBattleRunEffectMasterPath,
            (f, w, t, a, b, c) => CheckStandard(f, w, (EffectType)(int)t, a, b, c), DefaultJump<AfterBattleRunEffectType>(),
            afterBattleRun?.Select(e => ($"실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<AfterBattleWinEffectType>(AfterBattleWinSubmoduleMasterPath, AfterBattleWinEffectMasterPath,
            (f, w, t, a, b, c) => CheckStandard(f, w, (EffectType)(int)t, a, b, c), DefaultJump<AfterBattleWinEffectType>(),
            afterBattleWin?.Select(e => ($"실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<AfterHitEffectType>(AfterHitSubmoduleMasterPath, AfterHitEffectMasterPath,
            (f, w, t, a, b, c) => CheckStandard(f, w, (EffectType)(int)t, a, b, c), DefaultJump<AfterHitEffectType>(),
            afterHit?.Select(e => ($"실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<LoadCharacterFlowType>(AfterLoadCharacterSubmoduleMasterPath, AfterLoadCharacterFlowMasterPath,
            (f, w, t, a, b, c) => CheckStandard(f, w, (EffectType)(int)t, a, b, c), DefaultJump<LoadCharacterFlowType>(),
            afterLoadCharacter?.Select(e => ($"실행 {e.ExecuteNumber}의", (int)e.FlowType, e.Value1)));
        Kind<LoadUniqueItemFlowType>(AfterLoadUniqueItemSubmoduleMasterPath, AfterLoadUniqueItemFlowMasterPath,
            (f, w, t, a, b, c) => CheckStandard(f, w, (EffectType)(int)t, a, b, c), DefaultJump<LoadUniqueItemFlowType>(),
            afterLoadUniqueItem?.Select(e => ($"실행 {e.ExecuteNumber}의", (int)e.FlowType, e.Value1)));
        Kind<CharacterAfterCreateEffectType>(CharacterAfterCreateSubmoduleMasterPath, CharacterAfterCreateEffectMasterPath,
            (f, w, t, a, b, c) => EffectArgs.CheckCharacterCreate(f, w, t, a, b, c, check), DefaultJump<CharacterAfterCreateEffectType>(),
            characterAfterCreate?.Select(e => ($"캐릭터 '{e.CharacterCode}'의 실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<SkillExecuteEffectType>(SkillExecuteSubmoduleMasterPath, SkillExecuteEffectMasterPath,
            (f, w, t, a, b, c) => EffectArgs.CheckSkillExecute(f, w, t, a, b, c, check), DefaultJump<SkillExecuteEffectType>(),
            skillExecute?.Select(e => ($"스킬 '{e.SkillCode}'의 실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<AutoTargetEffectType>(SkillAutoTargetSubmoduleMasterPath, SkillAutoTargetEffectMasterPath,
            (f, w, t, a, b, c) =>
            {
                if (t == AutoTargetEffectType.SubTargetToResult)
                {
                    if (a < 0) throw new PackFormatException($"{f}: {w} value_1(후보 번호) '{a}'는 0 이상이어야 합니다.");
                }
                else CheckStandard(f, w, (EffectType)(int)t, a, b, c);
            }, DefaultJump<AutoTargetEffectType>(),
            skillAutoTarget?.Select(e => ($"스킬 '{e.SkillCode}'의 실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<OrderEffectType>(SkillOrderSubmoduleMasterPath, SkillOrderEffectMasterPath,
            (f, w, t, a, b, c) =>
            {
                if (t == OrderEffectType.CacheToResult) EffectArgs.CheckCacheIndex(f, w, a, "value_1");
                else CheckStandard(f, w, (EffectType)(int)t, a, b, c);
            }, DefaultJump<OrderEffectType>(),
            skillOrder?.Select(e => ($"스킬 '{e.SkillCode}'의 실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<SmeltFlowType>(SmeltSubmoduleMasterPath, SmeltFlowMasterPath,
            (f, w, t, a, b, c) => EffectArgs.CheckSmelt(f, w, t, a, b, c, check), DefaultJump<SmeltFlowType>(),
            smelt?.Select(e => ($"실행 {e.ExecuteNumber}의", (int)e.FlowType, e.Value1)));
        Kind<SynthesisFlowType>(SynthesisSubmoduleMasterPath, SynthesisFlowMasterPath,
            (f, w, t, a, b, c) => EffectArgs.CheckSynthesis(f, w, t, a, b, c, check), DefaultJump<SynthesisFlowType>(),
            synthesis?.Select(e => ($"실행 {e.ExecuteNumber}의", (int)e.FlowType, e.Value1)));
        Kind<UniqueItemAfterCreateEffectType>(UniqueItemAfterCreateSubmoduleMasterPath, UniqueItemAfterCreateEffectMasterPath,
            (f, w, t, a, b, c) => CheckStandard(f, w, (EffectType)(int)t, a, b, c), DefaultJump<UniqueItemAfterCreateEffectType>(),
            uniqueItemAfterCreate?.Select(e => ($"유니크 아이템 '{e.ItemCode}'의 실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        Kind<XpCalculateEffectType>(XpCalculateSubmoduleMasterPath, XpCalculateEffectMasterPath,
            (f, w, t, a, b, c) => CheckStandard(f, w, (EffectType)(int)t, a, b, c), DefaultJump<XpCalculateEffectType>(),
            xpCalculate?.Select(e => ($"경험치 종류 '{e.XpTypeCode}'의 실행 {e.ExecuteNumber}의", (int)e.EffectType, e.Value1)));
        return all;
    }
}
