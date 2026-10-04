using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// P1 개간 지역 · 주민 작업: 데이터(작업, 지역, 꾸미기 해금 조건), 작업 화면(전역 UI), 광산·밭의 정비 현장.
/// 광산: 나무·돌 직접 치움 → 주민 1명이 "광산 주변 정리"(30초) → 운영 (Lv.3, 광부 방문, 광산 꾸미기 구역) → 광부 배치 → 채굴.
/// 밭: 잡목·바위 직접 치움 → 주민 2명이 "농경지 개간"(45초, 골드·목재·돌) → 운영 (농부 방문) → 농부 배치 → 농사.
/// </summary>
public static partial class SettlementSetup
{
    private const string MineRegionPath = DataFolder + "/Regions/Region_mine.asset";
    private const string MineDecorUnlockPath = DataFolder + "/Regions/Unlock_mine_operational.asset";
    private const string MineDecorBoardPath = "Assets/Scriptable Obejects/Decor/Boards/Board_mine.asset";
    private const string MineDecorRegionPath = "Assets/Scriptable Obejects/Decor/Regions/Region_mine_base.asset";
    private const string MinePlayerClearDevelopment = "mine_path_open";
    private const string MineOperationalDevelopment = "mine_cleared";
    private const string FarmRegionPath = DataFolder + "/Regions/Region_farm.asset";
    // 농경지는 새 이웃의 집을 지으면 발견 (밭 장소도 이때부터 갈 수 있음 = 개간되지 않은 농경지)
    internal const string FarmDiscoverDevelopment = "house_2";
    private const string FarmPlayerClearDevelopment = "farm_path_open";
    internal const string FarmOperationalDevelopment = "farmland";

    // (ID, 제목, 설명, 끝났을 때 안내, 필요한 주민 수, 초, 필요 발전, 결과 발전, 골드, 목재, 돌)
    private static readonly (string id, string title, string description, string message, int workers, float seconds,
        string requires, string result, int gold, int wood, int stone)[] Tasks =
    {
        ("task_mine_tidy", "광산 주변 정리", "치운 나무와 돌 부스러기를 모으고\n입구 앞 땅을 다져요.", "광산 주변 정리가 끝났어요!",
            1, 30f, MinePlayerClearDevelopment, "mine_tidy", 0, 0, 0),
        // 비용은 예전 농경지 개간 건설과 같음 (골드 200, 목재 16, 돌 10)
        ("task_farm_till", "농경지 개간", "치운 자리의 흙을 갈아엎고\n고랑을 내서 밭을 만들어요.", "농경지 개간이 끝났어요!",
            2, 45f, FarmPlayerClearDevelopment, "farm_tilled", 200, 16, 10),
        // P2 광장 주민 작업 (지역이 없어 광장의 현장에서 일함). 비용·시간은 구현 지시서 6.1의 QA용 제안값
        ("task_common_space", "공동 공간 정비", "흩어진 통나무와 돌을 치우고\n다 같이 앉을 자리를 만들어요.", "다 같이 쉴 자리 정비가 끝났어요!",
            1, 30f, BoardManagedDevelopment, CommonSpaceDevelopment, 100, 0, 0),
        ("task_tidy_board_area", "게시판 주변 정리", "게시판 앞 낙엽과 잔돌을\n쓸어 모아요.", "게시판 주변 정리가 끝났어요!",
            1, 20f, BoardManagedDevelopment, BoardAreaDevelopment, 50, 0, 0),
    };

    // (에셋, ID, 이름, 장소, 발견 발전, 직접 개척 발전, 운영 발전, 생산 발전(전문 해달이 일하면), 후속 정비 작업, 꾸미기 격자, 꾸미기 구역)
    private static readonly (string path, string id, string name, string zone, string discover, string playerClear, string operational,
        string production, string[] tasks, string decorBoard, string decorRegion)[] Regions =
    {
        (MineRegionPath, "region_mine", "광산", MineZonePath, "chair", MinePlayerClearDevelopment, MineOperationalDevelopment,
            SettlementQuestGate.MineProductionDevelopment, new[] { "task_mine_tidy" }, MineDecorBoardPath, MineDecorRegionPath),
        // 밭 꾸미기 구역은 처음부터 열려 있던 구역이라 그대로 둠
        (FarmRegionPath, "region_farm", "밭", FarmZonePath, FarmDiscoverDevelopment, FarmPlayerClearDevelopment, FarmOperationalDevelopment,
            SettlementQuestGate.FarmProductionDevelopment, new[] { "task_farm_till" }, null, null),
    };

