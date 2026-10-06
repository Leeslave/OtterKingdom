using System;
using System.Collections.Generic;

/// <summary>
/// 왕국 레벨과 현재 경험치 (순수 C#). 경험치가 쌓이면 곡선에 따라 레벨이 오른다.
/// 경험치는 퀘스트 보상으로만 들어온다 (QuestManager가 AddExp를 부름).
/// 정착의 큰 발전(의자 → Lv.2, 광산 개척 → Lv.3, P4 일터 → Lv.11~15)을 끝내기 전에는 경험치가 차도 Cap에 머물고(막대는 꽉 참),
/// 그동안 받은 경험치는 버리지 않고 모아 둔다. 발전을 끝내면 ReachLevel로 그 레벨까지 바로 오르고, 모아 둔 경험치가 이어서 반영된다.
/// </summary>
public class LevelProgress
{
    public int Level { get; private set; } = 1;

    /// <summary>현재 레벨 안에서 쌓인 경험치 (레벨이 오르면 넘친 만큼만 남음). 레벨 제한에 걸린 동안은 필요 경험치보다 클 수 있음 (모아 둔 것)</summary>
    public int Exp { get; private set; }

    /// <summary>경험치로 오를 수 있는 가장 높은 레벨 (기본은 제한 없음)</summary>
    public int Cap { get; private set; } = int.MaxValue;

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

        int top = Math.Min(curve.MaxLevel, Cap);
        long exp = (long)Exp + amount;
        while (Level < top)
        {
            int need = curve.ExpToNext(Level);
            if (exp < need)
                break;

            exp -= need;
            Level++;
            reached.Add(Level);
        }

        // 최고 레벨이면 넘친 경험치는 버림. 제한에 걸리면 막대가 꽉 찬 채로 기다리고, 넘친 경험치는 모아 둠 (제한이 풀리면 반영)
        if (Level >= curve.MaxLevel)
            Exp = 0;
        else
            Exp = (int)Math.Min(exp, int.MaxValue);
    }

    /// <summary>경험치로 오를 수 있는 레벨을 정한다. 올라가면 쌓아 둔 경험치로 바로 레벨이 오를 수 있다</summary>
    public void SetCap(int cap, ILevelCurve curve, List<int> reached)
    {
        if (cap < 1)
            throw new ArgumentOutOfRangeException(nameof(cap));
        Cap = cap;
        AddExp(0, curve, reached);
    }

    /// <summary>큰 발전을 끝냄: 모자란 경험치를 채워 level까지 바로 올린다 (이미 넘었으면 그대로)</summary>
    public void ReachLevel(int level, ILevelCurve curve, List<int> reached)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));
        if (reached == null) throw new ArgumentNullException(nameof(reached));

        int target = Math.Min(level, curve.MaxLevel);
        while (Level < target)
        {
            Exp = Math.Max(0, Exp - curve.ExpToNext(Level));
            Level++;
            reached.Add(Level);
        }
        if (Level >= curve.MaxLevel)
            Exp = 0;
    }

    /// <summary>세이브 복원용 (곡선 밖의 레벨은 잘라냄, 알림 없음). 레벨 제한 동안 모아 둔 경험치는 그대로 (제한을 다시 정할 때 반영)</summary>
    public void Load(int level, int exp, ILevelCurve curve)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));

        Level = Math.Clamp(level, 1, curve.MaxLevel);
        int need = curve.ExpToNext(Level);
        Exp = need <= 0 ? 0 : Math.Max(0, exp);
    }
}
