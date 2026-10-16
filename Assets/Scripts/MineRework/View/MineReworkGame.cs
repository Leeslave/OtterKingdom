using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 광산 개편 테스트 씬의 중심 (Assets/Scenes/MineRework.unity). 본 게임 세이브 · 매니저와 따로 돈다.
/// 규칙(MineReworkModel)을 들고, 탭을 받아 바위 · 막힌 길 · 광부에 넘기고, 결과를 바위 · 효과 · 소리 · 화면에 그린다.
/// 층마다 색감(배경 · 가장자리 · 빛가루)을 바꾸고, 5초마다 · 꺼질 때 persistentDataPath/mine_rework_test.json에 저장.
/// Docs/광산_개편_기획.md
/// </summary>
public class MineReworkGame : MonoBehaviour
{
    private const string SaveFile = "mine_rework_test.json";
    private const float SaveInterval = 5f;
    private const float HintAfter = 5f;
    private const int RockOrderBase = 500;

    [Header("데이터")]
    [Tooltip("광석 · 층 · 곡괭이 · 수치")]
    [SerializeField] private MineReworkConfig _config;
    [Tooltip("바위 · 효과 · UI 그림")]
    [SerializeField] private MineReworkArt _art;

    [Header("장면")]
    [Tooltip("광산 카메라")]
    [SerializeField] private Camera _camera;
    [Tooltip("광산 배경 (층마다 색을 곱함)")]
    [SerializeField] private SpriteRenderer _background;
    [Tooltip("가장자리 어둡게 (화면을 덮음)")]
    [SerializeField] private SpriteRenderer _vignette;
    [Tooltip("바위 칸 자리 (발밑)")]
    [SerializeField] private List<Transform> _rockSlots = new List<Transform>();
    [Tooltip("막힌 길 큰 바위 자리 (발밑)")]
    [SerializeField] private Transform _boulderSpot;
    [Tooltip("광부 해달 (리그, 곡괭이질)")]
    [SerializeField] private QuarterRigView _miner;

    [Header("효과")]
    [SerializeField] private MineFx _fx;
    [SerializeField] private MineSfx _sfx;

    private MineReworkModel _model;
    private MineHud _hud;
    private readonly List<VeinRockView> _rocks = new List<VeinRockView>();
    private VeinRockView _boulder;
    private readonly MineRock _boulderRock = new MineRock();
    private readonly float[] _breakingUntil = new float[16];
    private Material _flashMaterial;
    private float _aspect;
    private float _saveTimer;
    private float _idle;
    private float _moteTimer;
    private float _shinyTimer;
    private bool _resetting;
    private MineLayerDefinition _lookLayer;
    private Sprite _defaultBackground;
    private Color _bgFrom, _bgTo;
    private float _vigFrom, _vigTo;
    private float _lookAge = 1f;

