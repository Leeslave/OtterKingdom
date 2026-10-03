using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬 전환 막. 어두워졌다(FadeOut) 밝아진다(FadeIn). 전역 UI의 가장 마지막 자식에 두고,
/// 다른 캔버스(장소 화면의 곡괭이 강화 버튼·팝업, 튜토리얼)보다도 위에 그리도록 자기 정렬 순서를 따로 가진다.
/// Resources에 구름 그림이 있으면 검은 막 대신 뭉게구름이 양옆에서 몰려와 화면을 덮었다가 다시 걷힌다 (막은 크림색).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    private const float CloudCoverSeconds = 0.42f;
    private const float CloudRevealSeconds = 0.5f;
    // 장소 화면 GameUI(200)·튜토리얼(500)보다 위
    private const int SortingOrder = 1000;
    // 한 프레임에 흐르는 시간의 상한: 씬을 불러오느라 멈췄던 시간이 첫 프레임에 몰려 연출을 건너뛰지 않게
    private const float MaxStepSeconds = 1f / 30f;
    // 구름 줄 (화면 높이 비율, 어느 쪽에서 오는지)
    private static readonly (float y, float side)[] CloudRows =
    {
        (0.06f, -1f), (0.26f, 1f), (0.46f, -1f), (0.66f, 1f), (0.86f, -1f), (1.02f, 1f),
    };

    [Header("연출")]
    [Tooltip("어두워지는 시간")]
    [SerializeField] private float _fadeOutDuration = 0.25f;
    [Tooltip("밝아지는 시간")]
    [SerializeField] private float _fadeInDuration = 0.3f;

    [Header("구름 전환")]
    [Tooltip("Resources의 구름 그림 경로 (없으면 검은 막만)")]
    [SerializeField] private string _cloudResource = "UI/TransitionCloud";

    [Tooltip("구름 뒤 막 색 (구름 사이 틈이 보여도 어색하지 않게)")]
    [SerializeField] private Color _cloudBackColor = new Color(0.86f, 0.93f, 0.98f, 1f);

    private CanvasGroup _group;
    private RectTransform _cloudLayer;
    private readonly List<(RectTransform rect, float side)> _clouds = new List<(RectTransform, float)>();

    private bool HasClouds => _clouds.Count > 0;

    private static float Step => Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        RaiseAboveEverything(gameObject, SortingOrder);
        BuildClouds();
    }

    /// <summary>화면을 덮는다. 덮여 있는 동안은 입력도 막는다.</summary>
    public IEnumerator FadeOut()
    {
        _group.blocksRaycasts = true;
        if (HasClouds)
            yield return MoveClouds(true, CloudCoverSeconds);
        else
            yield return Fade(1f, _fadeOutDuration);
    }

    /// <summary>막을 걷어낸다.</summary>
    public IEnumerator FadeIn()
    {
        // 새 씬이 첫 화면을 그린 뒤에 걷음 (씬을 연 프레임의 멈춤이 연출에 섞이지 않게)
        yield return null;
        if (HasClouds)
            yield return MoveClouds(false, CloudRevealSeconds);
        else
            yield return Fade(0f, _fadeInDuration);
        _group.blocksRaycasts = false;
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = _group.alpha;

        // 씬 로딩 중 timeScale이 바뀌어도 연출은 진행되도록 unscaled 시간 사용
        for (float t = 0f; t < duration; t += Step)
        {
            _group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        _group.alpha = target;
    }

    // 구름은 막과 같은 부모의 바로 위 층 (막의 투명도와 따로 움직이게)
    private void BuildClouds()
    {
        var sprite = string.IsNullOrEmpty(_cloudResource) ? null : Resources.Load<Sprite>(_cloudResource);
        var parent = transform.parent as RectTransform;
        if (sprite == null || parent == null)
            return;

        var image = GetComponent<Image>();
        if (image != null)
            image.color = _cloudBackColor;

        var go = new GameObject("TransitionClouds", typeof(RectTransform));
        _cloudLayer = (RectTransform)go.transform;
        _cloudLayer.SetParent(parent, false);
        _cloudLayer.anchorMin = Vector2.zero;
        _cloudLayer.anchorMax = Vector2.one;
        _cloudLayer.offsetMin = Vector2.zero;
        _cloudLayer.offsetMax = Vector2.zero;
        _cloudLayer.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        RaiseAboveEverything(go, SortingOrder + 1);

        foreach (var (y, side) in CloudRows)
        {
            var cloud = new GameObject("Cloud", typeof(RectTransform)).GetComponent<RectTransform>();
            cloud.SetParent(_cloudLayer, false);
            cloud.anchorMin = cloud.anchorMax = new Vector2(0.5f, y);
            var cloudImage = cloud.gameObject.AddComponent<Image>();
            cloudImage.sprite = sprite;
            cloudImage.raycastTarget = false;
            // 오른쪽에서 오는 구름은 좌우를 뒤집어 같은 모양이 반복돼 보이지 않게
            cloud.localScale = new Vector3(side > 0f ? -1f : 1f, 1f, 1f);
            _clouds.Add((cloud, side));
        }
        go.SetActive(false);
    }

    // 자기 캔버스로 정렬 순서를 따로 정함 (입력을 막으려면 그 캔버스에 레이캐스터도 있어야 함)
    private static void RaiseAboveEverything(GameObject target, int order)
    {
        var canvas = target.GetComponent<Canvas>();
        if (canvas == null)
            canvas = target.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        if (target.GetComponent<GraphicRaycaster>() == null)
            target.AddComponent<GraphicRaycaster>();
    }

    // 덮기: 양옆 밖에서 가운데로 몰려옴 / 걷기: 다시 양옆으로 흩어짐. 막도 함께 나타났다 사라짐
    private IEnumerator MoveClouds(bool cover, float duration)
    {
        _cloudLayer.gameObject.SetActive(true);
        _cloudLayer.SetAsLastSibling();
        float width = _cloudLayer.rect.width;
        float cloudWidth = width * 1.35f;
        foreach (var (rect, _) in _clouds)
            rect.sizeDelta = new Vector2(cloudWidth, cloudWidth * 0.51f);

        float startAlpha = _group.alpha;
        float endAlpha = cover ? 1f : 0f;
        for (float t = 0f; t <= duration; t += Step)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            float coverAmount = cover ? k : 1f - k;
            PlaceClouds(width, coverAmount);
            // 막은 구름이 거의 덮은 뒤에 차오르고, 걷힐 때는 먼저 빠짐
            _group.alpha = Mathf.Lerp(startAlpha, endAlpha, cover ? Mathf.Clamp01(k * 1.6f - 0.6f) : Mathf.Clamp01(k * 1.6f));
            yield return null;
        }
        PlaceClouds(width, cover ? 1f : 0f);
        _group.alpha = endAlpha;
        if (!cover)
            _cloudLayer.gameObject.SetActive(false);
    }

    // coverAmount 0 = 화면 밖, 1 = 가운데 (줄마다 조금씩 엇갈림)
    private void PlaceClouds(float width, float coverAmount)
    {
        for (int i = 0; i < _clouds.Count; i++)
        {
            var (rect, side) = _clouds[i];
            float covered = side * width * (0.12f + 0.05f * (i % 3));
            float hidden = side * width * 1.35f;
            rect.anchoredPosition = new Vector2(Mathf.Lerp(hidden, covered, coverAmount), 0f);
        }
    }
}
