using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 왕국 레벨 표(경험치 곡선 + 레벨업 보상)와 레벨업 팝업을 만든다. GlobalUISetup이 함께 호출한다.
/// 레벨 표는 이미 있으면 덮어쓰지 않는다 (기획이 에셋에서 고친 값을 지킴).
/// </summary>
public static class ProgressionSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Profile";
    internal const string LevelTablePath = DataFolder + "/LevelTable.asset";
    private const string GemPath = "Assets/Scriptable Obejects/Gem.asset";

    private static readonly Color Body = new Color32(0x6B, 0x4A, 0x3A, 0xFF);

    // Lv.1 → 2부터: (다음 레벨까지 경험치, 도달 보상 조개). 최고 Lv.20
    // 경험치는 레벨마다 약 ×1.3. 조개는 5레벨마다(마을 단계 후보) 크게 — 설계는 Docs/성장곡선_퀘스트설계.md
    private static readonly (int exp, int gem)[] Levels =
    {
        (100, 5), (150, 5), (220, 10), (320, 20), (450, 10), (600, 10), (800, 15), (1050, 15), (1350, 30),
        (1700, 15), (2100, 15), (2600, 20), (3200, 20), (3900, 40), (4700, 20), (5600, 25), (6600, 25), (7700, 30), (9000, 50),
    };

    public static void CreateData()
    {
        EnsureFolder(DataFolder);
        var (table, isNew) = LoadOrCreate<LevelTable>(LevelTablePath);
        if (!isNew)
            return;

        var gem = AssetDatabase.LoadAssetAtPath<Currency>(GemPath);
        var so = new SerializedObject(table);
        var list = so.FindProperty("_levels");
        list.arraySize = Levels.Length;
        for (int i = 0; i < Levels.Length; i++)
        {
            var entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("_expToNext").intValue = Levels[i].exp;
            entry.FindPropertyRelative("_rewardCurrency").objectReferenceValue = gem;
            entry.FindPropertyRelative("_rewardAmount").intValue = Levels[i].gem;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    /// <summary>레벨업 팝업을 canvas 아래에 만든다 (닫힌 채로 시작)</summary>
    public static LevelUpPopupView BuildPopup(RectTransform canvas)
    {
        var screen = CreateRect("LevelUp", canvas);
        Stretch(screen, 0);

        var dim = CreateImage("Dim", screen, null, true);
        Stretch(dim.rectTransform, 0);
        dim.color = new Color(0, 0, 0, 0.6f);
        var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;

        var safe = CreateRect("SafeArea", screen);
        Stretch(safe, 0);
        safe.gameObject.AddComponent<SafeAreaFltter>();

        var panelImage = CreateImage("Panel", safe, LoadPanelSprite(), true);
        panelImage.type = Image.Type.Sliced;
        var panel = panelImage.rectTransform;
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(760, 640));
        var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        var ribbon = CreateImage("Ribbon", panel, ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold"), false);
        Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 54), new Vector2(420, 112));
        var ribbonText = CreateText("Title", ribbon.rectTransform, _titleFont, "레벨 업!", 52, Cocoa);
        Stretch(ribbonText.rectTransform, 0);
        ribbonText.rectTransform.offsetMin = new Vector2(0, 10);

        var level = CreateText("Level", panel, _titleFont, "Lv.2", 120, Cocoa);
        TopBand(level.rectTransform, 40, 40, 110, 150);

        var reward = CreateRect("Reward", panel);
        TopBand(reward, 40, 40, 280, 76);
        var layout = reward.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var rewardIcon = CreateImage("Icon", reward, null, false);
        rewardIcon.preserveAspect = true;
        var iconSize = rewardIcon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = 68;
        iconSize.preferredHeight = 68;
        var rewardText = CreateText("Amount", reward, _titleFont, "+5", 48, Cocoa);

        var note = CreateText("Note", panel, _bodyFont, "새 퀘스트가 열렸어요!", 32, Body);
        TopBand(note.rectTransform, 40, 40, 380, 50);

        var confirm = CreateImage("ConfirmButton", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Primary"), true);
        Place(confirm.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(340, 104));
        var confirmButton = confirm.gameObject.AddComponent<Button>();
        confirmButton.targetGraphic = confirm;
        var confirmText = CreateText("Label", confirm.rectTransform, _titleFont, "확인", 42, Color.white);
        Stretch(confirmText.rectTransform, 0);
        confirmText.rectTransform.offsetMin = new Vector2(0, 10);

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", panel);
        Set(animator, "_panelGroup", panelGroup);
        Set(animator, "_dimButton", dimButton);

        var view = screen.gameObject.AddComponent<LevelUpPopupView>();
        Set(view, "_animator", animator);
        Set(view, "_levelText", level);
        Set(view, "_reward", reward.gameObject);
        Set(view, "_rewardIcon", rewardIcon);
        Set(view, "_rewardText", rewardText);
        Set(view, "_noteText", note);
        Set(view, "_confirmButton", confirmButton);

        screen.gameObject.SetActive(false);
        return view;
    }
}
