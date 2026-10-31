namespace Wera.Core;

/// <summary>이펙트 행의 인자를 로드할 때 맞춰 볼 마스터의 코드들. 마스터가 없으면 null이다.</summary>
public sealed record EffectCheck(IReadOnlySet<long>? Statuses, IReadOnlySet<long>? EquipmentSlots, IReadOnlySet<long>? SkillSlots,
    IReadOnlySet<long>? Skills = null, IReadOnlySet<long>? UniqueItems = null);

/// <summary>모든 이펙트 마스터(스킬 실행, 순서, 피격 후, ..., 생성 후)가 함께 쓰는 이펙트 종류의 인자 규칙과 점프 규칙.</summary>
public static class EffectArgs
{
    const string JumpPrefix = "JumpCache";

    /// <summary>JumpCache* 종류의 비교 방법. 그 종류가 아니면 null.</summary>
    public static FlagComparison? JumpComparison(EffectType type) =>
        type.ToString().StartsWith(JumpPrefix, StringComparison.Ordinal)
        && Enum.TryParse<FlagComparison>(type.ToString()[JumpPrefix.Length..], out var comparison) ? comparison : null;

    /// <summary>점프할 execute_number(Jump는 value_1, JumpCache*는 value_3). 점프하는 종류가 아니면 null.</summary>
    public static long? JumpTargetOf(EffectType type, long value1, long value3) =>
        type == EffectType.Jump ? value1 : JumpComparison(type) is not null ? value3 : null;

    /// <summary>cache_index가 0~<see cref="GamePack.MaxCacheIndex"/>인지 본다.</summary>
    public static void CheckCacheIndex(string file, string where, long index, string column)
    {
        if (index < 0 || index > GamePack.MaxCacheIndex)
            throw new PackFormatException($"{file}: {where} {column}(cache_index) '{index}'는 0~{GamePack.MaxCacheIndex}이어야 합니다.");
    }

    static void SubTargetIndex(string file, string where, long index, string column)
    {
        if (index < 0)
            throw new PackFormatException($"{file}: {where} {column}(서브 타겟 번호) '{index}'는 0 이상이어야 합니다.");
    }

    static void Slot(string file, string where, EffectType type, IReadOnlySet<long>? slots, string masterPath, string kind, long code, string column)
    {
        if (slots is null) throw new PackFormatException($"{file}: {where} {type}에는 {masterPath}가 필요합니다.");
        if (!slots.Contains(code))
            throw new PackFormatException($"{file}: {where} {column}({kind} 코드) '{code}'가 {kind} 마스터에 없습니다.");
    }

    /// <summary>
    /// 캐릭터 생성 후 이펙트 한 행의 인자를 검사한다. 생성 후에만 있는 종류(AddSkillToPool, AddEquipment, SetSkillToSlot, SetEquipmentToSlot)는
    /// value_1이 각각 스킬 마스터, 유니크 아이템 마스터, 스킬 슬롯 마스터, 장비 슬롯 마스터의 코드여야 하고, 나머지 종류는 <see cref="Check"/>를 따른다.
    /// </summary>
    public static void CheckCharacterCreate(string file, string where, CharacterAfterCreateEffectType type, long value1, long value2, long value3, EffectCheck check)
    {
        switch (type)
        {
            case CharacterAfterCreateEffectType.AddSkillToPool:
                Known(file, where, type, check.Skills, GamePack.SkillMasterPath, "스킬", value1);
                break;
            case CharacterAfterCreateEffectType.AddEquipment:
                Known(file, where, type, check.UniqueItems, GamePack.UniqueItemInventoryMasterPath, "유니크 아이템", value1);
                break;
            case CharacterAfterCreateEffectType.SetSkillToSlot:
                Known(file, where, type, check.SkillSlots, GamePack.SkillSlotMasterPath, "스킬 슬롯", value1);
                break;
            case CharacterAfterCreateEffectType.SetEquipmentToSlot:
                Known(file, where, type, check.EquipmentSlots, GamePack.EquipmentSlotMasterPath, "장비 슬롯", value1);
                break;
            default:
                Check(file, where, (EffectType)(int)type, value1, value2, value3, check);
                break;
        }
    }

