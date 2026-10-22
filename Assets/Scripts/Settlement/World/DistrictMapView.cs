using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 열린 단계만 용도 색으로 표시한다. 용도 색은 구역 화면(용도 고르기)·꾸미기 모드를 켰을 때만,
/// 부지 테두리는 꾸미기 중, 미지정 표지는 평상시에 보인다. 구역 화면이 열려 있으면 고르는 땅을 강조하고 카메라로 비춘다.
/// </summary>
// 카메라(PlazaCameraController, -50) 뒤에 LateUpdate가 돌도록
[DefaultExecutionOrder(50)]
public class DistrictMapView : MonoBehaviour
{
    private SettlementManager _manager;
    private DecorBoardView _board;
    private Transform _visuals;
    private Transform _slots;
    private Transform _signs;
    private Sprite _white;
    private Texture2D _texture;
    private bool _dirty = true;
    // 구역 화면(아래 시트)이 화면 아래 절반을 덮으므로, 고르는 땅이 위쪽 절반 가운데에 오도록 카메라를 이만큼(× 화면 반 높이) 내림
    private const float ChoosingCameraDrop = 0.48f;
    private Transform _tints;
    // 이름표: 그 땅 안에서 화면에 보이는 곳으로 따라감 (땅이 화면보다 커서 고정 위치면 HUD 뒤나 화면 밖에 걸림)
    private readonly List<(DistrictBlockDefinition block, Transform root, Rect land, Vector2 half)> _labels = new List<(DistrictBlockDefinition, Transform, Rect, Vector2)>();
    // 구역 화면에서 고르는 땅이 바뀌면 이름표가 살짝 튀어나옴
    private const float LabelPopSeconds = 0.22f;
    private DistrictBlockDefinition _labelBlock;
    private float _labelPopAt = -1f;
    private static readonly Vector2 LabelSize = new Vector2(7.4f, 1.5f);
    // 화면 위 HUD · 아래 패널이 덮는 비율 (아래는 구역 시트가 열려 있으면 더 큼)
    private const float HudTop = 0.27f; // 오른쪽 뽑기 버튼까지
    private const float PanelBottom = 0.27f;
    private const float SheetBottom = 0.44f;
    private Transform _focus;
    private DistrictBlockDefinition _focused;
    private DistrictPopupView _popup;
    private readonly List<(DistrictBlockDefinition block, Rect area)> _targets = new List<(DistrictBlockDefinition, Rect)>();

    private void Start()
    {
        _manager = SettlementManager.Instance;
        _board = DecorBoardView.Active;
        if (_manager != null) _manager.OnChanged += MarkDirty;
        _texture = new Texture2D(1, 1);
        _texture.SetPixel(0, 0, Color.white);
        _texture.Apply();
        _white = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }

    private void OnDestroy()
    {
        if (_manager != null) _manager.OnChanged -= MarkDirty;
        if (_white != null) Destroy(_white);
        if (_texture != null) Destroy(_texture);
    }

    private void MarkDirty() => _dirty = true;

    private float _labelCover;
    private DistrictBlockDefinition _labelFocus;

    // 이름표는 카메라가 이번 프레임에 다 움직인 뒤(PlazaCameraController.LateUpdate의 범위 맞추기 뒤)에 놓음.
    // Update에서 놓으면 지도 끝에서 카메라가 밀렸다 당겨질 때 한 프레임씩 어긋나 이름표가 떨림
    private void LateUpdate()
    {
        if (_tints != null && _tints.gameObject.activeInHierarchy)
            PlaceLabels(_labelCover, _labelFocus);
    }

    private void Update()
    {
        if (_manager == null || !_manager.IsLoaded || _board == null) return;
        if (_dirty) Rebuild();
        // 구역 색은 구역 화면(용도 고르기)이나 꾸미기 모드를 켰을 때만. 평상시 광장은 깨끗하게
        bool editing = DecorModePresenter.IsActive;
        if (_popup == null) _popup = FindAnyObjectByType<DistrictPopupView>(FindObjectsInactive.Include);
        bool choosing = _popup != null && _popup.IsOpen;
        bool relocating = RelocationModePresenter.IsOpen;
        _tints.gameObject.SetActive(editing || choosing || relocating);
        _labelCover = choosing ? SheetBottom : PanelBottom;
        _labelFocus = choosing ? _popup.Block : null;
        _slots.gameObject.SetActive(editing);
        _signs.gameObject.SetActive(!editing && !choosing && !relocating && _manager.IsAdministrator);
        UpdateFocus(choosing ? _popup.Block : null);
        UpdateMovingMarkers();
        bool plain = !editing && !choosing && !relocating && _manager.IsAdministrator;
        UpdateComplaints(plain);
        if (!plain || !PlazaTapInput.TryGetTap(out var world)) return;
        if (TapComplaint(world)) return;
        foreach (var target in _targets)
        {
            if (!target.area.Contains(world)) continue;
            _manager.RequestDistrict(target.block);
            break;
        }
    }

