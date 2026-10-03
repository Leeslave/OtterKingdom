using UnityEngine;

/// <summary>
/// 주민 작업 하나 (예: 광산 주변 정리). 정착한 해달을 필요한 수만큼 보내면 시간이 지나 끝난다.
/// 게시판 부탁(메인 진행) · 퀘스트(반복 보상)와 따로 간다: 작업 = 주민의 노동력을 쓰는 후속 정비.
/// 앞선 발전이 열려야 할 수 있고, 끝나면 결과 발전이 열린다 (그걸로 지역이 운영되거나 다음 작업이 열림).
/// </summary>
[CreateAssetMenu(fileName = "SettlementTask", menuName = "Game Data/Settlement/Task")]
public class SettlementTaskDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 저장되는 ID (예: task_mine_tidy)")]
    [SerializeField] private string _taskId;

    [Header("내용")]
    [SerializeField] private string _title;

    [TextArea]
    [SerializeField] private string _description;

    [Tooltip("다 끝났을 때 안내 (예: 광산 주변 정리가 끝났어요!)")]
    [SerializeField] private string _completionMessage;

    [Header("작업")]
    [Tooltip("보내야 하는 주민 해달 수")]
    [Min(1)]
    [SerializeField] private int _requiredWorkers = 1;

    [Tooltip("걸리는 시간 (초). 게임을 꺼 둔 동안에도 흐름")]
    [Min(0f)]
    [SerializeField] private float _durationSeconds = 30f;

    [Header("조건과 결과")]
    [Tooltip("이 발전이 열려야 할 수 있음 (비우면 처음부터)")]
    [SerializeField] private string _requiredDevelopment;

    [Tooltip("끝내면 열리는 발전 (비워도 됨)")]
    [SerializeField] private string _resultDevelopment;

    public string TaskId => _taskId;
    public string Title => _title;
    public string Description => _description;
    public string CompletionMessage => _completionMessage;
    public int RequiredWorkers => Mathf.Max(1, _requiredWorkers);
    public float DurationSeconds => Mathf.Max(0f, _durationSeconds);
    public string RequiredDevelopment => _requiredDevelopment;
    public string ResultDevelopment => _resultDevelopment;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_taskId))
            Debug.LogWarning($"[{name}] TaskId가 비어 있습니다.", this);
    }
}
