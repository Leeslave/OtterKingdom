using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 꾸미기 모드 전체를 연결한다: 들어가기/나가기(상단바·하단 바 숨김), 격자 표시, 보관함, 미리보기, 입력.
/// - 보관함 칸 → 화면 가운데 근처 빈자리에 미리보기
/// - 미리보기를 끌거나, 빈 곳을 탭하면 그 자리로 옮김. 놓인 물건을 탭하면 들어 올림
/// - [회전] [확인] [빼기]: 확인해야 격자가 바뀐다. 빼기는 새 물건이면 취소, 놓여 있던 물건이면 보관함으로
/// - 건물 탭(광장만): 건물을 골라 자리를 정하고 [확인] → 비용 확인 → 공사 시작 (SettlementManager.TryStartBuilding).
///   놓인 건물은 다 지은 뒤 옮길 수만 있고(빼기 없음), 해달이 걷는 길을 끊는 자리에는 놓거나 옮길 수 없다
/// 들고 있는 동안 광장 카메라 드래그는 미리보기를 끌 때만 멈춘다.
/// </summary>
// 광장 카메라(-50)보다 먼저 눌림을 보고, 미리보기를 끌 때는 카메라를 멈춘다
[DefaultExecutionOrder(-60)]
public class DecorModePresenter : MonoBehaviour
{
    private const string HintIdle = "보관함에서 장난감을 고르거나, 놓인 장난감을 눌러 옮겨요";
    private const string HintPlace = "초록색 칸에 놓을 수 있어요";
    private const string HintPlaced = "놓았어요!";
    private const string HintStored = "보관함에 넣었어요";
    private const string HintNoRoom = "놓을 자리가 없어요";
    private const string HintEmpty = "보관함에 남은 게 없어요";
    private const string HintShop = "상점은 준비 중이에요";
    private const string HintFairyComing = "상점을 여는 요정이 아직 오지 않았어요";
    private const string HintOccupied = "다른 물건이 있는 자리예요";
    private const string HintUnavailable = "여기에는 놓을 수 없어요";
    private const string HintLocked = "아직 열리지 않은 구역이에요";
    private const string HintBuildPlace = "자리를 고르고 [확인]을 누르면 공사를 시작해요";
    private const string HintBuildStarted = "공사를 시작했어요!";
    private const string HintMoved = "옮겼어요!";
    private const string HintUnderConstruction = "공사가 끝나면 옮길 수 있어요";
    private const string HintCutsPath = "해달이 지나갈 길이 막혀요. 다른 자리를 골라 주세요";

    // 이만큼(화면 짧은 변 대비) 움직이기 전까지는 탭으로 봄
    private const float TapThresholdScreenFraction = 0.02f;
    // 미리보기를 잡을 때 조금 넓게 (손가락이 물건을 가림)
    private const float GrabPaddingInCells = 0.3f;
    // 물건 위 버튼을 물건 윗변에서 띄우는 거리 (캔버스 단위)
    private const float ActionsGap = 40f;

    [Header("뷰")]
    [SerializeField] private DecorModeView _view;
    [SerializeField] private DecorStorageView _storage;

    [Header("숨길 화면")]
    [Tooltip("꾸미기 모드 동안 숨길 상단바·하단 바 묶음")]
    [SerializeField] private GameObject _hud;

    [Header("미리보기 칸")]
    [SerializeField] private Sprite _tileOkSprite;
    [SerializeField] private Sprite _tileBlockedSprite;

    public bool IsOpen => gameObject.activeSelf;

    /// <summary>다른 화면(게시판 건물 부탁)이 이 건물을 짓게 꾸미기 모드를 열어 달라고 할 때 (전역 UI가 들음)</summary>
    public static event Action<BuildingDefinition> BuildRequested;

    public static void RequestBuild(BuildingDefinition building)
    {
        if (building != null)
            BuildRequested?.Invoke(building);
    }

    /// <summary>꾸미기 모드 중인지 (월드의 다른 탭 입력 — 요정 NPC 등 — 이 무시하도록)</summary>
    public static bool IsActive { get; private set; }

