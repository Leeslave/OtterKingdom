using System;
using System.Collections.Generic;

/// <summary>세이브 한 줄: 퀘스트 ID, 진행 수치, 보상 수령 여부, 받은 레벨</summary>
[Serializable]
public class QuestSaveEntry
{
    public string questId;
    public int progress;
    public bool claimed;
    // 보상을 받은 순간의 왕국 레벨. 이 값이 생기기 전 세이브에는 없음 → 0 (모름)
    public int claimedLevel;

    public QuestSaveEntry() { }

    public QuestSaveEntry(string questId, int progress, bool claimed, int claimedLevel = 0)
    {
        this.questId = questId;
        this.progress = progress;
        this.claimed = claimed;
        this.claimedLevel = claimedLevel;
    }
}

/// <summary>
/// QuestLog ↔ 세이브 형식(List&lt;QuestSaveEntry&gt;) 변환. 모델이 세이브 타입을 모르도록 여기서만 한다.
/// </summary>
public static class QuestSaveConverter
{
    // 일일 초기화 날짜를 퀘스트 목록에 한 줄로 끼워 저장 (SaveData 형식을 바꾸지 않으려고). progress = 날짜 번호
    public const string DailyDayId = "__daily_day";
    // 레벨별 메인 체인을 맞춘 세이브인지 (progress 1). 없으면 옛 세이브
    public const string MainChainId = "__main_chain";

    /// <summary>기록이 있는 퀘스트만 ID 순으로 쓴다 (DB에 없는 ID도 보관해 둔 그대로 씀)</summary>
    public static void Write(QuestLog log, List<QuestSaveEntry> result)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));
        if (result == null) throw new ArgumentNullException(nameof(result));

        result.Clear();

        var ids = new List<string>(log.Records.Keys);
        ids.Sort(string.CompareOrdinal);
        foreach (var id in ids)
        {
            var record = log.Records[id];
            result.Add(new QuestSaveEntry(id, record.Progress, record.Claimed, record.ClaimedLevel));
        }

        if (log.DailyDay > 0)
            result.Add(new QuestSaveEntry(DailyDayId, log.DailyDay, false));
        if (log.MainChainReady)
            result.Add(new QuestSaveEntry(MainChainId, 1, false));
    }

    /// <summary>저장된 기록을 넣는다. 비었거나 잘못된 줄은 건너뛴다.</summary>
    public static void Read(IEnumerable<QuestSaveEntry> saved, QuestLog log)
    {
        if (saved == null) throw new ArgumentNullException(nameof(saved));
        if (log == null) throw new ArgumentNullException(nameof(log));

        foreach (var line in saved)
        {
            if (line == null || string.IsNullOrEmpty(line.questId))
                continue;

            if (line.questId == DailyDayId)
            {
                log.LoadDailyDay(line.progress);
                continue;
            }
            if (line.questId == MainChainId)
            {
                if (line.progress > 0)
                    log.MarkMainChainReady();
                continue;
            }

            log.LoadRecord(line.questId, line.progress, line.claimed, line.claimedLevel);
        }
    }
}
