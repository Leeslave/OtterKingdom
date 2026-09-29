using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 목록 칸 하나. 상태에 따라 원래 그림 + 체크 / 반투명 + 방문 흔적 / 실루엣 + ??? 를 그린다.
/// </summary>
public class CollectionSlotView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private Image _portrait;
    [SerializeField] private Image _silhouette;
    [SerializeField] private GameObject _check;
    [Tooltip("\"???\" 글자 (미획득)")]
    [SerializeField] private GameObject _unknownLabel;
    [Tooltip("발자국 + \"방문 흔적\" (방문 흔적)")]
    [SerializeField] private GameObject _visitLabel;
    [SerializeField] private GameObject _selectRing;

    [Header("배경 스프라이트")]
    [SerializeField] private Sprite _collectedSprite;
    [SerializeField] private Sprite _emptySprite;

    [Header("방문 흔적")]
    [Tooltip("방문 흔적일 때 그림의 투명도")]
    [SerializeField] private float _visitedAlpha = 0.4f;

    public CollectionEntry Entry { get; private set; }
    public event Action<CollectionSlotView> OnClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Bind(CollectionEntry entry, CollectionState state)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        Entry = entry;
        bool collected = state == CollectionState.Collected;
        bool visited = state == CollectionState.Visited;

        _background.sprite = collected ? _collectedSprite : _emptySprite;

        // 원래 그림: 획득(불투명) / 방문 흔적(반투명)
        var portrait = entry.Portrait;
        _portrait.sprite = portrait;
        _portrait.enabled = (collected || visited) && portrait != null;
        _portrait.color = new Color(1f, 1f, 1f, visited ? _visitedAlpha : 1f);

        // 실루엣: 미획득
        _silhouette.sprite = entry.Silhouette;
        _silhouette.enabled = state == CollectionState.Unknown && entry.Silhouette != null;

        _check.SetActive(collected);
        _unknownLabel.SetActive(state == CollectionState.Unknown);
        _visitLabel.SetActive(visited);
    }

    public void SetSelected(bool selected)
    {
        _selectRing.SetActive(selected);
    }
}
