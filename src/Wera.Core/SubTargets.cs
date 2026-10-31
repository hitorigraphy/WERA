namespace Wera.Core;

/// <summary>
/// 스킬의 서브 타겟 범위(skill_master의 sub_target_type): 메인 타겟을 기준으로 메인 타겟이 있는 편의 칸들 중 서브 타겟이 되는 유닛을 정한다
/// (메인 타겟 자신은 서브 타겟이 아니고, 유닛이 없거나 쓰러진 칸은 제외). 가로는 같은 줄(전위/중위/후위), 세로는 같은 열이고
/// 왼쪽, 오른쪽은 열 번호의 앞뒤, 전방은 앞 줄(위), 후방은 뒤 줄(아래)이다.
/// </summary>
public enum SkillSubTargetType
{
    /// <summary>서브 타겟 없음(기본).</summary>
    None,
    /// <summary>같은 가로 1줄의 다른 유닛.</summary>
    SameRow,
    /// <summary>같은 세로 1줄의 다른 유닛.</summary>
    SameColumn,
    /// <summary>오른쪽 한 칸.</summary>
    Right,
    /// <summary>왼쪽 한 칸.</summary>
    Left,
    /// <summary>후방(뒤 줄) 한 칸.</summary>
    Back,
    /// <summary>전방(앞 줄) 한 칸.</summary>
    Front,
    /// <summary>십자: 왼쪽, 오른쪽, 전방, 후방 한 칸씩.</summary>
    Cross,
    /// <summary>메인 타겟이 있는 편의 다른 유닛 전부.</summary>
    All,
    /// <summary>메인 타겟을 뺀 같은 편에서 랜덤 n명(가능한 수가 n보다 적으면 가능한 수만큼). 8명은 전체와 같아 7명까지만 있다.</summary>
    Random1, Random2, Random3, Random4, Random5, Random6, Random7,
    /// <summary>후방 양사선: 후방 왼쪽과 후방 오른쪽 대각선 방향(1은 한 칸, 2는 두 칸까지).</summary>
    BackDiagonals1, BackDiagonals2,
    /// <summary>후방 좌사선(1은 한 칸, 2는 두 칸까지).</summary>
    BackLeftDiagonal1, BackLeftDiagonal2,
    /// <summary>후방 우사선(1은 한 칸, 2는 두 칸까지).</summary>
    BackRightDiagonal1, BackRightDiagonal2,
    /// <summary>전방 양사선(1은 한 칸, 2는 두 칸까지).</summary>
    FrontDiagonals1, FrontDiagonals2,
    /// <summary>전방 좌사선(1은 한 칸, 2는 두 칸까지).</summary>
    FrontLeftDiagonal1, FrontLeftDiagonal2,
    /// <summary>전방 우사선(1은 한 칸, 2는 두 칸까지).</summary>
    FrontRightDiagonal1, FrontRightDiagonal2,
}

/// <summary>서브 타겟 범위로 서브 타겟이 될 슬롯들을 계산한다(전위 0~2, 중위 3~5, 후위 6~8 줄 번호 = 슬롯 / 3, 열 번호 = 슬롯 % 3).</summary>
public static class SubTargetPicker
{
    /// <summary>
    /// 메인 타겟의 슬롯을 기준으로 서브 타겟 슬롯들을 슬롯 번호 순으로 돌려준다. <paramref name="present"/>는 그 슬롯에 서브 타겟이 될 수 있는
    /// 유닛(살아 있음)이 있는지이고, <paramref name="nextRandom"/>(n)은 0 이상 n 미만의 값이다(랜덤 n명은 중복 없이 뽑는다).
    /// </summary>
    public static List<int> Pick(SkillSubTargetType type, int mainSlot, Func<int, bool> present, Func<int, int> nextRandom)
    {
        var (row, col) = (mainSlot / 3, mainSlot % 3);
        var others = Enumerable.Range(0, GameSession.PartySlotCount).Where(i => i != mainSlot && present(i)).ToList();

        List<int> At(params (int Row, int Col)[] cells) =>
            cells.Where(c => c.Row is >= 0 and < 3 && c.Col is >= 0 and < 3).Select(c => c.Row * 3 + c.Col).Where(others.Contains).Distinct().Order().ToList();
        List<int> Rays(int reach, params (int DRow, int DCol)[] directions) =>
            At(directions.SelectMany(d => Enumerable.Range(1, reach).Select(k => (row + d.DRow * k, col + d.DCol * k))).ToArray());

        if (type is >= SkillSubTargetType.Random1 and <= SkillSubTargetType.Random7)
        {
            var pool = others.ToList();
            var chosen = new List<int>();
            for (var n = (int)type - (int)SkillSubTargetType.Random1 + 1; n > 0 && pool.Count > 0; n--)
            {
                var pick = pool[nextRandom(pool.Count)];
                chosen.Add(pick);
                pool.Remove(pick);
            }
            return chosen.Order().ToList();
        }

        return type switch
        {
            SkillSubTargetType.SameRow => others.Where(i => i / 3 == row).ToList(),
            SkillSubTargetType.SameColumn => others.Where(i => i % 3 == col).ToList(),
            SkillSubTargetType.Right => At((row, col + 1)),
            SkillSubTargetType.Left => At((row, col - 1)),
            SkillSubTargetType.Back => At((row + 1, col)),
            SkillSubTargetType.Front => At((row - 1, col)),
            SkillSubTargetType.Cross => At((row, col - 1), (row, col + 1), (row - 1, col), (row + 1, col)),
            SkillSubTargetType.All => others,
            SkillSubTargetType.BackDiagonals1 => Rays(1, (1, -1), (1, 1)),
            SkillSubTargetType.BackDiagonals2 => Rays(2, (1, -1), (1, 1)),
            SkillSubTargetType.BackLeftDiagonal1 => Rays(1, (1, -1)),
            SkillSubTargetType.BackLeftDiagonal2 => Rays(2, (1, -1)),
            SkillSubTargetType.BackRightDiagonal1 => Rays(1, (1, 1)),
            SkillSubTargetType.BackRightDiagonal2 => Rays(2, (1, 1)),
            SkillSubTargetType.FrontDiagonals1 => Rays(1, (-1, -1), (-1, 1)),
            SkillSubTargetType.FrontDiagonals2 => Rays(2, (-1, -1), (-1, 1)),
            SkillSubTargetType.FrontLeftDiagonal1 => Rays(1, (-1, -1)),
            SkillSubTargetType.FrontLeftDiagonal2 => Rays(2, (-1, -1)),
            SkillSubTargetType.FrontRightDiagonal1 => Rays(1, (-1, 1)),
            SkillSubTargetType.FrontRightDiagonal2 => Rays(2, (-1, 1)),
            _ => [],
        };
    }
}
