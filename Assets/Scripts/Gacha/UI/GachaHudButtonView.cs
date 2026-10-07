using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 오른쪽의 뽑기 버튼. 뽑기가 열리면(요정이 온 뒤) 보이고, 공짜로 뽑을 수 있으면(뽑기권 · 오늘의 골드 뽑기 · 처음 선물 · 새 배너) 빨간 점.
/// 상태는 0.5초마다 다시 본다 (뽑기권은 재화라 바뀌는 곳이 많음)
/// </summary>
public class GachaHudButtonView : MonoBehaviour
{
    private const float RefreshSeconds = 0.5f;

    [Header("구성 요소")]
    [Tooltip("보이고 숨는 부분 (이 컴포넌트가 붙은 오브젝트는 늘 켜 둠)")]
    [SerializeField] private GameObject _body;

    [SerializeField] private Button _button;

    [Tooltip("공짜로 뽑을 수 있음 표시")]
    [SerializeField] private GameObject _badge;

    [Tooltip("살랑살랑 흔들리는 조개 그림")]
    [SerializeField] private RectTransform _icon;

    public event Action OnClicked;

    private float _timer;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke());
    }

    private void OnEnable()
    {
        _timer = 0f;
        Refresh();
    }

    private void Update()
    {
        // 빨간 점이 있으면 조개가 살랑 (눈에 띄게)
        if (_badge.activeSelf)
            _icon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 5f) * 7f);
        else
            _icon.localRotation = Quaternion.identity;

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f)
            return;
        _timer = RefreshSeconds;
        Refresh();
    }

    private void Refresh()
    {
        var manager = GachaManager.Instance;
        bool visible = manager != null && manager.IsLoaded && manager.IsUnlocked;
        if (_body.activeSelf != visible)
            _body.SetActive(visible);
        if (!visible)
            return;

        bool badge = manager.HasFreePull;
        if (!badge)
        {
            foreach (var banner in manager.OpenBanners())
            {
                if (manager.IsNewBanner(banner))
                {
                    badge = true;
                    break;
                }
            }
        }
        if (_badge.activeSelf != badge)
            _badge.SetActive(badge);
    }
}