    static void Known(string file, string where, CharacterAfterCreateEffectType type, IReadOnlySet<long>? codes, string masterPath, string kind, long code)
    {
        if (codes is null) throw new PackFormatException($"{file}: {where} {type}에는 {masterPath}가 필요합니다.");
        if (!codes.Contains(code))
            throw new PackFormatException($"{file}: {where} value_1({kind} 코드) '{code}'가 {kind} 마스터에 없습니다.");
    }

    /// <summary>
    /// 이펙트 한 행의 인자를 종류에 맞게 검사한다. 포커스 종류는 서브 타겟 번호(0 이상)와 슬롯 코드(장비 슬롯 마스터, 스킬 슬롯 마스터에 있어야 함)를,
    /// 포커스한 것과 캐시를 주고받는 종류는 value_1이 스테이터스 마스터의 코드, value_2가 cache_index를, 캐시 계산과 JumpCache*는 cache_index를 본다.
    /// 점프 대상은 모든 행을 읽은 뒤에 따로 확인한다.
    /// </summary>
    public static void Check(string file, string where, EffectType type, long value1, long value2, long value3, EffectCheck check)
    {
        switch (type)
        {
            case EffectType.None:
            case EffectType.Finish:
            case EffectType.Jump:
            case EffectType.FocusCasterCharacter:
            case EffectType.FocusMainTargetCharacter:
                break;
            case EffectType.FocusSubTargetCharacter:
                SubTargetIndex(file, where, value1, "value_1");
                break;
            case EffectType.FocusCasterEquipment:
            case EffectType.FocusMainTargetEquipment:
                Slot(file, where, type, check.EquipmentSlots, GamePack.EquipmentSlotMasterPath, "장비 슬롯", value1, "value_1");
                break;
            case EffectType.FocusSubTargetEquipment:
                SubTargetIndex(file, where, value1, "value_1");
                Slot(file, where, type, check.EquipmentSlots, GamePack.EquipmentSlotMasterPath, "장비 슬롯", value2, "value_2");
                break;
            case EffectType.FocusCasterSkill:
            case EffectType.FocusMainTargetSkill:
                Slot(file, where, type, check.SkillSlots, GamePack.SkillSlotMasterPath, "스킬 슬롯", value1, "value_1");
                break;
            case EffectType.FocusSubTargetSkill:
                SubTargetIndex(file, where, value1, "value_1");
                Slot(file, where, type, check.SkillSlots, GamePack.SkillSlotMasterPath, "스킬 슬롯", value2, "value_2");
                break;
            case EffectType.CacheToFocusedCharacterModValue:
            case EffectType.CacheToFocusedCharacterSetValue:
            case EffectType.FocusedCharacterValueToCache:
            case EffectType.CacheToFocusedItemModValue:
            case EffectType.CacheToFocusedItemSetValue:
            case EffectType.FocusedItemValueToCache:
            case EffectType.CacheToFocusedSkillModValue:
            case EffectType.CacheToFocusedSkillSetValue:
            case EffectType.FocusedSkillValueToCache:
                if (check.Statuses is null) throw new PackFormatException($"{file}: {where} {type}에는 {GamePack.StatusMasterPath}가 필요합니다.");
                if (!check.Statuses.Contains(value1))
                    throw new PackFormatException($"{file}: {where} value_1(status_code) '{value1}'가 스테이터스 마스터에 없습니다.");
                CheckCacheIndex(file, where, value2, "value_2");
                break;
            case EffectType.SetCacheValue:
                CheckCacheIndex(file, where, value1, "value_1");
                if (value3 < 0 || value3 > 28)
                    throw new PackFormatException($"{file}: {where} value_3(소수 자릿수) '{value3}'는 0~28이어야 합니다.");
                break;
            case EffectType.CacheAdd:
            case EffectType.CacheSubtract:
            case EffectType.CacheMultiply:
            case EffectType.CacheDivide:
                CheckCacheIndex(file, where, value1, "value_1");
                CheckCacheIndex(file, where, value2, "value_2");
                CheckCacheIndex(file, where, value3, "value_3");
                break;
            default:      // JumpCache*
                CheckCacheIndex(file, where, value1, "value_1");
                CheckCacheIndex(file, where, value2, "value_2");
                break;
        }
    }
}
