using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// P2 그림 가져오기: Tools/UIGen/p2_sources의 원본(GPT로 만든 그림을 PNG로 바꾼 것)을 게임에 맞게 다듬어 Assets에 넣는다.
/// - 흰 배경이면 가장자리에서 이어진 흰 부분만 지움 (외곽선 안쪽의 흰 털·종이는 그대로)
/// - 그림이 있는 곳만 남기고 정해진 크기로 줄임 (아이콘 160, 얼굴 256, 광장 소품은 세로 픽셀 수)
/// - 또박이 걷기 시트: 4×6 칸을 방문 해달 시트와 같은 240×260 칸, 발밑 기준선 249로 다시 맞춤 (OtterVisitorSpriteSetup이 자름)
/// 여러 번 실행해도 결과가 같다. Tools/Settlement/Import P2 Art
/// </summary>
public static partial class SettlementSetup
{
    private const string P2SourceFolder = "Tools/UIGen/p2_sources";
    private const string VisitorSheetFolder = "Assets/Sprites/Characters/Visitors";

    // (원본, 결과 폴더, 결과 이름, 가장 긴 변 픽셀(아이콘·얼굴) 또는 세로 픽셀(광장 소품), 정사각 캔버스인지)
    private static readonly (string source, string folder, string name, int size, bool square)[] P2Art =
    {
        ("clerk_portrait", OtterFolder, ClerkPortraitName, 256, true),
        ("icon_board_upgrade", ArtFolder, "ICON_BoardUpgrade", 160, true),
        ("icon_common_space", ArtFolder, "ICON_CommonSpace", 160, true),
        ("icon_tidy_board", ArtFolder, "ICON_TidyBoard", 160, true),
        ("icon_town_hall", ArtFolder, "ICON_TownHall", 160, true),
        // 접수소 아이콘은 따로 없어서 광장 그림을 줄여 씀
        ("guild_office", ArtFolder, "ICON_GuildOffice", 160, true),
        ("board_upgraded", ArtFolder, "Prop_Board_Upgraded", 420, false),
        ("guild_office", ArtFolder, "Prop_GuildOffice", 560, false),
        ("town_hall", ArtFolder, "Prop_TownHall", 660, false),
    };

    // 방문 해달 시트 칸 (OtterVisitorSpriteSetup과 같게)
    private const int SheetCols = 4;
    private const int SheetRows = 6;
    private const int SheetCellW = 240;
    private const int SheetCellH = 260;
    private const int SheetBaseline = 249;
    // 물감이 시트의 해달 키(귀 끝 → 발끝)에 맞춤
    private const int SheetCharacterHeight = 222;

    [MenuItem("Tools/Settlement/Import P2 Art")]
    public static void ImportP2Art()
    {
        foreach (var art in P2Art)
        {
            var texture = LoadSource(art.source);
            if (texture == null)
                continue;
            var trimmed = Trim(RemoveWhiteBackground(texture));
            var result = art.square ? FitSquare(trimmed, art.size) : FitHeight(trimmed, art.size);
            Save(result, $"{art.folder}/{art.name}.png");
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(trimmed);
            Object.DestroyImmediate(result);
        }

        var sheet = LoadSource("clerk_sheet");
        if (sheet != null)
        {
            var packed = RepackSheet(RemoveWhiteBackground(sheet));
            Save(packed, $"{VisitorSheetFolder}/Clerk.png");
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(packed);
        }
        AssetDatabase.Refresh();
        Debug.Log("[SettlementSetup] P2 그림 가져오기 완료");
    }

