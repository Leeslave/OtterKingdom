using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 낚시 개편 테스트 씬 중심 (Docs/낚시_개편_기획.md): 누르기 → 규칙(FishingReworkModel) → 바다 · 물고기 · 줄 · 효과 · 소리 · 화면.
/// 바다는 코드로 깖 (하늘 · 먼 섬 · 물 · 물결 · 빛줄기 · 바닥 · 해초 · 부표). 1유닛 = 1m, 수면 = 0, 선착장 끝 = x 0.
/// 저장: persistentDataPath/fishing_rework_test.json (본 게임 세이브와 따로)
/// 본 게임(_hosted, 낚시터 씬 안): GameManager의 낚시 규칙(FishingService.Model) · 가방 · 골드를 쓰고, 바다는 _seaRoot 아래
/// 선착장과 떨어진 곳에 있음. 낚시터 = 바다: 선착장 정리 · 낚시꾼 해달 배치가 끝났으면 늘 바다 (Enter, 나가는 버튼 없음).
/// 선착장은 그 전과 꾸미기 모드에서만 보임. 저장은 GameManager.
/// 안 만지고 두면 낚시꾼 해달이 알아서 던짐 (방치)
/// </summary>
// 본 게임: GameManager(0)가 낚시 규칙을 만든 뒤에
[DefaultExecutionOrder(10)]
public class FishingReworkGame : MonoBehaviour
{
    private const string SaveFile = "fishing_rework_test.json";
    private const float ViewWidth = 8.6f;
    private const float HomeX = 1.8f, HomeY = -1.2f;
    private const float WorldLeft = -14f, WorldRight = 112f;

    [SerializeField] private FishingReworkConfig _config;
    [SerializeField] private FishingReworkArt _art;
    [SerializeField] private Camera _camera;
    [SerializeField] private QuarterRigView _otter;
    [SerializeField] private MineFx _fx;
    [SerializeField] private MineSfx _sfx;
    [Tooltip("본 게임 (낚시터 씬 안, GameManager의 낚시 규칙 · 가방 · 골드)")]
    [SerializeField] private bool _hosted;
    [Tooltip("바다 전체의 기준 (수면 · 선착장 끝 = 이 위치). 비우면 원점")]
    [SerializeField] private Transform _seaRoot;

    private FishingReworkModel _model;
    private FishingHud _hud;
    private readonly Dictionary<int, FishView> _views = new Dictionary<int, FishView>();
    private Transform _fishRoot;
    private LineRenderer _line;
    private SpriteRenderer _hook, _sinker, _sky, _wave;
    private readonly List<(Transform t, float parallax, float offset)> _parallax = new List<(Transform, float, float)>();
    private readonly List<(Transform t, float phase)> _sway = new List<(Transform, float)>();
    private readonly List<(Transform t, float x)> _buoys = new List<(Transform, float)>();
    // 빨간 핀 부표: 직접 꽂은 자리
    private SpriteRenderer _pin;
    // 핀 꽂기: 바다를 끌어서 화면 가운데에 둔 거리에 꽂음 (지도 앱처럼 핀은 가운데 고정, 바다가 움직임)
    private bool _pinning;
    private bool _pinDragging;
    private Vector2 _pinDragLast;
    private Vector2? _pinGlide;
    private readonly List<SpriteRenderer> _rays = new List<SpriteRenderer>();
    private Vector2 _cameraVelocity;
    private Vector2 _cameraPos = new Vector2(HomeX, HomeY);
    private float _aspect;
    private bool _holdValid;
    // 던질 곳 고르기 (도넛 게이지): 꾹 누르고 있으면 게이지가 0 → 끝 → 0으로 오가고, 떼면 그 거리로 던짐.
    // 톡 누르면(GaugeAfterSeconds 전에 뗌) 지난 거리
    private bool _aiming;
    private float _aimTime;
    private float _aimDistance;
    private const float AimSnapMeters = 2f;
    private const float GaugeAfterSeconds = 0.18f;

    // 게이지가 끝까지 한 번 차는 초 (줄이 길면 조금 더 천천히 — 긴 줄에서도 맞추기 쉽게)
    private float GaugeSeconds => 0.95f + _model.CastDistance * 0.012f;
    private float _bubbleTimer, _reelTimer, _saveTimer;
    private bool _resetting;
    private bool _active = true;
    private Vector3 _dockCameraPosition;
    private float _dockCameraSize;
    private float _idleTime;
    private bool _autoFishing;
    private float _autoTimer;
    private bool _autoHold;
    // 이만큼 안 만지면 낚시꾼 해달이 알아서 던짐 (본 게임)
    private const float AutoAfterSeconds = 6f;

    public FishingReworkModel Model => _model;
    public FishingReworkArt Art => _art;
    public bool Hosted => _hosted;
    /// <summary>본 게임: 바다 화면이 열려 있음</summary>
    public bool Active => _active;
    /// <summary>낚시꾼 해달이 알아서 낚는 중 (안 만진 지 오래)</summary>
    public bool AutoFishing => _autoFishing;
    /// <summary>핀 꽂는 중 (바다를 끌어 고름)</summary>
    public bool Pinning => _pinning;
    /// <summary>핀 꽂는 중: 화면 가운데의 거리 (줄 길이 안)</summary>
    public float PinCenter => Mathf.Clamp(_cameraPos.x, FishingReworkModel.MinCastDistance, _model.CastDistance);