    // 광산 정비 현장 (광산 씬 월드 좌표): 치운 자리 가운데에서 일하고, 화면 아래 길로 들어오고 나감
    private static readonly Vector2[] MineStandPoints = { new Vector2(-0.8f, -0.9f), new Vector2(0.9f, -1.1f), new Vector2(0.05f, -1.7f) };
    private static readonly Vector2 MineLookPoint = new Vector2(0.1f, -0.3f);
    private static readonly Vector2 MineEntryPoint = new Vector2(0.1f, -7f);
    private static readonly Vector2 MineMarkerPoint = new Vector2(0.1f, -0.7f);
    private static readonly Vector2 MineProgressPoint = new Vector2(0.1f, 0.4f);

    #region 데이터

    // CreateData 끝에서 부름: 작업 → 지역 → 설정 목록. 광산 꾸미기 격자는 Tools/Decor/Setup Decor가 먼저 만들어 둠
    private static void CreateRegionData(SerializedObject configSo)
    {
        EnsureFolder($"{DataFolder}/Tasks");
        EnsureFolder($"{DataFolder}/Regions");

        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodItemPath);
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var tasks = new Dictionary<string, SettlementTaskDefinition>();
        foreach (var t in Tasks)
        {
            var task = LoadOrCreate<SettlementTaskDefinition>($"{DataFolder}/Tasks/{t.id}.asset").asset;
            var so = new SerializedObject(task);
            so.FindProperty("_taskId").stringValue = t.id;
            so.FindProperty("_title").stringValue = t.title;
            so.FindProperty("_description").stringValue = t.description;
            so.FindProperty("_completionMessage").stringValue = t.message;
            so.FindProperty("_requiredWorkers").intValue = t.workers;
            so.FindProperty("_durationSeconds").floatValue = t.seconds;
            so.FindProperty("_requiredDevelopment").stringValue = t.requires;
            so.FindProperty("_resultDevelopment").stringValue = t.result;
            so.FindProperty("_requiredGold").intValue = t.gold;
            var items = so.FindProperty("_requiredItems");
            items.arraySize = 0;
            AddItemAmount(items, wood, t.wood);
            AddItemAmount(items, stone, t.stone);
            so.ApplyModifiedPropertiesWithoutUndo();
            tasks[t.id] = task;
        }

        var regions = new List<DevelopableRegionDefinition>();
        foreach (var r in Regions)
        {
            DecorBoardDefinition board = null;
            DecorRegionDefinition decorRegion = null;
            if (r.decorBoard != null)
            {
                board = AssetDatabase.LoadAssetAtPath<DecorBoardDefinition>(r.decorBoard);
                decorRegion = AssetDatabase.LoadAssetAtPath<DecorRegionDefinition>(r.decorRegion);
                if (board == null || decorRegion == null)
                    Debug.LogWarning($"[SettlementSetup] {r.name} 꾸미기 격자가 없습니다. Tools/Decor/Setup Decor를 먼저 실행하세요.");
                else
                    LockDecorRegionUntilOperational(decorRegion);
            }

            var region = LoadOrCreate<DevelopableRegionDefinition>(r.path).asset;
            var regionSo = new SerializedObject(region);
            regionSo.FindProperty("_regionId").stringValue = r.id;
            regionSo.FindProperty("_displayName").stringValue = r.name;
            regionSo.FindProperty("_zone").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(r.zone);
            regionSo.FindProperty("_discoverDevelopment").stringValue = r.discover;
            regionSo.FindProperty("_playerClearDevelopment").stringValue = r.playerClear;
            regionSo.FindProperty("_operationalDevelopment").stringValue = r.operational;
            regionSo.FindProperty("_productionDevelopment").stringValue = r.production;
            var regionTasks = new List<SettlementTaskDefinition>();
            foreach (var id in r.tasks)
                regionTasks.Add(tasks[id]);
            SetList(regionSo.FindProperty("_preparationTasks"), regionTasks);
            regionSo.FindProperty("_decorBoard").objectReferenceValue = board;
            regionSo.FindProperty("_decorRegion").objectReferenceValue = decorRegion;
            regionSo.ApplyModifiedPropertiesWithoutUndo();
            regions.Add(region);
        }

