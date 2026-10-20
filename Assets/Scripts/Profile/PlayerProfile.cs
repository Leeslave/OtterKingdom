using System;

/// <summary>
/// 상단바에 보이는 플레이어 정보 (순수 C#): 이름, 레벨, 다음 레벨까지 진행 비율.
/// 레벨을 무엇으로 올릴지(경험치 규칙)는 아직 정하지 않았으므로 값만 받아 보관한다.
/// </summary>
public class PlayerProfile
{
    public const int MaxNameLength = 4;

    public static bool TryNormalizeNewName(string name, out string normalized)
    {
        normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > MaxNameLength) return false;
        foreach (char character in normalized)
            if (char.IsControl(character) || character == '<' || character == '>') return false;
        return true;
    }

    public event Action OnChanged;

    public string Name { get; private set; }
    public int Level { get; private set; } = 1;

    /// <summary>상단바 아바타에 보일 대표 해달 (비면 첫 해달)</summary>
    public string PortraitOtterId { get; private set; }

    /// <summary>다음 레벨까지 진행 비율 (0 ~ 1)</summary>
    public float ExpRatio { get; private set; }

    public PlayerProfile(string name)
    {
        Name = Validate(name);
    }

    public void SetName(string name)
    {
        string validated = Validate(name);
        if (validated == Name)
            return;

        Name = validated;
        OnChanged?.Invoke();
    }

    public void SetPortrait(string otterId)
    {
        string id = string.IsNullOrWhiteSpace(otterId) ? null : otterId.Trim();
        if (id == PortraitOtterId)
            return;

        PortraitOtterId = id;
        OnChanged?.Invoke();
    }

    /// <param name="level">1 이상</param>
    /// <param name="expRatio">다음 레벨까지 진행 비율. 0 ~ 1을 벗어나면 자름</param>
    public void SetLevel(int level, float expRatio)
    {
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level), "레벨은 1 이상이어야 합니다.");
        if (float.IsNaN(expRatio))
            throw new ArgumentException("진행 비율이 NaN입니다.", nameof(expRatio));

        float ratio = Math.Clamp(expRatio, 0f, 1f);
        if (level == Level && ratio == ExpRatio)
            return;

        Level = level;
        ExpRatio = ratio;
        OnChanged?.Invoke();
    }

    private static string Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("이름이 비어 있습니다.", nameof(name));

        return name.Trim();
    }
}
