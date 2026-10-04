using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 큰 부탁 하나 (예: 마을 회의소 마련하기) = 이미 있는 게시판 부탁 여러 개를 순서대로 묶어 보여 주는 것.
/// 단계마다 따로 저장하지 않고, 각 부탁의 완료·진행 기록에서 계산한다. 보상·결제·건설은 각 부탁이 그대로 한다.
/// </summary>
[CreateAssetMenu(fileName = "MilestoneGroup", menuName = "Game Data/Settlement/Milestone Group")]
public class MilestoneGroupDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("ID (예: group_town_council). 세이브에는 저장하지 않음")]
    [SerializeField] private string _groupId;

    [Header("내용")]
    [SerializeField] private string _title;

    [TextArea]
    [SerializeField] private string _description;

    [SerializeField] private Sprite _icon;

    [Header("단계")]
    [Tooltip("이 발전이 열리면 게시판·접수소에서 보임 (예: guild_office_built)")]
    [SerializeField] private string _visibleDevelopment;

    [Tooltip("순서대로의 부탁 (단계 = 그 부탁의 완료 기록)")]
    [SerializeField] private List<BoardRequestDefinition> _steps = new List<BoardRequestDefinition>();

    public string GroupId => _groupId;
    public string Title => _title;
    public string Description => _description;
    public Sprite Icon => _icon;
    public string VisibleDevelopment => _visibleDevelopment;
    public IReadOnlyList<BoardRequestDefinition> Steps => _steps;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_groupId))
            Debug.LogWarning($"[{name}] GroupId가 비어 있습니다.", this);
        if (_steps.Count == 0)
            Debug.LogWarning($"[{name}] 단계(부탁)가 비어 있습니다.", this);
    }
}