    /// <summary>지금 장소에 꾸미기 격자가 있어 모드에 들어갈 수 있는지</summary>
    public bool CanEnter => DecorBoardView.Active != null && DecorBoardView.Active.Layout != null && DecorManager.Instance != null;

    private DecorManager _manager;
    private DecorBoardView _board;
    private DecorLayout _layout;
    private DecorEditSession _session;
    private DecorGhostView _ghost;
    private DecorGridOverlay _overlay;
    private Camera _camera;
    private Behaviour _cameraDrag;
    private readonly List<ItemCategory> _tabCategories = new List<ItemCategory>();
    private bool _glyphsReady;

    // 입력
    private bool _pressing;
    private bool _pressOnUI;
    private bool _dragging;
    private bool _movedBeyondTap;
    private Vector2 _pressScreen;
    private Vector2Int _grabOffset;

    private void Awake()
    {
        _view.OnDoneClicked += Exit;
        _view.OnRotateClicked += RotateSession;
        _view.OnConfirmClicked += ConfirmSession;
        _view.OnRemoveClicked += RemoveSession;
        _storage.OnDecorClicked += BeginNew;
        _storage.OnShopClicked += OpenShop;
        _storage.OnTabChanged += RefreshStorage;
    }

    // 보관함 [+ 상점] → 요정 상점을 꾸미기 물건 탭으로 (상점이 없거나 요정이 아직 광장에 오지 않았으면 안내만)
    private void OpenShop()
    {
        var shop = FairyShopPresenter.Instance;
        if (shop == null)
        {
            _view.ShowHint(HintShop);
            return;
        }
        if (!FairyAccess.IsShopOpen)
        {
            _view.ShowHint(HintFairyComing);
            return;
        }

        CancelSession();
        shop.Open(_tabCategories.Count > 0 ? _tabCategories[0] : null);
    }

    #region 들어가기 / 나가기

    public void Enter()
    {
        if (!CanEnter)
            return;

        _manager = DecorManager.Instance;
        _board = DecorBoardView.Active;
        _layout = _board.Layout;
        _camera = Camera.main;
        _cameraDrag = _camera != null ? _camera.GetComponent<PlazaCameraController>() : null;

        if (!_glyphsReady)
            PrepareGlyphs();

        _overlay = CreateWorldObject<DecorGridOverlay>(_overlay, "DecorGridOverlay");
        _overlay.Show(_board);
        _ghost = CreateWorldObject<DecorGhostView>(_ghost, "DecorGhost");
        _ghost.Init(_tileOkSprite, _tileBlockedSprite, _board.GhostSortingOrder);
        _ghost.Hide();

        BuildTabs();
        _storage.SetBuildingsTabVisible(CanBuildHere);
        _hud.SetActive(false);
        gameObject.SetActive(true);
        IsActive = true;

        _manager.OnStorageChanged += RefreshStorage;
        _session = null;
        _view.HideActions();
        _view.ShowHint(HintIdle);
        RefreshStorage();
    }

    /// <summary>꾸미기 모드를 열고(이미 열려 있으면 그대로) 건물 탭에서 이 건물의 자리 고르기를 시작</summary>
    public void EnterForBuilding(BuildingDefinition building)
    {
        if (!IsOpen)
            Enter();
        if (!IsOpen || building == null || !CanBuildHere)
            return;
        _storage.SelectBuildingsTab();
        RefreshStorage();
        BeginNewBuilding(building);
    }

    public void Exit()
    {
        if (!IsOpen)
            return;

        CancelSession();
        if (_manager != null)
            _manager.OnStorageChanged -= RefreshStorage;
        if (_overlay != null)
            _overlay.Hide();
        SetCameraDrag(true);

        _hud.SetActive(true);
        gameObject.SetActive(false);
        IsActive = false;

        if (_manager != null)
            _manager.NotifyEditFinished();
    }

    private void OnDisable()
    {
        // 장소를 옮기는 등으로 꺼질 때도 들고 있던 물건과 카메라를 원래대로
        IsActive = false;
        CancelSession();
        SetCameraDrag(true);
        _pressing = false;
        _dragging = false;
    }

