using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 모든 장소 씬에 걸쳐 살아 있는 전역 UI(네비게이션 바, 가방, 이동 팝업)의 루트. DontDestroyOnLoad로 유지한다.
/// 두 가지 방법 모두 동작한다:
/// 1) 씬에 Resources/GlobalUI 프리팹을 직접 배치 → 편집 모드에서도 보임. 씬을 옮겨 두 번째 것이 깨어나면 스스로 사라짐
/// 2) 배치하지 않은 장소 씬 → 씬이 열릴 때 프리팹을 자동으로 만듦 (깜빡하고 안 넣어도 바가 사라지지 않게)
/// </summary>
// 같은 오브젝트의 다른 컴포넌트와 자식들이 깨어나기 전에 중복 여부를 먼저 판단
[DefaultExecutionOrder(-300)]
public class GlobalUIRoot : MonoBehaviour
{
    private const string ResourcePath = "GlobalUI";

    public static GlobalUIRoot Instance { get; private set; }

    [Header("데이터")]
    [Tooltip("이동할 수 있는 장소 전부. 이 목록의 씬에서만 전역 UI가 생긴다")]
    [SerializeField] private List<ZoneDefinition> _zones;

    public IReadOnlyList<ZoneDefinition> Zones => _zones;

    public bool IsZoneScene(string sceneName) => ZoneLookup.FindByScene(_zones, sceneName) != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 씬에 배치된 두 번째 전역 UI: 자식들이 깨어나기 전에 꺼서 EventSystem 등이 잠깐이라도 겹치지 않게
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RemoveOtherEventSystems();
    }

    // 장소 씬이 전역 UI보다 먼저 만든 임시 EventSystem(GameUI 등이 없을 때 만듦)을 치워 전역 UI 것 하나만 남김.
    // 둘이 함께 켜져 있으면 "There can be only one active Event System" 경고가 나고 입력이 한쪽으로만 감
    private void RemoveOtherEventSystems()
    {
        var own = GetComponentInChildren<EventSystem>(true);
        if (own == null)
            return;
        foreach (var other in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
        {
            if (other == own)
                continue;
            // EventSystem만 있는 오브젝트(Transform · EventSystem · 입력 모듈)면 통째로, 아니면 그 기능만 끔
            if (other.GetComponents<Component>().Length <= 3)
            {
                other.gameObject.SetActive(false);
                Destroy(other.gameObject);
            }
            else
            {
                foreach (var module in other.GetComponents<BaseInputModule>())
                    module.enabled = false;
                other.enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #region 자동 생성

    // 첫 씬의 Awake가 끝난 뒤 (매니저들이 준비된 뒤) 한 번 호출됨
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TrySpawn(SceneManager.GetActiveScene());
    }

    // 테스트 씬에서 시작해 나중에 장소 씬으로 들어간 경우를 위해 씬마다 확인
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawn(scene);
        if (Instance != null)
            Instance.RemoveOtherEventSystems();
    }

    private static void TrySpawn(Scene scene)
    {
        if (Instance != null)
            return;

        var prefab = Resources.Load<GlobalUIRoot>(ResourcePath);
        if (prefab == null)
            return;

        // 장소가 아닌 씬에서는 자동으로 만들지 않음 (테스트 씬은 프리팹을 직접 배치해서 사용)
        if (!prefab.IsZoneScene(scene.name))
            return;

        if (InventoryManager.Instance == null || CurrencyManager.Instance == null)
        {
            Debug.LogWarning($"[GlobalUI] '{scene.name}' 씬에 InventoryManager 또는 CurrencyManager가 없어 네비게이션 바를 만들지 않습니다.");
            return;
        }

        Instantiate(prefab).name = ResourcePath;
    }

    #endregion
}
