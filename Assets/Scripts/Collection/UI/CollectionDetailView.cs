using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 위쪽 상세: 큰 그림, 이름, 상태 배지, 한 줄 소개, 설명 박스. 받은 값만 그린다.
/// 아직 얻지 못한 항목은 실루엣과 "???"로 가린다.
/// </summary>
public class CollectionDetailView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Image _portrait;
    [SerializeField] private Image _silhouette;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private GameObject _badge;
    [SerializeField] private TextMeshProUGUI _badgeText;
    [SerializeField] private TextMeshProUGUI _taglineText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [Tooltip("\"획득 장소: 밭\" 줄")]
    [SerializeField] private TextMeshProUGUI _extraText;

    [Header("문구")]
    [SerializeField] private string _unknownName = "???";
    [SerializeField] private string _unknownTagline = "아직 만나지 못했어요.";
    [SerializeField] private string _visitedBadge = "방문 흔적";
    [SerializeField] private string _visitedTagline = "섬에 다녀간 흔적이 있어요.";
    [SerializeField] private string _emptyMessage = "항목을 골라 보세요.";

    [Header("방문 흔적")]
    [SerializeField] private float _visitedAlpha = 0.4f;

    public void Show(CollectionEntry entry, CollectionState state)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        bool collected = state == CollectionState.Collected;
        bool visited = state == CollectionState.Visited;
        var tab = entry.Tab;

        var portrait = entry.Portrait;
        _portrait.sprite = portrait;
        _portrait.enabled = (collected || visited) && portrait != null;
        _portrait.color = new Color(1f, 1f, 1f, visited ? _visitedAlpha : 1f);
        _silhouette.sprite = entry.Silhouette;
        _silhouette.enabled = state == CollectionState.Unknown && entry.Silhouette != null;

        // 이름은 방문 흔적부터 공개, 설명은 획득해야 공개
        _nameText.text = state == CollectionState.Unknown ? _unknownName : entry.DisplayName;

        _badge.SetActive(state != CollectionState.Unknown);
        _badgeText.text = visited ? _visitedBadge : tab != null ? tab.CollectedLabel : "";

        _taglineText.text = collected ? entry.Tagline : visited ? _visitedTagline : _unknownTagline;
        _descriptionText.text = collected ? entry.Description : "";
        _extraText.text = collected && tab != null && !string.IsNullOrEmpty(entry.ExtraValue)
            ? $"{tab.ExtraLabel}: {entry.ExtraValue}"
            : "";
    }

    public void ShowNothing()
    {
        _portrait.enabled = false;
        _silhouette.enabled = false;
        _nameText.text = "";
        _badge.SetActive(false);
        _taglineText.text = _emptyMessage;
        _descriptionText.text = "";
        _extraText.text = "";
    }
}
