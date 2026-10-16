using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 광장 월드에 스티커 모양(9-slice 판 · 글자)을 그리는 도우미. 그림은 DistrictVisualStyle, 글꼴은 RuntimeUIKit 스타일.
/// 그림이 없으면 흰 사각형으로 그린다 (깨지지 않게)
/// </summary>
public static class WorldSticker
{
    public static readonly Color Cocoa = new Color32(0x4B, 0x2E, 0x22, 0xFF);

    private static DistrictVisualStyle _style;
    private static Sprite _white;
    // (원본 그림, 모서리 월드 크기) → 9-slice용 그림 (모서리가 월드에서 같은 크기로 보이게 PPU를 맞춤)
    private static readonly Dictionary<(Sprite, float), Sprite> Sliced = new Dictionary<(Sprite, float), Sprite>();

    public static DistrictVisualStyle Style
    {
        get
        {
            if (_style == null)
                _style = Resources.Load<DistrictVisualStyle>(DistrictVisualStyle.ResourcePath);
            return _style;
        }
    }

    /// <summary>9-slice 판 하나 (center · size는 월드 단위, corner = 모서리 둥근 부분의 월드 크기)</summary>
    public static SpriteRenderer Panel(Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size, Color color, int order, float corner)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var renderer = go.AddComponent<SpriteRenderer>();
        var sliced = SlicedSprite(sprite, corner);
        renderer.sprite = sliced;
        renderer.color = color;
        renderer.sortingOrder = order;
        if (sliced != null && sliced != White)
        {
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(Mathf.Max(size.x, corner * 2f), Mathf.Max(size.y, corner * 2f));
        }
        else
        {
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
        }
        return renderer;
    }

    /// <summary>
    /// 건물 지붕 위 표 자리 (차지한 칸 기준). 집 · 회관 같은 큰 건물(폭 6칸 이상)은 칸 폭의 1.1배쯤 위로 솟고,
    /// 벤치 · 의자 같은 작은 것은 그보다 낮다 (작은 것에 같은 비율을 쓰면 표가 떠 보임)
    /// </summary>
    public static Vector2 AboveBuilding(Rect footprint)
    {
        float rise = footprint.width >= 6f ? footprint.width * 1.12f + 0.75f : footprint.width * 0.8f + 0.7f;
        return new Vector2(footprint.center.x, footprint.yMin + rise);
    }

    /// <summary>판의 크기를 바꿈 (Panel로 만든 것)</summary>
    public static void Resize(SpriteRenderer renderer, Vector2 size)
    {
        if (renderer.drawMode == SpriteDrawMode.Sliced)
            renderer.size = size;
        else
            renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    /// <summary>월드 글자 (가운데 맞춤, 한 줄. 넘치면 줄어듦)</summary>
    public static TextMeshPro Label(Transform parent, string text, Vector2 center, Vector2 size, float fontSize, int order, bool title = true)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshPro>();
        var font = title ? RuntimeUIKit.Style.TitleFont : RuntimeUIKit.Style.BodyFont;
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * 0.6f;
        label.fontSizeMax = fontSize;
        label.color = Cocoa;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = size;
        label.transform.position = center;
        label.sortingOrder = order;
        return label;
    }

    private static Sprite White
    {
        get
        {
            if (_white == null)
            {
                var texture = Texture2D.whiteTexture;
                _white = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            }
            return _white;
        }
    }

    private static Sprite SlicedSprite(Sprite sprite, float corner)
    {
        if (sprite == null || corner <= 0f)
            return White;
        if (Sliced.TryGetValue((sprite, corner), out var cached) && cached != null)
            return cached;
        // 테두리가 없는 그림(칸 그림)은 가장자리 30%를 모서리로
        var border = sprite.border;
        if (border == Vector4.zero)
        {
            float b = Mathf.Round(Mathf.Min(sprite.rect.width, sprite.rect.height) * 0.3f);
            border = new Vector4(b, b, b, b);
        }
        float ppu = Mathf.Max(border.x, border.z, 1f) / corner;
        var result = Sprite.Create(sprite.texture, sprite.rect, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
        result.name = sprite.name + "_World";
        Sliced[(sprite, corner)] = result;
        return result;
    }
}
