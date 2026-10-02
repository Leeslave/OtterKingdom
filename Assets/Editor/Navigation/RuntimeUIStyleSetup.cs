using TMPro;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 코드로만 만드는 장소 UI(GameUI)의 모양(Resources/RuntimeUIStyle.asset)을 만든다: 다른 화면과 같은 패널·버튼·폰트.
/// GlobalUISetup이 함께 호출한다.
/// </summary>
public static class RuntimeUIStyleSetup
{
    private const string StylePath = "Assets/Resources/RuntimeUIStyle.asset";
    private const string TitleFontPath = "Assets/Fonts/Cafe24Ssurround-v2.0 SDF.asset";
    private const string BodyFontPath = "Assets/Fonts/NanumSquareRoundOTFR SDF.asset";

    [MenuItem("Tools/UI/Create Runtime UI Style")]
    public static void CreateStyle()
    {
        var style = LoadOrCreate<RuntimeUIStyle>(StylePath).asset;
        var so = new SerializedObject(style);
        so.FindProperty("_titleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        so.FindProperty("_bodyFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        so.FindProperty("_panel").objectReferenceValue = LoadPanelSprite();
        so.FindProperty("_bubble").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        so.FindProperty("_primaryButton").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Primary");
        so.FindProperty("_secondaryButton").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Secondary");
        so.FindProperty("_optionButton").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        so.FindProperty("_featureButton").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Coin");
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}
