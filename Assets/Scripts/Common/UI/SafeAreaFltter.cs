using UnityEngine;

/// <summary>
/// 노치/펀치홀/홈 인디케이터를 피하도록 RectTransform을 Screen.safeArea에 맞춘다.
/// Canvas 바로 아래, 화면 전체를 덮는 루트 오브젝트에 붙인다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFltter : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        Apply();
    }

    private void Update()
    {
        // 회전, 해상도 변경을 알려주는 이벤트가 없어서 매 프레임 비교
        bool screenChanged =
            Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y;
        if (Screen.safeArea != _lastSafeArea || screenChanged)
            Apply();
    }

    private void Apply()
    {
        Rect safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        // 픽셀 좌표 -> 0-1 비율로 변환
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;

        // 앵커에 딱 붙도록 여백 제거
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;
    }
}
