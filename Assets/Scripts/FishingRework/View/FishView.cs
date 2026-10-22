using TMPro;
using UnityEngine;

/// <summary>
/// 바다 속 물고기 한 마리의 모습: 헤엄(꼬리 흔들기 · 방향), 희귀 빛, 걸리면 퍼덕임, 너무 무거우면 "무거워요" 표시
/// </summary>
public class FishView : MonoBehaviour
{
    private const int Order = -60;

    private FishingFish _fish;
    private SpriteRenderer _body;
    private SpriteRenderer _glow;
    private TextMeshPro _tag;
    private float _baseScale;
    private float _wiggle;
    private float _heavyShow;
    private float _appear;

    public FishingFish Fish => _fish;

    public static FishView Create(Transform parent, FishingFish fish, MineReworkArt ui, TMP_FontAsset font)
    {
        var go = new GameObject($"Fish_{fish.Def.FishId}_{fish.Id}");
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<FishView>();
        view.Setup(fish, ui, font);
        return view;
    }

    private void Setup(FishingFish fish, MineReworkArt ui, TMP_FontAsset font)
    {
        _fish = fish;
        _wiggle = fish.BobPhase;
        _body = new GameObject("Body").AddComponent<SpriteRenderer>();
        _body.transform.SetParent(transform, false);
        _body.sprite = fish.Def.Icon;
        _body.sortingOrder = Order;
        float width = fish.Def.Icon != null ? fish.Def.Icon.bounds.size.x : 1f;
        _baseScale = fish.Def.LengthAt(fish.Size) / Mathf.Max(0.01f, width);

        if (fish.Def.Rarity >= FishRarity.Rare && ui != null)
        {
            _glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            _glow.transform.SetParent(transform, false);
            _glow.sprite = ui.Glow;
            _glow.sortingOrder = Order - 1;
            var color = FishingReworkArt.RarityColor(fish.Def.Rarity);
            _glow.color = new Color(color.r, color.g, color.b, 0.35f);
            float glowWidth = ui.Glow != null ? ui.Glow.bounds.size.x : 1f;
            _glow.transform.localScale = Vector3.one * (fish.Def.LengthAt(fish.Size) * 1.6f / glowWidth);
        }

        _tag = new GameObject("Tag").AddComponent<TextMeshPro>();
        _tag.transform.SetParent(transform, false);
        if (font != null)
            _tag.font = font;
        _tag.fontSize = 2.4f;
        _tag.alignment = TextAlignmentOptions.Center;
        _tag.textWrappingMode = TextWrappingModes.NoWrap;
        _tag.color = new Color(1f, 0.45f, 0.4f);
        _tag.outlineWidth = 0.25f;
        _tag.outlineColor = new Color32(75, 46, 34, 255);
        _tag.sortingOrder = Order + 30;
        _tag.rectTransform.sizeDelta = new Vector2(4f, 1f);
        _tag.gameObject.SetActive(false);

        transform.localPosition = fish.Position;
        transform.localScale = Vector3.zero;
    }

    /// <summary>릴보다 무거워서 놓침: 잠깐 무게 표시</summary>
    public void ShowHeavy()
    {
        _heavyShow = 1.6f;
        _tag.text = $"{_fish.Weight:0.#}kg 무거워요!";
        _tag.gameObject.SetActive(true);
    }

    /// <summary>바늘이 가까우면 너무 무거운 물고기는 늘 표시</summary>
    public void Tick(float dt, Vector2 hook, bool hookInWater, float capacity)
    {
        _appear = Mathf.Min(1f, _appear + dt * 2.5f);
        _wiggle += dt * (_fish.Hooked ? 22f : 6f + _fish.Def.SwimSpeed * 4f);
        transform.localPosition = new Vector3(_fish.Position.x, _fish.Position.y, 0f);

        // 그림이 왼쪽을 보면(본 게임 물고기 아이콘) 뒤집어서 오른쪽 기준으로
        float face = _fish.Def.FacesLeft ? -1f : 1f;
        float flip = (_fish.Dir >= 0f ? 1f : -1f) * face;
        float pop = Mathf.Sin(_appear * Mathf.PI * 0.5f);
        float squash = 1f + 0.06f * Mathf.Sin(_wiggle);
        if (_fish.Hooked)
        {
            // 걸림: 머리를 위로, 퍼덕퍼덕
            _body.transform.localRotation = Quaternion.Euler(0f, 0f, 70f + 25f * Mathf.Sin(_wiggle));
            _body.transform.localScale = new Vector3(_baseScale * face, _baseScale * squash, 1f);
        }
        else
        {
            _body.transform.localRotation = Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(_wiggle * 0.5f) * flip);
            _body.transform.localScale = new Vector3(_baseScale * flip * squash, _baseScale, 1f);
        }
        transform.localScale = Vector3.one * pop;

        if (_glow != null)
        {
            var c = _glow.color;
            c.a = 0.25f + 0.15f * Mathf.Sin(Time.time * 3f + _fish.Id);
            _glow.color = c;
        }

        bool near = hookInWater && !_fish.Hooked && _fish.Weight > capacity && (hook - _fish.Position).sqrMagnitude < 9f;
        if (_heavyShow > 0f)
            _heavyShow -= dt;
        bool show = _heavyShow > 0f || near;
        if (show && !_tag.gameObject.activeSelf)
        {
            _tag.text = $"{_fish.Weight:0.#}kg 무거워요!";
            _tag.gameObject.SetActive(true);
        }
        else if (!show && _tag.gameObject.activeSelf)
            _tag.gameObject.SetActive(false);
        if (show)
        {
            _tag.transform.localPosition = new Vector3(0f, _fish.Def.LengthAt(_fish.Size) * 0.45f + 0.35f, 0f);
            _tag.transform.localScale = Vector3.one / Mathf.Max(0.05f, pop);
        }
    }
}
