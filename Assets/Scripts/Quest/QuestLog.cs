using System;
using System.Collections.Generic;

public enum QuestStatus
{
    InProgress, // 진행 중
    Claimable,  // 목표 달성, 보상 받기 전
    Claimed,    // 보상까지 받음
}

/// <summary>퀘스트 하나의 진행 기록 (세이브와 같은 단위)</summary>
public readonly struct QuestRecord
{
    public readonly int Progress;
    public readonly bool Claimed;

    public QuestRecord(int progress, bool claimed)
    {
        Progress = progress;
        Claimed = claimed;
    }
}

/// <summary>
/// 퀘스트 진행 모델 (순수 C#). 퀘스트 ID별 진행 수치와 보상 수령 여부만 가진다. UI, 재화, 세이브 형식을 모른다.
/// </summary>
public class QuestLog
{
    /// <summary>진행 수치가 오르거나 보상을 받았을 때 (여러 개가 한꺼번에 바뀌면 null — 일일 초기화)</summary>
    public event Action<QuestDefinition> OnChanged;

    // 세이브와 같은 ID 기준. DB에서 지워진 퀘스트의 기록도 그대로 보관해 다음 저장 때 잃지 않음
    private readonly Dictionary<string, QuestRecord> _records = new Dictionary<string, QuestRecord>();

    public IReadOnlyDictionary<string, QuestRecord> Records => _records;

    /// <summary>일일 퀘스트를 마지막으로 초기화한 날 (0 = 아직 없음). 날이 바뀌면 QuestManager가 초기화한다</summary>
    public int DailyDay { get; private set; }

    /// <returns>진행 수치 (목표를 넘지 않음)</returns>
    public int GetProgress(QuestDefinition quest)
    {
        if (quest == null)
            throw new ArgumentNullException(nameof(quest));

        return Math.Min(GetRecord(quest).Progress, quest.Goal);
    }

    public QuestStatus GetStatus(QuestDefinition quest)
    {
        if (quest == null)
            throw new ArgumentNullException(nameof(quest));

        var record = GetRecord(quest);
        if (record.Claimed)
            return QuestStatus.Claimed;

        return record.Progress >= quest.Goal ? QuestStatus.Claimable : QuestStatus.InProgress;
    }

    /// <summary>진행 수치를 올린다. 목표에서 멈추고, 이미 달성했거나 보상을 받은 퀘스트는 그대로.</summary>
    /// <returns>수치가 바뀌었으면 true</returns>
    public bool AddProgress(QuestDefinition quest, int amount)
    {
        if (quest == null)
            throw new ArgumentNullException(nameof(quest));
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "진행 수치는 줄일 수 없습니다.");

        if (amount == 0 || GetStatus(quest) != QuestStatus.InProgress)
            return false;

        var record = GetRecord(quest);
        int progress = (int)Math.Min((long)record.Progress + amount, quest.Goal);
        _records[quest.QuestId] = new QuestRecord(progress, false);
        OnChanged?.Invoke(quest);
        return true;
    }

    /// <summary>보상 받음으로 표시. 목표를 달성했고 아직 받지 않았을 때만.</summary>
    /// <returns>받음으로 바뀌었으면 true (보상 지급은 호출한 쪽이 한다)</returns>
    public bool TryClaim(QuestDefinition quest)
    {
        if (GetStatus(quest) != QuestStatus.Claimable)
            return false;

        _records[quest.QuestId] = new QuestRecord(GetProgress(quest), true);
        OnChanged?.Invoke(quest);
        return true;
    }

    /// <summary>세이브 복원용. 알림 없이 기록을 넣는다.</summary>
    public void LoadRecord(string questId, int progress, bool claimed)
    {
        if (string.IsNullOrEmpty(questId))
            throw new ArgumentException("questId가 비어 있습니다.", nameof(questId));

        _records[questId] = new QuestRecord(Math.Max(0, progress), claimed);
    }

    /// <summary>이 퀘스트들의 진행·수령 기록을 지운다 (일일 초기화). 알림은 한 번만</summary>
    public void Reset(IEnumerable<QuestDefinition> quests, int day)
    {
        if (quests == null)
            throw new ArgumentNullException(nameof(quests));

        foreach (var quest in quests)
        {
            if (quest != null)
                _records.Remove(quest.QuestId);
        }
        DailyDay = day;
        OnChanged?.Invoke(null);
    }

    /// <summary>세이브 복원용</summary>
    public void LoadDailyDay(int day) => DailyDay = Math.Max(0, day);

    public int Count(IEnumerable<QuestDefinition> quests, QuestStatus status)
    {
        if (quests == null)
            throw new ArgumentNullException(nameof(quests));

        int count = 0;
        foreach (var quest in quests)
        {
            if (quest != null && GetStatus(quest) == status)
                count++;
        }
        return count;
    }

    private QuestRecord GetRecord(QuestDefinition quest)
    {
        return _records.TryGetValue(quest.QuestId, out var record) ? record : default;
    }
}