    // 미리보기·격자 표시는 장소 씬의 격자 아래에 둔다 (장소를 옮기면 씬과 함께 사라지므로 들어갈 때마다 확인)
    private T CreateWorldObject<T>(T existing, string name) where T : Component
    {
        if (existing != null && existing.transform.parent == _board.transform)
            return existing;

        var go = new GameObject(name);
        go.transform.SetParent(_board.transform, false);
        return go.AddComponent<T>();
    }

    private void BuildTabs()
    {
        if (_tabCategories.Count > 0)
            return;

        // 보관함 탭: 꾸미기 물건들의 분류 (중복 제거, 정렬 순서)
        foreach (var decor in _manager.Catalog.Decors)
        {
            var category = decor != null && decor.Item != null ? decor.Item.Category : null;
            if (category != null && !_tabCategories.Contains(category))
                _tabCategories.Add(category);
        }
        _tabCategories.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        _storage.BuildTabs(_tabCategories);
    }

    private void RefreshStorage()
    {
        var holding = _session != null && _session.IsNew ? _session.Decor : null;
        if (_storage.ShowsBuildings && CanBuildHere)
            _storage.RefreshBuildings(SettlementManager.Instance.BuildingCatalog, BuildingSlotInfo, holding);
        else
            _storage.Refresh(_manager.Catalog.Decors, _manager.StorageCount, _manager.PlacedCount, holding);
    }

    // 건물은 광장 격자에만 지음
    private bool CanBuildHere
    {
        get
        {
            var settlement = SettlementManager.Instance;
            return settlement != null && settlement.IsLoaded && _board != null && _board.Board != null
                && _board.Board.BoardId == settlement.Config.PlazaBoardId;
        }
    }

    // 건물 칸 위쪽 한 줄: 열렸으면 골드 비용, 아니면 잠긴 이유를 짧게
    private (string top, bool available) BuildingSlotInfo(BuildingDefinition building)
    {
        var settlement = SettlementManager.Instance;
        switch (settlement.CheckBuildingUnlocked(building))
        {
            case BuildingBlock.None:
                int gold = settlement.BuildingGoldCost(building);
                return (gold > 0 ? $"{gold:N0}G" : "무료", true);
            case BuildingBlock.Level:
                return ($"Lv.{building.RequiredLevel}", false);
            case BuildingBlock.Traits:
                return (FirstMissingTrait(building), false);
            case BuildingBlock.MaxCount:
                return ("다 지음", false);
            default:
                return ("잠김", false);
        }
    }

    private static string FirstMissingTrait(BuildingDefinition building)
    {
        var settlement = SettlementManager.Instance;
        foreach (var requirement in building.Traits)
        {
            int have = settlement.TraitCount(requirement.Trait);
            if (have < requirement.Count)
                return $"{OtterTraits.DisplayName(requirement.Trait)} {have}/{requirement.Count}";
        }
        return "잠김";
    }

    #endregion

    #region 들기 / 놓기

    private void BeginNew(DecorDefinition decor)
    {
        if (decor is BuildingDefinition building)
        {
            BeginNewBuilding(building);
            return;
        }
        if (_manager.StorageCount(decor) <= 0)
        {
            _view.ShowHint(HintEmpty, true);
            return;
        }

        CancelSession();
        var center = _board.WorldToCell(_camera.transform.position);
        bool found = DecorEditSession.TryFindFreeNear(_layout, decor, center, DecorRotation.R0, out var origin);
        _session = DecorEditSession.ForNew(decor, origin);
        _view.ShowHint(found ? HintPlace : HintNoRoom, !found);
        ShowSession();
        RefreshStorage();
    }

    // 건물 칸 → 열렸으면 화면 가운데 근처 빈자리에 미리보기 (비용·공사 자리는 [확인] 때 다시 확인)
    private void BeginNewBuilding(BuildingDefinition building)
    {
        var settlement = SettlementManager.Instance;
        var block = settlement.CheckBuildingUnlocked(building);
        if (block != BuildingBlock.None)
        {
            _view.ShowHint(settlement.BuildingBlockText(building, block), true);
            return;
        }

        CancelSession();
        var center = _board.WorldToCell(_camera.transform.position);
        bool found = DecorEditSession.TryFindFreeNear(_layout, building, center, DecorRotation.R0, out var origin);
        _session = DecorEditSession.ForNew(building, origin);
        _view.ShowHint(found ? $"{HintBuildPlace} ({settlement.BuildingCostText(building)})" : HintNoRoom, !found);
        ShowSession();
        RefreshStorage();
    }

