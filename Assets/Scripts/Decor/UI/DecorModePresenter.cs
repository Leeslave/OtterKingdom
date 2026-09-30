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
    private const string HintOccupied = "다른 물건이 있는 자리예요";
    private const string HintUnavailable = "여기에는 놓을 수 없어요";
    private const string HintLocked = "아직 열리지 않은 구역이에요";

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
        _storage.OnShopClicked += () => _view.ShowHint(HintShop);
        _storage.OnTabChanged += RefreshStorage;
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
        _hud.SetActive(false);
        gameObject.SetActive(true);

        _manager.OnStorageChanged += RefreshStorage;
        _session = null;
        _view.HideActions();
        _view.ShowHint(HintIdle);
        RefreshStorage();
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
    }

    private void OnDisable()
    {
        // 장소를 옮기는 등으로 꺼질 때도 들고 있던 물건과 카메라를 원래대로
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
        _storage.Refresh(_manager.Catalog.Decors, _manager.StorageCount, _session != null && _session.IsNew ? _session.Decor : null);
    }

    #endregion

    #region 들기 / 놓기

    private void BeginNew(DecorDefinition decor)
    {
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

    private void BeginEditPlaced(PlacedDecor placed)
    {
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

    // 새 물건: 꺼낸 것을 취소 / 놓여 있던 물건: 보관함으로
    private void RemoveSession()
    {
        if (_session == null)
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
        _view.ShowActions(_session.CanRotate);
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
            var result = _session.Check(_layout);
            _view.ShowHint(result == DecorPlacementResult.Ok ? HintPlace : ReasonText(result), result != DecorPlacementResult.Ok);
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
            .Append(HintEmpty).Append(HintShop).Append(HintOccupied).Append(HintUnavailable).Append(HintLocked);
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
