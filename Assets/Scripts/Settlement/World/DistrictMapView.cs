using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>열린 단계만 용도 색으로 표시한다. 부지 테두리는 건설·꾸미기 중, 미지정 표지는 평상시에 보인다.</summary>
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

    private void Update()
    {
        if (_manager == null || !_manager.IsLoaded || _board == null) return;
        if (_dirty) Rebuild();
        bool editing = DecorModePresenter.IsActive;
        _slots.gameObject.SetActive(editing);
        _signs.gameObject.SetActive(!editing && _manager.IsAdministrator);
        if (!_signs.gameObject.activeSelf || !PlazaTapInput.TryGetTap(out var world)) return;
        foreach (var target in _targets)
        {
            if (!target.area.Contains(world)) continue;
            _manager.RequestDistrict(target.block);
            break;
        }
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
        _slots = Child("Slots", _visuals);
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
                    var tint = color; tint.a = 0.16f;
                    Paint("Tint_" + block.BlockId, _visuals, WorldRect(area), tint, PlazaDepth.FlatPropOrder);
                }
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

    private void AddSign(DistrictBlockDefinition block, Vector2 center)
    {
        var area = new Rect(center - new Vector2(1.65f, 0.65f), new Vector2(3.3f, 1.3f));
        Paint("Sign_" + block.BlockId, _signs, area, new Color(1f, 0.96f, 0.84f, 0.96f), 31990);
        var text = Child("Label", _signs).gameObject.AddComponent<TextMeshPro>();
        var style = Resources.Load<TutorialStyle>("TutorialStyle");
        if (style != null && style.BodyFont != null) text.font = style.BodyFont;
        text.text = $"{block.DisplayName}\n이 땅을 뭘로 쓸까요?";
        text.fontSize = 2.6f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 1.8f;
        text.fontSizeMax = 2.6f;
        text.color = new Color(0.30f, 0.20f, 0.13f);
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = area.size - Vector2.one * 0.12f;
        text.transform.position = center;
        text.sortingOrder = 31991;
        _targets.Add((block, area));
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
