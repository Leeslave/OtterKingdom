using System;
using System.Collections.Generic;

/// <summary>
/// 왕국 레벨과 현재 경험치 (순수 C#). 경험치가 쌓이면 곡선에 따라 레벨이 오른다.
/// 경험치는 퀘스트 보상으로만 들어온다 (QuestManager가 AddExp를 부름).
/// </summary>
public class LevelProgress
{
    public int Level { get; private set; } = 1;

    /// <summary>현재 레벨 안에서 쌓인 경험치 (레벨이 오르면 넘친 만큼만 남음)</summary>
    public int Exp { get; private set; }

    /// <summary>다음 레벨까지 진행 비율 (최고 레벨이면 1)</summary>
    public float Ratio(ILevelCurve curve)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));

        int need = curve.ExpToNext(Level);
        return need <= 0 ? 1f : Math.Min(1f, (float)Exp / need);
    }

    /// <summary>경험치를 더한다. 여러 레벨이 한 번에 오를 수 있다</summary>
    /// <param name="reached">새로 도달한 레벨들이 차례로 담긴다 (레벨업 보상·팝업용)</param>
    public void AddExp(int amount, ILevelCurve curve, List<int> reached)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));
        if (reached == null) throw new ArgumentNullException(nameof(reached));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "경험치는 줄일 수 없습니다.");

        if (Level >= curve.MaxLevel)
            return;

        long exp = (long)Exp + amount;
        while (Level < curve.MaxLevel)
        {
            int need = curve.ExpToNext(Level);
            if (exp < need)
                break;

            exp -= need;
            Level++;
            reached.Add(Level);
        }

        // 최고 레벨이면 넘친 경험치는 버림
        Exp = Level >= curve.MaxLevel ? 0 : (int)exp;
    }

    /// <summary>세이브 복원용 (곡선 밖의 값은 잘라냄, 알림 없음)</summary>
    public void Load(int level, int exp, ILevelCurve curve)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));

        Level = Math.Clamp(level, 1, curve.MaxLevel);
        int need = curve.ExpToNext(Level);
        Exp = need <= 0 ? 0 : Math.Clamp(exp, 0, need - 1);
    }
}
