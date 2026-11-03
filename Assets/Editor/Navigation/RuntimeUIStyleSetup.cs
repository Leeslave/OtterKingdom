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
        so.FindProperty("_ribbon").objectReferenceValue = LoadSprite("Assets/Art/UI/Common", "UI_Ribbon_Peach");
        so.FindProperty("_closeButton").objectReferenceValue = LoadSprite("Assets/Art/UI/Inventory", "UI_Inventory_CloseButton");
        so.FindProperty("_tabNormal").objectReferenceValue = LoadSprite("Assets/Art/UI/Inventory", "UI_Inventory_Tab_Normal");
        so.FindProperty("_tabSelected").objectReferenceValue = LoadSprite("Assets/Art/UI/Inventory", "UI_Inventory_Tab_Selected");
        so.FindProperty("_card").objectReferenceValue = LoadSprite("Assets/Art/UI/Inventory", "UI_Inventory_Box");
        so.FindProperty("_slot").objectReferenceValue = LoadSprite("Assets/Art/UI/Inventory", "UI_Inventory_Slot_Empty");
        so.FindProperty("_slotFilled").objectReferenceValue = LoadSprite("Assets/Art/UI/Inventory", "UI_Inventory_Slot_Filled");
        so.FindProperty("_inset").objectReferenceValue = LoadSprite("Assets/Art/UI/Common", "UI_Box_Inset");
        so.FindProperty("_barTrack").objectReferenceValue = LoadSprite("Assets/Art/UI/Common", "UI_Slider_Track");
        so.FindProperty("_barFill").objectReferenceValue = LoadSprite("Assets/Art/UI/Common", "UI_Bar_Fill");
        so.FindProperty("_knob").objectReferenceValue = LoadSprite("Assets/Art/UI/Common", "UI_Knob");
        so.FindProperty("_plusButton").objectReferenceValue = LoadSprite("Assets/Art/UI/Common", "UI_Button_PlusSmall");
        so.FindProperty("_lockIcon").objectReferenceValue = LoadSprite("Assets/Art/UI/Inventory", "LockIcon");
        so.FindProperty("_tag").objectReferenceValue = LoadSprite("Assets/Art/UI/Common", "UI_Tag_Category");
        so.FindProperty("_tagOn").objectReferenceValue = LoadSprite("Assets/Art/UI/Collection", "UI_Badge_Status");
        so.FindProperty("_tagWarn").objectReferenceValue = LoadSprite("Assets/Art/UI/Common", "UI_Tag_Highlight");
        so.FindProperty("_tagGold").objectReferenceValue = LoadSprite("Assets/Art/UI/Collection", "UI_Tab_Gold");
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}
