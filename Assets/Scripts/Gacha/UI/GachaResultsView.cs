using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>10회 뽑기 결과: 10칸 (등급 테두리 · NEW · 픽업), 새 장난감 수 · 반짝 조각, [확인] · [10회 더]</summary>
public class GachaResultsView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private RectTransform _panel;

    [SerializeField] private CanvasGroup _group;

    [Tooltip("결과 뒤를 어둡게 하는 막 (뗏목 장난감이 비치지 않게, 화면 전체)")]
    [SerializeField] private CanvasGroup _backdrop;

    [Tooltip("결과 칸 10개 (뽑은 순서)")]
    [SerializeField] private GachaResultCellView[] _cells = new GachaResultCellView[10];

    [Tooltip("새 장난감 n · 반짝 조각 +n")]
    [SerializeField] private TextMeshProUGUI _summaryText;

    [Header("버튼")]
    [SerializeField] private Button _okButton;

    [SerializeField] private Button _againButton;

    [SerializeField] private Image _againImage;

    [SerializeField] private Image _againIcon;

    [SerializeField] private TextMeshProUGUI _againLabel;

    [SerializeField] private TextMeshProUGUI _againCost;

    [Header("등급 색 (흔함 · 레어 · 에픽)")]
    [Tooltip("칸 테두리에 곱하는 색")]
    [SerializeField] private Color[] _frameColors =
    {
        new Color(1f, 0.96f, 0.88f, 1f),
        new Color(0.68f, 0.85f, 1f, 1f),
        new Color(1f, 0.86f, 0.45f, 1f),
    };

    public event Action OnOk;
    public event Action OnAgain;

    private void Awake()
    {
        _okButton.onClick.AddListener(() => OnOk?.Invoke());
        _againButton.onClick.AddListener(() => OnAgain?.Invoke());
    }

    public void Show(GachaPullReport report, GachaAgainInfo again, bool animate)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        int fresh = 0, shards = 0;
        for (int i = 0; i < _cells.Length; i++)
        {
            bool has = i < report.Pulls.Count;
            _cells[i].gameObject.SetActive(has);
            if (!has)
                continue;
            var pull = report.Pulls[i];
            _cells[i].Show(pull, _frameColors[Mathf.Clamp(pull.Tier, 0, _frameColors.Length - 1)]);
            if (pull.IsNew)
                fresh++;
            shards += pull.Shards;
        }
        _summaryText.text = shards > 0 ? $"새 장난감 {fresh}개 · 반짝 조각 +{shards}" : $"새 장난감 {fresh}개";

        _againLabel.text = again.Label;
        _againImage.sprite = again.Button;
        _againIcon.sprite = again.Icon;
        _againIcon.gameObject.SetActive(again.Icon != null);
        _againCost.text = again.Cost;

        gameObject.SetActive(true);
        StopAllCoroutines();
        if (animate)
            StartCoroutine(PopIn());
        else
            Settle();
    }

    public void Hide()
    {
        StopAllCoroutines();
        _backdrop.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    // 패널이 뜨고, 칸이 하나씩 통 튀어나옴
    private IEnumerator PopIn()
    {
        foreach (var cell in _cells)
            cell.transform.localScale = Vector3.zero;
        _backdrop.gameObject.SetActive(true);
        yield return GachaTween.Run(0.3f, t =>
        {
            _group.alpha = t;
            _backdrop.alpha = t;
            _panel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, GachaTween.OutBack(t));
        });
        foreach (var cell in _cells)
        {
            if (!cell.gameObject.activeSelf)
                continue;
            StartCoroutine(GachaTween.Run(0.25f, t => cell.transform.localScale = Vector3.one * GachaTween.OutBack(t)));
            yield return GachaTween.Wait(0.05f);
        }
    }

    private void Settle()
    {
        _backdrop.gameObject.SetActive(true);
        _backdrop.alpha = 1f;
        _group.alpha = 1f;
        _panel.localScale = Vector3.one;
        foreach (var cell in _cells)
            cell.transform.localScale = Vector3.one;
    }
}
