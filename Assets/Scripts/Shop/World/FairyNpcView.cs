using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 광장의 요정 NPC. 둥실둥실 떠 있고, 머리 위 "상점" 말풍선이 위아래로 흔들린다. 탭하면 요정 상점이 열린다.
/// 드래그(카메라 이동)와 구분하려고, 누른 곳에서 거의 움직이지 않고 뗐을 때만 탭으로 본다.
/// 이 오브젝트의 위치 = 요정 발밑 (땅에 닿는 높이로 앞뒤를 정함, PlazaDepth).
/// </summary>
public class FairyNpcView : MonoBehaviour
{
    private const float TapThresholdScreenFraction = 0.02f;
    private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

    [Header("구성 요소")]
    [SerializeField] private SpriteRenderer _fairy;
    [SerializeField] private SpriteRenderer _bubble;
    [Tooltip("탭을 받을 영역 (요정 몸)")]
    [SerializeField] private Collider2D _tapArea;

    [Header("움직임")]
    [Tooltip("위아래로 떠 있는 폭 (월드 단위)")]
    [SerializeField] private float _floatHeight = 0.12f;
    [Tooltip("초당 떠오르기 횟수")]
    [SerializeField] private float _floatPerSecond = 0.6f;
    [Tooltip("말풍선이 흔들리는 폭 (월드 단위)")]
    [SerializeField] private float _bubbleBob = 0.08f;

    private Vector3 _fairyBase;
    private Vector3 _bubbleBase;
    private bool _pressing;
    private bool _pressOnUI;
    private Vector2 _pressScreen;

    private void Awake()
    {
        _fairyBase = _fairy.transform.localPosition;
        _bubbleBase = _bubble.transform.localPosition;

        int order = PlazaDepth.SortingOrderFor(transform.position.y);
        _fairy.sortingOrder = order;
        _bubble.sortingOrder = order + 1;
    }

    private void Update()
    {
        float t = Time.time * _floatPerSecond * Mathf.PI * 2f;
        _fairy.transform.localPosition = _fairyBase + Vector3.up * (Mathf.Sin(t) * _floatHeight);
        _bubble.transform.localPosition = _bubbleBase + Vector3.up * (Mathf.Sin(t * 1.5f) * _bubbleBob);

        HandleTap();
    }

    private void HandleTap()
    {
        var pointer = Pointer.current;
        var camera = Camera.main;
        if (pointer == null || camera == null)
            return;

        Vector2 screen = pointer.position.ReadValue();
        if (pointer.press.wasPressedThisFrame)
        {
            _pressing = true;
            _pressScreen = screen;
            _pressOnUI = IsOverUI(screen);
        }

        // 두 손가락이 닿으면 카메라 핀치 줌 → 이번 누름은 탭이 아님
        if (_pressing && IsMultiTouch())
            _pressing = false;

        if (!_pressing || !pointer.press.wasReleasedThisFrame)
            return;

        _pressing = false;
        float threshold = Mathf.Min(Screen.width, Screen.height) * TapThresholdScreenFraction;
        if (_pressOnUI || (screen - _pressScreen).sqrMagnitude > threshold * threshold)
            return;
        if (DecorModePresenter.IsActive || FairyShopPresenter.Instance == null || FairyShopPresenter.Instance.IsOpen)
            return;

        Vector2 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
        if (_tapArea.OverlapPoint(world))
            FairyShopPresenter.Instance.Open();
    }

    /// <summary>요정이 다른 자리로 옮겨 감 (P4: 해달 상점을 다 지으면 그 앞으로 이사). 발밑 위치 기준으로 앞뒤 정렬을 다시 잡음</summary>
    public void MoveTo(Vector2 foot)
    {
        transform.position = new Vector3(foot.x, foot.y, transform.position.z);
        int order = PlazaDepth.SortingOrderFor(foot.y);
        _fairy.sortingOrder = order;
        _bubble.sortingOrder = order + 1;
    }

    private static bool IsMultiTouch()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return false;

        int count = 0;
        foreach (var touch in touchscreen.touches)
        {
            if (touch.isInProgress && ++count >= 2)
                return true;
        }
        return false;
    }

    private static bool IsOverUI(Vector2 screen)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        UiHits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screen }, UiHits);
        return UiHits.Count > 0;
    }
}
