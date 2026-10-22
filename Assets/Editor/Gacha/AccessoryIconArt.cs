using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 장신구 아이콘 3종 (깃털 모자 · 반짝 장갑 · 행운 목걸이)을 코드로 그린다 (가챠 개편 5단계, 그림이 오기 전까지 쓰는 그림).
/// 등급은 결과 카드 · 명부의 등급 표시로 구분하므로 같은 종류는 한 그림. 4배로 그려 줄여서 가장자리를 부드럽게.
/// 색은 해달 그림체(코코아 테두리 · 크림 바탕)에 맞춤. 배치 모드: -executeMethod AccessoryIconArt.Generate
/// </summary>
public static class AccessoryIconArt
{
    internal const string Folder = "Assets/Art/Item/Accessory";
    private const int Size = 256;
    private const int Scale = 4;

    private static readonly Color Outline = new Color32(0x6B, 0x4A, 0x3A, 0xFF);

    [MenuItem("Tools/Gacha/Draw Accessory Icons")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        Save("ICON_Accessory_FeatherHat", DrawHat);
        Save("ICON_Accessory_SparkleGloves", DrawGloves);
        Save("ICON_Accessory_LuckyNecklace", DrawNecklace);
        AssetDatabase.Refresh();
        foreach (var name in new[] { "ICON_Accessory_FeatherHat", "ICON_Accessory_SparkleGloves", "ICON_Accessory_LuckyNecklace" })
            ConfigureSprite($"{Folder}/{name}.png");
        Debug.Log("[AccessoryIconArt] 장신구 아이콘 3종을 그렸습니다.");
    }