    private void BeginEditPlaced(PlacedDecor placed)
    {
        // 공사 중인 건물은 옮기지 않음 (남은 시간만 알림)
        var record = placed.Decor.IsBuilding && SettlementManager.Instance != null
            ? SettlementManager.Instance.BuildingRecordOf(placed.InstanceId)
            : null;
        if (record != null && !record.Built)
        {
            _view.ShowHint($"{HintUnderConstruction} ({SettlementManager.FormatShort(record.Remaining(SettlementManager.NowTicks))} 남음)", true);
            return;
        }

        CancelSession();
        _session = DecorEditSession.ForPlaced(placed);
        _board.SetHidden(placed.InstanceId, true);
        _view.ShowHint(HintPlace);
        ShowSession();
    }

    private void RotateSession()
    {
        if (_session == null)
            return;

        _session.Rotate();
        UpdateGhost();
    }

    private void ConfirmSession()
    {
        if (_session == null)
            return;
        if (_session.Decor is BuildingDefinition building)
        {
            ConfirmBuilding(building);
            return;
        }

        var result = _session.IsNew
            ? _manager.TryPlaceFromStorage(_board.Board, _session.Decor, _session.Origin, _session.Rotation, out _)
            : _layout.TryMove(_session.InstanceId, _session.Origin, _session.Rotation);

        if (result != DecorPlacementResult.Ok)
        {
            _view.ShowHint(ReasonText(result), true);
            return;
        }

        if (!_session.IsNew)
            _board.SetHidden(_session.InstanceId, false);
        EndSession();
        _view.ShowHint(HintPlaced);
    }

    // 건물: 새로 짓기 → 자리·길 확인 → 비용 확인 대화 → 공사 시작 / 놓인 건물 → 자리·길 확인 → 옮기기
    private void ConfirmBuilding(BuildingDefinition building)
    {
        var check = _session.Check(_layout);
        if (check != DecorPlacementResult.Ok)
        {
            _view.ShowHint(ReasonText(check), true);
            return;
        }
        if (_board.WouldCutPath(_session.Area))
        {
            _view.ShowHint(HintCutsPath, true);
            return;
        }

        if (!_session.IsNew)
        {
            var moved = _layout.TryMove(_session.InstanceId, _session.Origin, _session.Rotation);
            if (moved != DecorPlacementResult.Ok)
            {
                _view.ShowHint(ReasonText(moved), true);
                return;
            }
            _board.SetHidden(_session.InstanceId, false);
            EndSession();
            _view.ShowHint(HintMoved);
            return;
        }

        var settlement = SettlementManager.Instance;
        var block = settlement.CheckBuilding(building);
        if (block != BuildingBlock.None)
        {
            _view.ShowHint(settlement.BuildingBlockText(building, block), true);
            return;
        }

        var origin = _session.Origin;
        string cost = settlement.BuildingCostText(building);
        var game = GameManager.Instance;
        if (game == null)
        {
            StartBuilding(building, origin);
            return;
        }
        game.ShowConfirm($"{building.DisplayName} 짓기", $"{cost}\n이 자리에 지을까요?", () => StartBuilding(building, origin));
    }

    private void StartBuilding(BuildingDefinition building, Vector2Int origin)
    {
        var settlement = SettlementManager.Instance;
        var block = settlement.TryStartBuilding(building, origin, out var placement);
        if (block != BuildingBlock.None)
        {
            _view.ShowHint(settlement.BuildingBlockText(building, block), true);
            return;
        }
        if (placement != DecorPlacementResult.Ok)
        {
            _view.ShowHint(ReasonText(placement), true);
            return;
        }
        if (_session != null && _session.IsNew && _session.Decor == building)
            EndSession();
        _view.ShowHint(HintBuildStarted);
    }

