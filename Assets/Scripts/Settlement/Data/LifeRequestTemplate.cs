using System.Collections.Generic;
using UnityEngine;

/// <summary>생활 의뢰가 하는 일. 에셋에 숫자로 저장되므로 순서를 바꾸지 않는다</summary>
public enum LifeRequestKind
{
    Deliver = 0,      // 아이템을 건네줌 (오늘의 간식: 작물, 집수리 재료: 목재·돌)
    ResidentWork = 1, // 주민 한 명을 짧은 작업에 보냄 (회관 주변 정리)
}

/// <summary>
/// 선택형 생활 의뢰의 틀 (P3). 게시판 "주민들의 부탁" 칸(최대 2)에 회차마다 하나씩 만들어진다.
/// 만료·접속 날짜·연속 출석 조건이 없고, 끝내면 다음 틀이 정해진 순서로 온다 (회차를 저장해 재접속으로 다시 뽑지 않음).
/// 이미 얻을 수 있는 재료만 요구한다 (후보 아이템 중 지금 얻을 수 있는 것 → 없으면 다음 틀).
/// </summary>
[CreateAssetMenu(fileName = "LifeRequest", menuName = "Game Data/Settlement/Life Request")]
public class LifeRequestTemplate : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 저장되는 ID (예: life_snack)")]
    [SerializeField] private string _templateId;

    [SerializeField] private LifeRequestKind _kind;

    [Header("내용")]
    [Tooltip("제목 (예: 오늘의 간식)")]
    [SerializeField] private string _title;

    [Tooltip("부탁하는 말. {item} · {amount}는 고른 아이템 이름 · 개수로 바뀜")]
    [TextArea]
    [SerializeField] private string _line;

    [SerializeField] private Sprite _icon;

    [Header("건네주기")]
    [Tooltip("후보 아이템 (회차마다 차례로, 지금 얻을 수 있는 것만)")]
    [SerializeField] private List<ItemDefinition> _items = new List<ItemDefinition>();

    [Min(1)]
    [SerializeField] private int _amount = 6;

    [Tooltip("후보 아이템이 나는 장소 (예: Farm). 그 장소에서 생산할 수 있을 때만 요구함. 비우면 늘 얻을 수 있음 (광장의 목재·돌)")]
    [SerializeField] private string _productionZone;

    [Header("주민 작업")]
    [Tooltip("보낼 작업의 틀 (광장 현장 · 걸리는 시간 · 사람 수). 회차마다 따로 시작함")]
    [SerializeField] private SettlementTaskDefinition _task;

    [Header("보상")]
    [Min(0)]
    [SerializeField] private int _gold;

    [Tooltip("경험치: 받는 순간 레벨에서 다음 레벨까지 필요한 경험치의 %")]
    [Range(0f, 100f)]
    [SerializeField] private float _xpPercentOfLevel = 8f;

    public string TemplateId => _templateId;
    public LifeRequestKind Kind => _kind;
    public string Title => _title;
    public string Line => _line;
    public Sprite Icon => _icon;
    public IReadOnlyList<ItemDefinition> Items => _items;
    public int Amount => Mathf.Max(1, _amount);
    public string ProductionZone => _productionZone;
    public SettlementTaskDefinition Task => _task;
    public int Gold => Mathf.Max(0, _gold);
    public float XpPercentOfLevel => _xpPercentOfLevel;

    /// <summary>테스트·설정 도구용</summary>
    public void Setup(string templateId, LifeRequestKind kind, string title, string line, IEnumerable<ItemDefinition> items,
        int amount, SettlementTaskDefinition task, int gold, float xpPercent, Sprite icon = null, string productionZone = null)
    {
        _productionZone = productionZone;
        _templateId = templateId;
        _kind = kind;
        _title = title;
        _line = line;
        _items = items != null ? new List<ItemDefinition>(items) : new List<ItemDefinition>();
        _amount = amount;
        _task = task;
        _gold = gold;
        _xpPercentOfLevel = xpPercent;
        _icon = icon;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_templateId))
            Debug.LogWarning($"[{name}] TemplateId가 비어 있습니다.", this);
        if (_kind == LifeRequestKind.ResidentWork && _task == null)
            Debug.LogWarning($"[{name}] 주민 작업 의뢰인데 작업 틀이 비어 있습니다.", this);
        if (_kind == LifeRequestKind.Deliver && _items.Count == 0)
            Debug.LogWarning($"[{name}] 건네줄 후보 아이템이 비어 있습니다.", this);
    }
}