    // 바다 좌표(m, 수면 0 · 선착장 끝 0) ↔ 월드
    private Vector3 W(Vector2 sea) => _seaRoot != null ? _seaRoot.TransformPoint(sea) : (Vector3)sea;
    private Vector2 L(Vector3 world) => _seaRoot != null ? (Vector2)_seaRoot.InverseTransformPoint(world) : (Vector2)world;
    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFile);

    #region 시작 · 저장

    private void Awake()
    {
        if (_hosted)
        {
            var game = GameManager.Instance;
            _model = game != null && game.FishingService != null ? game.FishingService.Model : null;
            if (_model == null)
            {
                Debug.LogWarning("[FishingReworkGame] GameManager에 바다 낚시가 없어 바다를 그리지 않습니다.");
                enabled = false;
                return;
            }
            _config = _model.Config;
        }
        else
            _model = new FishingReworkModel(_config, Load(), new System.Random(Environment.TickCount));
        if (_seaRoot == null)
            _seaRoot = new GameObject("Sea").transform;

        RuntimeUIKit.PrepareGlyphs(Glyphs());
        _fx.Setup(_art.Ui, _camera, RuntimeUIKit.Style.TitleFont);
        BuildWorld();
        BuildTackle();
        _fishRoot = new GameObject("Fish").transform;
        _fishRoot.SetParent(_seaRoot, false);
        foreach (var fish in _model.Fish)
            AddView(fish);

        _model.FishSpawned += AddView;
        _model.FishRemoved += RemoveView;
        _model.Casted += HandleCast;
        _model.Splashed += HandleSplash;
        _model.Hooked += HandleHooked;
        _model.TooHeavy += HandleTooHeavy;
        _model.Caught += HandleCaught;
        _model.Missed += HandleMissed;
        _model.RequestDone += HandleRequestDone;
        _model.Upgraded += HandleUpgraded;

        _hud = new GameObject("Hud").AddComponent<FishingHud>();
        _hud.transform.SetParent(transform, false);
        _hud.Setup(this, _model, _art, _sfx);

        if (_otter != null)
        {
            _otter.ExternalFishLine = true;
            _otter.PlayLoop(QuarterRigView.Loop.Fish);
        }
        if (_hosted)
        {
            // 낚시꾼 해달이 일할 수 있게 될 때까지 숨김 (Update의 AutoEnter). 효과(MineFx)는 카메라를 붙잡으므로 선착장에서는 꺼 둠
            _active = false;
            _seaRoot.gameObject.SetActive(false);
            _hud.gameObject.SetActive(false);
            _fx.gameObject.SetActive(false);
            _sky.gameObject.SetActive(false);
            return;
        }
        FitCamera();
        _camera.transform.position = W(new Vector2(HomeX, HomeY)) + Vector3.back * 10f;
        _fx.SetCameraBase(_camera.transform.position);
        Save();
    }

    private void OnDestroy()
    {
        if (_model == null)
            return;
        _model.FishSpawned -= AddView;
        _model.FishRemoved -= RemoveView;
        _model.Casted -= HandleCast;
        _model.Splashed -= HandleSplash;
        _model.Hooked -= HandleHooked;
        _model.TooHeavy -= HandleTooHeavy;
        _model.Caught -= HandleCaught;
        _model.Missed -= HandleMissed;
        _model.RequestDone -= HandleRequestDone;
        _model.Upgraded -= HandleUpgraded;
        if (_hosted && GameManager.Instance != null && GameManager.Instance.FishingService != null)
            GameManager.Instance.FishingService.HandFishing = false;
    }

    /// <summary>
    /// 본 게임: 낚시터 = 바다. 낚시꾼 해달이 일할 수 있으면 바로 들어감 — 도착하자마자는 구름 뒤에서,
    /// 그 뒤(그 자리에서 해달 배치 · 꾸미기 모드 끝)는 팝업 · 연출이 끝나고
    /// </summary>
    private void AutoEnter()
    {
        var game = GameManager.Instance;
        if (game == null || DecorModePresenter.IsActive || !game.CanOpenSeaFishing)
            return;
        if (Time.timeSinceLevelLoad > 1f && PresentationGate.IsBusy)
            return;
        Enter();
    }

    /// <summary>본 게임: 선착장 → 바다</summary>
    public void Enter()
    {
        if (!_hosted || _active || _model == null)
            return;
        _active = true;
        _dockCameraPosition = _camera.transform.position;
        _dockCameraSize = _camera.orthographicSize;
        _seaRoot.gameObject.SetActive(true);
        _hud.gameObject.SetActive(true);
        _fx.gameObject.SetActive(true);
        _sky.gameObject.SetActive(true);
        _hud.Refresh();
        _cameraPos = new Vector2(HomeX, HomeY);
        _cameraVelocity = Vector2.zero;
        FitCamera();
        _camera.transform.position = W(_cameraPos) + Vector3.back * 10f;
        _fx.SetCameraBase(_camera.transform.position);
        _idleTime = 0f;
        _autoFishing = false;
        if (_otter != null)
        {
            _otter.ExternalFishLine = true;
            _otter.PlayLoop(QuarterRigView.Loop.Fish);
        }
        var game = GameManager.Instance;
        if (game != null)
        {
            game.FishingService.HandFishing = true;
            game.SetSeaFishingButtonVisible(false);
        }
        _sfx.Play(MineSfx.Kind.Open);
    }

    /// <summary>본 게임: 바다 → 선착장 (꾸미기 모드 동안만. 걸린 것 · 던진 줄은 거두고 나감)</summary>
    public void Exit()
    {
        if (!_hosted || !_active)
            return;
        EndPin(false);
        _active = false;
        _model.Reset();
        _seaRoot.gameObject.SetActive(false);
        _hud.gameObject.SetActive(false);
        _camera.transform.position = _dockCameraPosition;
        _camera.orthographicSize = _dockCameraSize;
        _fx.SetCameraBase(_dockCameraPosition);
        _fx.gameObject.SetActive(false);
        _sky.gameObject.SetActive(false);
        var game = GameManager.Instance;
        if (game != null)
        {
            game.FishingService.HandFishing = false;
            game.SetSeaFishingButtonVisible(true);
            game.SaveSeaFishing();
        }
    }

    private static FishingReworkSave Load()
    {
        try
        {
            if (File.Exists(SavePath))
                return JsonUtility.FromJson<FishingReworkSave>(File.ReadAllText(SavePath)) ?? new FishingReworkSave();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FishingRework] 저장을 못 읽어 처음부터: {e.Message}");
        }
        return new FishingReworkSave();
    }

    public void Save()
    {
        if (_model == null || _resetting || _hosted)
            return;
        File.WriteAllText(SavePath, JsonUtility.ToJson(_model.Save));
        _saveTimer = 0f;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Save();
    }

    private void OnApplicationQuit() => Save();

    private void OnDisable() => Save();

    /// <summary>테스트: 처음부터</summary>
    public void ResetAll()
    {
        if (_hosted)
            return;
        _resetting = true;
        if (File.Exists(SavePath))
            File.Delete(SavePath);
        var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // 테스트 씬은 빌드 목록에 없어서 경로로 다시 엶
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.name);
#endif
    }

    private string Glyphs()
    {
        var sb = new StringBuilder(FishingGlyphs.Ui);
        foreach (var fish in _config.Fish)
        {
            if (fish != null)
                sb.Append(fish.DisplayName).Append(fish.Description);
        }
        foreach (FishingUpgrade upgrade in Enum.GetValues(typeof(FishingUpgrade)))
        {
            var track = _config.Track(upgrade);
            sb.Append(track.DisplayName).Append(track.EffectName).Append(track.Unit);
        }
        return sb.ToString();
    }

    #endregion

    #region 바다 깔기

    private SpriteRenderer NewSprite(string name, Sprite sprite, Vector3 position, int order, Transform parent = null)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        if (parent != null)
            renderer.transform.SetParent(parent, false);
        renderer.sprite = sprite;
        renderer.transform.localPosition = position;
        renderer.sortingOrder = order;
        return renderer;
    }

    private void BuildWorld()
    {
        var root = _seaRoot;

        _sky = NewSprite("Sky", _art.Sky, Vector3.zero, -300, _camera.transform);
        _sky.transform.localPosition = new Vector3(0f, 0f, 20f);

        for (int i = 0; i < 4; i++)
        {
            var cloud = NewSprite($"Cloud{i}", _art.Cloud, Vector3.zero, -290, root);
            cloud.transform.localScale = Vector3.one * (1.2f + 0.4f * (i % 2));
            _parallax.Add((cloud.transform, 0.88f, i * 7f - 6f));
        }
        for (int i = 0; i < 3; i++)
        {
            var island = NewSprite($"Island{i}", _art.Island, Vector3.zero, -280, root);
            island.color = new Color(1f, 1f, 1f, 0.9f);
            _parallax.Add((island.transform, 0.93f, i * 24f - 12f));
        }

        // 물 (위 0m → 아래 64m)
        var water = NewSprite("Water", _art.Water, new Vector3((WorldLeft + WorldRight) * 0.5f, 0f, 0f), -200, root);
        var size = _art.Water.bounds.size;
        water.transform.localScale = new Vector3((WorldRight - WorldLeft) / size.x, 64f / size.y, 1f);

        _wave = NewSprite("Wave", _art.Wave, new Vector3((WorldLeft + WorldRight) * 0.5f, 0.05f, 0f), -140, root);
        _wave.drawMode = SpriteDrawMode.Tiled;
        _wave.size = new Vector2(WorldRight - WorldLeft, _art.Wave.bounds.size.y);

        for (int i = 0; i < 6; i++)
        {
            var ray = NewSprite($"Ray{i}", _art.Ray, Vector3.zero, -190, root);
            ray.transform.rotation = Quaternion.Euler(0f, 0f, -14f);
            ray.transform.localScale = new Vector3(1.6f + 1.4f * (i % 3) * 0.5f, 1.3f, 1f);
            ray.color = new Color(1f, 1f, 1f, 0.55f);
            _rays.Add(ray);
            _parallax.Add((ray.transform, 0.6f, i * 3.3f - 7f));
        }

        BuildSeabed(root);
        BuildDecor(root);
        BuildBuoys(root);

        NewSprite("Pier", _art.Pier, new Vector3(0.3f, 0.95f, 0f), -40, root);
    }

    private void BuildSeabed(Transform root)
    {
        var go = new GameObject("Seabed");
        go.transform.SetParent(root, false);
        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        var sand = new Color(0.93f, 0.82f, 0.62f);
        var deepSand = new Color(0.5f, 0.42f, 0.42f);
        const float bottom = -80f;
        int columns = 0;
        for (float x = WorldLeft; x <= WorldRight + 0.01f; x += 0.5f)
        {
            float top = -_config.SeabedDepth(x);
            float k = Mathf.InverseLerp(-3f, -35f, top);
            vertices.Add(new Vector3(x, top, 0f));
            vertices.Add(new Vector3(x, top - 0.5f, 0f));
            vertices.Add(new Vector3(x, bottom, 0f));
            var c = Color.Lerp(sand, deepSand, k);
            colors.Add(Color.Lerp(c, Color.white, 0.25f));
            colors.Add(c);
            colors.Add(Color.Lerp(c, new Color(0.2f, 0.16f, 0.2f), 0.7f));
            columns++;
        }
        for (int i = 0; i < columns - 1; i++)
        {
            int a = i * 3, b = (i + 1) * 3;
            for (int row = 0; row < 2; row++)
            {
                triangles.Add(a + row); triangles.Add(b + row); triangles.Add(a + row + 1);
                triangles.Add(b + row); triangles.Add(b + row + 1); triangles.Add(a + row + 1);
            }
        }
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = -120;

        var edge = go.AddComponent<LineRenderer>();
        edge.useWorldSpace = false;
        edge.positionCount = columns;
        for (int i = 0; i < columns; i++)
            edge.SetPosition(i, vertices[i * 3] + Vector3.up * 0.03f);
        edge.widthMultiplier = 0.09f;
        edge.sharedMaterial = renderer.sharedMaterial;
        edge.startColor = edge.endColor = new Color(0.29f, 0.18f, 0.13f, 0.85f);
        edge.sortingOrder = -119;
    }

    private void BuildDecor(Transform root)
    {
        var rng = new System.Random(42);
        float x = 1.2f;
        while (x < WorldRight - 2f)
        {
            double roll = rng.NextDouble();
            Sprite sprite;
            float scale;
            bool sways = false;
            int order = -110;
            if (roll < 0.5 && _art.Seaweed.Length > 0)
            {
                sprite = _art.Seaweed[rng.Next(_art.Seaweed.Length)];
                scale = 0.55f + (float)rng.NextDouble() * 0.45f + x * 0.004f;
                sways = true;
                order = -105;
            }
            else if (roll < 0.78 && _art.Coral.Length > 0)
            {
                sprite = _art.Coral[rng.Next(_art.Coral.Length)];
                scale = 0.45f + (float)rng.NextDouble() * 0.45f;
            }
            else if (_art.Rocks.Length > 0)
            {
                sprite = _art.Rocks[rng.Next(_art.Rocks.Length)];
                scale = 0.45f + (float)rng.NextDouble() * 0.6f;
            }
            else
                break;
            var decor = NewSprite("Decor", sprite, new Vector3(x, -_config.SeabedDepth(x) + 0.08f, 0f), order, root);
            decor.transform.localScale = new Vector3(rng.NextDouble() < 0.5 ? -scale : scale, scale, 1f);
            decor.color = Color.Lerp(Color.white, new Color(0.55f, 0.65f, 0.8f), Mathf.InverseLerp(5f, 50f, _config.SeabedDepth(x)));
            if (sways)
                _sway.Add((decor.transform, (float)rng.NextDouble() * 6f));
            x += 1.6f + (float)rng.NextDouble() * 3.2f;
        }
    }

    private void BuildBuoys(Transform root)
    {
        for (int d = 10; d <= 90; d += 10)
        {
            var buoy = NewSprite($"Buoy{d}", _art.Buoy, new Vector3(d, 0f, 0f), -100, root);
            var label = new GameObject("Label").AddComponent<TMPro.TextMeshPro>();
            label.transform.SetParent(buoy.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            if (RuntimeUIKit.Style.TitleFont != null)
                label.font = RuntimeUIKit.Style.TitleFont;
            label.text = $"{d}m";
            label.fontSize = 3f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(75, 46, 34, 255);
            label.rectTransform.sizeDelta = new Vector2(3f, 1f);
            label.sortingOrder = -99;
            _buoys.Add((buoy.transform, d));
        }
        _pin = NewSprite("Pin", _art.Buoy, Vector3.zero, -98, root);
        _pin.color = new Color(1f, 0.35f, 0.3f);
        _pin.transform.localScale = Vector3.one * 1.25f;
        _pin.enabled = false;
    }

    private void BuildTackle()
    {
        var material = new Material(Shader.Find("Sprites/Default"));
        _line = new GameObject("Line").AddComponent<LineRenderer>();
        _line.transform.SetParent(_seaRoot, false);
        _line.useWorldSpace = false;
        _line.positionCount = 24;
        _line.widthMultiplier = 0.035f;
        _line.sharedMaterial = material;
        _line.startColor = _line.endColor = new Color(1f, 1f, 1f, 0.9f);
        _line.sortingOrder = 15;
        _line.numCapVertices = 2;
        _hook = NewSprite("Hook", _art.Hook, Vector3.zero, 17, _seaRoot);
        _sinker = NewSprite("Sinker", _art.Sinker, Vector3.zero, 16, _seaRoot);
    }

    #endregion

    #region 물고기 모습

    private void AddView(FishingFish fish)
    {
        if (_fishRoot == null || _views.ContainsKey(fish.Id))
            return;
        _views[fish.Id] = FishView.Create(_fishRoot, fish, _art.Ui, RuntimeUIKit.Style.TitleFont);
    }

    private void RemoveView(FishingFish fish)
    {
        if (_views.TryGetValue(fish.Id, out var view))
        {
            _views.Remove(fish.Id);
            if (view != null)
                Destroy(view.gameObject);
        }
    }

    #endregion

    #region 일어난 일

    private Vector2 RigTip => _otter != null ? L(_otter.RodTipWorld) : _model.Tip;

    private void HandleCast()
    {
        if (_otter != null)
            _otter.PlayLoop(QuarterRigView.Loop.Fish);
        _sfx.Play(MineSfx.Kind.Tap, 0.7f);
        _hud.ShowHint();
    }

    private void HandleSplash(Vector2 at)
    {
        _fx.Ring(W(at), new Color(1f, 1f, 1f, 0.9f), 1.4f, 0.5f);
        _fx.Chips(W(at + Vector2.up * 0.1f), new Color(0.75f, 0.92f, 0.98f), 10, 0.8f);
        _sfx.Play(MineSfx.Kind.Pop, 0.75f);
    }

    private void HandleHooked(FishingFish fish)
    {
        var color = FishingReworkArt.RarityColor(fish.Def.Rarity);
        _fx.Ring(W(fish.Position), color, 1.8f, 0.45f);
        if (fish.Def.Rarity >= FishRarity.Rare)
        {
            _fx.Sparks(W(fish.Position), color, 12, 1f, 0.45f);
            _sfx.Play(MineSfx.Kind.Rare);
        }
        else
            _sfx.Play(MineSfx.Kind.Hit, 1.3f);
        _fx.Text(W(fish.Position + Vector2.up * 0.8f), "걸렸다!", Color.white, 5f, 0.8f, 0.8f);
        _fx.Shake(0.08f);
        if (_otter != null)
            _otter.PlayFishStrike();
        _hud.ShowHint();
    }

    private void HandleTooHeavy(FishingFish fish)
    {
        if (_views.TryGetValue(fish.Id, out var view))
            view.ShowHeavy();
        _fx.Ring(W(fish.Position), new Color(1f, 0.5f, 0.45f, 0.8f), 1.2f, 0.35f);
        _sfx.Play(MineSfx.Kind.Hit, 0.6f, 0.6f);
        _hud.ShowHeavyTip(fish);
    }

    private void HandleCaught(FishingCatch result)
    {
        Vector2 at = RigTip + Vector2.down * 0.6f;
        _fx.Sparks(W(at), FishingReworkArt.RarityColor(result.Def.Rarity), 10, 0.9f, 0.4f);
        _fx.Text(W(at + Vector2.up * 0.6f), "가방에 쏙!", Color.white, 5f, 1f, 1.2f);
        _sfx.Play(result.IsNew ? MineSfx.Kind.Discover : MineSfx.Kind.Collect);
        if (_otter != null)
            _otter.PlayFishStrike();
        _hud.ShowCatch(result);
        Save();
    }

    private void HandleMissed() => _hud.ShowHint();

    private void HandleRequestDone(FishingRequest request)
    {
        _sfx.Play(MineSfx.Kind.Jackpot, 1f, 0.7f);
        _hud.PulseRequest();
    }

    private void HandleUpgraded(FishingUpgrade upgrade)
    {
        _sfx.Play(MineSfx.Kind.Craft);
        if (_otter != null)
            _fx.Sparks(_otter.transform.position + Vector3.up, new Color(1f, 0.9f, 0.5f), 14, 1f, 0.5f);
        Save();
    }

    #endregion

    #region 매 프레임

    private static Vector2 PointerPosition()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            return touchscreen.primaryTouch.position.ReadValue();
        var mouse = Mouse.current;
        return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
    }

    // 던질 곳: 빨간 핀 ±2m에서 떼면 핀 자리로
    private float SnapAim(float distance)
    {
        float pin = _model.PinDistance;
        return pin > 0f && Mathf.Abs(distance - pin) <= AimSnapMeters ? pin : distance;
    }

    // 의뢰 물고기 · 아직 덜 모은 강화 재료 물고기
    private bool Wanted(FishingFish fish)
    {
        var request = _model.Request;
        if (request != null && !request.Done && request.Kind == FishingRequestKind.Fish && request.Fish == fish.Def)
            return true;
        return _model.TryNeededFor(fish.Def, out _, out int owned, out int needed) && owned < needed;
    }

    /// <summary>해달(방치)이 던질 곳: 의뢰 물고기 > 강화 재료 > 아무 물고기 (가끔 빗나감). 없으면 줄 길이의 반~끝</summary>
    private float AutoAim()
    {
        float total = 0f;
        foreach (var fish in _model.Fish)
        {
            if (!fish.Hooked && fish.Weight <= _model.ReelCapacity && fish.Position.x <= _model.CastDistance && -fish.Position.y <= _model.MaxDepth + 0.5f)
                total += Wanted(fish) ? 5f : 1f;
        }
        if (total <= 0f)
            return _model.CastDistance * UnityEngine.Random.Range(0.5f, 1f);
        float pick = UnityEngine.Random.value * total;
        foreach (var fish in _model.Fish)
        {
            if (fish.Hooked || fish.Weight > _model.ReelCapacity || fish.Position.x > _model.CastDistance || -fish.Position.y > _model.MaxDepth + 0.5f)
                continue;
            pick -= Wanted(fish) ? 5f : 1f;
            if (pick <= 0f)
                return _model.AimFor(fish) + UnityEngine.Random.Range(-1.5f, 1.5f);
        }
        return _model.CastDistance;
    }

    private static bool PointerDown()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            return true;
        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.isPressed;
    }

    private void Update()
    {
        // 바다에 있는 동안 꾸미기 모드를 열면 선착장으로 (꾸미기는 선착장 격자 · 카메라를 씀)
        if (_hosted && _active && DecorModePresenter.IsActive)
        {
            Exit();
            return;
        }
        if (_hosted && !_active && _model != null)
            AutoEnter();
        if (_model == null || !_active)
            return;
        float dt = Mathf.Min(Time.deltaTime, 0.1f);

        bool blocked = _hud.Blocking || (_hosted && GameManager.Instance != null && GameManager.Instance.IsModalOpen);
        bool pressed = MineTapInput.Poll(_camera).Count > 0 && !blocked;
        bool down = PointerDown();
        if (pressed)
            _holdValid = true;
        if (!down || blocked)
            _holdValid = false;
        bool holding = _holdValid && down;
        if (pressed || down)
        {
            _idleTime = 0f;
            _autoFishing = false;
        }
        else
            _idleTime += dt;
        if (_pinning)
        {
            UpdatePin(pressed, down);
            holding = false;
            _idleTime = 0f;
        }
        else
        {
            UpdateAim(pressed, down, blocked);
            holding |= TickAuto(dt);
        }

        _model.Tick(dt, holding && _model.Phase != FishingPhase.Casting);

        if (!Mathf.Approximately(_aspect, _camera.aspect))
            FitCamera();
        UpdateCamera(dt);
        UpdateWorld(dt);
        UpdateTackle(dt, holding);
        foreach (var view in _views.Values)
            view.Tick(dt, _model.Hook, _model.Phase == FishingPhase.InWater, _model.ReelCapacity);

        _saveTimer += dt;
        if (_saveTimer > 10f)
            Save();
    }

    #region 핀 꽂기

    /// <summary>핀 꽂기 시작: 지금 핀(없으면 지난 거리)으로 바다가 흘러가고, 끌어서 고름</summary>
    public void BeginPin()
    {
        if (_pinning || _model.Phase != FishingPhase.Ready)
            return;
        _pinning = true;
        _pinDragging = false;
        _aiming = false;
        _hud.AimPreview = null;
        _autoFishing = false;
        float start = _model.PinDistance > 0f ? _model.PinDistance : _model.LastCastDistance;
        _pinGlide = new Vector2(start, HomeY);
    }

    /// <summary>핀 꽂기 끝: place면 화면 가운데 거리에 꽂음. 카메라는 선착장으로 돌아감</summary>
    public void EndPin(bool place)
    {
        if (!_pinning)
            return;
        _pinning = false;
        _pinDragging = false;
        _pinGlide = null;
        _holdValid = false;
        if (place)
        {
            _model.SetPin(PinCenter);
            _sfx.Play(MineSfx.Kind.Tap);
        }
    }

    public void ClearPin()
    {
        _model.ClearPin();
        EndPin(false);
    }

    // 끌면 바다가 손가락을 따라 움직임 (화면 가운데 = 핀 자리)
    private void UpdatePin(bool pressed, bool down)
    {
        if (_model.Phase != FishingPhase.Ready)
        {
            EndPin(false);
            return;
        }
        var screen = PointerPosition();
        if (pressed)
        {
            _pinDragging = true;
            _pinGlide = null;
            _pinDragLast = screen;
        }
        if (!down)
            _pinDragging = false;
        if (!_pinDragging)
            return;
        var from = _camera.ScreenToWorldPoint(new Vector3(_pinDragLast.x, _pinDragLast.y, 10f));
        var to = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
        Vector2 delta = _seaRoot.InverseTransformVector(to - from);
        _cameraPos = ClampPinView(_cameraPos - delta);
        _cameraVelocity = Vector2.zero;
        _pinDragLast = screen;
    }

    // 핀 꽂는 동안 카메라: 가로 = 가장 가까운 거리 ~ 줄 길이, 세로 = 수면 ~ 바닥 조금 위
    private Vector2 ClampPinView(Vector2 view)
    {
        float x = Mathf.Clamp(view.x, FishingReworkModel.MinCastDistance, _model.CastDistance);
        float floor = -_config.SeabedDepth(x) + _camera.orthographicSize * 0.45f;
        return new Vector2(x, Mathf.Clamp(view.y, Mathf.Min(HomeY, floor), HomeY));
    }

    #endregion

    // 준비 중: 꾹 누르면 게이지 (누른 곳 위에 도넛), 떼면 던짐. 톡 누르고 떼면 지난 거리
    private void UpdateAim(bool pressed, bool down, bool blocked)
    {
        if (_model.Phase != FishingPhase.Ready || blocked)
        {
            if (_aiming)
            {
                _aiming = false;
                _hud.AimPreview = null;
            }
            return;
        }
        if (!_aiming && pressed)
        {
            _aiming = true;
            _aimTime = 0f;
            _aimDistance = FishingReworkModel.MinCastDistance;
            _hud.GaugeAt = PointerPosition();
        }
        if (!_aiming)
            return;
        bool gauge = _aimTime >= GaugeAfterSeconds;
        if (down)
        {
            _aimTime += Time.unscaledDeltaTime;
            if (_aimTime >= GaugeAfterSeconds)
            {
                float t = Mathf.PingPong((_aimTime - GaugeAfterSeconds) / GaugeSeconds, 1f);
                _aimDistance = Mathf.Lerp(FishingReworkModel.MinCastDistance, _model.CastDistance, t);
            }
            _aimDistance = Mathf.Clamp(_aimDistance, FishingReworkModel.MinCastDistance, _model.CastDistance);
            _hud.AimPreview = gauge ? _aimDistance : (float?)null;
            return;
        }
        _aiming = false;
        _hud.AimPreview = null;
        _holdValid = false;
        _model.TryCast(gauge ? SnapAim(_aimDistance) : _model.LastCastDistance);
    }

    /// <summary>
    /// 본 게임: 안 만진 지 AutoAfterSeconds가 지나면 낚시꾼 해달이 알아서 던지고, 눌렀다 뗐다를 아무렇게나 해서
    /// 걸리는 대로 낚음 (손으로 고르는 것보다 느리고 싼 물고기 위주). 누르면 바로 손으로
    /// </summary>
    /// <returns>해달이 줄을 당기는 중</returns>
    private bool TickAuto(float dt)
    {
        if (!_hosted)
            return false;
        if (!_autoFishing)
        {
            if (_idleTime < AutoAfterSeconds || _model.Phase != FishingPhase.Ready)
                return false;
            _autoFishing = true;
            _autoTimer = 0f;
            _hud.ShowHint();
        }
        if (_model.Phase == FishingPhase.Ready)
        {
            _autoTimer -= dt;
            if (_autoTimer <= 0f)
            {
                _autoTimer = UnityEngine.Random.Range(1.5f, 3f);
                _model.TryCast(AutoAim());
            }
            return false;
        }
        if (_model.Phase != FishingPhase.InWater)
            return false;
        _autoTimer -= dt;
        if (_autoTimer <= 0f)
        {
            _autoHold = !_autoHold;
            _autoTimer = _autoHold ? UnityEngine.Random.Range(0.3f, 0.9f) : UnityEngine.Random.Range(0.8f, 1.8f);
        }
        return _autoHold;
    }

    private void FitCamera()
    {
        _aspect = _camera.aspect;
        _camera.orthographicSize = Mathf.Clamp(ViewWidth * 0.5f / Mathf.Max(0.3f, _aspect), 5.5f, 9f);
        float h = _camera.orthographicSize * 2f;
        var s = _art.Sky.bounds.size;
        _sky.transform.localScale = new Vector3(h * _aspect / s.x * 1.1f, h / s.y * 1.1f, 1f);
    }

    /// <summary>바늘이 보이는 곳 (준비 중이면 선착장 앞)</summary>
    private Vector2 HookVisual()
    {
        var tip = RigTip;
        switch (_model.Phase)
        {
            case FishingPhase.Ready:
                return tip + new Vector2(0.05f * Mathf.Sin(Time.time * 1.6f), -1.1f);
            case FishingPhase.Casting:
                // 그림 속 낚싯대 끝에서 출발해 규칙의 날아가는 길로 이어짐
                float blend = Mathf.Clamp01(1f - (_model.Hook.x) / 4f);
                return _model.Hook + (tip - _model.Tip) * blend;
            default:
                return _model.Hook;
        }
    }

    private void UpdateCamera(float dt)
    {
        if (_pinning)
        {
            if (_pinGlide.HasValue)
                _cameraPos = Vector2.SmoothDamp(_cameraPos, ClampPinView(_pinGlide.Value), ref _cameraVelocity, 0.3f, 80f, dt);
            _fx.SetCameraBase(W(_cameraPos) + Vector3.back * 10f);
            return;
        }
        var target = new Vector2(HomeX, HomeY);
        if (_model.Phase != FishingPhase.Ready)
        {
            var hook = HookVisual();
            target = new Vector2(Mathf.Max(HomeX, hook.x + 0.6f), Mathf.Min(HomeY, hook.y + 1.6f));
            float floor = -_config.SeabedDepth(target.x) + _camera.orthographicSize * 0.45f;
            target.y = Mathf.Max(target.y, Mathf.Min(HomeY, floor));
        }
        float smooth = _model.Phase == FishingPhase.Casting ? 0.18f : 0.28f;
        _cameraPos = Vector2.SmoothDamp(_cameraPos, target, ref _cameraVelocity, smooth, 80f, dt);
        _fx.SetCameraBase(W(_cameraPos) + Vector3.back * 10f);
    }

    private void UpdateWorld(float dt)
    {
        float camX = _cameraPos.x, t = Time.time;
        foreach (var (tr, parallax, offset) in _parallax)
        {
            bool cloud = tr.name.StartsWith("Cloud");
            bool island = tr.name.StartsWith("Island");
            float span = cloud ? 28f : island ? 72f : 20f;
            float drift = cloud ? t * 0.25f : 0f;
            // 카메라를 따라오되 조금 늦게 (먼 것일수록 덜 움직임), 화면 밖으로 나가면 반대쪽으로
            float local = Mathf.Repeat(offset + drift - camX * (1f - parallax) + span * 0.5f, span) - span * 0.5f;
            float y = cloud ? 4.6f + (offset % 3f) * 0.4f : island ? 0f : -0.05f;
            tr.localPosition = new Vector3(camX + local, island ? y + 0.25f : y, 0f);
        }
        foreach (var ray in _rays)
        {
            var c = ray.color;
            c.a = 0.35f + 0.2f * Mathf.Sin(t * 0.7f + ray.transform.position.x);
            ray.color = c;
        }
        _wave.transform.localPosition = new Vector3((WorldLeft + WorldRight) * 0.5f + Mathf.Sin(t * 0.8f) * 0.4f, 0.05f, 0f);
        foreach (var (tr, phase) in _sway)
            tr.localRotation = Quaternion.Euler(0f, 0f, 5f * Mathf.Sin(t * 1.3f + phase));
        foreach (var (tr, x) in _buoys)
        {
            tr.localPosition = new Vector3(x, 0.08f * Mathf.Sin(t * 1.6f + x), 0f);
            tr.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(t * 1.1f + x * 0.7f));
        }
        float pin = _model.PinDistance;
        _pin.enabled = pin > 0f;
        if (pin > 0f)
        {
            _pin.transform.localPosition = new Vector3(pin, 0.12f + 0.1f * Mathf.Sin(t * 2.2f), 0f);
            _pin.transform.localRotation = Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(t * 1.7f));
        }
    }

    private void UpdateTackle(float dt, bool holding)
    {
        var tip = RigTip;
        var hook = HookVisual();
        var phase = _model.Phase;
        float length = Vector2.Distance(tip, hook);
        float sag;
        switch (phase)
        {
            case FishingPhase.InWater: sag = Mathf.Min(2.5f, length * (holding ? 0.03f : 0.1f)); break;
            case FishingPhase.Hooked: sag = 0f; break;
            case FishingPhase.Casting: sag = Mathf.Min(1.5f, length * 0.05f); break;
            default: sag = 0f; break;
        }
        var control = (tip + hook) * 0.5f + Vector2.down * sag;
        for (int i = 0; i < _line.positionCount; i++)
        {
            float k = i / (float)(_line.positionCount - 1);
            var p = (1 - k) * (1 - k) * tip + 2 * (1 - k) * k * control + k * k * hook;
            if (phase == FishingPhase.Hooked)
                p += Vector2.right * (0.04f * Mathf.Sin(Time.time * 40f + k * 9f) * Mathf.Sin(k * Mathf.PI));
            _line.SetPosition(i, p);
        }

        var dir = (tip - hook).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        bool hooked = phase == FishingPhase.Hooked;
        _hook.gameObject.SetActive(!hooked);
        _hook.transform.localPosition = hook;
        _hook.transform.rotation = Quaternion.Euler(0f, 0f, phase == FishingPhase.Ready ? 0f : angle);
        _hook.transform.localScale = Vector3.one * 0.45f / Mathf.Max(0.01f, _art.Hook.bounds.size.x);
        _sinker.transform.localPosition = hook + dir * 0.45f;
        _sinker.transform.localScale = Vector3.one * (0.3f + 0.04f * _model.Level(FishingUpgrade.Sinker)) / Mathf.Max(0.01f, _art.Sinker.bounds.size.x);

        bool underwater = (phase == FishingPhase.InWater || phase == FishingPhase.Hooked) && hook.y < -0.2f;
        if (underwater)
        {
            _bubbleTimer -= dt;
            if (_bubbleTimer <= 0f)
            {
                _bubbleTimer = holding ? 0.12f : 0.3f;
                _fx.Mote(W(hook + UnityEngine.Random.insideUnitCircle * 0.15f), new Color(1f, 1f, 1f, 0.75f), 0.14f, 1.3f);
            }
        }
        if ((holding && phase == FishingPhase.InWater) || phase == FishingPhase.Hooked)
        {
            _reelTimer -= dt;
            if (_reelTimer <= 0f)
            {
                _reelTimer = hooked ? 0.09f : 0.13f;
                _sfx.Play(MineSfx.Kind.Tap, 1.8f, 0.22f, 0.05f);
            }
        }
    }

    #endregion
}

/// <summary>화면에 쓰는 글자 (폰트 아틀라스에 미리)</summary>
public static class FishingGlyphs
{
    public const string Ui = "0123456789+-./:%×→·~!?()kgm골드낚시의뢰마리한넘는물고기아무거나받기바꾸기초강화도감테스트" +
        "던지기누르면빨리감아요떼면가라앉아요걸렸다빈바늘이에새대물기록발견처음부터모두최대줄길이찌무게릴" +
        "닫기거리깊이흔함보통희귀귀함전설잡은수가장큰값어디서아직못낚았어잠깐뒤에다시할수있금지은화면을눌러" +
        "단계다음끝까지비용만큼더멀리깊게것무거운버티는빠르게오는내놓친너무힘이모자라올렸던져진무엇을까" +
        "가방쏙팔전부씩비었봐요최대";
}
