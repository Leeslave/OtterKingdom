using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장소 개척 (광산 길 열기): 길을 막은 나무·돌(ClearingObstacleView)을 플레이어가 직접 다 치우면
/// 그 장소의 게시판 부탁을 끝낸다 → 왕국 레벨이 오르고 새 해달이 찾아옴 (부탁 데이터대로).
/// 개척 전에는 기능 오브젝트(광산 입구)를 숨기고 안내 말풍선을 띄운다. 장소 튜토리얼은 개척 뒤에 (IsWaiting).
/// </summary>
public class ZoneClearingView : MonoBehaviour
{
    public static ZoneClearingView Active { get; private set; }

    /// <summary>이 씬이 개척을 기다리는 중인지 (장소 튜토리얼을 미룸)</summary>
    public static bool IsWaiting => Active != null && !Active._cleared;

    [Header("장소")]
    [Tooltip("이 씬의 장소 (게시판 부탁의 '직접 치우러 갈 장소'와 같아야 함)")]
    [SerializeField] private ZoneDefinition _zone;

    [Header("구성 요소")]
    [Tooltip("길을 막은 장애물들")]
    [SerializeField] private List<ClearingObstacleView> _obstacles = new List<ClearingObstacleView>();

    [Tooltip("개척 전에는 숨기는 기능 오브젝트 (광산 입구)")]
    [SerializeField] private List<GameObject> _hiddenUntilCleared = new List<GameObject>();

    [Header("안내")]
    [Tooltip("개척 전 화면 아래 말풍선")]
    [SerializeField] private string _guide = "길을 막은 나무와 돌을 톡톡 눌러 치워요!";

    private bool _cleared;
    private bool _applied;
    private bool _guideShown;

    // 씬이 열리자마자 숨겨서 입구의 Start(곡괭이 강화 버튼 띄우기)가 개척 전에 돌지 않게. 개척됐으면 첫 Update에서 켬
    private void Awake()
    {
        foreach (var go in _hiddenUntilCleared)
            go.SetActive(false);
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    private void Update()
    {
        var manager = SettlementManager.Instance;
        if (manager == null)
        {
            // 정착 진행이 없는 테스트 씬: 처음부터 열린 곳
            Apply(true, false);
            return;
        }
        if (!manager.IsLoaded)
            return;

        bool cleared = manager.IsZoneCleared(_zone);
        bool canClear = !cleared && manager.CanClearZone(_zone);
        if (canClear && AllObstaclesCleared(manager))
            cleared = manager.TryClearZone(_zone);
        Apply(cleared, canClear && !cleared);
    }

    private bool AllObstaclesCleared(SettlementManager manager)
    {
        foreach (var obstacle in _obstacles)
        {
            if (!manager.IsObstacleCleared(obstacle.ObstacleId))
                return false;
        }
        return true;
    }

    private void Apply(bool cleared, bool canClear)
    {
        foreach (var obstacle in _obstacles)
            obstacle.Interactable = canClear;

        bool showGuide = canClear && GameManager.Instance != null;
        if (showGuide != _guideShown)
        {
            _guideShown = showGuide;
            if (showGuide)
                GameManager.Instance.ShowGuide(_guide);
            else if (GameManager.Instance != null)
                GameManager.Instance.HideGuide();
        }

        if (_applied && cleared == _cleared)
            return;
        _applied = true;
        _cleared = cleared;
        foreach (var go in _hiddenUntilCleared)
            go.SetActive(cleared);
    }
}
