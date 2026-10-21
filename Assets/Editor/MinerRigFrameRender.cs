using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Renders the mining bubble's otter (MineEmoteView) from the miner's cutout
// rig - the same look the plaza shows (Resources/MinerRigV1, QuarterRigView's
// Mine loop): one pickaxe blow as PoseTimes.Length poses in a row, written to
// OutputPath for MineSceneSetup to slice (MinerRig_Mine_0_0..7, feet pivot).
// The bubble then plays them as plain sprite frames - no rig at runtime.
// The rock stays whole (rock_0) so the clip loops without it jumping back.
//
// Each pose is drawn twice, over black and over white, and the alpha comes
// from the difference, so the result doesn't depend on how the render
// pipeline treats the target's alpha. Drawn at Supersample x and averaged down.
// Needs a graphics device: the editor, or batch mode without -nographics.
// Logs the cell size and baseline for MineSceneSetup.Sheets.
// Run via: OtterKingdom > Tools > Render Miner Rig Mine Frames
public static class MinerRigFrameRender
{
    public const string RigFolder = "MinerRigV1";
    public const string OutputPath = "Assets/Sprites/Characters/MinerOtter/MinerRig_Mine.png";
    // Times (s) in QuarterRigView's Mine loop, one blow (0.5 -> 2.4 s): ready at the rock, lifting,
    // over the shoulder, top of the swing, swinging down, BANG, follow-through with the chips, straightening.
    public static readonly float[] PoseTimes = { .5f, .85f, 1.1f, 1.3f, 1.46f, 1.5f, 1.62f, 1.95f };
    // Output pixels per world unit, with the rig at MineSceneSetup.MineRigScale.
    public const float PixelsPerUnit = 200f;
    private const int Supersample = 2;
    private const int Padding = 6;
    // World area around the feet (origin) drawn before cropping.
    private static readonly Rect Capture = new Rect(-2f, -1f, 4f, 4f);

