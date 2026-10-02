using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>건설에 드는 아이템 한 종류와 개수</summary>
[Serializable]
public class ItemAmount
{
    [SerializeField] private ItemDefinition _item;
    [SerializeField] private int _amount;

    public ItemDefinition Item => _item;
    public int Amount => _amount;

    public ItemAmount() { }

    public ItemAmount(ItemDefinition item, int amount)
    {
        _item = item;
        _amount = amount;
    }
}

/// <summary>무엇을 짓는지 (연출·문구 구분용)</summary>
public enum ConstructionTarget
{
    House,
    Clearing, // 개간 (잡목·돌 치우기)
}

/// <summary>
/// 건설 한 가지 (집, 농경지 개간). 비용을 내고 시간이 지나면 결과(UnlockResultId)가 열린다.
/// 시간이 0이면 바로 끝난다 (첫 집처럼 짧은 연출만).
/// </summary>
[CreateAssetMenu(fileName = "Construction", menuName = "Game Data/Settlement/Construction")]
public class ConstructionDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 저장되는 ID (예: con_house_2)")]
    [SerializeField] private string _constructionId;

    [Tooltip("팝업에 보일 이름 (예: 새 이웃의 집)")]
    [SerializeField] private string _displayName;

    [SerializeField] private Sprite _icon;

    [SerializeField] private ConstructionTarget _targetType;

    [Header("비용")]
    [Tooltip("골드 (재화는 SettlementConfig의 골드)")]
    [SerializeField] private int _requiredGold;

    [SerializeField] private List<ItemAmount> _requiredItems = new List<ItemAmount>();

    [Header("작업")]
    [Tooltip("걸리는 시간(초). 0이면 바로 완료")]
    [SerializeField] private float _durationSeconds;

    [Tooltip("건설 해달이 있어야 시작할 수 있음")]
    [SerializeField] private bool _needsBuilder;

    [Tooltip("진행 중 말풍선 제목 (예: 농경지 개간 중)")]
    [SerializeField] private string _progressLabel;

    [Tooltip("진행 중 말풍선 아래 안내 (예: 완료하면 첫 밭이 열려요)")]
    [SerializeField] private string _progressHint;

    [Header("결과")]
    [Tooltip("완료하면 열리는 발전 ID (광장 오브젝트·장소 해금이 이 ID를 봄. 예: house_1, farmland)")]
    [SerializeField] private string _unlockResultId;

    public string ConstructionId => _constructionId;
    public string DisplayName => _displayName;
    public Sprite Icon => _icon;
    public ConstructionTarget TargetType => _targetType;
    public int RequiredGold => _requiredGold;
    public IReadOnlyList<ItemAmount> RequiredItems => _requiredItems;
    public float DurationSeconds => _durationSeconds;
    public bool IsInstant => _durationSeconds <= 0f;
    public bool NeedsBuilder => _needsBuilder;
    public string ProgressLabel => _progressLabel;
    public string ProgressHint => _progressHint;
    public string UnlockResultId => _unlockResultId;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_constructionId))
            Debug.LogWarning($"[{name}] ConstructionId가 비어 있습니다.", this);
        if (_requiredGold < 0 || _durationSeconds < 0f)
            Debug.LogWarning($"[{name}] 비용과 시간은 0 이상이어야 합니다.", this);
    }
}
