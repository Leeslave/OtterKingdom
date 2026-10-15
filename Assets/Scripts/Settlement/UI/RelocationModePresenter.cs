using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 이사 지시서 화면 (구역·이사 4단계): 마을회관의 [이사하기]로 열린다. 지시서의 건물을 하나씩, 맞는 구역의 빈 부지 칸을 반짝여 보여 주고
/// 플레이어가 칸을 누르면 확인 → 건설 해달이 들고 옮김(SettlementManager.TryRelocateToSlot). 광장에서만, 코드로 만든 화면 (전역 UI 위 · 게임 팝업 아래).
/// 데이터 초기화가 씬을 넘어 사는 오브젝트를 없애므로 씬이 열릴 때마다 없으면 다시 만든다
/// </summary>
public class RelocationModePresenter : MonoBehaviour
{
    private const int SortingOrder = 110;
    private const float PulseSpeed = 3f;
    private const float RefreshSeconds = 0.5f;
    private float _refreshTimer;
    private static readonly Color TargetPad = new Color(1f, 0.78f, 0.62f, 1f);
    private const float BobHeight = 0.18f;

    private static RelocationModePresenter _instance;

    /// <summary>이사 지시서 화면이 열려 있는지 (구역 색을 함께 보임)</summary>
    public static bool IsOpen => _instance != null && _instance._root.activeSelf;