    private static Texture2D LoadSource(string name)
    {
        string path = $"{P2SourceFolder}/{name}.png";
        if (!File.Exists(path))
        {
            Debug.LogError($"[SettlementSetup] P2 원본 그림이 없습니다: {path}");
            return null;
        }
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(path));
        return texture;
    }

    private static void Save(Texture2D texture, string path)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
    }

    #region 다듬기

    // 가장자리와 이어진 밝고 무채색인 픽셀만 투명하게 (이미 투명한 배경이면 그대로). 경계는 밝기만큼 반투명으로 남겨 부드럽게
    private static Texture2D RemoveWhiteBackground(Texture2D source)
    {
        int w = source.width, h = source.height;
        var pixels = source.GetPixels32();
        bool IsBackground(Color32 c) => c.a < 20 || (c.r > 228 && c.g > 228 && c.b > 228 && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b)) < 18);

        var visited = new bool[w * h];
        var queue = new Queue<int>();
        void Push(int x, int y)
        {
            int i = y * w + x;
            if (visited[i] || !IsBackground(pixels[i]))
                return;
            visited[i] = true;
            queue.Enqueue(i);
        }
        for (int x = 0; x < w; x++)
        {
            Push(x, 0);
            Push(x, h - 1);
        }
        for (int y = 0; y < h; y++)
        {
            Push(0, y);
            Push(w - 1, y);
        }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w, y = i / w;
            if (x > 0) Push(x - 1, y);
            if (x < w - 1) Push(x + 1, y);
            if (y > 0) Push(x, y - 1);
            if (y < h - 1) Push(x, y + 1);
        }

        var result = new Color32[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            if (visited[i])
                c.a = 0;
            else if (IsNextToBackground(visited, i, w, h))
            {
                // 외곽선 바깥 테두리의 밝은 반쯤 섞인 픽셀은 밝을수록 투명하게 (흰 테두리가 남지 않게)
                float light = (c.r + c.g + c.b) / (3f * 255f);
                c.a = (byte)(c.a * Mathf.Clamp01((1f - light) * 4f + 0.15f));
            }
            result[i] = c;
        }
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.SetPixels32(result);
        texture.Apply();
        return texture;
    }

    private static bool IsNextToBackground(bool[] background, int i, int w, int h)
    {
        int x = i % w, y = i / w;
        return (x > 0 && background[i - 1]) || (x < w - 1 && background[i + 1])
            || (y > 0 && background[i - w]) || (y < h - 1 && background[i + w]);
    }

    // 그림이 있는 곳(알파 > 16)만 남김
    private static Texture2D Trim(Texture2D source)
    {
        var rect = OpaqueBounds(source, new RectInt(0, 0, source.width, source.height));
        var texture = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
        texture.SetPixels(source.GetPixels(rect.x, rect.y, rect.width, rect.height));
        texture.Apply();
        return texture;
    }

    private static RectInt OpaqueBounds(Texture2D texture, RectInt area)
    {
        var pixels = texture.GetPixels32();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = area.yMin; y < area.yMax; y++)
        {
            for (int x = area.xMin; x < area.xMax; x++)
            {
                if (pixels[y * texture.width + x].a <= 16)
                    continue;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }
        return maxX < 0 ? area : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // 정사각 캔버스 가운데에 (여백 6%)
    private static Texture2D FitSquare(Texture2D source, int size)
    {
        float scale = size * 0.94f / Mathf.Max(source.width, source.height);
        int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
        var canvas = Blank(size, size);
        Blit(source, canvas, new RectInt((size - w) / 2, (size - h) / 2, w, h));
        return canvas;
    }

    // 세로를 height로 맞추고 가로는 비율대로 (광장 소품: 발밑 = 그림 아래)
    private static Texture2D FitHeight(Texture2D source, int height)
    {
        float scale = (float)height / source.height;
        int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        var canvas = Blank(w, height);
        Blit(source, canvas, new RectInt(0, 0, w, height));
        return canvas;
    }

    private static Texture2D Blank(int w, int h)
    {
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.SetPixels32(new Color32[w * h]);
        return texture;
    }

    // source 전체를 target의 rect에 늘이거나 줄여 그림 (줄일 때 뭉개지지 않게 칸 평균)
    private static void Blit(Texture2D source, Texture2D target, RectInt rect)
    {
        float sx = (float)source.width / rect.width;
        float sy = (float)source.height / rect.height;
        int samples = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(sx, sy)), 1, 4);
        var src = source.GetPixels();
        for (int y = 0; y < rect.height; y++)
        {
            for (int x = 0; x < rect.width; x++)
            {
                Color sum = Color.clear;
                for (int j = 0; j < samples; j++)
                {
                    for (int i = 0; i < samples; i++)
                    {
                        float u = (x + (i + 0.5f) / samples) * sx;
                        float v = (y + (j + 0.5f) / samples) * sy;
                        var c = src[Mathf.Clamp((int)v, 0, source.height - 1) * source.width + Mathf.Clamp((int)u, 0, source.width - 1)];
                        // 알파를 곱해 더해야 투명한 이웃의 색이 번지지 않음
                        sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                    }
                }
                sum /= samples * samples;
                var color = sum.a > 0.0001f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
                target.SetPixel(rect.x + x, rect.y + y, color);
            }
        }
        target.Apply();
    }

    #endregion

    #region 걷기 시트

    // GPT 시트(칸이 정확하지 않음)를 칸마다 그림이 있는 곳만 잘라, 모든 칸에 같은 배율로 240×260 칸 가운데·발밑 기준선에 맞춰 다시 그림
    private static Texture2D RepackSheet(Texture2D source)
    {
        float cellW = (float)source.width / SheetCols;
        float cellH = (float)source.height / SheetRows;
        var bounds = new RectInt[SheetRows, SheetCols];
        int idleHeight = 0;
        for (int row = 0; row < SheetRows; row++)
        {
            for (int col = 0; col < SheetCols; col++)
            {
                // 텍스처는 아래부터 세므로 row 0(맨 위)는 y가 가장 큼
                var area = new RectInt(Mathf.RoundToInt(col * cellW), Mathf.RoundToInt(source.height - (row + 1) * cellH),
                    Mathf.RoundToInt(cellW), Mathf.RoundToInt(cellH));
                area.width = Mathf.Min(area.width, source.width - area.x);
                area.height = Mathf.Min(area.height, source.height - area.y);
                bounds[row, col] = OpaqueBounds(source, area);
                if (row == 0)
                    idleHeight = Mathf.Max(idleHeight, bounds[row, col].height);
            }
        }

        float scale = (float)SheetCharacterHeight / Mathf.Max(1, idleHeight);
        var sheet = Blank(SheetCellW * SheetCols, SheetCellH * SheetRows);
        for (int row = 0; row < SheetRows; row++)
        {
            for (int col = 0; col < SheetCols; col++)
            {
                var b = bounds[row, col];
                var frame = new Texture2D(b.width, b.height, TextureFormat.RGBA32, false);
                frame.SetPixels(source.GetPixels(b.x, b.y, b.width, b.height));
                frame.Apply();

                int w = Mathf.Min(SheetCellW - 2, Mathf.RoundToInt(b.width * scale));
                int h = Mathf.Min(SheetBaseline - 2, Mathf.RoundToInt(b.height * scale));
                int cellLeft = col * SheetCellW;
                int cellBottom = (SheetRows - 1 - row) * SheetCellH;
                // 발밑(그림 아래)을 칸 위에서 249 = 칸 아래에서 11에 둠
                var rect = new RectInt(cellLeft + (SheetCellW - w) / 2, cellBottom + (SheetCellH - SheetBaseline), w, h);
                Blit(frame, sheet, rect);
                Object.DestroyImmediate(frame);
            }
        }
        return sheet;
    }

    #endregion
}