    // 새 물건: 꺼낸 것을 취소 / 놓여 있던 물건: 보관함으로 (놓인 건물은 버튼이 없음)
    private void RemoveSession()
    {
        if (_session == null || (_session.Decor.IsBuilding && !_session.IsNew))
            return;

        bool wasPlaced = !_session.IsNew;
        if (wasPlaced)
            _manager.ReturnToStorage(_board.Board, _session.InstanceId);
        EndSession();
        _view.ShowHint(wasPlaced ? HintStored : HintIdle);
    }

    // 확인하지 않고 그만둠: 놓여 있던 물건은 원래 자리 그대로 다시 보임
    private void CancelSession()
    {
        if (_session == null)
            return;

        if (!_session.IsNew && _board != null)
            _board.SetHidden(_session.InstanceId, false);
        EndSession();
    }

    private void EndSession()
    {
        _session = null;
        if (_ghost != null)
            _ghost.Hide();
        _view.HideActions();
        if (_manager != null)
            RefreshStorage();
    }

    private void ShowSession()
    {
        _view.ShowActions(_session.CanRotate, !_session.Decor.IsBuilding || _session.IsNew);
        UpdateGhost();
    }

    private void UpdateGhost()
    {
        var result = _session.Check(_layout);
        _ghost.Show(_session.Decor, _session.Rotation, _board.AreaWorldRect(_session.Area), result == DecorPlacementResult.Ok, _board.Fill);
    }

    private string ReasonText(DecorPlacementResult result)
    {
        switch (result)
        {
            case DecorPlacementResult.Occupied: return HintOccupied;
            case DecorPlacementResult.Locked: return LockedText();
            case DecorPlacementResult.NotOwned: return HintEmpty;
            default: return HintUnavailable;
        }
    }

    // 잠긴 구역이면 여는 조건도 보여줌 (예: "아직 열리지 않은 구역이에요 (Lv.5 이상 · 조개 100)")
    private string LockedText()
    {
        foreach (var cell in _session.Area.allPositionsWithin)
        {
            string regionId = _layout.RegionAt(cell);
            if (regionId == null || _layout.IsRegionUnlocked(regionId))
                continue;

            var region = _manager.FindRegion(_board.Board, regionId);
            if (region == null || region.Requirements.Count == 0)
                break;

            var parts = new List<string>();
            foreach (var requirement in region.Requirements)
            {
                if (requirement != null)
                    parts.Add(requirement.Describe());
            }
            return $"{HintLocked} ({string.Join(" · ", parts)})";
        }
        return HintLocked;
    }

    #endregion