    [MenuItem("OtterKingdom/Tools/Render Miner Rig Mine Frames")]
    public static void Render()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[MinerRigFrameRender] Stop Play mode first.");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var previous = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            RenderSheet();
        }
        finally
        {
            if (!Application.isBatchMode && previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    private static void RenderSheet()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(QuarterRigView);
        var actionTime = type.GetField("actionTime", Private);
        var applyPose = type.GetMethod("ApplyPose", Private);
        var rockField = type.GetField("rock", Private);
        var stagesField = type.GetField("rockStages", Private);
        if (actionTime == null || applyPose == null || rockField == null || stagesField == null)
        {
            Debug.LogError("[MinerRigFrameRender] QuarterRigView changed (actionTime / ApplyPose / rock / rockStages) - update this tool.");
            return;
        }

        var go = new GameObject("MinerRig");
        go.SetActive(false);
        var rig = go.AddComponent<QuarterRigView>();
        var rigSo = new SerializedObject(rig);
        rigSo.FindProperty("resourceFolder").stringValue = RigFolder;
        rigSo.FindProperty("characterScale").floatValue = MineSceneSetup.MineRigScale;
        rigSo.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(true); // builds the parts (ExecuteAlways)
        if (!rig.CanMine)
        {
            Debug.LogError($"[MinerRigFrameRender] Resources/{RigFolder} has no rock_0 - not a mining rig.");
            return;
        }
        rig.PlayLoop(QuarterRigView.Loop.Mine);

        // Sprite-Lit materials: one plain white global light, as with no lighting.
        var light = new GameObject("Light").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
        light.color = Color.white;

        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Capture.height * 0.5f;
        cam.transform.position = new Vector3(Capture.center.x, Capture.center.y, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        int hiW = Mathf.RoundToInt(Capture.width * PixelsPerUnit * Supersample);
        int hiH = Mathf.RoundToInt(Capture.height * PixelsPerUnit * Supersample);
        var target = new RenderTexture(hiW, hiH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = target;
        cam.aspect = Capture.width / Capture.height;

        bool linear = PlayerSettings.colorSpace == ColorSpace.Linear;
        int w = hiW / Supersample, h = hiH / Supersample;
        var frames = new Color[PoseTimes.Length][];
        for (int f = 0; f < PoseTimes.Length; f++)
        {
            actionTime.SetValue(rig, PoseTimes[f]);
            applyPose.Invoke(rig, null);
            var rock = (SpriteRenderer)rockField.GetValue(rig);
            var stages = (Sprite[])stagesField.GetValue(rig);
            rock.sprite = stages[0];

            var overBlack = Grab(cam, target, Color.black);
            var overWhite = Grab(cam, target, Color.white);
            frames[f] = Matte(overBlack, overWhite, hiW, hiH, linear);
        }
        cam.targetTexture = null;
        Object.DestroyImmediate(target);

        // Crop to everything drawn in any pose, keeping the feet at the cell's horizontal centre.
        int feetX = Mathf.RoundToInt(-Capture.xMin * PixelsPerUnit);
        int feetFromTop = Mathf.RoundToInt(Capture.yMax * PixelsPerUnit);
        int minX = w, maxX = -1, minY = h, maxY = -1; // y from the bottom (texture rows)
        foreach (var px in frames)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 0.01f)
                    {
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
        if (maxX < 0)
        {
            Debug.LogError("[MinerRigFrameRender] Nothing was drawn - check the rig resources and the render pipeline.");
            return;
        }
        int half = Mathf.Max(feetX - minX, maxX + 1 - feetX) + Padding;
        int cellW = half * 2;
        int bottom = Mathf.Max(0, minY - Padding), top = Mathf.Min(h, maxY + 1 + Padding);
        int cellH = top - bottom;
        int baselineFromTop = feetFromTop - (h - top);

        var sheet = new Texture2D(cellW * frames.Length, cellH, TextureFormat.RGBA32, false);
        var clear = new Color[sheet.width * sheet.height];
        sheet.SetPixels(clear);
        for (int f = 0; f < frames.Length; f++)
            for (int y = 0; y < cellH; y++)
                for (int x = 0; x < cellW; x++)
                {
                    int sx = feetX - half + x, sy = bottom + y;
                    if (sx < 0 || sx >= w) continue;
                    sheet.SetPixel(f * cellW + x, y, frames[f][sy * w + sx]);
                }
        sheet.Apply();
        File.WriteAllBytes(OutputPath, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        AssetDatabase.ImportAsset(OutputPath);
        Debug.Log($"[MinerRigFrameRender] Wrote {OutputPath}: {frames.Length} frames, cell {cellW}x{cellH}, " +
                  $"baseline {baselineFromTop}px from the top, {PixelsPerUnit} px/unit (update MineSceneSetup.Sheets if these changed).");
    }

    private static Color[] Grab(Camera cam, RenderTexture target, Color background)
    {
        cam.backgroundColor = background;
        cam.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var read = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        var pixels = read.GetPixels();
        Object.DestroyImmediate(read);
        return pixels;
    }

    // Over black the pixel is colour * alpha, over white it is that + (1 - alpha) (blended in linear
    // space in a linear project). Averages Supersample x Supersample blocks, premultiplied.
    private static Color[] Matte(Color[] overBlack, Color[] overWhite, int hiW, int hiH, bool linear)
    {
        int w = hiW / Supersample, h = hiH / Supersample;
        var result = new Color[w * h];
        float n = Supersample * Supersample;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int dy = 0; dy < Supersample; dy++)
                    for (int dx = 0; dx < Supersample; dx++)
                    {
                        int i = (y * Supersample + dy) * hiW + x * Supersample + dx;
                        Color k = overBlack[i], wh = overWhite[i];
                        if (linear) { k = k.linear; wh = wh.linear; }
                        float alpha = Mathf.Clamp01(1f - ((wh.r - k.r) + (wh.g - k.g) + (wh.b - k.b)) / 3f);
                        r += k.r; g += k.g; b += k.b; a += alpha;
                    }
                a /= n;
                if (a <= 0.003f) continue;
                var c = new Color(Mathf.Clamp01(r / n / a), Mathf.Clamp01(g / n / a), Mathf.Clamp01(b / n / a), a);
                result[y * w + x] = linear ? new Color(c.gamma.r, c.gamma.g, c.gamma.b, a) : c;
            }
        return result;
    }
}