        SetList(configSo.FindProperty("_regions"), regions);
        SetList(configSo.FindProperty("_tasks"), new List<SettlementTaskDefinition>(tasks.Values));
    }

    // 광산 앞마당 꾸미기 구역: 처음엔 잠겨 있고 "광산 정비를 마치면" 열림 (운영되면 SettlementManager가 엶)
    private static void LockDecorRegionUntilOperational(DecorRegionDefinition decorRegion)
    {
        var unlock = LoadOrCreate<DevelopmentUnlockRequirement>(MineDecorUnlockPath).asset;
        var unlockSo = new SerializedObject(unlock);
        unlockSo.FindProperty("_developmentId").stringValue = MineOperationalDevelopment;
        unlockSo.FindProperty("_description").stringValue = "광산 정비를 마치면";
        unlockSo.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(decorRegion);
        so.FindProperty("_displayName").stringValue = "광산 앞마당";
        so.FindProperty("_unlockedByDefault").boolValue = false;
        var requirements = so.FindProperty("_requirements");
        requirements.arraySize = 1;
        requirements.GetArrayElementAtIndex(0).objectReferenceValue = unlock;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region 작업 화면

    private static SettlementTaskPopupView BuildTaskPopup(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildPopupShell(canvas, "SettlementTask", LoadPanelSprite());
        Place(panel, new Vector2(0.5f, 0f), new Vector2(0, 290), new Vector2(900, 820));
        var close = BuildCloseButton(panel);

        var title = Label("Title", panel, _titleFont, "광산 주변 정리", 46, Cocoa, TextAlignmentOptions.Center, 28);
        TopBand(title.rectTransform, 60, 60, 44, 64);
        var description = Label("Description", panel, _bodyFont, "치운 나무와 돌 부스러기를 모으고\n입구 앞 땅을 다져요.", 28, Body, TextAlignmentOptions.Center, 18);
        description.textWrappingMode = TextWrappingModes.Normal;
        TopBand(description.rectTransform, 60, 60, 116, 84);

        var info = CreateRect("Info", panel);
        TopBand(info, 60, 60, 214, 64);
        Row(info, 20, TextAnchor.MiddleCenter);
        var workers = BuildInfoChip(info, "Workers", "필요 인원", "1명");
        var duration = BuildInfoChip(info, "Duration", "작업 시간", "30초");

        // 보낼 해달 고르기
        var assign = CreateRect("Assign", panel);
        TopBand(assign, 40, 40, 300, 290);
        var assignLabel = Label("Label", assign, _titleFont, "보낼 해달", 30, Cocoa, TextAlignmentOptions.Center, 20);
        TopBand(assignLabel.rectTransform, 0, 0, 0, 44);
        var row = CreateRect("Workers", assign);
        TopBand(row, 0, 0, 52, 230);
        Row(row, 22, TextAnchor.MiddleCenter);
        var chips = new List<WorkerChipView>();
        for (int i = 0; i < 4; i++)
            chips.Add(BuildWorkerChip(row, $"Worker{i + 1}"));

        // 작업 중
        var progress = CreateRect("Progress", panel);
        TopBand(progress, 60, 60, 310, 270);
        var track = CreateImage("Bar", progress, Common("UI_Slider_Track"), false);
        track.pixelsPerUnitMultiplier = 2f;
        TopBand(track.rectTransform, 30, 30, 40, 44);
        var fill = CreateImage("Fill", track.rectTransform, ImportSprite(CommonSpriteFolder, "UI_Bar_Fill"), false);
        fill.pixelsPerUnitMultiplier = 2f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        var bar = track.gameObject.AddComponent<ProgressBarView>();
        Set(bar, "_fill", fill.rectTransform);
        var time = Label("Time", progress, _titleFont, "남은 시간 00:30", 36, Cocoa, TextAlignmentOptions.Center, 22);
        TopBand(time.rectTransform, 0, 0, 108, 56);
        var crew = Label("Crew", progress, _bodyFont, "몽실 작업 중", 28, Green, TextAlignmentOptions.Center, 18);
        TopBand(crew.rectTransform, 0, 0, 172, 48);

        var note = Label("Note", panel, _bodyFont, "해달이 걸어가서 일을 시작해요.", 26, Body, TextAlignmentOptions.Center, 18);
        TopBand(note.rectTransform, 60, 60, 604, 44);

        var (start, startLabel, startImage) = BuildButton(panel, "StartButton", Common("UI_Button_Coin"), "작업 시작", 40, Cocoa);
        Place(startImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 32), new Vector2(520, 112));

        var view = screen.gameObject.AddComponent<SettlementTaskPopupView>();
        Set(view, "_animator", animator);
        Set(view, "_titleText", title);
        Set(view, "_descriptionText", description);
        Set(view, "_workersText", workers);
        Set(view, "_durationText", duration);
        Set(view, "_infoRow", info.gameObject);
        Set(view, "_assignGroup", assign.gameObject);
        var so = new SerializedObject(view);
        SetList(so.FindProperty("_workerChips"), chips);
        so.ApplyModifiedPropertiesWithoutUndo();
        Set(view, "_progressGroup", progress.gameObject);
        Set(view, "_bar", bar);
        Set(view, "_timeText", time);
        Set(view, "_crewText", crew);
        Set(view, "_noteText", note);
        Set(view, "_startButton", start);
        Set(view, "_startLabel", startLabel);
        Set(view, "_closeButton", close);

        CornerClose(close);
        screen.gameObject.SetActive(false);
        return view;
    }

    // "필요 인원  1명" 칩 (값 글자를 돌려줌)
    private static TextMeshProUGUI BuildInfoChip(RectTransform parent, string name, string key, string value)
    {
        var bg = CreateImage(name, parent, Common("UI_Chip_Normal"), false);
        bg.pixelsPerUnitMultiplier = 1.4f;
        var element = bg.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = 300;
        element.preferredHeight = 64;
        var keyText = Label("Key", bg.rectTransform, _bodyFont, key, 24, Body, TextAlignmentOptions.Left, 16);
        TopLeft(keyText.rectTransform, 24, 8, 140, 48);
        var valueText = Label("Value", bg.rectTransform, _titleFont, value, 30, Cocoa, TextAlignmentOptions.Right, 18);
        Place(valueText.rectTransform, new Vector2(1, 1), new Vector2(-24, -8), new Vector2(130, 48));
        valueText.rectTransform.pivot = new Vector2(1, 1);
        return valueText;
    }

    // 주민 해달 한 칸: 얼굴 틀 + 이름 + 체크 + 바쁨 표시
    private static WorkerChipView BuildWorkerChip(RectTransform parent, string name)
    {
        var root = CreateRect(name, parent);
        var element = root.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = 180;
        element.preferredHeight = 222;
        var group = root.gameObject.AddComponent<CanvasGroup>();

        var frame = CreateImage("Frame", root, Common("UI_RoundButton_Cream"), true);
        Place(frame.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(168, 168));
        var button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        var portrait = CreateImage("Portrait", frame.rectTransform, null, false);
        portrait.preserveAspect = true;
        Stretch(portrait.rectTransform, 16);

        var check = CreateImage("Check", frame.rectTransform, Common("UI_Button_Confirm"), false);
        Place(check.rectTransform, new Vector2(1, 1), new Vector2(6, 6), new Vector2(60, 60));

        var busy = CreateImage("Busy", frame.rectTransform, Common("UI_Button_Paper"), false);
        busy.pixelsPerUnitMultiplier = 1.6f;
        // 흐려진 해달 칸 위에서도 이유 글자는 또렷하게
        busy.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
        busy.gameObject.SetActive(false);
        Place(busy.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(150, 48));
        var busyText = Label("Text", busy.rectTransform, _bodyFont, "작업 중", 24, Body, TextAlignmentOptions.Center, 16);
        Stretch(busyText.rectTransform, 4);

        var nameText = Label("Name", root, _titleFont, "몽실", 28, Cocoa, TextAlignmentOptions.Center, 18);
        Place(nameText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(180, 48));

        var view = root.gameObject.AddComponent<WorkerChipView>();
        Set(view, "_portrait", portrait);
        Set(view, "_nameText", nameText);
        Set(view, "_check", check.gameObject);
        Set(view, "_busyTag", busy.gameObject);
        Set(view, "_busyText", busyText);
        Set(view, "_group", group);
        Set(view, "_button", button);
        return view;
    }

    #endregion

    #region 광산 정비 현장

    // 망치 표지판(주민을 기다릴 때) · 일할 자리 · 남은 시간 말풍선. 해달 크기는 광부 해달에 맞춤
    private static void BuildTaskSite(Transform root, MinerOtterController miner) =>
        BuildTaskSite(root, MineRegionPath, miner.GetComponent<SpriteRenderer>(), "hint_mine_workers", "광산 주변 정리\n0:30",
            MineStandPoints, MineLookPoint, MineEntryPoint, MineMarkerPoint, MineProgressPoint);

    // 지역의 후속 정비 현장 (광산·밭 공용). sizeRef = 그 장소 해달의 그림 (일하러 온 주민의 크기를 맞춤)
    private static void BuildTaskSite(Transform root, string regionPath, SpriteRenderer sizeRef, string hintFlag, string sampleText,
        Vector2[] standPoints, Vector2 lookPoint, Vector2 entryPoint, Vector2 markerPoint, Vector2 progressPoint)
    {
        var site = new GameObject("PreparationSite").transform;
        site.SetParent(root, false);

        var stands = new List<Transform>();
        for (int i = 0; i < standPoints.Length; i++)
            stands.Add(Point(site, $"Stand{i + 1}", standPoints[i]));
        var look = Point(site, "LookPoint", lookPoint);
        var entry = Point(site, "EntryPoint", entryPoint);

        var marker = CreateSprite("Marker", site, LoadPropArt("UI_Bubble_Hammer"), markerPoint, false);
        marker.sortingOrder = OverlayOrder - 5;
        var tap = marker.gameObject.AddComponent<CircleCollider2D>();
        tap.radius = 0.8f;
        tap.offset = new Vector2(0f, 0.45f);
        var hint = CreateTapHint(site, hintFlag, "눌러서 해달을 보내요!", (Vector3)markerPoint + new Vector3(0f, 1.1f, 0f));

        var bubble = CreateSprite("ProgressBubble", site, LoadPropArt("UI_Bubble_Speech"), progressPoint, false);
        bubble.transform.localScale = Vector3.one * 0.85f;
        bubble.sortingOrder = OverlayOrder - 4;
        var progress = CreateWorldText("Text", bubble.transform, new Vector3(0f, 1.05f, 0f), sampleText);
        progress.outlineWidth = 0f;
        progress.fontSize = 2.6f;
        progress.enableAutoSizing = true;
        progress.fontSizeMin = 1.8f;
        progress.fontSizeMax = 2.6f;
        progress.rectTransform.sizeDelta = new Vector2(3.4f, 1.2f);
        progress.sortingOrder = OverlayOrder - 3;

        float refHeight = sizeRef != null && sizeRef.sprite != null ? sizeRef.bounds.size.y : 1f;
        var settings = AssetDatabase.LoadAssetAtPath<PlazaSettings>(PlazaSettingsPath);
        float plazaHeight = settings != null ? settings.otterHeight : 1.67f;

        var view = site.gameObject.AddComponent<RegionTaskSiteView>();
        var so = new SerializedObject(view);
        so.FindProperty("_region").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DevelopableRegionDefinition>(regionPath);
        SetList(so.FindProperty("_standPoints"), stands);
        so.FindProperty("_lookPoint").objectReferenceValue = look;
        so.FindProperty("_entryPoint").objectReferenceValue = entry;
        so.FindProperty("_marker").objectReferenceValue = marker;
        so.FindProperty("_markerTap").objectReferenceValue = tap;
        so.FindProperty("_hint").objectReferenceValue = hint;
        so.FindProperty("_progressBubble").objectReferenceValue = bubble.gameObject;
        so.FindProperty("_progressText").objectReferenceValue = progress;
        so.FindProperty("_workerScale").floatValue = refHeight / plazaHeight;
        so.FindProperty("_workerHeight").floatValue = refHeight;
        so.FindProperty("_depthBase").intValue = MineDepthBase;
        so.FindProperty("_speechBubble").objectReferenceValue = LoadPropArt("UI_Bubble_Speech");
        so.FindProperty("_alertBubble").objectReferenceValue = LoadPropArt("UI_Bubble_Alert");
        so.FindProperty("_hammerBubble").objectReferenceValue = LoadPropArt("UI_Bubble_Hammer");
        so.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontAssetPath);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform Point(Transform parent, string name, Vector2 position)
    {
        var point = new GameObject(name).transform;
        point.SetParent(parent, false);
        point.position = position;
        return point;
    }

    #endregion
}
