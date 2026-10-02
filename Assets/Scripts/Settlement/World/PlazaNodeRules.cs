using System;

/// <summary>
/// 광장 바위·나무의 수치 규칙 (화면과 떼어 테스트할 수 있게).
/// 바위: 곡괭이 레벨이 높을수록 한 번에 많이 깎고, 약점(반짝이)을 치면 크게 들어간다.
/// 나무: 한 번 쉴 때까지 정해진 횟수만 흔들 수 있고, 마지막 흔들기에 더 많이 떨어진다.
/// </summary>
public static class PlazaNodeRules
{
    /// <summary>약점을 쳤을 때 배율</summary>
    public const int CritMultiplier = 3;

    /// <summary>한 번 칠 때 깎는 양: 곡괭이 1·2레벨 1, 3·4레벨 2, 5레벨 3</summary>
    public static int Damage(int pickaxeLevel)
    {
        if (pickaxeLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(pickaxeLevel));
        return 1 + (pickaxeLevel - 1) / 2;
    }

    public static int CritDamage(int pickaxeLevel) => Damage(pickaxeLevel) * CritMultiplier;

    /// <summary>
    /// 남은 체력에 맞는 금 간 그림 번호 (0 = 멀쩡함, stageCount-1 = 가장 많이 금 감).
    /// 한 번이라도 맞으면 1단계부터 보인다
    /// </summary>
    public static int CrackStage(int remaining, int maxHp, int stageCount)
    {
        if (maxHp <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHp));
        if (stageCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(stageCount));
        if (remaining >= maxHp || stageCount == 1)
            return 0;
        float damaged = 1f - Math.Max(0, remaining) / (float)maxHp;
        int stage = 1 + (int)(damaged * (stageCount - 1));
        return Math.Min(stage, stageCount - 1);
    }

    /// <summary>shakeIndex번째(0부터) 흔들기에 떨어지는 목재 수. 마지막 흔들기는 lastBonus만큼 더</summary>
    public static int ShakeWood(int shakeIndex, int shakesPerRest, int perShake, int lastBonus)
    {
        if (shakeIndex < 0 || shakeIndex >= shakesPerRest)
            throw new ArgumentOutOfRangeException(nameof(shakeIndex));
        return shakeIndex == shakesPerRest - 1 ? perShake + lastBonus : perShake;
    }
}
