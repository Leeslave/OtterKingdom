using TMPro;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 튜토리얼 모양(Resources/TutorialStyle.asset)을 만든다: 다른 UI와 같은 패널·버튼·폰트, 안내 해달(몽실) 얼굴, 강조 테두리.
/// GlobalUISetup이 함께 호출한다.
/// </summary>
public static class TutorialStyleSetup
{
    private const string StylePath = "Assets/Resources/TutorialStyle.asset";
    private const string TutorialArtFolder = "Assets/Art/UI/Tutorial";
    private const string TitleFontPath = "Assets/Fonts/Cafe24Ssurround-v2.0 SDF.asset";
    private const string BodyFontPath = "Assets/Fonts/NanumSquareRoundOTFR SDF.asset";
    // 강조 테두리 PNG(160px, 모서리 반지름 44)의 9-slice 폭
    private const float HighlightBorder = 52f;

    [MenuItem("Tools/Tutorial/Create Style")]
    public static void CreateStyle()
    {
        var style = LoadOrCreate<TutorialStyle>(StylePath).asset;
        var so = new SerializedObject(style);
        so.FindProperty("_titleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        so.FindProperty("_bodyFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        so.FindProperty("_panel").objectReferenceValue = LoadPanelSprite();
        so.FindProperty("_divider").objectReferenceValue = ImportSprite(CollectionSpriteFolder, "UI_Deco_Leaf");
        so.FindProperty("_guidePortrait").objectReferenceValue = ImportSprite("Assets/Art/Otter", "ICON_Otter_Cast_Explorer");
        so.FindProperty("_portraitFrame").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_RoundButton_Cream");
        so.FindProperty("_nextButton").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Primary");
        so.FindProperty("_skipButton").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Secondary");
        so.FindProperty("_highlight").objectReferenceValue = ImportHighlight();
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static Sprite ImportHighlight()
    {
        string path = $"{TutorialArtFolder}/UI_Tutorial_Highlight.png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = new Vector4(HighlightBorder, HighlightBorder, HighlightBorder, HighlightBorder);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
