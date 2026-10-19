using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 광산 씬의 오래된 갱도 입구: 광산 카트 뒤의 숨 쉬는 빛 + "오래된 갱도" 글자. 갱도를 찾은 뒤에만 보이고(MineMazeRules.FoundDevelopment),
/// 누르면 "들어가 볼까요?" → 미로 장소(MineMaze)로 간다.
/// 광산 씬은 MineSceneSetup이 통째로 다시 만들기 때문에 씬에 넣지 않고, 광산 씬이 열릴 때 여기서 만든다 (Resources/MineMazeLink가 있을 때만)
/// </summary>
public class MineMazeEntranceView : MonoBehaviour
{
    private static readonly Color GlowColor = new Color(1f, 0.85f, 0.45f, 1f);
    private const int GlowOrder = 1;
    private const int LabelOrder = 6;

    private MineMazeLinkConfig _link;
    private BoxCollider2D _collider;
    private SpriteRenderer _glow;
    private TextMeshPro _label;
    private Camera _camera;
    private float _age;
    private bool _shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Spawn(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Spawn(scene);

    private static void Spawn(Scene scene)
    {
        if (scene.name != GameManager.MineZoneId)
            return;
        var link = MineMazeLinkConfig.Instance;
        if (link == null || link.MazeZone == null)
            return;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<MineMazeEntranceView>() != null)
                return;
        }
        var go = new GameObject("OldTunnelEntrance");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<MineMazeEntranceView>().Setup(link);
    }

    private void Setup(MineMazeLinkConfig link)
    {
        _link = link;
        transform.position = link.EntranceWorld;
        _collider = gameObject.AddComponent<BoxCollider2D>();
        _collider.size = link.EntranceWorldSize;

        _glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
        _glow.transform.SetParent(transform, false);
        _glow.sprite = link.Glow;
        _glow.sortingOrder = GlowOrder;

        _label = new GameObject("Label").AddComponent<TextMeshPro>();
        _label.transform.SetParent(transform, false);
        _label.transform.localPosition = LabelOffset(0f);
        var font = RuntimeUIKit.Style.TitleFont;
        if (font != null)
        {
            font.TryAddCharacters("오래된 갱도", out _);
            _label.font = font;
        }
        _label.text = "오래된 갱도";
        _label.fontSize = 4f;
        _label.alignment = TextAlignmentOptions.Center;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.outlineWidth = 0.25f;
        _label.outlineColor = new Color32(75, 46, 34, 255);
        _label.color = new Color(1f, 0.95f, 0.75f);
        _label.sortingOrder = LabelOrder;
        _label.rectTransform.sizeDelta = new Vector2(6f, 2f);
        SetShown(false);
    }

    private void Awake() => _camera = Camera.main;

    // 글자는 카트 아래 (위쪽은 화면 오른쪽 위 뽑기 버튼과 겹침)
    private Vector3 LabelOffset(float bob) => new Vector3(0f, -(_link.EntranceWorldSize.y * 0.5f + 0.45f) + bob, 0f);

    private void SetShown(bool shown)
    {
        _shown = shown;
        _collider.enabled = shown;
        _glow.enabled = shown && _glow.sprite != null;
        _label.enabled = shown;
    }

    private void Update()
    {
        if (_link == null)
            return;
        var settlement = SettlementManager.Instance;
        bool found = GameManager.Instance != null && settlement != null && settlement.IsLoaded
                     && settlement.HasDevelopment(MineMazeRules.FoundDevelopment);
        if (found != _shown)
            SetShown(found);
        if (!found)
            return;

        // 숨 쉬는 빛 · 통통 뜨는 글자
        _age += Time.deltaTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_age * 2.4f);
        if (_glow.sprite != null)
        {
            float size = Mathf.Max(_link.EntranceWorldSize.x, _link.EntranceWorldSize.y) * 1.5f / Mathf.Max(0.01f, _glow.sprite.bounds.size.x);
            _glow.transform.localScale = Vector3.one * size * (0.92f + 0.12f * pulse);
            var c = GlowColor;
            c.a = 0.35f + 0.3f * pulse;
            _glow.color = c;
        }
        _label.transform.localPosition = LabelOffset(Mathf.Sin(_age * 3f) * 0.06f);

        HandleClick();
    }

    private void HandleClick()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame || _camera == null)
            return;
        var game = GameManager.Instance;
        if (game.IsModalOpen)
            return;
        Vector2 screen = pointer.position.ReadValue();
        if (game.IsScreenPointOverUI(screen))
            return;
        Vector2 world = _camera.ScreenToWorldPoint(screen);
        if (!_collider.OverlapPoint(world))
            return;

        game.ShowConfirm("오래된 갱도", "깡깡이가 찾은 오래된 갱도에 들어가 볼까요?", () =>
        {
            var navigator = FindAnyObjectByType<SceneNavigator>();
            if (navigator == null || !navigator.TryGo(_link.MazeZone))
                game.ShowAlert("지금은 갱도에 들어갈 수 없어요");
        });
    }
}