    #region 입력

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null || _camera == null)
            return;

        Vector2 screen = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
            BeginPress(screen);
        else if (_pressing && pointer.press.isPressed)
            ContinuePress(screen);

        if (_pressing && pointer.press.wasReleasedThisFrame)
            EndPress(screen);
    }

    private void BeginPress(Vector2 screen)
    {
        _pressing = true;
        _pressScreen = screen;
        _movedBeyondTap = false;
        _pressOnUI = IsOverUI(screen);
        _dragging = false;
        if (_pressOnUI || _session == null)
            return;

        Vector2 world = ScreenToWorld(screen);
        var grabRect = _ghost.WorldRect;
        float pad = _board.CellSize * GrabPaddingInCells;
        grabRect = new Rect(grabRect.xMin - pad, grabRect.yMin - pad, grabRect.width + pad * 2f, grabRect.height + pad * 2f);
        if (!grabRect.Contains(world))
            return;

        _dragging = true;
        _grabOffset = _session.Origin - _board.WorldToCell(world);
        SetCameraDrag(false);
    }

    private void ContinuePress(Vector2 screen)
    {
        float threshold = Mathf.Min(Screen.width, Screen.height) * TapThresholdScreenFraction;
        if ((screen - _pressScreen).sqrMagnitude > threshold * threshold)
            _movedBeyondTap = true;

        if (!_dragging)
            return;

        var origin = _board.WorldToCell(ScreenToWorld(screen)) + _grabOffset;
        if (origin != _session.Origin)
        {
            _session.MoveTo(origin);
            UpdateGhost();
        }
    }

    private void EndPress(Vector2 screen)
    {
        _pressing = false;
        if (_dragging)
        {
            _dragging = false;
            SetCameraDrag(true);
            ShowPlacementHint();
            return;
        }

        if (_pressOnUI || _movedBeyondTap)
            return;

        HandleTap(ScreenToWorld(screen));
    }

    // 놓인 물건을 탭 → 들어 올림, 빈 곳을 탭 → 들고 있는 물건을 그 자리로
    private void HandleTap(Vector2 world)
    {
        var cell = _board.WorldToCell(world);
        var placed = _layout.GetAt(cell);
        if (placed != null && (_session == null || placed.InstanceId != _session.InstanceId))
        {
            BeginEditPlaced(placed);
            return;
        }

        if (_session == null)
            return;

        var size = _session.Size;
        _session.MoveTo(cell - new Vector2Int(size.x / 2, size.y / 2));
        UpdateGhost();
        ShowPlacementHint();
    }

    // 지금 자리에 놓을 수 있는지 안내 (옮길 때마다. 막혔으면 이유)
    private void ShowPlacementHint()
    {
        var result = _session.Check(_layout);
        if (result == DecorPlacementResult.Ok && _session.Decor.IsBuilding && _board.WouldCutPath(_session.Area))
            _view.ShowHint(HintCutsPath, true);
        else
            _view.ShowHint(result == DecorPlacementResult.Ok ? HintPlace : ReasonText(result), result != DecorPlacementResult.Ok);
    }

    private void LateUpdate()
    {
        if (_session == null || _camera == null)
            return;

        // 물건 위 버튼이 미리보기 윗변 가운데를 따라다님
        var rect = _ghost.WorldRect;
        Vector3 top = new Vector3(rect.center.x, rect.yMax, 0f);
        Vector2 screen = _camera.WorldToScreenPoint(top);
        var parent = (RectTransform)_view.Actions.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local))
            _view.Actions.anchoredPosition = local + Vector2.up * ActionsGap;
    }

    private Vector2 ScreenToWorld(Vector2 screen)
    {
        Vector3 p = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
        return p;
    }

    private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

    private static bool IsOverUI(Vector2 screen)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        UiHits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screen }, UiHits);
        return UiHits.Count > 0;
    }

    private void SetCameraDrag(bool enabled)
    {
        if (_cameraDrag != null)
            _cameraDrag.enabled = enabled;
    }

    #endregion

    // 처음 보는 한글을 폰트 아틀라스에 미리 넣는다 (퀘스트 화면과 같은 이유: 스크롤 목록이 있는 화면에서
    // 새 글자가 한꺼번에 추가되면 D3D12 에디터에서 GPU가 멈춰 크래시가 났다)
    private void PrepareGlyphs()
    {
        var text = new StringBuilder("0123456789x()·!,. 전체배치됨상점꾸미기모드완료보관함");
        text.Append(HintIdle).Append(HintPlace).Append(HintPlaced).Append(HintStored).Append(HintNoRoom)
            .Append(HintEmpty).Append(HintShop).Append(HintFairyComing).Append(HintOccupied).Append(HintUnavailable).Append(HintLocked)
            .Append(HintBuildPlace).Append(HintBuildStarted).Append(HintMoved).Append(HintUnderConstruction).Append(HintCutsPath)
            .Append("건물다지음잠김무료골드목재돌남음짓기이자리에지을까요?왕국레벨부터특성해달이모자라요더없어요다른건물을짓는중이에요건설재료Lv.G/");
        foreach (var building in DecorManager.Instance.Catalog.Buildings)
        {
            if (building != null)
                text.Append(building.DisplayName);
        }
        for (int i = 1; i <= OtterTraits.Count; i++)
            text.Append(OtterTraits.DisplayName((OtterTrait)i));
        foreach (var decor in DecorManager.Instance.Catalog.Decors)
        {
            if (decor != null)
                text.Append(decor.DisplayName).Append(decor.Item != null && decor.Item.Category != null ? decor.Item.Category.DisplayName : "");
        }

        var fonts = new HashSet<TMP_FontAsset>();
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            fonts.Add(label.font);

        string characters = text.ToString();
        foreach (var font in fonts)
        {
            if (font != null)
                font.TryAddCharacters(characters, out _);
        }
        _glyphsReady = true;
    }
}