    private GameObject _root;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _body;
    private TextMeshProUGUI _hint;
    private SettlementManager _manager;
    private DecorBoardView _board;
    private PlazaCameraController _camera;
    private Transform _world;
    private readonly List<(RelocationSlot slot, Rect rect, SpriteRenderer fill)> _slots = new List<(RelocationSlot, Rect, SpriteRenderer)>();
    private readonly List<SpriteRenderer> _pulse = new List<SpriteRenderer>();
    private int _target;
    private Rect _targetRect;
    private Transform _ribbon;
    private Vector3 _ribbonBase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureExists();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
        _instance.Close();
    }

    private static void EnsureExists()
    {
        if (_instance != null)
            return;
        var canvas = RuntimeUIKit.CreateCanvas(nameof(RelocationModePresenter), SortingOrder, true);
        _instance = canvas.gameObject.AddComponent<RelocationModePresenter>();
        _instance.Build(canvas.transform);
    }

    /// <summary>지시서 화면을 연다 (광장 · 지시서가 있을 때만)</summary>
    /// <returns>열었으면 true</returns>
    public static bool Open()
    {
        EnsureExists();
        return _instance.OpenInternal();
    }

    private void Build(Transform canvas)
    {
        _root = new GameObject("RelocationMode", typeof(RectTransform));
        _root.transform.SetParent(canvas, false);
        RuntimeUIKit.Stretch((RectTransform)_root.transform);

        // 글과 버튼을 모두 아래 패널에: 구역 땅이 지도 위쪽 끝에 있으면 카메라가 더 못 올라가 위 패널에 가려짐
        var bottom = RuntimeUIKit.CreatePanel(_root.transform, 1000f, "Bottom");
        bottom.anchorMin = bottom.anchorMax = bottom.pivot = new Vector2(0.5f, 0f);
        bottom.anchoredPosition = new Vector2(0f, 40f);
        _title = RuntimeUIKit.CreateLabel(bottom, "이사 지시서", 40, true);
        _body = RuntimeUIKit.CreateLabel(bottom, string.Empty, 32);
        _hint = RuntimeUIKit.CreateLabel(bottom, string.Empty, 27);
        var row = RuntimeUIKit.CreateRow(bottom, RuntimeUIKit.ButtonHeight);
        RuntimeUIKit.CreateButton(row, "건물 보기", () => PanTo(_targetRect.center), RuntimeUIKit.ButtonKind.Option, flexible: true);
        RuntimeUIKit.CreateButton(row, "빈 자리 보기", PanToSlots, RuntimeUIKit.ButtonKind.Option, flexible: true);
        RuntimeUIKit.CreateButton(row, "닫기", Close, RuntimeUIKit.ButtonKind.Primary, flexible: true);

        // 월드 글자 · 안내 글자를 미리 아틀라스에 (처음 보일 때 글자를 넣으면 에디터가 멈출 수 있음)
        RuntimeUIKit.PrepareGlyphs("옮길 건물여기로!이사 지시서건물채를 주거상업생산농업지역으로보상 골드경험치반짝이는 빈 자리를 눌러의 새 자리를 골라요모두 옮기는 중이에요이사가 끝나면 보상을 받아요0123456789/#·");
        _root.SetActive(false);
    }

    private bool OpenInternal()
    {
        _manager = SettlementManager.Instance;
        _board = DecorBoardView.Active;
        if (_manager == null || !_manager.IsLoaded || _manager.RelocationOrder == null || _board == null || _board.Board != _manager.PlazaBoard)
            return false;
        _camera = FindAnyObjectByType<PlazaCameraController>();
        _root.SetActive(true);
        if (_camera != null)
            _camera.SetOverview(true);
        _target = 0;
        Refresh(true);
        return true;
    }

    private void Close()
    {
        if (!_root.activeSelf)
            return;
        _root.SetActive(false);
        ClearWorld();
        if (_camera != null)
            _camera.SetOverview(false);
        _target = 0;
    }

    private void Update()
    {
        if (!_root.activeSelf)
            return;
        if (_manager == null || _board == null || _manager.RelocationOrder == null)
        {
            Close();
            return;
        }
        // 이사가 끝나거나 다음 지시서가 나오면 다시 그림
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = RefreshSeconds;
            if (_manager.NextRelocationTarget() != _target)
                Refresh(true);
            else
                RefreshCount();
        }
        if (_ribbon != null)
            _ribbon.position = _ribbonBase + Vector3.up * (BobHeight * Mathf.Abs(Mathf.Sin(Time.unscaledTime * PulseSpeed)));
        float alpha = 0.7f + 0.25f * Mathf.Sin(Time.unscaledTime * PulseSpeed);
        foreach (var renderer in _pulse)
        {
            if (renderer == null) continue;
            var c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }
        if (GameManager.Instance != null && GameManager.Instance.IsModalOpen)
            return;
        if (!PlazaTapInput.TryGetTap(out var world))
            return;
        foreach (var (slot, rect, _) in _slots)
        {
            if (rect.Contains(world))
            {
                Choose(slot);
                return;
            }
        }
    }

    // 지금 옮길 건물(지시서 순서) · 그 건물이 들어갈 빈 부지 칸을 다시 그림
    private void Refresh(bool pan)
    {
        var order = _manager.RelocationOrder;
        if (order == null)
        {
            Close();
            return;
        }
        string kind = Districts.DisplayName(order.Type);
        RefreshCount();

        ClearWorld();
        int target = _manager.NextRelocationTarget();
        var layout = DecorManager.Instance.GetLayout(_board.Board);
        if (target == 0 || !layout.TryGet(target, out var placed))
        {
            _target = 0;
            _hint.text = "모두 옮기는 중이에요! 이사가 끝나면 보상을 받아요.";
            return;
        }
        bool changed = target != _target;
        _target = target;
        _targetRect = _board.AreaWorldRect(placed.Area);
        DrawTarget(_targetRect);
        var slots = _manager.RelocationSlotsFor(target);
        var color = Color.Lerp(Color.white, DistrictGuideRules.ColorOf(order.Type), 0.45f);
        foreach (var slot in slots)
        {
            var rect = _board.AreaWorldRect(new RectInt(slot.Slot.Area.position + _board.Board.SaveOrigin, slot.Slot.Area.size));
            // 빈 자리 = 바닥에 깐 스티커 판 (용도 색) + "여기로!"
            var pad = WorldSticker.Panel(World, "Slot", PlateSprite, rect.center, rect.size - new Vector2(0.3f, 0.3f), color, _board.OverlaySortingOrder + 3, 0.45f);
            _pulse.Add(pad);
            // 나무 · 해달에 가려도 읽히게 작은 종이 태그 위에
            WorldSticker.Panel(World, "SlotTag", PlateSprite, rect.center, new Vector2(2.6f, 1.1f), Color.white, 31990, 0.3f);
            WorldSticker.Label(World, "여기로!", rect.center + new Vector2(0f, 0.06f), new Vector2(2.2f, 0.8f), 4.6f, 31991);
            _slots.Add((slot, rect, pad));
        }
        _hint.text = slots.Count > 0
            ? $"반짝이는 빈 자리를 눌러 {placed.Decor.DisplayName}의 새 자리를 골라요"
            : $"{kind}에 {placed.Decor.DisplayName}{KoreanParticle.SubjectParticle(placed.Decor.DisplayName)} 들어갈 빈 자리가 없어요. 영토를 넓히거나 물건을 치워 주세요";
        if (pan && changed)
            PanToSlots();
    }

    private void RefreshCount()
    {
        var order = _manager.RelocationOrder;
        if (order == null)
            return;
        int done = _manager.RelocationDoneCount();
        _title.text = $"이사 지시서 #{order.Serial}";
        _body.text = $"건물 {order.InstanceIds.Count}채를 {Districts.DisplayName(order.Type)}으로 <b>{done}/{order.InstanceIds.Count}</b>\n<size=26>보상 골드 {RelocationRules.RewardGold(_manager.Config, order)} · 경험치 {_manager.Config.RelocationExp}";
    }

    private void Choose(RelocationSlot slot)
    {
        int target = _target;
        var game = GameManager.Instance;
        var layout = DecorManager.Instance.GetLayout(_board.Board);
        if (target == 0 || !layout.TryGet(target, out var placed))
            return;
        int seconds = Mathf.RoundToInt((float)DevTimers.Duration(_manager.Config.RelocationSeconds));
        string message = $"{slot.Block.DisplayName}의 이 자리로 옮길까요?\n건설 해달이 {SettlementManager.FormatShort(System.TimeSpan.FromSeconds(seconds))} 동안 들고 옮겨요.";
        void Move()
        {
            var result = _manager.TryRelocateToSlot(target, slot.Slot, out _);
            if (result != RelocationStartResult.Started && result != RelocationStartResult.Instant)
                _hint.text = "그 자리에 옮길 수 없어요. 다른 자리를 골라 주세요";
            Refresh(true);
        }
        if (game == null)
            Move();
        else
            game.ShowConfirm($"{placed.Decor.DisplayName} 옮기기", message, Move);
    }

    private void PanToSlots()
    {
        if (_slots.Count == 0)
        {
            PanTo(_targetRect.center);
            return;
        }
        PanTo(_slots[0].rect.center);
    }

    private void PanTo(Vector2 world)
    {
        if (_camera != null)
            _camera.PanTo(world);
    }

    // 옮길 건물: 발밑에 복숭아색 판 + 지붕 위에서 통통 튀는 "옮길 건물" 리본
    private void DrawTarget(Rect footprint)
    {
        var pad = WorldSticker.Panel(World, "TargetPad", PlateSprite, footprint.center, footprint.size + new Vector2(0.4f, 0.4f), TargetPad, _board.OverlaySortingOrder + 3, 0.45f);
        _pulse.Add(pad);
        var style = WorldSticker.Style;
        var top = WorldSticker.AboveBuilding(footprint) + Vector2.up * 0.2f;
        _ribbon = new GameObject("TargetRibbon").transform;
        _ribbon.SetParent(World, false);
        WorldSticker.Panel(_ribbon, "Ribbon", style != null ? style.Ribbon : null, Vector2.zero, new Vector2(3.4f, 1.15f), Color.white, 31993, 0.4f);
        WorldSticker.Label(_ribbon, "옮길 건물", new Vector2(0f, 0.04f), new Vector2(2.8f, 0.8f), 5f, 31994);
        _ribbonBase = top;
        _ribbon.position = top;
    }

    private Transform World
    {
        get
        {
            if (_world == null)
                _world = new GameObject("RelocationWorld").transform;
            return _world;
        }
    }

    private static Sprite PlateSprite => WorldSticker.Style != null ? WorldSticker.Style.Plate : null;

    private void ClearWorld()
    {
        _slots.Clear();
        _pulse.Clear();
        _ribbon = null;
        if (_world != null)
            Destroy(_world.gameObject);
        _world = null;
    }
}
