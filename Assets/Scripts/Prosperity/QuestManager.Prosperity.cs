using System;
using System.Collections.Generic;
using UnityEngine;

public partial class QuestManager
{
    private int _prosperityMainStart;
    private float _prosperityQuestPoll;
    private QuestDatabase _originalQuestDatabase;
    private readonly List<QuestDefinition> _prosperityQuests = new List<QuestDefinition>();

    private void InitializeProsperityQuests()
    {
        if (CastleRelease.IsActive || _originalQuestDatabase != null) return;
        var curve = ProfileManager.Instance != null ? ProfileManager.Instance.LevelTable : null;
        if (_database == null || curve == null) return;
        if (Log.Records.TryGetValue(ProsperityQuestPlan.TransitionId, out var transition))
            _prosperityMainStart = transition.Progress >= 23 && transition.Progress <= 27 ? transition.Progress : 0;
        else
        {
            _prosperityMainStart = ProsperityQuestPlan.ChooseStart(PlayerLevel, Log.Records);
            Log.LoadRecord(ProsperityQuestPlan.TransitionId, _prosperityMainStart, false);
        }
        if (_prosperityMainStart == 0) return;

        _originalQuestDatabase = _database;
        _database = Instantiate(_database);
        QuestDefinition previous = null;
        foreach (var row in ProsperityQuestPlan.Rows)
        {
            if (row.Step == 1)
            {
                // 최초 전환 레벨 이전의 옛 체인을 완료한 뒤 새 체인으로 진입한다.
                if (row.Level == _prosperityMainStart)
                    _originalQuestDatabase.TryGet(row.Level == 23 ? "main_lv22_sales" : $"main_lv{row.Level - 1}_sales", out previous);
            }
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            int gold = 0;
            QuestDefinition template = null, matching = null;
            foreach (var old in _originalQuestDatabase.Quests)
            {
                if (old.Kind == QuestKind.Main && old.RequiredLevel == row.Level && ProsperityQuestPlan.LegacyLevel(old.QuestId) == row.Level)
                { gold += old.RewardAmount; template = old; }
                if (old.GoalType == row.Type && old.Item == null && old.ItemFilter != null) matching = old;
            }
            quest.SetupProsperity(row, previous, ProsperityQuestPlan.Share(curve.ExpToNext(row.Level), row.Step),
                ProsperityQuestPlan.Share(gold, row.Step), template != null ? template.RewardCurrency : null,
                matching != null ? matching.Icon : template != null ? template.Icon : null,
                matching != null ? matching.ItemFilter : null);
            _prosperityQuests.Add(quest);
            previous = quest;
        }
        _database.AppendRuntime(_prosperityQuests);
    }

    private bool IsSelectedProsperityChain(QuestDefinition quest)
    {
        if (quest == null) return false;
        if (quest.Kind == QuestKind.Main && CastleRelease.IsActive
            && _database.TryGet(CastleRelease.FinalQuestId, out var finalQuest)
            && quest.SortOrder > finalQuest.SortOrder) return false;
        if (ProsperityQuestPlan.IsNew(quest.QuestId))
            return _prosperityMainStart > 0 && quest.RequiredLevel >= _prosperityMainStart;
        int legacy = ProsperityQuestPlan.LegacyLevel(quest.QuestId);
        return _prosperityMainStart == 0 || legacy < _prosperityMainStart || legacy > 27;
    }

    private bool IsProsperityAvailable(QuestDefinition quest)
    {
        if (!IsSelectedProsperityChain(quest)) return false;
        if (ProsperityQuestPlan.IsNew(quest.QuestId))
            return PlayerLevel >= quest.RequiredLevel && (quest.Prerequisite == null || Log.GetStatus(quest.Prerequisite) == QuestStatus.Claimed);
        if (_prosperityMainStart > 0 && quest.QuestId == "main_lv28_daily")
            return PlayerLevel >= 28 && _database.TryGet("prosperity_main_27_5", out var last)
                && Log.GetStatus(last) == QuestStatus.Claimed;
        return QuestProgressRules.IsAvailable(quest, PlayerLevel, Log);
    }

    private void TickProsperityQuests()
    {
        if (_prosperityMainStart == 0) return;
        _prosperityQuestPoll -= Time.unscaledDeltaTime;
        if (_prosperityQuestPoll > 0) return;
        _prosperityQuestPoll = .5f;
        RefreshProsperityGoals();
    }

    private void RefreshProsperityGoals()
    {
        foreach (var quest in _prosperityQuests)
            if (quest.GoalType == QuestGoalType.ProsperityRecord && IsAvailable(quest))
            {
                int value = ProsperityRecordValue(quest.Target);
                if (quest.Target.StartsWith("warehouse:", StringComparison.Ordinal)) Log.SetObservedProgress(quest, value);
                else Log.SetProgressAtLeast(quest, value);
            }
    }

    private static int ProsperityRecordValue(string target)
    {
        var research = ProsperityManager.Instance != null ? ProsperityManager.Instance.Research : null;
        var settlement = SettlementManager.Instance;
        if (target.StartsWith("tech:", StringComparison.Ordinal)) return research != null && research.HasTech(target.Substring(5)) ? 1 : 0;
        if (target.StartsWith("market:", StringComparison.Ordinal)) return research != null && research.Data.marketCompleted.Contains(target.Substring(7)) ? 1 : 0;
        if (target == "markets")
        {
            int count = 0;
            if (research != null) foreach (var order in ProsperityResearch.MarketOrders)
                if (research.Data.marketCompleted.Contains(order.Id)) count++;
            return count;
        }
        if (target == "techs")
        {
            int count = 0;
            if (research != null) foreach (var tech in ProsperityResearch.Techs) if (research.HasTech(tech.Id)) count++;
            return count;
        }
        if (settlement == null || !settlement.IsLoaded) return 0;
        if (target.StartsWith("warehouse:", StringComparison.Ordinal)) return settlement.Warehouse?.Count(target.Substring(10)) ?? 0;
        if (target == "carts") return settlement.Settlement.Industry.cartRoutes.Count;
        return 0;
    }

    private void DisposeProsperityQuests()
    {
        if (_originalQuestDatabase == null) return;
        foreach (var quest in _prosperityQuests) Destroy(quest);
        _prosperityQuests.Clear();
        Destroy(_database);
        _database = _originalQuestDatabase;
        _originalQuestDatabase = null;
    }
}
