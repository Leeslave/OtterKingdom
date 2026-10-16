using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The "mining in progress" bubble beside the mine entrance: a speech bubble
// (tail pointing at the tunnel) with the miner otter's mining clip playing
// inside it. It stands in for the otter while the otter is inside, so
// MinerOtterController shows/hides it. Tapping it asks
// "채굴을 그만할까요?"; "예" turns mining off and the otter walks out.
//
// The bubble pops in when shown and bobs a little; the otter inside is a
// SpriteFrameAnimator playing ClipMine on loop. Each find's icon pops out of
// the top of the bubble and floats away (ShowFind).
public class MineEmoteView : MonoBehaviour
{
    public const string ClipMine = "Mine";

    [SerializeField] private SpriteRenderer bubbleRenderer;
    [SerializeField] private SpriteRenderer otterRenderer;
    [SerializeField] private SpriteFrameAnimator otterAnimator;
    [SerializeField] private Collider2D bubbleCollider;

    [Header("Animation")]
    [SerializeField] private float bobHeight = 0.05f;
    [Tooltip("Seconds per bob up and down.")]
    [SerializeField, Min(0.1f)] private float bobPeriod = 2f;
    [SerializeField, Min(0.01f)] private float popInSeconds = 0.2f;

    [Header("Find pop-up")]
    [Tooltip("Width of the found item's icon, world units.")]
    [SerializeField, Min(0.05f)] private float findIconWidth = 0.7f;
    [Tooltip("How far the icon rises from the top of the bubble.")]
    [SerializeField] private float findRise = 0.9f;
    [SerializeField, Min(0.1f)] private float findSeconds = 1.3f;
    [Tooltip("Above the bubble (3) and the otter inside it (4).")]
    [SerializeField] private int findSortingOrder = 5;

    private class FindPopup
    {
        public SpriteRenderer Renderer;
        public Vector3 Start;
        public float Scale;
        public float Age;
    }

    private Camera mainCamera;
    private Vector3 restLocalPosition;
    private float shownTime;
    private bool visible;
    private readonly List<FindPopup> popups = new List<FindPopup>();

    private void Awake()
    {
        mainCamera = Camera.main;
        restLocalPosition = transform.localPosition;
        ApplyVisible(false);
    }

    public void SetVisible(bool show)
    {
        if (visible == show) return;
        ApplyVisible(show);
        shownTime = Time.time;
        transform.localScale = show ? Vector3.zero : Vector3.one;
        if (show) otterAnimator.Play(ClipMine, restart: true);
    }

    private void ApplyVisible(bool show)
    {
        visible = show;
        bubbleRenderer.enabled = show;
        otterRenderer.enabled = show;
        otterAnimator.enabled = show;
        bubbleCollider.enabled = show;
    }

    // The found item's icon pops out of the top of the bubble, rises and
    // fades. Not parented to the bubble, so the bob and a hide don't move or
    // cut it short.
    public void ShowFind(Sprite icon)
    {
        if (icon == null) return;

        var go = new GameObject("FindPopup");
        go.transform.SetParent(transform.parent, false);
        var popupRenderer = go.AddComponent<SpriteRenderer>();
        popupRenderer.sprite = icon;
        popupRenderer.sortingOrder = findSortingOrder;

        var start = new Vector3(transform.position.x, bubbleRenderer.bounds.max.y, transform.position.z);
        go.transform.position = start;
        float scale = findIconWidth / Mathf.Max(0.01f, icon.bounds.size.x);
        go.transform.localScale = Vector3.zero;
        popups.Add(new FindPopup { Renderer = popupRenderer, Start = start, Scale = scale });
    }

    private void UpdatePopups(float deltaTime)
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var popup = popups[i];
            popup.Age += deltaTime;
            float t = popup.Age / findSeconds;
            if (t >= 1f || popup.Renderer == null)
            {
                if (popup.Renderer != null) Destroy(popup.Renderer.gameObject);
                popups.RemoveAt(i);
                continue;
            }

            // Quick pop to full size, ease-out rise, fade over the last 40%.
            float pop = Mathf.Clamp01(t / 0.15f);
            float scale = popup.Scale * Mathf.Sin(pop * Mathf.PI * 0.5f) * (1f + 0.2f * Mathf.Sin(pop * Mathf.PI));
            float rise = findRise * (1f - (1f - t) * (1f - t));
            var tr = popup.Renderer.transform;
            tr.localScale = new Vector3(scale, scale, 1f);
            tr.position = popup.Start + new Vector3(0f, rise, 0f);
            var color = popup.Renderer.color;
            color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            popup.Renderer.color = color;
        }
    }

    private void Update()
    {
        UpdatePopups(Time.deltaTime);
        if (!visible) return;

        float age = Time.time - shownTime;
        float pop = Mathf.Clamp01(age / popInSeconds);
        // Small overshoot so it pops rather than just grows.
        float scale = pop < 1f ? Mathf.Sin(pop * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(pop * Mathf.PI)) : 1f;
        transform.localScale = new Vector3(scale, scale, 1f);

        float bob = Mathf.Sin(age / bobPeriod * Mathf.PI * 2f) * bobHeight;
        transform.localPosition = restLocalPosition + new Vector3(0f, bob, 0f);

        HandleClick();
    }

    private void HandleClick()
    {
        if (GameManager.Instance == null) return;

        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (mainCamera == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        if (GameManager.Instance.IsScreenPointOverUI(screenPos)) return;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        if (!bubbleCollider.OverlapPoint(worldPos)) return;

        GameManager.Instance.ShowConfirm("채굴 중단", "채굴을 그만할까요?",
            () => GameManager.Instance.SetMiningActive(false));
    }
}
