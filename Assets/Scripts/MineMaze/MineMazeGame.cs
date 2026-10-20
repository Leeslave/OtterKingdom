using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 광산 미로 테스트 씬의 중심 (Assets/Scenes/MineMaze.unity, 지하 1층). 본 게임에서는 인벤토리와 함께 저장한다.
/// 일인칭 갱도 그림을 지금 칸 · 보는 쪽에 맞춰 바꾸고(양옆 갈림길 · 앞이 막힘), 앞에 돌 · 상자 · 계단이 있으면 그 앞에 세운다.
/// 돌은 광산 개편의 바위 그림 · 손맛을 그대로 쓴다 (탭 → 규칙 → 바위 · 효과 · 소리).
/// 저장: 5초마다 · 꺼질 때. 독립 테스트만 mine_maze_test.json 사용. Docs/광산_미로탐험과_왕도성장_기획.md 4.4~4.7
/// </summary>
public class MineMazeGame : MonoBehaviour
{
    private const string SaveFile = "mine_maze_test.json";
    private const float SaveInterval = 5f;
    private const float HintAfter = 4f;
    private const float TopReserve = 3.05f;   // 위 UI (층 이름 · 광석 띠) 아래부터 갱도
    private const float ViewOverscan = 1.14f; // 세로 화면: 갱도 그림 양옆을 조금 잘라 크게
    private const float MaxViewHeight = 9.6f; // 가로 화면: 갱도 그림 높이 상한 (화면 16 중)
    private const int ObjectOrder = 500;
    // 바위 그림(밝은 광장 바위)을 어두운 갱도에 맞춰 조금 어둡고 푸르게
    private static readonly Color CaveTint = new Color(0.8f, 0.87f, 0.93f);

    [Header("데이터")]
    [SerializeField] private MineMazeConfig _config;
    [SerializeField] private MineMazeArt _mazeArt;
    [Tooltip("바위 · 효과 · UI 부품 (광산 개편)")]
    [SerializeField] private MineReworkArt _art;

    [Header("장면")]
    [SerializeField] private Camera _camera;
    [Tooltip("갱도 그림")]
    [SerializeField] private SpriteRenderer _view;
    [Tooltip("앞이 막힌 벽 (갱도 그림 위)")]
    [SerializeField] private SpriteRenderer _wall;
    [Tooltip("앞 칸의 돌 (발밑)")]
    [SerializeField] private Transform _rockSpot;
    [Tooltip("앞 칸의 보물 상자")]
    [SerializeField] private SpriteRenderer _chest;
    [Tooltip("앞 칸의 계단")]
    [SerializeField] private SpriteRenderer _stairs;
    [Tooltip("한 칸 갈 때 어두워지는 막")]
    [SerializeField] private SpriteRenderer _fade;

    [Header("효과")]
    [SerializeField] private MineFx _fx;
    [SerializeField] private MineSfx _sfx;

    private MineMazeModel _model;
    private MineMazeHud _hud;
    private VeinRockView _rock;
    private Material _flashMaterial;
    private float _aspect;
    private float _viewWidth;
    private float _viewHeight;
    private Vector2 _viewCenter;
    private float _viewScale = 1f;
    private float _saveTimer;
    private float _idle;
    private float _shinyTimer;
    private bool _busy;
    private bool _resetting;
    private float _breakingUntil;
    private DateTime? _pausedUtc;

    public MineMazeModel Model => _model;
    /// <summary>한 칸 가는 중 (버튼 · 탭을 안 받음)</summary>
    public bool Busy => _busy;
    /// <summary>갱도 그림 아래 끝 (화면 비율, 0 = 아래). HUD가 그 아래에 버튼을 놓음</summary>
    public float ViewBottomScreen => _camera.WorldToViewportPoint(new Vector3(0f, _viewCenter.y - _viewHeight * 0.5f, 0f)).y;

