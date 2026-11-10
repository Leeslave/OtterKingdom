using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>실제 Unity UI 생성과 JsonUtility 저장 왕복. 플레이어 세이브 파일을 사용하지 않는다.</summary>
public class ProsperityOnboardingUnityTests
{
    private readonly List<Object> _created = new List<Object>();
    private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
    private Scene _previousScene;
    private Scene _scene;
    private string _previousSceneName;
    private HashSet<Canvas> _existingCanvases;
    private UnityEngine.EventSystems.EventSystem _previousEventSystem;
    private Func<string, bool> _previousTechProvider;
    private Func<KingdomBonusKind, int> _previousBonusSource;

    private T Make<T>() where T : ScriptableObject
    {
        var value = ScriptableObject.CreateInstance<T>();
        _created.Add(value);
        return value;
    }

    private T Component<T>() where T : MonoBehaviour
    {
        var root = new GameObject(typeof(T).Name);
        root.SetActive(false);
        _created.Add(root);
        return root.AddComponent<T>();
    }

    private static void Set(object owner, string name, object value) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Call(object owner, string name) => owner.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    private static PropertyInfo Singleton(Type type) => type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

    [SetUp]
    public void SetUp()
    {
        foreach (var type in new[] { typeof(ProfileManager), typeof(SettlementManager), typeof(GameManager) })
        {
            _singletons[type] = Singleton(type).GetValue(null);
            Singleton(type).SetValue(null, null);
        }
        _previousTechProvider = TechGate.Provider;
        _previousBonusSource = KingdomBonus.Source;
        _previousEventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        _previousScene = SceneManager.GetActiveScene();
        // Test Runner의 저장하지 않은 임시 씬을 재사용하고 이름만 되돌린다.
        _scene = _previousScene;
        _previousSceneName = _scene.name;
        _existingCanvases = new HashSet<Canvas>(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        _scene.name = "Plaza";
        SceneManager.SetActiveScene(_scene);
        GameNotices.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var overlay in Object.FindObjectsByType<TutorialOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (overlay.gameObject.scene == _scene) Call(overlay, "OnDestroy");
        // 이미 생성한 화면부터 즉시 닫아 MonoBehaviour 파괴 시 지연 Destroy를 남기지 않는다.
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!_existingCanvases.Contains(canvas)) Object.DestroyImmediate(canvas.gameObject);
        foreach (var created in _created) if (created != null) Object.DestroyImmediate(created);
        _created.Clear();
        if (_previousEventSystem == null)
        {
            var events = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (events != null && events.gameObject.scene == _scene) Object.DestroyImmediate(events.gameObject);
        }
        foreach (var pair in _singletons) Singleton(pair.Key).SetValue(null, pair.Value);
        _singletons.Clear();
        TechGate.Provider = _previousTechProvider;
        KingdomBonus.Source = _previousBonusSource;
        if (_previousScene.IsValid()) SceneManager.SetActiveScene(_previousScene);
        if (_scene.IsValid()) _scene.name = _previousSceneName;
        GameNotices.Clear();
    }

    [Test]
    public void EligibleLegacy21_AutomaticallyCreatesIntro_ThenOpensResearch_ForCompleteOrSkip()
    {
        var table = Make<LevelTable>();
        var serialized = new SerializedObject(table);
        var levels = serialized.FindProperty("_levels");
        levels.arraySize = 49;
        for (int i = 0; i < levels.arraySize; i++)
            levels.GetArrayElementAtIndex(i).FindPropertyRelative("_expToNext").intValue = 10000;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var profile = Component<ProfileManager>();
        Set(profile, "_levelTable", table);
        Call(profile, "Awake");
        profile.LoadFromSave(new ProfileSaveData { name = "해달왕", level = 21 });
        var settlement = Component<SettlementManager>();
        Call(settlement, "Awake");
        settlement.Settlement.UnlockDevelopment("mine_source_removed");
        settlement.Settlement.UnlockDevelopment(CastleRules.Built);
        typeof(SettlementManager).GetProperty("IsLoaded").SetValue(settlement, true);
        var game = Component<GameManager>();
        Singleton(typeof(GameManager)).SetValue(null, game);

        foreach (bool skipped in new[] { false, true })
        {
            var manager = Component<ProsperityManager>();
            var research = new ProsperityResearch(new ProsperitySaveData());
            typeof(ProsperityManager).GetProperty("Research").SetValue(manager, research);
            Set(manager, "_profile", profile);
            Set(manager, "_settlement", settlement);
            Call(manager, "TickOnboarding");
            var overlay = Object.FindAnyObjectByType<TutorialOverlay>();
            Assert.IsNotNull(overlay, "자동 안내가 생성돼야 함");
            Assert.IsTrue(TutorialOverlay.IsShowing);
            Assert.AreEqual(0, research.Data.rp);
            var callback = (Action<bool>)typeof(TutorialOverlay)
                .GetField("_onFinished", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(overlay);
            // Finish()는 콜백 전에 카운트를 내린다. EditMode에서는 해당 수명주기를 직접 재현한다.
            Call(overlay, "OnDestroy");
            callback(skipped);
            Object.DestroyImmediate(overlay.gameObject);
            Set(manager, "_onboardingPoll", 0f);
            Call(manager, "TickOnboarding");
            var screen = manager.GetComponent<ProsperityScreen>();
            Assert.IsNotNull(screen);
            Assert.IsTrue(screen.IsOpen, "안내 종료 뒤 첫 연구 화면이 열려야 함");
            Assert.IsTrue(research.Data.introCompleted);
            Assert.AreEqual(3, research.Data.rp);
            Assert.AreEqual(ProsperityFlowStep.Sawmill, manager.NextStep);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.gameObject.name == "왕국 발전") Object.DestroyImmediate(canvas.gameObject);
            Object.DestroyImmediate(manager.gameObject);
        }
    }

    [Test]
    public void LegacyJson_RepairsRecordsAndRoundTripsWithoutDuplicatingRp()
    {
        const string json = "{\"schemaVersion\":5,\"profile\":{\"level\":21,\"exp\":123},\"mineMaze\":{\"community\":{\"sourceRemoved\":true}},\"settlement\":{\"initialized\":true,\"completedRequests\":[\"req_build_castle\"]}}";
        var save = JsonUtility.FromJson<SaveData>(json);
        SaveDataSanitizer.Sanitize(save);
        SaveMigrations.Run(save);
        Assert.Contains("mine_source_removed", save.settlement.unlockedDevelopments);
        Assert.Contains(CastleRules.Built, save.settlement.unlockedDevelopments);
        var model = new ProsperityResearch(save.prosperity);
        model.CompleteIntro(21, true, true);
        model.TryLearn(TechIds.Sawmill, 21);
        var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
        SaveDataSanitizer.Sanitize(restored);
        var loaded = new ProsperityResearch(restored.prosperity);
        Assert.IsTrue(loaded.HasTech(TechIds.Sawmill));
        Assert.IsFalse(loaded.CompleteIntro(21, true, true));
        Assert.IsFalse(loaded.SyncLevel(21));
        Assert.AreEqual(2, loaded.Data.rp);
        Assert.AreEqual(123, restored.profile.exp);
        Assert.AreEqual(SaveData.CurrentSchemaVersion, restored.schemaVersion);
    }
}