    #region 민원 말풍선 (구역·이사 5막)

    // 맞는 구역이 있는데 그 밖에 있는 건물 위: "민원!" 리본 (통통). 누르면 이사 지시서 화면 · 지시서가 없으면 또박이 한마디
    private const float ComplaintRefreshSeconds = 0.5f;
    private static readonly Vector2 ComplaintSize = new Vector2(2.2f, 0.95f);
    private readonly Dictionary<int, (Transform root, Vector2 basePos, DistrictType type)> _complaints = new Dictionary<int, (Transform, Vector2, DistrictType)>();
    private readonly HashSet<int> _complaintSeen = new HashSet<int>();
    private float _complaintTimer;

    private void UpdateComplaints(bool visible)
    {
        _complaintTimer -= Time.unscaledDeltaTime;
        if (_complaintTimer <= 0f)
        {
            _complaintTimer = ComplaintRefreshSeconds;
            RefreshComplaints();
        }
        float bob = 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.4f));
        foreach (var pair in _complaints)
        {
            if (pair.Value.root == null) continue;
            pair.Value.root.gameObject.SetActive(visible);
            pair.Value.root.position = pair.Value.basePos + Vector2.up * bob;
        }
    }

    private void RefreshComplaints()
    {
        _complaintSeen.Clear();
        if (_manager.IsAdministrator && _manager.PlazaBoard == _board.Board)
        {
            foreach (var c in _manager.RelocationCandidates())
            {
                if (_manager.Settlement.IsRelocating(c.InstanceId) || RelocationRules.Fits(_manager.Config, _manager.Settlement, c)
                    || !DistrictRules.HasDistrict(_manager.Config, _manager.Settlement, c.Type))
                    continue;
                _complaintSeen.Add(c.InstanceId);
                var pos = WorldSticker.AboveBuilding(WorldRect(c.Area));
                if (_complaints.TryGetValue(c.InstanceId, out var existing) && existing.root != null)
                {
                    _complaints[c.InstanceId] = (existing.root, pos, c.Type);
                    continue;
                }
                _complaints[c.InstanceId] = (CreateComplaint(), pos, c.Type);
            }
        }
        var gone = new List<int>();
        foreach (var id in _complaints.Keys)
            if (!_complaintSeen.Contains(id)) gone.Add(id);
        foreach (var id in gone)
        {
            if (_complaints[id].root != null) Destroy(_complaints[id].root.gameObject);
            _complaints.Remove(id);
        }
    }

    private Transform CreateComplaint()
    {
        var style = WorldSticker.Style;
        var root = Child("Complaint", transform); // 원점에서 만들고 옮김
        WorldSticker.Panel(root, "Ribbon", style != null ? style.Ribbon : null, Vector2.zero, ComplaintSize, Color.white, 31993, 0.36f);
        WorldSticker.Label(root, "민원!", new Vector2(0f, 0.04f), ComplaintSize - new Vector2(0.5f, 0.25f), 4.6f, 31994);
        return root;
    }

    private bool TapComplaint(Vector2 world)
    {
        foreach (var pair in _complaints)
        {
            if (pair.Value.root == null || !pair.Value.root.gameObject.activeSelf) continue;
            var area = new Rect((Vector2)pair.Value.root.position - ComplaintSize * 0.5f, ComplaintSize);
            if (!area.Contains(world)) continue;
            if (!RelocationModePresenter.Open())
                GameNotices.Post(new GameNotice($"또박이: 이 건물은 {Districts.DisplayName(pair.Value.type)}에 있어야 해요. 꾸미기에서 옮겨 주세요"));
            return true;
        }
        return false;
    }

    #endregion

    #region 이사 중 표시 (구역·이사 4단계)

    private class MovingMarker
    {
        public Transform Root;
        public SpriteRenderer Fill;
        public TextMeshPro Label;
    }

    // 종이 판 (글 + 막대) 크기, 막대 안쪽 폭
    private static readonly Vector2 MarkerSize = new Vector2(3.1f, 1.3f);
    private const float TrackWidth = 2.6f;
    private const float TrackHeight = 0.34f;
    private const float FillMin = 0.3f;
    private readonly Dictionary<int, MovingMarker> _moving = new Dictionary<int, MovingMarker>();
    private readonly List<int> _movingGone = new List<int>();

    // 옮기는 중인 건물 위: 종이 판에 "이사 중 0:42" + 진행 막대 (평상시에도 보임)
    private void UpdateMovingMarkers()
    {
        var plaza = _manager.PlazaBoard;
        var layout = plaza != null && plaza == _board.Board ? DecorManager.Instance.GetLayout(plaza) : null;
        long now = SettlementManager.NowTicks;
        _movingGone.Clear();
        foreach (var id in _moving.Keys)
            if (layout == null || !_manager.Settlement.IsRelocating(id) || !layout.TryGet(id, out _)) _movingGone.Add(id);
        foreach (var id in _movingGone)
        {
            if (_moving[id].Root != null) Destroy(_moving[id].Root.gameObject);
            _moving.Remove(id);
        }
        if (layout == null) return;
        foreach (var move in _manager.Settlement.RelocationMoves)
        {
            if (!layout.TryGet(move.InstanceId, out var placed)) continue;
            if (!_moving.TryGetValue(move.InstanceId, out var marker))
                _moving[move.InstanceId] = marker = CreateMovingMarker();
            var rect = _board.AreaWorldRect(placed.Area);
            marker.Root.position = WorldSticker.AboveBuilding(rect);
            float width = Mathf.Lerp(FillMin, TrackWidth - 0.06f, move.Progress(now));
            WorldSticker.Resize(marker.Fill, new Vector2(width, TrackHeight - 0.08f));
            marker.Fill.transform.localPosition = new Vector3(-(TrackWidth - 0.06f) * 0.5f + width * 0.5f, -0.3f, 0f);
            marker.Label.text = $"이사 중 {SettlementManager.FormatShort(move.Remaining(now))}";
        }
    }

    private MovingMarker CreateMovingMarker()
    {
        var style = WorldSticker.Style;
        var marker = new MovingMarker { Root = Child("Moving", transform) };
        var root = marker.Root; // 만들 때는 원점에 있으므로 아래 월드 위치 = 판 기준 위치
        WorldSticker.Panel(root, "Plate", style != null ? style.Plate : null, Vector2.zero, MarkerSize, Color.white, 31993, 0.32f);
        marker.Label = WorldSticker.Label(root, "", new Vector2(0f, 0.2f), new Vector2(TrackWidth, 0.6f), 4.2f, 31996);
        WorldSticker.Panel(root, "Track", style != null ? style.BarTrack : null, new Vector2(0f, -0.3f), new Vector2(TrackWidth, TrackHeight), Color.white, 31994, 0.12f);
        marker.Fill = WorldSticker.Panel(root, "Fill", style != null ? style.BarFill : null, new Vector2(0f, -0.3f), new Vector2(FillMin, TrackHeight - 0.08f), Color.white, 31995, 0.1f);
        return marker;
    }

    #endregion

    // 고르는 중인 땅: 테두리 + (아직 용도가 없으면) 옅은 바탕, 카메라는 그 땅을 아래 시트 위쪽 화면에 비춤
    private void UpdateFocus(DistrictBlockDefinition block)
    {
        if (block == _focused) return;
        _focused = block;
        if (_focus != null) Destroy(_focus.gameObject);
        _focus = null;
        // 구역 땅은 화면보다 커서 고르는 동안은 가장 멀리 보고, 닫으면 원래 확대로
        var controller = FindAnyObjectByType<PlazaCameraController>();
        if (controller != null) controller.SetOverview(block != null);
        if (block == null) return;

        _focus = Child("Focus", transform);
        int open = Mathf.Max(1, DistrictRules.OpenStages(block, _manager.Settlement));
        var type = _manager.DistrictTypeOf(block);
        var line = type == DistrictType.None ? new Color(1f, 0.97f, 0.86f, 0.95f) : DistrictGuideRules.ColorOf(type);
        line.a = 0.95f;
        int order = _board.OverlaySortingOrder + 2;
        const float width = 0.14f;
        for (int i = 0; i < open && i < block.Stages.Count; i++)
        {
            foreach (var area in block.Stages[i].Areas)
            {
                var r = WorldRect(area);
                if (type == DistrictType.None) Paint("Fill", _focus, r, new Color(1f, 0.97f, 0.86f, 0.28f), PlazaDepth.FlatPropOrder);
                Paint("Edge", _focus, new Rect(r.xMin, r.yMin, r.width, width), line, order);
                Paint("Edge", _focus, new Rect(r.xMin, r.yMax - width, r.width, width), line, order);
                Paint("Edge", _focus, new Rect(r.xMin, r.yMin, width, r.height), line, order);
                Paint("Edge", _focus, new Rect(r.xMax - width, r.yMin, width, r.height), line, order);
            }
        }

        var area0 = GuideWorldRect(block);
        var camera = Camera.main;
        if (area0.HasValue && controller != null && camera != null)
            controller.PanTo(area0.Value.center - Vector2.up * camera.orthographicSize * ChoosingCameraDrop);
    }

    public Rect? GuideTarget(DistrictBlockDefinition block)
    {
        var area = GuideWorldRect(block);
        var camera = Camera.main;
        if (!area.HasValue || camera == null) return null;
        Vector2 min = camera.WorldToScreenPoint(area.Value.min);
        Vector2 max = camera.WorldToScreenPoint(area.Value.max);
        // 중앙 공터가 화면보다 커도 강조 테두리가 화면 밖으로 사라지지 않도록 보이는 땅 안에서 표시한다.
        float left = Mathf.Max(min.x, Screen.width * 0.10f);
        float bottom = Mathf.Max(min.y, Screen.height * 0.12f);
        float right = Mathf.Min(max.x, Screen.width * 0.90f);
        float top = Mathf.Min(max.y, Screen.height * 0.88f);
        return right > left && top > bottom ? Rect.MinMaxRect(left, bottom, right, top) : (Rect?)null;
    }

    public void FocusGuide(DistrictBlockDefinition block)
    {
        var area = GuideWorldRect(block);
        var camera = FindAnyObjectByType<PlazaCameraController>();
        if (area.HasValue && camera != null) camera.PanTo(area.Value.center);
    }

    private Rect? GuideWorldRect(DistrictBlockDefinition block) =>
        _board != null && block != null && block.Stages.Count > 0 && block.Stages[0].Areas.Count > 0
            ? WorldRect(block.Stages[0].Areas[0]) : (Rect?)null;

    private Rect WorldRect(RectInt area) => _board.AreaWorldRect(new RectInt(area.position + _board.Board.SaveOrigin, area.size));

    private void Rebuild()
    {
        _dirty = false;
        if (_visuals != null) { _visuals.gameObject.SetActive(false); Destroy(_visuals.gameObject); }
        _targets.Clear();
        _visuals = Child("Districts", transform);
        _tints = Child("Tints", _visuals);
        _labels.Clear();
        _slots = Child("Slots", _visuals);
        _focused = null; // 용도가 바뀌었으면 강조 색도 다시
        if (_focus != null) { Destroy(_focus.gameObject); _focus = null; }
        _signs = Child("Signs", _visuals);
        foreach (var block in _manager.DistrictBlocks)
        {
            if (!DistrictRules.IsEditable(block)) continue;
            int open = DistrictRules.OpenStages(block, _manager.Settlement);
            if (open == 0) continue;
            var type = _manager.DistrictTypeOf(block);
            if (_manager.Settlement.GetDistrict(block.BlockId) == DistrictType.None)
            {
                if (block.Stages[0].Areas.Count > 0) AddSign(block, WorldRect(block.Stages[0].Areas[0]).center);
                continue;
            }
            var color = DistrictGuideRules.ColorOf(type);
            for (int i = 0; i < open; i++)
            {
                foreach (var area in block.Stages[i].Areas)
                {
                    var tint = color; tint.a = 0.24f; // 켰을 때만 보이므로 알아볼 수 있게 (땅이 화면보다 커서 너무 진하면 화면 전체가 물듦)
                    Paint("Tint_" + block.BlockId, _tints, WorldRect(area), tint, PlazaDepth.FlatPropOrder);
                }
            }
            // 땅이 화면보다 커서 색만으로는 잘 안 읽히므로 땅 위쪽에 이름표 ("중앙 공터 · 주거 지역")
            if (block.Stages[0].Areas.Count > 0)
            {
                var top = WorldRect(block.Stages[0].Areas[0]);
                AddDistrictLabel(block, $"{block.DisplayName} · {Districts.DisplayName(type)}", top, color);
            }
            color.a = 0.8f;
            foreach (var slot in block.Slots)
            {
                if (slot == null || slot.Stage >= open) continue;
                var r = WorldRect(slot.Area);
                const float width = 0.045f;
                Paint(slot.SlotId, _slots, new Rect(r.xMin, r.yMin, r.width, width), color, _board.OverlaySortingOrder + 1);
                Paint(slot.SlotId, _slots, new Rect(r.xMin, r.yMax - width, r.width, width), color, _board.OverlaySortingOrder + 1);
                Paint(slot.SlotId, _slots, new Rect(r.xMin, r.yMin, width, r.height), color, _board.OverlaySortingOrder + 1);
                Paint(slot.SlotId, _slots, new Rect(r.xMax - width, r.yMin, width, r.height), color, _board.OverlaySortingOrder + 1);
            }
        }
    }

    // 아직 용도를 안 정한 땅: 종이 판 표지 (누르면 구역 화면)
    private void AddSign(DistrictBlockDefinition block, Vector2 center)
    {
        var size = new Vector2(4.6f, 1.9f);
        var style = WorldSticker.Style;
        WorldSticker.Panel(_signs, "Sign_" + block.BlockId, style != null ? style.Plate : null, center, size, Color.white, 31990, 0.38f);
        WorldSticker.Label(_signs, block.DisplayName, center + new Vector2(0f, 0.32f), new Vector2(size.x - 0.6f, 0.62f), 4.4f, 31991);
        var question = WorldSticker.Label(_signs, "이 땅을 뭘로 쓸까요?", center - new Vector2(0f, 0.36f), new Vector2(size.x - 0.6f, 0.55f), 3.4f, 31991, false);
        question.color = new Color32(0x6B, 0x4A, 0x3A, 0xFF);
        _targets.Add((block, new Rect(center - size * 0.5f, size)));
    }

    // 정한 땅의 이름표: 종이 판 + 용도 색 칩 + "중앙 공터 · 주거 지역" (위치는 PlaceLabels가 정함)
    private void AddDistrictLabel(DistrictBlockDefinition block, string label, Rect land, Color color)
    {
        var style = WorldSticker.Style;
        var root = Child("DistrictLabel", _tints); // 원점에서 만들고 옮김
        WorldSticker.Panel(root, "Plate", style != null ? style.Plate : null, Vector2.zero, LabelSize, Color.white, 31990, 0.36f);
        var chip = color; chip.a = 1f;
        WorldSticker.Panel(root, "Chip", style != null ? style.Chip : null, new Vector2(-LabelSize.x * 0.5f + 0.78f, 0.06f), new Vector2(0.62f, 0.62f), chip, 31991, 0.2f);
        WorldSticker.Label(root, label, new Vector2(0.32f, 0.08f), new Vector2(LabelSize.x - 1.6f, 0.9f), 5.6f, 31992);
        root.position = new Vector2(land.center.x, land.yMax - 4f);
        _labels.Add((block, root, land, LabelSize * 0.5f));
    }

    // 꾸미기 · 이사 화면: 이름표를 그 땅과 보이는 화면(HUD · 아래 패널 제외)이 겹치는 곳의 위쪽 가운데로 (땅이 안 보이면 숨김).
    // 구역 화면(focus): 고르는 땅의 이름표 하나만, 화면 위쪽 가운데에 고정 — 이전 · 다음으로 카메라가 움직여도 미끄러지지 않고 글만 바뀜
    private void PlaceLabels(float bottomCover, DistrictBlockDefinition focus)
    {
        var camera = Camera.main;
        if (camera == null || _labels.Count == 0) return;
        float height = camera.orthographicSize * 2f;
        float width = height * camera.aspect;
        Vector2 c = camera.transform.position;
        var view = Rect.MinMaxRect(c.x - width * 0.5f, c.y - height * 0.5f + height * bottomCover, c.x + width * 0.5f, c.y + height * 0.5f - height * HudTop);
        if (focus != _labelBlock)
        {
            _labelBlock = focus;
            _labelPopAt = Time.unscaledTime;
        }
        float pop = Mathf.Clamp01((Time.unscaledTime - _labelPopAt) / LabelPopSeconds);
        float scale = focus != null ? Mathf.Lerp(0.82f, 1f, 1f - (1f - pop) * (1f - pop)) : 1f;
        _placedLabels.Clear();
        // 화면에 많이 보이는 땅의 이름표부터 자리를 잡고, 이웃 땅 이름표와 겹치면 비켜 놓음 (자리가 없으면 숨김)
        if (focus == null)
            _labels.Sort((a, b) => SeenArea(b.land, view).CompareTo(SeenArea(a.land, view)));
        foreach (var (block, root, land, half) in _labels)
        {
            if (root == null) continue;
            if (focus != null)
            {
                bool mine = block == focus;
                root.gameObject.SetActive(mine);
                if (!mine) continue;
                root.localScale = Vector3.one * scale;
                root.position = new Vector2(view.center.x, view.yMax - half.y - 0.3f);
                continue;
            }
            root.localScale = Vector3.one;
            bool visible = land.Overlaps(view);
            root.gameObject.SetActive(visible);
            if (!visible) continue;
            var seen = Rect.MinMaxRect(Mathf.Max(land.xMin, view.xMin), Mathf.Max(land.yMin, view.yMin), Mathf.Min(land.xMax, view.xMax), Mathf.Min(land.yMax, view.yMax));
            float x = Mathf.Clamp(seen.center.x, view.xMin + half.x + 0.2f, view.xMax - half.x - 0.2f);
            float y = Mathf.Clamp(seen.yMax - half.y - 0.6f, view.yMin + half.y + 0.2f, view.yMax - half.y - 0.2f);
            if (!TryAvoidLabels(x, ref y, half, Mathf.Max(seen.yMin, view.yMin + 0.2f) + half.y, Mathf.Min(seen.yMax, view.yMax - 0.2f) - half.y))
            {
                root.gameObject.SetActive(false);
                continue;
            }
            root.position = new Vector2(x, y);
            _placedLabels.Add(new Rect(x - half.x, y - half.y, half.x * 2f, half.y * 2f));
        }
    }

    private const float LabelGap = 0.15f;
    private readonly List<Rect> _placedLabels = new List<Rect>();

    private static float SeenArea(Rect land, Rect view)
    {
        float w = Mathf.Min(land.xMax, view.xMax) - Mathf.Max(land.xMin, view.xMin);
        float h = Mathf.Min(land.yMax, view.yMax) - Mathf.Max(land.yMin, view.yMin);
        return w > 0f && h > 0f ? w * h : 0f;
    }

    // 먼저 놓인 이름표와 겹치면 아래로 비키고, 아래가 막히면 위로 (그 땅 안 · 화면 안에서만). 둘 다 안 되면 false
    private bool TryAvoidLabels(float x, ref float y, Vector2 half, float minY, float maxY)
    {
        if (minY > maxY)
            return false;
        float start = y;
        for (int pass = 0; pass < 2; pass++)
        {
            y = Mathf.Clamp(start, minY, maxY);
            for (int i = 0; i < _placedLabels.Count + 1; i++)
            {
                var hit = FirstLabelHit(x, y, half);
                if (!hit.HasValue)
                    return true;
                y = pass == 0 ? hit.Value.yMin - half.y - LabelGap : hit.Value.yMax + half.y + LabelGap;
                if (y < minY || y > maxY)
                    break;
            }
        }
        return false;
    }

    private Rect? FirstLabelHit(float x, float y, Vector2 half)
    {
        var mine = new Rect(x - half.x - LabelGap, y - half.y - LabelGap, (half.x + LabelGap) * 2f, (half.y + LabelGap) * 2f);
        foreach (var placed in _placedLabels)
            if (placed.Overlaps(mine)) return placed;
        return null;
    }

    private void Paint(string label, Transform parent, Rect rect, Color color, int order)
    {
        var item = Child(label, parent);
        item.position = rect.center;
        item.localScale = new Vector3(rect.width, rect.height, 1);
        var renderer = item.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = _white;
        renderer.color = color;
        renderer.sortingOrder = order;
    }

    private static Transform Child(string label, Transform parent)
    {
        var child = new GameObject(label).transform;
        child.SetParent(parent, false);
        return child;
    }
}