    private Color RockColor => _model.Layer.RockTint * CaveTint;

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFile);

    #region 시작 · 저장

    private void Awake()
    {
        var shader = Shader.Find("GUI/Text Shader");
        if (shader != null)
            _flashMaterial = new Material(shader);

        var loaded = Load();
        _model = new MineMazeModel(_config, loaded, new System.Random(Environment.TickCount), DateTime.UtcNow);
        if (InGame)
        {
            _model.UseKingdomLevel(ProfileManager.Instance.Level);
            _model.UseFishingAccess(SettlementManager.Instance != null && SettlementManager.Instance.HasDevelopment("fishing_dock"));
            // 캔 광석 · 돌은 가방으로 바로 (못 들어간 것만 작업대에)
            if (InventoryManager.Instance != null)
                _model.UseBag(new InventoryOreBag(InventoryManager.Instance));
            HideGameBars(true);
        }
        if (loaded.savedUtcTicks > 0 && loaded.savedUtcTicks <= DateTime.UtcNow.Ticks)
            _model.AdvanceMiner((DateTime.UtcNow - new DateTime(loaded.savedUtcTicks, DateTimeKind.Utc)).TotalSeconds, true);
        PrepareGlyphs();
        _fx.Setup(_art, _camera, RuntimeUIKit.Style.TitleFont);

        _rock = _rockSpot.GetComponent<VeinRockView>();
        if (_rock == null)
            _rock = _rockSpot.gameObject.AddComponent<VeinRockView>();
        _rock.Setup(_art, _flashMaterial, WhiteSprite(), ObjectOrder);
        if (_fade.sprite == null)
            _fade.sprite = WhiteSprite();

        _hud = new GameObject("Hud").AddComponent<MineMazeHud>();
        _hud.transform.SetParent(transform, false);
        _hud.Setup(this, _model, _art, _mazeArt, _sfx);

        _model.Changed += _hud.Refresh;
        SettlementManager.SaveRequested += SaveCommunity;
        _model.Discovered += ore => StartCoroutine(DiscoverLater(ore));
        _model.RockRespawned += HandleRockRespawned;

        FitCamera();
        ShowView();
        _hud.ShowEntrance();
        Save();
    }

    private MineMazeSave Load()
    {
        var inGameState = InGame && InventoryManager.Instance != null ? InventoryManager.Instance.MineMazeState : null;
        if (inGameState != null && !inGameState.IsEmpty)
            return inGameState;
        // First in-game visit migrates the existing standalone mine save.
        try
        {
            if (File.Exists(SavePath))
                return JsonUtility.FromJson<MineMazeSave>(File.ReadAllText(SavePath)) ?? new MineMazeSave();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MineMaze] 저장을 못 읽어 처음부터: {e.Message}");
        }
        return new MineMazeSave();
    }

    private bool Save()
    {
        if (_model == null || _resetting)
            return false;
        _saveTimer = 0f;
        var saved = _model.ToSave();
        saved.savedUtcTicks = (_pausedUtc ?? DateTime.UtcNow).Ticks;
        if (InGame && InventoryManager.Instance != null)
        {
            // Commit the bag and remaining ore in a single atomic save.
            var manager = InventoryManager.Instance;
            manager.MineMazeState = saved;
            var service = new SaveService();
            service.Load(out var main);
            if (main == null || service.WritesBlocked) return false;
            main.mineMaze = saved;
            if (SettlementManager.Instance != null && SettlementManager.Instance.IsLoaded)
                SettlementManager.Instance.WriteToSave(main.settlement ??= new SettlementSaveData());
            manager.WriteToSave(main.inventory);
            main.inventoryCapacity = manager.Inventory.Capacity;
            // Item acquisition can also advance collections and quests.
            if (CollectionManager.Instance != null) CollectionManager.Instance.WriteToSave(main.collection);
            if (QuestManager.Instance != null) QuestManager.Instance.WriteToSave(main.quests);
            // Keep the kingdom offline-production clock anchored to its last simulation.
            if (!service.Save(main, stampTime: false)) return false;
        }
        else File.WriteAllText(SavePath, JsonUtility.ToJson(saved));
        _saveTimer = 0f;
        return true;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            if (_pausedUtc == null) _pausedUtc = DateTime.UtcNow;
            Save();
        }
        else if (_pausedUtc.HasValue)
        {
            _model.AdvanceMiner((DateTime.UtcNow - _pausedUtc.Value).TotalSeconds, true);
            _pausedUtc = null;
            Save();
        }
    }

    private void OnApplicationQuit() => Save();

    private void SaveCommunity() => Save();

    private void OnDisable()
    {
        SettlementManager.SaveRequested -= SaveCommunity;
        Save();
    }

    private void OnDestroy() => HideGameBars(false);


    // 처음 보는 한글을 폰트에 미리 (도중에 추가되면 에디터가 멈춤)
    private void PrepareGlyphs()
    {
        var sb = new System.Text.StringBuilder(MazeGlyphs.Ui).Append(MazeGlyphs.Trips).Append(MineGlyphs.Ui).Append(_config.FloorName);
        foreach (var ore in _config.Rework.Ores)
        {
            if (ore != null)
                sb.Append(ore.DisplayName);
        }
        foreach (var pickaxe in _config.Rework.Pickaxes)
            sb.Append(pickaxe.DisplayName);
        sb.Append(_config.Layer.DisplayName).Append(_config.Layer.Subtitle);
        RuntimeUIKit.PrepareGlyphs(sb.ToString());
    }

    private static Sprite WhiteSprite()
    {
        var texture = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    #endregion

    #region 본 게임 연결 (광산 카트로 들어왔을 때)

    private readonly List<GameObject> _hiddenBars = new List<GameObject>();

    /// <summary>본 게임에서 광산 카트로 들어옴 (전역 UI · 프로필이 있음). 씬만 따로 열면 테스트 씬</summary>
    public bool InGame => GlobalUIRoot.Instance != null && ProfileManager.Instance != null && MineMazeLinkConfig.Instance != null;

    // 미로 화면이 위 · 아래를 다 쓰므로 전역 상단바 · 네비게이션 바를 잠시 숨김 (화면 어두워짐 · 알림 · 팝업은 그대로)
    private void HideGameBars(bool hide)
    {
        if (!hide)
        {
            foreach (var bar in _hiddenBars)
            {
                if (bar != null)
                    bar.SetActive(true);
            }
            _hiddenBars.Clear();
            return;
        }
        var root = GlobalUIRoot.Instance;
        if (root == null)
            return;
        // 상단바 · 네비게이션 바 · 정착 칩 · 안내 띠 · 뽑기 버튼은 모두 전역 UI의 HudSafeArea 아래 (GlobalUISetup)
        foreach (var nav in root.GetComponentsInChildren<NavBarView>(true))
        {
            var hud = nav.transform;
            while (hud != null && hud.name != HudGroupName)
                hud = hud.parent;
            HideBar(hud != null ? hud.gameObject : nav.gameObject);
        }
        foreach (var top in root.GetComponentsInChildren<TopBarView>(true))
            HideBar(top.gameObject);
    }

    private const string HudGroupName = "HudSafeArea";

    private void HideBar(GameObject bar)
    {
        if (!bar.activeSelf || _hiddenBars.Contains(bar))
            return;
        bar.SetActive(false);
        _hiddenBars.Add(bar);
    }

    /// <summary>광산으로 돌아감 (본 게임에서만)</summary>
    public void ReturnToMine()
    {
        var link = MineMazeLinkConfig.Instance;
        var navigator = FindAnyObjectByType<SceneNavigator>();
        if (link == null || link.MineZone == null || navigator == null)
            return;
        Save();
        navigator.TryGo(link.MineZone);
    }

    #endregion

    #region 화면 맞춤

    // 위는 UI 자리, 그 아래 갱도 그림을 화면 폭에 맞춤 (가로 화면은 높이 상한). 돌 · 상자 · 계단은 그림 아래쪽 바닥에
    private void FitCamera()
    {
        _aspect = _camera.aspect;
        float halfH = _camera.orthographicSize;
        float width = halfH * 2f * _aspect;
        var size = _view.sprite.bounds.size;
        _viewWidth = Mathf.Min(width * ViewOverscan, MaxViewHeight * size.x / size.y);
        _viewHeight = _viewWidth * size.y / size.x;
        _viewScale = _viewWidth / size.x;
        _viewCenter = new Vector2(0f, halfH - TopReserve - _viewHeight * 0.5f);
        _camera.transform.position = new Vector3(0f, 0f, -10f);
        _fx.SetCameraBase(_camera.transform.position);

        _view.transform.position = _viewCenter;
        _view.transform.localScale = Vector3.one * _viewScale;
        _wall.transform.position = _viewCenter;
        _wall.transform.localScale = Vector3.one * _viewScale;
        float fadeSize = _fade.sprite.bounds.size.x;
        _fade.transform.position = new Vector3(0f, 0f, 0f);
        _fade.transform.localScale = new Vector3(width / fadeSize * 1.05f, halfH * 2f / fadeSize * 1.05f, 1f);

        float bottom = _viewCenter.y - _viewHeight * 0.5f;
        // 돌: 보이는 폭이 갱도 그림 폭의 약 40% (바위 그림 폭 2유닛 = 크기 1)
        _rockSpot.position = new Vector3(0f, bottom + _viewHeight * 0.1f, 0f);
        _rockSpot.localScale = Vector3.one * (_viewWidth * 0.2f);
        _chest.transform.position = new Vector3(0f, bottom + _viewHeight * 0.14f, 0f);
        _chest.transform.localScale = Vector3.one * (_viewWidth * 0.15f);
        _stairs.transform.position = new Vector3(0f, bottom + _viewHeight * 0.2f, 0f);
        _stairs.transform.localScale = Vector3.one * (_viewWidth * 0.17f);
        _hud?.LayoutBelowView();
    }

    #endregion

    #region 갱도 그림

    // 지금 칸 · 보는 쪽에 맞는 그림 + 앞 칸의 물건
    private void ShowView()
    {
        bool left = !_model.Map.IsWall(_model.CellToward(MazeTurn.Left));
        bool right = !_model.Map.IsWall(_model.CellToward(MazeTurn.Right));
        var ahead = _model.Look(MazeTurn.Forward);
        _view.sprite = _mazeArt.View(left, right);
        _wall.enabled = ahead == MazeAhead.Wall;

        var rock = _model.RockAhead;
        _rock.gameObject.SetActive(rock != null);
        if (rock != null)
            _rock.Show(rock.ToRock(), RockColor);
        _chest.enabled = ahead == MazeAhead.Chest || ahead == MazeAhead.OpenedChest;
        _chest.sprite = ahead == MazeAhead.Chest ? _mazeArt.ChestClosed : _mazeArt.ChestOpen;
        _stairs.enabled = ahead == MazeAhead.Stairs;
        _hud.RefreshPrompt();
        _hud.RefreshMap();
    }

    private void HideObjects()
    {
        _rock.gameObject.SetActive(false);
        _chest.enabled = false;
        _stairs.enabled = false;
    }

    #endregion

    #region 들어가기 · 가기 · 나가기 (MineMazeHud가 부름)

    public void Enter()
    {
        var now = DateTime.UtcNow;
        if (!_model.Enter(now))
        {
            _hud.ShowEntrance();
            _hud.ShowToast($"갱도가 아직 위험해요. {WaitText(_model.EnterWait(now))} 뒤에 다시 들어갈 수 있어요");
            return;
        }
        ShowView();
        _hud.Flash(new Color(0f, 0f, 0f, 0.9f));
        _sfx.Play(MineSfx.Kind.Open, 0.8f);
        _hud.ShowFloorTitle();
        Save();
    }

    public void SelectFloor(int floor)
    {
        if (_busy || !_model.SelectFloor(floor, DateTime.UtcNow)) return;
        _breakingUntil = 0;
        _hud.RebuildFloor();
        ShowView();
        Save();
    }

    public void Go(MazeTurn turn)
    {
        if (_busy || !_model.Inside)
            return;
        var before = _model.Facing;
        var move = _model.Go(turn);
        if (move == MazeMove.Wall)
        {
            _hud.ShowToast("그쪽은 막혀 있어요");
            return;
        }
        _idle = 0f;
        if (move == MazeMove.Blocked)
        {
            var what = _model.Look(MazeTurn.Forward);
            _hud.ShowToast(what == MazeAhead.Chest ? "보물 상자가 있어요!" : "돌이 길을 막고 있어요!");
        }
        StartCoroutine(Step(turn, move == MazeMove.Moved, before != _model.Facing));
    }

    // 한 칸: 앞 = 다가가며 어두워짐, 옆 = 옆으로 밀려남, 뒤 = 어두워졌다 밝아짐
    private IEnumerator Step(MazeTurn turn, bool moved, bool turned)
    {
        _busy = true;
        HideObjects();
        _sfx.Play(MineSfx.Kind.Tap, moved ? 0.7f : 0.9f, 0.6f);
        const float half = 0.13f;
        float side = turn == MazeTurn.Left ? 1f : turn == MazeTurn.Right ? -1f : 0f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float k = t / half;
            Animate(k, side, turn == MazeTurn.Forward && moved ? 1.12f : 1f, k * 0.85f);
            yield return null;
        }
        ShowView();
        HideObjects();
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float k = 1f - t / half;
            Animate(k, -side, turn == MazeTurn.Forward && moved ? 0.96f : 1f, k * 0.85f);
            yield return null;
        }
        Animate(0f, 0f, 1f, 0f);
        ShowView();
        if (_model.RockAhead != null)
            _rock.PlaySpawn(_model.RockAhead.ToRock(), RockColor);
        _busy = false;
        if (moved && _model.OnStairs)
            _hud.ShowToast("아래층으로 내려가는 계단이에요");
        Save();
    }

    private void Animate(float k, float side, float zoom, float dark)
    {
        float scale = Mathf.Lerp(1f, zoom, k);
        _view.transform.localScale = _wall.transform.localScale = Vector3.one * (_viewScale * scale);
        var position = _viewCenter + new Vector2(side * _viewWidth * 0.3f * k, 0f);
        _view.transform.position = _wall.transform.position = position;
        _fade.color = new Color(0f, 0f, 0f, dark);
    }

    public void Exit()
    {
        if (!_model.Inside)
            return;
        StopAllCoroutines();
        _busy = false;
        Animate(0f, 0f, 1f, 0f);
        var result = _model.Exit(DateTime.UtcNow);
        ShowView();
        _hud.Flash(new Color(0f, 0f, 0f, 0.9f));
        _hud.ShowResult(result);
        Save();
    }

    /// <summary>계단: 아래층은 아직 (테스트 씬은 지하 1층만)</summary>
    public void Descend()
    {
        if (_busy || !_model.Inside || !_model.OnStairs) return;
        _hud.ShowGate();
    }

    public bool CraftPickaxe()
    {
        if (!_model.TryCraft(out string reason)) { _hud.ShowToast(reason); return false; }
        _sfx.Play(MineSfx.Kind.Discover);
        _hud.ShowToast($"{_model.Pickaxe.DisplayName} 완성!");
        Save();
        return true;
    }

    /// <summary>"2분 05초" (다시 들어가기까지)</summary>
    public static string WaitText(TimeSpan wait)
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt((float)wait.TotalSeconds));
        return seconds >= 60 ? $"{seconds / 60}분 {seconds % 60:00}초" : $"{seconds}초";
    }

    // 본 게임 가방: 광석 ID와 같은 아이템 (돌 = ore_stone)
    private sealed class InventoryOreBag : IMineOreBag
    {
        private readonly InventoryManager _manager;

        public InventoryOreBag(InventoryManager manager) => _manager = manager;

        private bool TryItem(MineOreDefinition ore, out ItemDefinition item)
        {
            item = null;
            return ore != null && _manager.Config.ItemDatabase.TryGet(ore.OreId, out item);
        }

        public int Count(MineOreDefinition ore) => TryItem(ore, out var item) ? _manager.Inventory.GetCount(item) : 0;

        public int Add(MineOreDefinition ore, int amount) =>
            amount > 0 && TryItem(ore, out var item) ? _manager.Inventory.Add(item, amount, ItemChangeReason.Mining) : 0;

        public int Remove(MineOreDefinition ore, int amount)
        {
            if (amount <= 0 || !TryItem(ore, out var item))
                return 0;
            int n = Math.Min(amount, _manager.Inventory.GetCount(item));
            return n > 0 && _manager.Inventory.TryRemove(item, n, ItemChangeReason.Construction) ? n : 0;
        }
    }

    public void TakeOre(MineOreDefinition ore, int amount)
    {
        var manager = InventoryManager.Instance;
        if (!InGame || manager == null || ore == null) return;
        if (!manager.Config.ItemDatabase.TryGet(ore.OreId, out var item))
        {
            _hud.ShowToast("등록되지 않은 광석이에요");
            return;
        }
        int moved = _model.TransferToInventory(ore, item, manager.Inventory, amount);
        if (moved == 0) { _hud.ShowToast("가방에 빈칸이나 쌓을 공간이 없어요"); return; }
        bool saved = Save();
        _hud.ShowToast(saved ? $"{ore.DisplayName} {moved}개를 가방에 담았어요" : "가방에 담았지만 저장에 실패했어요. 잠시 후 다시 저장합니다.");
    }

    public void SellOre(MineOreDefinition ore)
    {
        if (_model.Sell(ore, 1)) Save();
    }

    public void HitGate()
    {
        if (!_model.HitGate(out string reason)) { _hud.ShowToast(reason); return; }
        _sfx.Play(MineSfx.Kind.Tap);
        if (_model.FloorCleared)
        {
            _sfx.Play(MineSfx.Kind.Discover);
            _hud.Flash(new Color(.8f, .9f, 1, .5f));
        }
        Save();
    }

    #endregion

    #region 돌 · 상자

    private void HitRock()
    {
        var rock = _model.RockAhead;
        if (rock == null)
            return;
        var vein = rock.Vein;
        bool wasBlocking = rock.Blocking;
        var hit = _model.HitAhead(Time.unscaledTime, DateTime.UtcNow);
        if (hit == null)
            return;
        _idle = 0f;
        var tint = RockColor;
        var center = _rock.Center;
        var stone = _config.Rework.Stone;
        var chipColor = vein != null && vein != stone ? Color.Lerp(tint, vein.Color, 0.5f) : tint * new Color(0.75f, 0.82f, 0.86f);
        float comboPitch = 0.92f + Mathf.Min(hit.Combo, 25) * 0.018f + UnityEngine.Random.Range(-0.03f, 0.03f);

        if (!hit.Broken)
            _rock.PlayHit(rock.ToRock(), hit.Crit);
        _fx.Swing(center + Vector2.up * 0.5f, _model.Pickaxe.Icon, 1.8f);
        _fx.Chips(center + Vector2.up * 0.25f, chipColor, hit.Crit ? 9 : 5, hit.Crit ? 1.5f : 1.15f);
        if (hit.Crit)
        {
            _sfx.Play(MineSfx.Kind.Crit, comboPitch);
            _fx.Sparks(center, new Color(1f, 0.85f, 0.4f), 10, 1.5f, 0.7f);
            _fx.Text(center + Vector2.up * 1.6f, "쾅!", new Color(1f, 0.6f, 0.3f), 9f, 0.6f, 0.8f);
            _fx.Shake(0.12f);
        }
        else
        {
            _sfx.Play(MineSfx.Kind.Hit, comboPitch, 1f, 0.02f);
            _fx.Shake(0.03f);
        }
        _hud.ShowCombo(hit.Combo, hit.Crit);
        if (!hit.Broken)
            return;

        _breakingUntil = Time.time + 0.5f;
        _rock.PlayBreak();
        _sfx.Play(MineSfx.Kind.Break, 0.95f + UnityEngine.Random.Range(-0.05f, 0.08f));
        _fx.Chips(center, chipColor, 16, 1.8f);
        _fx.Glow(center, new Color(0.85f, 0.95f, 1f, 0.6f), 5f, 0.3f);
        _fx.Ring(center, new Color(1f, 1f, 1f, 0.8f), 4f, 0.35f);
        _fx.Shake(hit.Shiny ? 0.3f : 0.16f);
        if (hit.Shiny)
        {
            _fx.Sparks(center, new Color(1f, 0.85f, 0.35f), 20, 1.8f, 0.8f);
            _fx.Ring(center, new Color(1f, 0.85f, 0.35f), 5.5f, 0.6f);
            _hud.Flash(new Color(1f, 0.92f, 0.6f, 0.45f));
        }
        DropGains(hit.Gains, center, 1f);
        _hud.ShowToast(wasBlocking ? "길이 뚫렸어요!" : "돌을 부쉈어요!");
        StartCoroutine(PromptLater());
    }

    private IEnumerator PromptLater()
    {
        yield return new WaitForSeconds(0.55f);
        ShowView();
    }

    private void OpenChest()
    {
        var gains = _model.OpenChestAhead();
        if (gains == null)
            return;
        _idle = 0f;
        var center = (Vector2)_chest.transform.position + Vector2.up * (_chest.bounds.size.y * 0.5f);
        _chest.sprite = _mazeArt.ChestOpen;
        _sfx.Play(MineSfx.Kind.Discover);
        _fx.Glow(center, new Color(1f, 0.9f, 0.55f, 0.8f), 6f, 0.45f);
        _fx.Ring(center, new Color(1f, 0.85f, 0.35f), 5f, 0.55f);
        _fx.Sparks(center, new Color(1f, 0.88f, 0.45f), 24, 1.8f, 0.8f);
        _fx.Shake(0.15f);
        _hud.Flash(new Color(1f, 0.95f, 0.75f, 0.5f));
        DropGains(gains, center, 1f);
        _hud.ShowToast("보물 상자를 열었어요!");
        _hud.RefreshPrompt();
        _hud.RefreshMap();
    }

    // 광석이 튀어나와 통통 → 띠로 날아감 (광산 개편과 같음)
    private void DropGains(List<MineGain> gains, Vector2 from, float size)
    {
        int total = 0;
        float delay = 0f;
        foreach (var gain in gains)
        {
            total += gain.Amount;
            int icons = Mathf.Clamp(gain.Amount, 1, 5);
            int each = gain.Amount / icons;
            int extra = gain.Amount - each * icons;
            for (int i = 0; i < icons; i++)
            {
                var ore = gain.Ore;
                int amount = each + (i < extra ? 1 : 0);
                _fx.PopOre(from, ore.Icon, delay, landed => _hud.FlyOre(landed, ore, amount), ore.IsRare ? size * 1.35f : size);
                delay += 0.045f;
            }
            if (gain.Ore.IsRare || gain.Ore.Rarity == MineOreRarity.Relic)
            {
                _fx.Ring(from, gain.Ore.Color, 4.4f, 0.55f);
                _fx.Sparks(from, gain.Ore.Color, 14, 1.6f, 0.7f);
                _fx.Text(from + Vector2.up * 2.2f, gain.Ore.DisplayName + "!", new Color(1f, 0.88f, 0.4f), 7f, 1.2f, 1f);
                _sfx.Play(MineSfx.Kind.Rare);
            }
        }
        _sfx.Play(MineSfx.Kind.Pop, 1f, 0.7f);
        if (total > 0)
            _fx.Text(from + Vector2.up * 1.6f, $"+{total}", Color.white, 6f, 0.9f, 1.3f);
    }

    private void HandleRockRespawned(Vector2Int cell)
    {
        _hud.RefreshMap();
        if (!_model.Inside || _busy || cell != _model.CellToward(MazeTurn.Forward))
            return;
        StartCoroutine(RespawnLater());
    }

    private IEnumerator RespawnLater()
    {
        float wait = Mathf.Max(0f, _breakingUntil - Time.time);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);
        var rock = _model.RockAhead;
        if (rock == null || _busy)
            yield break;
        _rock.gameObject.SetActive(true);
        _rock.PlaySpawn(rock.ToRock(), RockColor);
        _fx.Chips(_rock.transform.position + Vector3.up * 0.2f, new Color(0.6f, 0.7f, 0.75f), 6, 0.8f);
        _sfx.Play(MineSfx.Kind.Pop, 0.65f, 0.35f, 0.05f);
        if (rock.Shiny)
        {
            _fx.Ring(_rock.Center, new Color(1f, 0.85f, 0.35f), 4f, 0.6f);
            _sfx.Play(MineSfx.Kind.Rare, 1.2f, 0.5f);
        }
        _hud.RefreshPrompt();
    }

    private IEnumerator DiscoverLater(MineOreDefinition ore)
    {
        yield return new WaitForSeconds(1.05f);
        _hud.ShowDiscovery(ore);
    }

    #endregion

    #region 테스트 (MineMazeHud 테스트 창)

    public void CheatMapOtter()
    {
        _model.CheatMapOtter();
        _hud.RefreshMap();
        _hud.ShowToast("지도 해달이 왔어요!");
    }

    public void CheatRespawn()
    {
        _model.CheatRespawnAll(DateTime.UtcNow);
        ShowView();
    }

    public void CheatPickaxe(int delta)
    {
        _model.CheatPickaxe(delta);
        _hud.ShowToast($"{_model.Pickaxe.DisplayName} (힘 {_model.Pickaxe.Power})");
    }

    public void CheatShiny()
    {
        if (_model.CheatShinyAhead())
            ShowView();
        else
            _hud.ShowToast("앞에 돌이 없어요");
    }

    public void ResetAll()
    {
        if (InGame && InventoryManager.Instance != null)
        {
            _model = new MineMazeModel(_config, new MineMazeSave(), new System.Random(), DateTime.UtcNow);
            Save();
            _resetting = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }
        _resetting = true;
        if (File.Exists(SavePath))
            File.Delete(SavePath);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    #endregion

    private void Update()
    {
        if (_pausedUtc.HasValue) return;
        float dt = Time.deltaTime;
        _model.AdvanceMiner(Time.unscaledDeltaTime);
        if (!Mathf.Approximately(_aspect, _camera.aspect))
            FitCamera();
        _model.Tick(DateTime.UtcNow);

        if (_model.Inside && !_busy && !_hud.Blocking)
        {
            foreach (var tap in MineTapInput.Poll(_camera))
                HandleTap(tap);
        }

        // "여기 쳐 봐" 살짝 흔들기
        _idle += dt;
        if (_idle > HintAfter && _model.Inside && _rock.gameObject.activeSelf && _rock.Present)
        {
            _idle = 0f;
            _rock.Wiggle();
        }

        // 반짝 광맥 둘레 반짝임
        _shinyTimer -= dt;
        if (_shinyTimer <= 0f)
        {
            _shinyTimer = 0.25f;
            if (_rock.gameObject.activeSelf && _rock.Present && _rock.Shiny)
                _fx.Sparks(_rock.Center + UnityEngine.Random.insideUnitCircle * 1.6f, new Color(1f, 0.9f, 0.5f), 1, 0.3f, 0.45f);
        }

        _saveTimer += dt;
        if (_saveTimer >= SaveInterval)
            Save();
    }

    private void HandleTap(Vector2 world)
    {
        if (_rock.gameObject.activeSelf && _rock.Present && (_rock.Contains(world) || Vector2.Distance(world, _rock.Center) < _viewWidth * 0.2f))
        {
            HitRock();
            return;
        }
        if (_chest.enabled && _model.Look(MazeTurn.Forward) == MazeAhead.Chest && _chest.bounds.Contains(new Vector3(world.x, world.y, _chest.transform.position.z)))
            OpenChest();
    }
}