    // 큰 캔버스 좌표(0~1, 아래가 0)에서 색을 고르는 함수 → 4배 그려 평균
    private static void Save(string name, Func<float, float, Color> shade)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < Scale; sy++)
                {
                    for (int sx = 0; sx < Scale; sx++)
                    {
                        float u = (x + (sx + 0.5f) / Scale) / Size;
                        float v = (y + (sy + 0.5f) / Scale) / Size;
                        var c = shade(u, v);
                        sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                    }
                }
                sum /= Scale * Scale;
                pixels[y * Size + x] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes($"{Folder}/{name}.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void ConfigureSprite(string path)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    #region 모양

    // 타원 안이면 1 (cx, cy 중심, rx, ry 반지름)
    private static bool Ellipse(float u, float v, float cx, float cy, float rx, float ry) =>
        (u - cx) * (u - cx) / (rx * rx) + (v - cy) * (v - cy) / (ry * ry) <= 1f;

    private static bool Ring(float u, float v, float cx, float cy, float r, float width)
    {
        float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
        return Mathf.Abs(d - r) <= width;
    }

    // 별 모양 반짝이 (네 갈래)
    private static bool Sparkle(float u, float v, float cx, float cy, float r)
    {
        float dx = Mathf.Abs(u - cx), dy = Mathf.Abs(v - cy);
        return dx * dy * 40f + dx + dy <= r && (dx < r * 0.25f || dy < r * 0.25f || dx + dy < r * 0.45f);
    }

    // 모양 + 테두리 두께: 테두리면 Outline, 안이면 fill
    private static Color Layer(Color current, bool inside, bool insideGrown, Color fill)
    {
        if (inside)
            return fill;
        if (insideGrown)
            return Outline;
        return current;
    }

    private static Color DrawHat(float u, float v)
    {
        var c = Color.clear;
        const float o = 0.025f;
        var brim = new Color32(0xC9, 0x8A, 0x55, 0xFF);
        var crown = new Color32(0xE0, 0xA4, 0x6A, 0xFF);
        var band = new Color32(0xE8, 0x6A, 0x6A, 0xFF);
        var feather = new Color32(0x8F, 0xD3, 0xE8, 0xFF);
        // 깃털 (뒤): 기울어진 긴 타원
        float fu = (u - 0.66f) * 0.94f - (v - 0.62f) * 0.34f, fv = (u - 0.66f) * 0.34f + (v - 0.62f) * 0.94f;
        c = Layer(c, Ellipse(fu, fv, 0f, 0f, 0.09f, 0.26f), Ellipse(fu, fv, 0f, 0f, 0.09f + o, 0.26f + o), feather);
        if (Ellipse(fu, fv, 0f, 0f, 0.012f, 0.24f) && fv > -0.2f)
            c = Outline;
        // 챙
        c = Layer(c, Ellipse(u, v, 0.5f, 0.33f, 0.4f, 0.11f), Ellipse(u, v, 0.5f, 0.33f, 0.4f + o, 0.11f + o), brim);
        // 몸통 (위가 둥근 반타원)
        bool body = Ellipse(u, v, 0.5f, 0.36f, 0.24f, 0.3f) && v >= 0.33f;
        bool bodyGrown = Ellipse(u, v, 0.5f, 0.36f, 0.24f + o, 0.3f + o) && v >= 0.33f - o;
        c = Layer(c, body, bodyGrown, crown);
        // 띠
        if (body && v >= 0.37f && v <= 0.44f)
            c = band;
        return c;
    }

    private static Color DrawGloves(float u, float v)
    {
        var c = Color.clear;
        const float o = 0.025f;
        var glove = new Color32(0xF4, 0xC7, 0x6B, 0xFF);
        var cuff = new Color32(0xFF, 0xF1, 0xD6, 0xFF);
        var shine = new Color32(0xFF, 0xF6, 0xB0, 0xFF);
        // 손바닥 · 엄지 · 소매
        bool palm = Ellipse(u, v, 0.48f, 0.5f, 0.2f, 0.25f);
        bool thumb = Ellipse(u, v, 0.31f, 0.47f, 0.08f, 0.12f);
        bool wrist = u >= 0.32f && u <= 0.64f && v >= 0.18f && v <= 0.32f;
        bool palmG = Ellipse(u, v, 0.48f, 0.5f, 0.2f + o, 0.25f + o);
        bool thumbG = Ellipse(u, v, 0.31f, 0.47f, 0.08f + o, 0.12f + o);
        bool wristG = u >= 0.32f - o && u <= 0.64f + o && v >= 0.18f - o && v <= 0.32f + o;
        c = Layer(c, palm || thumb, palmG || thumbG, glove);
        c = Layer(c, wrist, wristG, cuff);
        // 손가락 사이 선
        if (palm && v > 0.58f && (Mathf.Abs(u - 0.43f) < 0.008f || Mathf.Abs(u - 0.53f) < 0.008f))
            c = Outline;
        // 반짝이
        if (Sparkle(u, v, 0.76f, 0.76f, 0.12f) || Sparkle(u, v, 0.2f, 0.8f, 0.07f))
            c = shine;
        return c;
    }

    private static Color DrawNecklace(float u, float v)
    {
        var c = Color.clear;
        const float o = 0.025f;
        var cord = new Color32(0xB0, 0x7A, 0x52, 0xFF);
        var bead = new Color32(0x9F, 0xD8, 0xC4, 0xFF);
        var shell = new Color32(0xFF, 0xC4, 0xC4, 0xFF);
        // 끈 (위쪽 반원)
        if (Ring(u, v, 0.5f, 0.55f, 0.3f, 0.018f) && v >= 0.38f)
            c = cord;
        // 구슬
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Lerp(200f, 340f, i / 5f) * Mathf.Deg2Rad;
            float bx = 0.5f + Mathf.Cos(a) * 0.3f, by = 0.55f - Mathf.Sin(a) * 0.3f;
            if (by < 0.38f)
                continue;
            c = Layer(c, Ellipse(u, v, bx, by, 0.035f, 0.035f), Ellipse(u, v, bx, by, 0.035f + o * 0.6f, 0.035f + o * 0.6f), bead);
        }
        // 조개 펜던트 (부채꼴) + 줄무늬
        bool shellIn = Ellipse(u, v, 0.5f, 0.3f, 0.15f, 0.13f) && v >= 0.22f;
        bool shellG = Ellipse(u, v, 0.5f, 0.3f, 0.15f + o, 0.13f + o) && v >= 0.22f - o;
        c = Layer(c, shellIn, shellG, shell);
        if (shellIn && v > 0.25f && (Mathf.Abs(u - 0.45f) < 0.007f || Mathf.Abs(u - 0.55f) < 0.007f || Mathf.Abs(u - 0.5f) < 0.007f))
            c = Outline;
        return c;
    }

    #endregion
}
