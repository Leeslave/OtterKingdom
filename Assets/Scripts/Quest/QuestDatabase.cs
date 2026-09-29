using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 퀘스트. ID로 찾기와 정렬된 목록을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "QuestDatabase", menuName = "Game Data/Quest/Quest Database")]
public class QuestDatabase : ScriptableObject
{
    [Tooltip("퀘스트 전부. ID가 겹치면 먼저 등록된 것만 쓰고 경고")]
    [SerializeField]
    private List<QuestDefinition> _quests = new List<QuestDefinition>();

    private Dictionary<string, QuestDefinition> _byId;
    private List<QuestDefinition> _valid;

    /// <summary>ID가 겹치거나 빈 칸을 뺀 퀘스트 (SortOrder → ID 순)</summary>
    public IReadOnlyList<QuestDefinition> Quests
    {
        get
        {
            EnsureLookup();
            return _valid;
        }
    }

    public bool TryGet(string questId, out QuestDefinition quest)
    {
        if (questId == null)
            throw new ArgumentNullException(nameof(questId));

        EnsureLookup();
        return _byId.TryGetValue(questId, out quest);
    }

    private void EnsureLookup()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, QuestDefinition>();
        _valid = new List<QuestDefinition>();
        foreach (var quest in _quests)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId) || _byId.ContainsKey(quest.QuestId))
                continue;

            _byId.Add(quest.QuestId, quest);
            _valid.Add(quest);
        }

        _valid.Sort((a, b) =>
        {
            int order = a.SortOrder.CompareTo(b.SortOrder);
            return order != 0 ? order : string.CompareOrdinal(a.QuestId, b.QuestId);
        });
    }

    // 에디터에서 목록이나 ID를 바꾸면 조회표를 다시 만들도록
    private void OnEnable() => _byId = null;

    private void OnValidate()
    {
        _byId = null;

        var seen = new HashSet<string>();
        foreach (var quest in _quests)
        {
            if (quest == null)
            {
                Debug.LogWarning($"[{name}] 빈 칸이 있습니다.", this);
                continue;
            }
            if (!seen.Add(quest.QuestId))
                Debug.LogWarning($"[{name}] 퀘스트 ID가 겹칩니다: {quest.QuestId} (먼저 등록된 것만 사용)", this);
        }
    }
}