    public bool InBoulder { get; private set; }
    public bool MinerVisible => _miner != null && !InBoulder;
    public Vector3 MinerBubbleWorld => _miner.transform.position + new Vector3(0.2f, 2.35f, 0f);
    // 광부 앞 바위 (리그가 그리는 바위, 보는 쪽)
    private Vector2 MinerRock => (Vector2)_miner.transform.position + new Vector2(_miner.FacingLeft ? -0.7f : 0.7f, 0.3f);

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFile);

    #region 시작 · 저장

    private void Awake()
    {
        var shader = Shader.Find("GUI/Text Shader");
        if (shader != null)
            _flashMaterial = new Material(shader);

        var save = Load();
        double offline = save.started && save.savedUtcTicks > 0 ? (DateTime.UtcNow - new DateTime(save.savedUtcTicks, DateTimeKind.Utc)).TotalSeconds : 0;
        _model = new MineReworkModel(_config, save, new System.Random(Environment.TickCount));
        var offlineGains = _model.ApplyOffline(offline);

        PrepareGlyphs();
        _fx.Setup(_art, _camera, RuntimeUIKit.Style.TitleFont);
        BuildRocks();

        _hud = new GameObject("Hud").AddComponent<MineHud>();
        _hud.transform.SetParent(transform, false);
        _hud.Setup(this, _model, _art, _sfx);

        _model.Changed += _hud.Refresh;
        _model.Discovered += HandleDiscovered;
        _model.RockSpawned += HandleRockSpawned;
        _model.MinerFound += HandleMinerFound;
        _model.JackpotChanged += HandleJackpot;
        _model.LayerChanged += HandleLayerChanged;

        FitCamera();
        ApplyLook(_model.Layer, true);
        ShowAllRocks(false);
        _hud.ShowLayerTitle(_model.LayerIndex, _model.Layer);
        _hud.ShowOffline(offlineGains, offline);
        Save();
    }

    private void Start()
    {
        if (_miner != null)
            _miner.PlayLoop(QuarterRigView.Loop.Mine);
    }

    private static MineReworkSave Load()
    {
        try
        {
            if (File.Exists(SavePath))
                return JsonUtility.FromJson<MineReworkSave>(File.ReadAllText(SavePath)) ?? new MineReworkSave();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MineRework] 저장을 못 읽어 처음부터: {e.Message}");
        }
        return new MineReworkSave();
    }

    private void Save()
    {
        if (_model == null || _resetting)
            return;
        File.WriteAllText(SavePath, JsonUtility.ToJson(_model.ToSave(DateTime.UtcNow)));
        _saveTimer = 0f;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Save();
    }

    private void OnApplicationQuit() => Save();

    private void OnDisable() => Save();

    // 처음 보는 한글을 폰트에 미리 (도중에 추가되면 에디터가 멈춤)
    private void PrepareGlyphs()
    {
        var sb = new StringBuilder(MineGlyphs.Ui);
        foreach (var ore in _config.Ores)
        {
            if (ore != null)
                sb.Append(ore.DisplayName).Append(ore.Description).Append(ore.UseText);
        }
        foreach (var layer in _config.Layers)
            sb.Append(layer.DisplayName).Append(layer.Subtitle);
        foreach (var pickaxe in _config.Pickaxes)
            sb.Append(pickaxe.DisplayName).Append(pickaxe.Description);
        RuntimeUIKit.PrepareGlyphs(sb.ToString());
    }

    #endregion

    #region 화면 맞춤 · 색감

    private void FitCamera()
    {
        _aspect = _camera.aspect;
        var size = _background.sprite.bounds.size;
        _camera.orthographicSize = Mathf.Min(size.y * 0.5f, size.x * 0.5f / Mathf.Max(0.01f, _aspect));
        var center = _background.transform.position;
        var position = new Vector3(center.x, center.y, -10f);
        _camera.transform.position = position;
        _fx.SetCameraBase(position);
        float h = _camera.orthographicSize * 2f;
        var v = _vignette.sprite.bounds.size;
        _vignette.transform.position = new Vector3(center.x, center.y, 0f);
        _vignette.transform.localScale = new Vector3(h * _aspect / v.x * 1.02f, h / v.y * 1.02f, 1f);
    }

    private void ApplyLook(MineLayerDefinition layer, bool instant)
    {
        _lookLayer = layer;
        if (_defaultBackground == null)
            _defaultBackground = _background.sprite;
        _background.sprite = layer.Background != null ? layer.Background : _defaultBackground;
        _bgFrom = _background.color;
        _vigFrom = _vignette.color.a;
        _bgTo = layer.BackgroundTint;
        _vigTo = layer.Darkness;
        _lookAge = instant ? 1f : 0f;
        if (instant)
        {
            _background.color = _bgTo;
            _vignette.color = new Color(0f, 0f, 0f, _vigTo);
        }
    }

    private void UpdateLook(float dt)
    {
        if (_lookAge < 1f)
        {
            _lookAge = Mathf.Min(1f, _lookAge + dt / 0.6f);
            _background.color = Color.Lerp(_bgFrom, _bgTo, _lookAge);
            _vignette.color = new Color(0f, 0f, 0f, Mathf.Lerp(_vigFrom, _vigTo, _lookAge));
        }
        if (_model.JackpotActive)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            _background.color = Color.Lerp(_bgTo, new Color(1f, 0.92f, 0.62f), 0.25f + 0.15f * pulse);
        }

        // 떠다니는 빛가루
        _moteTimer -= dt;
        if (_moteTimer <= 0f)
        {
            _moteTimer = _model.JackpotActive ? 0.05f : 0.2f;
            float h = _camera.orthographicSize;
            float w = h * _camera.aspect;
            var c = (Vector2)_camera.transform.position;
            var at = c + new Vector2(UnityEngine.Random.Range(-w, w), UnityEngine.Random.Range(-h * 0.85f, h * 0.6f));
            var color = _model.JackpotActive ? new Color(1f, 0.85f, 0.35f, 0.9f) : _lookLayer.MoteColor;
            _fx.Mote(at, color, UnityEngine.Random.Range(0.12f, 0.28f), UnityEngine.Random.Range(2.2f, 3.6f));
        }
    }

    #endregion

    #region 바위

    private void BuildRocks()
    {
        var white = WhiteSprite();
        for (int i = 0; i < _rockSlots.Count && i < _config.RockSlots; i++)
        {
            var slot = _rockSlots[i];
            var view = slot.GetComponent<VeinRockView>();
            if (view == null)
                view = slot.gameObject.AddComponent<VeinRockView>();
            view.Setup(_art, _flashMaterial, white, RockOrderBase - Mathf.RoundToInt(slot.position.y * 20f));
            _rocks.Add(view);
        }
        _boulder = _boulderSpot.GetComponent<VeinRockView>();
        if (_boulder == null)
            _boulder = _boulderSpot.gameObject.AddComponent<VeinRockView>();
        _boulder.Setup(_art, _flashMaterial, white, RockOrderBase - Mathf.RoundToInt(_boulderSpot.position.y * 20f));
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

    private void ShowAllRocks(bool animate)
    {
        var tint = _model.Layer.RockTint;
        for (int i = 0; i < _rocks.Count; i++)
        {
            var rock = _model.Rocks[i];
            _rocks[i].gameObject.SetActive(!InBoulder);
            if (animate && rock.Present)
                StartCoroutine(SpawnLater(i, i * 0.08f));
            else
                _rocks[i].Show(rock, tint);
        }
    }

    private void HandleRockSpawned(int slot)
    {
        if (slot >= _rocks.Count || InBoulder)
            return;
        float wait = Mathf.Max(0f, _breakingUntil[slot] - Time.time);
        StartCoroutine(SpawnLater(slot, wait));
    }

    private IEnumerator SpawnLater(int slot, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        var rock = _model.Rocks[slot];
        if (!rock.Present || InBoulder)
            yield break;
        var view = _rocks[slot];
        view.PlaySpawn(rock, _model.Layer.RockTint);
        _fx.Chips(view.transform.position + Vector3.up * 0.1f, new Color(0.85f, 0.75f, 0.6f), 5, 0.6f);
        _sfx.Play(MineSfx.Kind.Pop, 0.65f, 0.35f, 0.05f);
        if (rock.Shiny)
        {
            _fx.Ring(view.Center, new Color(1f, 0.85f, 0.35f), 3f, 0.6f);
            _sfx.Play(MineSfx.Kind.Rare, 1.2f, 0.5f);
        }
    }

    private void HitRock(int slot)
    {
        var rock = _model.Rocks[slot];
        var view = _rocks[slot];
        var vein = rock.Vein;
        var hit = _model.Hit(slot, Time.unscaledTime);
        if (hit == null)
            return;
        _idle = 0f;
        var center = view.Center;
        var chipColor = vein != null && vein != _config.Stone ? Color.Lerp(_model.Layer.RockTint, vein.Color, 0.5f) : _model.Layer.RockTint * new Color(0.9f, 0.85f, 0.8f);
        float comboPitch = 0.92f + Mathf.Min(hit.Combo, 25) * 0.018f + UnityEngine.Random.Range(-0.03f, 0.03f);

        if (!hit.Broken)
            view.PlayHit(rock, hit.Crit);
        _fx.Swing(center + Vector2.up * 0.3f, _model.Pickaxe.Icon);
        _fx.Chips(center + Vector2.up * 0.25f, chipColor, hit.Crit ? 9 : 5, hit.Crit ? 1.3f : 1f);
        if (hit.Crit)
        {
            _sfx.Play(MineSfx.Kind.Crit, comboPitch);
            _fx.Sparks(center, new Color(1f, 0.85f, 0.4f), 10, 1.3f, 0.6f);
            _fx.Text(center + Vector2.up * 1.1f, "쾅!", new Color(1f, 0.6f, 0.3f), 7f, 0.6f, 0.8f);
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

        _breakingUntil[slot] = Time.time + 0.45f;
        view.PlayBreak();
        _sfx.Play(MineSfx.Kind.Break, 0.95f + UnityEngine.Random.Range(-0.05f, 0.08f));
        _fx.Chips(center, chipColor, 14, 1.5f);
        _fx.Glow(center, new Color(1f, 0.97f, 0.88f, 0.7f), 3.4f, 0.3f);
        _fx.Ring(center, new Color(1f, 1f, 1f, 0.8f), 2.6f, 0.35f);
        _fx.Shake(hit.Shiny ? 0.3f : 0.16f);
        if (hit.Shiny)
        {
            _fx.Sparks(center, new Color(1f, 0.85f, 0.35f), 20, 1.6f, 0.7f);
            _fx.Ring(center, new Color(1f, 0.85f, 0.35f), 4f, 0.6f);
            _hud.Flash(new Color(1f, 0.92f, 0.6f, 0.45f));
        }
        DropGains(hit.Gains, center, 0.75f);
        if (hit.JackpotStarted)
            StartCoroutine(JackpotStart());
    }

    // 광석이 바위에서 튀어나와 통통 → 띠로 날아감 (아이콘은 최대 5개, 개수는 나눠 담음)
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
                bool big = ore.IsRare;
                _fx.PopOre(from, ore.Icon, delay, landed => _hud.FlyOre(landed, ore, amount), big ? size * 1.35f : size);
                delay += 0.045f;
            }
            if (gain.Ore.IsRare)
            {
                _fx.Ring(from, gain.Ore.Color, 3.4f, 0.55f);
                _fx.Sparks(from, gain.Ore.Color, 14, 1.4f, 0.6f);
                _fx.Text(from + Vector2.up * 1.7f, gain.Ore.DisplayName + "!", new Color(1f, 0.88f, 0.4f), 6f, 1.2f, 1f);
                _sfx.Play(MineSfx.Kind.Rare);
            }
        }
        _sfx.Play(MineSfx.Kind.Pop, 1f, 0.7f);
        if (total > 0)
            _fx.Text(from + Vector2.up * 1.2f, $"+{total}", Color.white, 5f, 0.9f, 1.3f);
    }

    #endregion

    #region 광부

    private void HandleMinerFound(MineGain gain)
    {
        if (_miner == null || InBoulder)
            return;
        var ore = gain.Ore;
        _fx.PopOre(MinerRock, ore.Icon, 0f, landed => _hud.FlyOre(landed, ore, gain.Amount), ore.IsRare ? 0.8f : 0.55f);
        if (ore.IsRare)
        {
            _fx.Sparks(MinerRock, ore.Color, 10, 1f, 0.5f);
            _sfx.Play(MineSfx.Kind.Rare, 1.1f, 0.7f);
        }
    }

    private void Cheer()
    {
        if (_model.TryCheer())
        {
            _sfx.Play(MineSfx.Kind.Cheer);
            _fx.Text(MinerBubbleWorld + Vector3.up * 0.4f, "힘내!", new Color(1f, 0.7f, 0.75f), 6f, 1f, 1f);
            _fx.Sparks(_miner.transform.position + Vector3.up * 1f, new Color(1f, 0.7f, 0.8f), 12, 1f, 0.5f);
            _hud.ShowToast("깡깡이가 신나서 두 배로 캐요!");
        }
        else
        {
            _hud.ShowToast($"깡깡이가 쉬는 중이에요 ({Mathf.CeilToInt(_model.CheerCooldownLeft)}초)");
        }
    }

    public void PlayPickaxeSpark()
    {
        if (_miner == null)
            return;
        _fx.Sparks(_miner.transform.position + Vector3.up * 1.2f, new Color(1f, 0.9f, 0.5f), 18, 1.2f, 0.6f);
        _fx.Ring(_miner.transform.position + Vector3.up * 1f, new Color(1f, 0.9f, 0.5f), 3f, 0.5f);
    }

    #endregion

    #region 대박 · 발견

    private IEnumerator JackpotStart()
    {
        _sfx.Play(MineSfx.Kind.Jackpot);
        _hud.Flash(new Color(1f, 0.9f, 0.5f, 0.7f));
        _fx.Shake(0.4f);
        _hud.ShowToast("광맥 대박! 바위가 쏟아져요!");
        var c = (Vector2)_camera.transform.position;
        for (int i = 0; i < 4; i++)
        {
            _fx.Ring(c + UnityEngine.Random.insideUnitCircle * 3f, new Color(1f, 0.85f, 0.35f), 5f, 0.7f);
            _fx.Sparks(c + UnityEngine.Random.insideUnitCircle * 3f, new Color(1f, 0.9f, 0.45f), 12, 2f, 0.7f);
            yield return new WaitForSeconds(0.12f);
        }
    }

    private void HandleJackpot(bool on)
    {
        _hud.SetJackpot(on);
        if (!on)
        {
            _hud.ShowToast("대박이 끝났어요");
            ApplyLook(_model.Layer, false);
        }
    }

    private void HandleDiscovered(MineOreDefinition ore) => StartCoroutine(DiscoverLater(ore));

    private IEnumerator DiscoverLater(MineOreDefinition ore)
    {
        yield return new WaitForSeconds(1.05f);
        _hud.ShowDiscovery(ore);
    }

    #endregion

    #region 층 · 막힌 길 (MineHud가 부름)

    public void Travel(int index)
    {
        if (InBoulder)
            LeaveBoulder();
        if (!_model.TryTravel(index))
            return;
        _hud.Flash(new Color(0f, 0f, 0f, 0.85f));
        _sfx.Play(MineSfx.Kind.Open, 0.8f);
    }

    private void HandleLayerChanged()
    {
        ApplyLook(_model.Layer, false);
        _hud.RefreshLayer();
        if (!InBoulder)
        {
            ShowAllRocks(true);
            _hud.ShowLayerTitle(_model.LayerIndex, _model.Layer);
        }
    }

    public void EnterBoulder()
    {
        int next = _model.BlockedLayer;
        if (next < 0)
            return;
        InBoulder = true;
        foreach (var rock in _rocks)
            rock.gameObject.SetActive(false);
        var layer = _config.Layers[next];
        _boulder.gameObject.SetActive(true);
        _boulderRock.MaxHp = _config.BoulderHits;
        _boulderRock.Hp = _model.BoulderLeft;
        _boulder.PlaySpawn(_boulderRock, layer.RockTint * new Color(0.82f, 0.8f, 0.85f));
        ApplyLook(layer, false);
        _hud.Flash(new Color(0f, 0f, 0f, 0.85f));
        _hud.SetBoulder(layer, next, _model.BoulderLeft, _config.BoulderHits);
        _hud.ShowToast("큰 바위를 마구 쳐서 길을 열어요!");
        _sfx.Play(MineSfx.Kind.Boulder, 0.8f);
    }

    public void LeaveBoulder()
    {
        if (!InBoulder)
            return;
        InBoulder = false;
        _boulder.gameObject.SetActive(false);
        ApplyLook(_model.Layer, false);
        _hud.SetBoulder(null, 0, 0, 1);
        ShowAllRocks(false);
    }

    private void HitBoulder()
    {
        int next = _model.BlockedLayer;
        var hit = _model.HitBoulder(Time.unscaledTime);
        if (hit == null)
            return;
        _idle = 0f;
        var center = _boulder.Center;
        _fx.Swing(center + new Vector2(0.4f, 0.6f), _model.Pickaxe.Icon, 1.8f);
        _boulderRock.Hp = Mathf.Max(1, _model.BoulderLeft);
        _fx.Chips(center, new Color(0.7f, 0.65f, 0.6f), hit.Crit ? 12 : 7, hit.Crit ? 1.6f : 1.2f);
        _sfx.Play(MineSfx.Kind.Boulder, 0.8f + Mathf.Min(hit.Combo, 25) * 0.02f);
        _fx.Shake(hit.Crit ? 0.2f : 0.08f);
        _hud.ShowCombo(hit.Combo, hit.Crit);
        if (hit.Crit)
            _fx.Text(center + Vector2.up * 1.6f, "쾅!", new Color(1f, 0.6f, 0.3f), 9f, 0.6f, 0.8f);

        if (!hit.LayerOpened)
        {
            _boulder.PlayHit(_boulderRock, hit.Crit);
            _hud.SetBoulder(_config.Layers[next], next, _model.BoulderLeft, _config.BoulderHits);
            return;
        }

        // 길이 열림
        _boulder.PlayBreak();
        _fx.Chips(center, new Color(0.7f, 0.65f, 0.6f), 30, 2.2f);
        _fx.Glow(center, Color.white, 8f, 0.5f);
        _fx.Ring(center, Color.white, 7f, 0.6f);
        _fx.Shake(0.45f);
        _hud.Flash(new Color(1f, 1f, 1f, 0.9f));
        InBoulder = false;
        _hud.SetBoulder(null, 0, 0, 1);
        StartCoroutine(OpenedLater(next));
    }

    private IEnumerator OpenedLater(int index)
    {
        yield return new WaitForSeconds(0.5f);
        _boulder.gameObject.SetActive(false);
        ShowAllRocks(true);
        ApplyLook(_model.Layer, false);
        _hud.ShowLayerTitle(index, _model.Layer);
        yield return new WaitForSeconds(0.9f);
        _hud.ShowLayerOpened(index, _model.Layer);
    }

    public void OnPickaxeCrafted()
    {
        _hud.Refresh();
        PlayPickaxeSpark();
        _hud.Flash(new Color(1f, 0.95f, 0.75f, 0.6f));
        _hud.ShowCrafted(_model.Pickaxe, _model.BlockedLayer);
    }

    #endregion

    #region 테스트 (MineHud 테스트 창)

    public void CheatOres(int amount)
    {
        var layer = _model.Layer;
        var done = new HashSet<MineOreDefinition>();
        foreach (var drop in layer.Veins)
        {
            if (drop?.Ore != null && done.Add(drop.Ore))
                _model.CheatAdd(drop.Ore, amount);
        }
        foreach (var drop in layer.RareDrops)
        {
            if (drop?.Ore != null && done.Add(drop.Ore))
                _model.CheatAdd(drop.Ore, Mathf.Max(1, amount / 4));
        }
    }

    public void CheatOffline(double seconds) => _hud.ShowOffline(_model.ApplyOffline(seconds), seconds);

    public void ResetAll()
    {
        _resetting = true;
        if (File.Exists(SavePath))
            File.Delete(SavePath);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    #endregion

    private void Update()
    {
        float dt = Time.deltaTime;
        if (!Mathf.Approximately(_aspect, _camera.aspect))
            FitCamera();
        _model.Tick(dt);
        UpdateLook(dt);

        if (!_hud.Blocking)
        {
            foreach (var tap in MineTapInput.Poll(_camera))
                HandleTap(tap);
        }

        // "여기 쳐 봐" 살짝 흔들기
        _idle += dt;
        if (_idle > HintAfter && !InBoulder)
        {
            _idle = 0f;
            var present = _rocks.FindAll(r => r.Present);
            if (present.Count > 0)
                present[UnityEngine.Random.Range(0, present.Count)].Wiggle();
        }

        // 반짝 광맥 둘레 반짝임
        _shinyTimer -= dt;
        if (_shinyTimer <= 0f)
        {
            _shinyTimer = 0.25f;
            foreach (var rock in _rocks)
            {
                if (rock.Present && rock.Shiny && rock.gameObject.activeSelf)
                    _fx.Sparks(rock.Center + UnityEngine.Random.insideUnitCircle * 0.9f, new Color(1f, 0.9f, 0.5f), 1, 0.3f, 0.35f);
            }
        }

        _saveTimer += dt;
        if (_saveTimer >= SaveInterval)
            Save();
    }

    private void HandleTap(Vector2 world)
    {
        if (InBoulder)
        {
            if (_boulder.Contains(world) || Vector2.Distance(world, _boulder.Center) < 2.2f)
                HitBoulder();
            return;
        }
        for (int i = 0; i < _rocks.Count; i++)
        {
            if (_rocks[i].Contains(world))
            {
                HitRock(i);
                return;
            }
        }
        if (_miner != null && Vector2.Distance(world, (Vector2)_miner.transform.position + Vector2.up * 0.8f) < 1.1f)
            Cheer();
    }
}

/// <summary>광산 화면에 쓰는 한글 (폰트에 미리 넣음)</summary>
public static class MineGlyphs
{
    public const string Ui = "0123456789층조약돌굴골드곡괭이힘치명타광부희귀작업대이동광석도감테스트대박콤보다음바위가득해요깨길돌아가기열렸어"
        + "응원하쉬는중초새발견좋아만들재료모아날갈끝까지았최고의용궁보석방어디든캘수있지금여기뚫필요모은것유물자세히팔개닫판매가쓰임"
        + "흔함보통드묾귀함전설옛왕국흔적완성으로나중에러캐뒀시간분동안받오세움부족을쳐서큰마구쾅신나두배로깡깡이더날카로워졌모자라"
        + "아직못찾은지면공짜처음부터셈쏟아져끝났×✕✔▼!?+%/·…:()x";
}
