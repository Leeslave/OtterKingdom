using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장소 개척 (광산 길 열기, 농경지 개간): 길을 막은 나무·돌(ClearingObstacleView)을 플레이어가 직접 다 치운다.
/// - 개간 지역(DevelopableRegionDefinition)이 있는 장소: 다 치우면 "길은 열렸지만 정비가 필요" →
///   주민 해달이 후속 정비(RegionTaskSiteView)를 끝내야 운영되고 그때 게시판 부탁이 끝난다 (왕국 레벨·전문 해달 방문)
/// - 지역이 없는 장소: 다 치우면 바로 게시판 부탁을 끝냄
/// 운영되어도 그 지역의 전문 해달(광부·농부)을 광장에서 배치하기 전까지는 "광장에서 만나 배치해 주세요" 안내만 하고
/// 기능 오브젝트(광산 입구·광부, 밭·농부)를 숨긴다. 장소 튜토리얼(= 생산 안내)은 배치한 뒤에 (IsWaiting).
/// </summary>
public class ZoneClearingView : MonoBehaviour
{
    public static ZoneClearingView Active { get; private set; }

    /// <summary>이 씬이 개척이나 전문 해달 배치를 기다리는 중인지 (장소 튜토리얼을 미룸)</summary>
    public static bool IsWaiting => Active != null && !Active._cleared;

    [Header("장소")]
    [Tooltip("이 씬의 장소 (게시판 부탁의 '직접 치우러 갈 장소'와 같아야 함)")]
    [SerializeField] private ZoneDefinition _zone;

    [Header("구성 요소")]
    [Tooltip("길을 막은 장애물들")]
    [SerializeField] private List<ClearingObstacleView> _obstacles = new List<ClearingObstacleView>();

    [Tooltip("운영되고 전문 해달을 배치하기 전에는 숨기는 기능 오브젝트 (광산 입구·광부, 밭·농부)")]
    [SerializeField] private List<GameObject> _hiddenUntilCleared = new List<GameObject>();

    [Header("안내")]
    [Tooltip("직접 치우는 중 화면 아래 말풍선")]
    [SerializeField] private string _guide = "길을 막은 나무와 돌을 톡톡 눌러 치워요!";

    [Tooltip("다 치운 뒤 주민 해달을 보내야 할 때")]
    [SerializeField] private string _awaitingWorkersGuide = "길이 열렸어요! 주민 해달을 보내 정비해요";

    [Tooltip("주민 해달이 정비하는 중")]
    [SerializeField] private string _preparingGuide = "주민 해달이 주변을 정리하고 있어요";

    [Tooltip("운영되었지만 전문 해달을 아직 배치하지 않음. {0} = 해달 이름+을/를, {1} = 장소 이름")]
    [SerializeField] private string _awaitingSpecialistGuide = "광장에서 {0} 만나 {1}에 배치해 주세요";

    private bool _cleared;
    private bool _applied;
    private string _shownGuide;

    // 씬이 열리자마자 숨겨서 입구의 Start(곡괭이 강화 버튼 띄우기)가 개척 전에 돌지 않게. 운영되면 첫 Update에서 켬
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
            Apply(true, false, null);
            return;
        }
        if (!manager.IsLoaded)
            return;

        bool cleared = manager.IsZoneCleared(_zone);
        if (cleared)
            ClearLeftoverObstacles(manager);
        bool canClear = !cleared && manager.CanClearZone(_zone);
        foreach (var obstacle in _obstacles)
            obstacle.ZoneCleared = cleared;
        var region = manager.FindRegion(_zone);
        if (region == null)
        {
            if (canClear && AllObstaclesCleared(manager))
                cleared = manager.TryClearZone(_zone);
            Apply(cleared, canClear && !cleared, canClear && !cleared ? _guide : null);
            return;
        }

        canClear &= manager.GetRegionState(region) == RegionProgressState.PlayerClearing;
        if (canClear && AllObstaclesCleared(manager))
        {
            manager.MarkRegionPlayerCleared(region);
            canClear = false;
            cleared = manager.IsZoneCleared(_zone);
        }
        // 운영돼도 전문 해달을 배치하기 전에는 기능을 열지 않음
        var waiting = cleared ? manager.WaitingSpecialist(_zone) : null;
        if (waiting != null)
        {
            Apply(false, false, SpecialistGuide(waiting));
            return;
        }
        Apply(cleared, canClear, GuideFor(manager.GetRegionState(region)));
    }

    private string SpecialistGuide(SettlementOtterDefinition otter) =>
        string.Format(_awaitingSpecialistGuide, otter.DisplayName + KoreanParticle.ObjectParticle(otter.DisplayName), _zone.DisplayName);

    private string GuideFor(RegionProgressState state)
    {
        switch (state)
        {
            case RegionProgressState.PlayerClearing: return _guide;
            case RegionProgressState.AwaitingWorkers: return _awaitingWorkersGuide;
            case RegionProgressState.WorkerPreparing: return _preparingGuide;
            default: return null;
        }
    }

    // 이미 열린 장소에 남은 장애물 (옛 세이브는 부탁을 한꺼번에 끝내서 장애물 표시가 없음) → 치운 것으로 남겨 길을 막지 않게
    private void ClearLeftoverObstacles(SettlementManager manager)
    {
        foreach (var obstacle in _obstacles)
        {
            if (!manager.IsObstacleCleared(obstacle.ObstacleId))
                manager.MarkObstacleCleared(obstacle.ObstacleId);
        }
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

    private void Apply(bool cleared, bool canClear, string guide)
    {
        foreach (var obstacle in _obstacles)
            obstacle.Interactable = canClear;

        if (GameManager.Instance == null)
            guide = null;
        if (guide != _shownGuide)
        {
            _shownGuide = guide;
            if (guide != null)
                GameManager.Instance.ShowGuide(guide);
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
