using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 광장 농장 화면 (Docs/농장_설계형_기획.md): 다 지은 밭 칸 위에 작물(자라는 단계) · 흙 그림, 다 자란 밭은 반짝,
/// 상자 위 개수(가득이면 빨강), 스프링클러 물방울. 밭을 누르면 심기 · 흙 창, 상자를 누르면 거둠.
/// 농부(새싹이)를 직접 움직임: 다 자란 밭 → 수확 → 들고 가장 가까운 상자 → 넣기 (길 위에선 빨리).
/// 광장 씬에 저절로 하나 생김. 규칙은 SettlementManager.Farm
/// </summary>
public class PlazaFarmView : MonoBehaviour
{
    private const int PlantsPerField = 6;
    private const float RefreshSeconds = 0.25f;
    private const float FieldWorkSpeed = 1.2f;

    private static PlazaFarmView _instance;

    private SettlementManager _manager;
    private DecorBoardView _board;
    private SettlementPlazaView _plaza;
    private float _refreshTimer;
    private readonly Dictionary<int, FieldVisual> _fields = new Dictionary<int, FieldVisual>();
    private readonly Dictionary<int, CrateVisual> _crates = new Dictionary<int, CrateVisual>();
    private readonly List<int> _gone = new List<int>();
    private PlazaFarmSheet _sheet;
    private float _sprinklerTimer;
    private readonly List<(SpriteRenderer drop, Vector2 velocity, float age)> _drops = new List<(SpriteRenderer, Vector2, float)>();
    private Sprite _dot;

    private class FieldVisual
    {
        public Transform Root;
        public SpriteRenderer Soil;
        public readonly SpriteRenderer[] Plants = new SpriteRenderer[PlantsPerField];
        public SpriteRenderer Sparkle;
        public float Age;
    }

    private class CrateVisual
    {
        public Transform Root;
        public TextMeshPro Count;
        public SpriteRenderer Icon;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Ensure();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

    private static void Ensure()
    {
        if (_instance == null)
            _instance = new GameObject(nameof(PlazaFarmView)).AddComponent<PlazaFarmView>();
    }

    private void Awake()
    {
        _dot = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
    }

    private bool Ready()
    {
        if (_manager == null)
        {
            _manager = SettlementManager.Instance;
            if (_manager != null)
            {
                _manager.OnCrateCollected += HandleCollected;
                _manager.OnFarmChanged += MarkDirty;
            }
        }
        _board = DecorBoardView.Active;
        _plaza = SettlementPlazaView.Active;
        return _manager != null && _manager.FarmOpen && _board != null && _plaza != null && _board.Board == _manager.PlazaBoard;
    }

    private void OnDestroy()
    {
        if (_manager != null)
        {
            _manager.OnCrateCollected -= HandleCollected;
            _manager.OnFarmChanged -= MarkDirty;
        }
    }

    private void MarkDirty() => _refreshTimer = 0f;

    private void Update()
    {
        if (!Ready())
        {
            ClearVisuals();
            return;
        }
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = RefreshSeconds;
            RefreshFields();
            RefreshCrates();
        }
        AnimateFields(Time.deltaTime);
        UpdateSprinklers(Time.deltaTime);
        UpdateWorker(Time.deltaTime);
        HandleTaps();
    }

    #region 밭 · 상자 그림

    private void RefreshFields()
    {
        var layout = DecorManager.Instance.GetLayout(_board.Board);
        _gone.Clear();
        _gone.AddRange(_fields.Keys);
        foreach (var field in _manager.Farm.Fields)
        {
            if (!layout.TryGet(field.instanceId, out var placed))
                continue;
            _gone.Remove(field.instanceId);
            if (!_fields.TryGetValue(field.instanceId, out var visual))
                _fields[field.instanceId] = visual = CreateField();
            var rect = _board.AreaWorldRect(placed.Area);
            visual.Root.position = new Vector3(rect.xMin, rect.yMin, 0f);
            FlattenPart(placed.InstanceId);

            var soil = _manager.SoilOf(field);
            visual.Soil.sprite = soil != null ? soil.Sprite : null;
            visual.Soil.enabled = visual.Soil.sprite != null && field.soil > 0;
            if (visual.Soil.enabled)
                Fit(visual.Soil, rect.size, new Vector2(rect.width * 0.5f, rect.height * 0.5f));

            var crop = _manager.FindCrop(field.cropId);
            Sprite[] stage = null;
            if (crop != null)
            {
                float progress = crop.baseDurationSec > 0f ? 1f - field.remainingSec / crop.baseDurationSec : 1f;
                stage = field.ready ? crop.grownSprites : progress > 0.45f ? crop.sproutSprites : crop.seedSprites;
            }
            for (int i = 0; i < PlantsPerField; i++)
            {
                var plant = visual.Plants[i];
                var sprite = stage != null && stage.Length > 0 ? stage[i % stage.Length] : null;
                plant.enabled = sprite != null;
                if (sprite == null)
                    continue;
                plant.sprite = sprite;
                plant.color = crop.spriteTint;
                // 3 × 2 줄 (뒤 줄부터)
                int col = i % 3, row = i / 3;
                var local = new Vector2(rect.width * (0.2f + 0.3f * col), rect.height * (0.62f - 0.36f * row));
                plant.transform.localPosition = local;
                float size = rect.width * 0.3f;
                var b = sprite.bounds.size;
                plant.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, Mathf.Max(b.x, b.y)));
                plant.sortingOrder = PlazaDepth.SortingOrderFor(rect.yMin + local.y - 0.1f);
            }
            visual.Sparkle.enabled = field.ready;
            visual.Sparkle.transform.localPosition = new Vector3(rect.width * 0.5f, rect.height * 0.9f, 0f);
            visual.Sparkle.sortingOrder = PlazaDepth.SortingOrderFor(rect.yMin) + 5;
        }
        foreach (int id in _gone)
        {
            Destroy(_fields[id].Root.gameObject);
            _fields.Remove(id);
        }
        // 길도 바닥에 깔림
        var stone = _manager.StonePath ? _manager.FarmConfig.StonePathSprite : null;
        foreach (var path in _manager.FarmParts(FarmPartKind.Path, false))
        {
            FlattenPart(path.InstanceId);
            var view = _board.FindView(path.InstanceId);
            if (stone != null && view != null)
            {
                foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>())
                    renderer.sprite = stone;
            }
        }
    }

    // 밭 · 길은 바닥 (해달이 그 위를 걸어도 해달 뒤로 가지 않게)
    private void FlattenPart(int instanceId)
    {
        var view = _board.FindView(instanceId);
        if (view == null)
            return;
        foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>())
            renderer.sortingOrder = PlazaDepth.FlatPropOrder + 10;
    }

    private FieldVisual CreateField()
    {
        var visual = new FieldVisual { Root = new GameObject("FarmField").transform };
        visual.Root.SetParent(transform, false);
        visual.Soil = NewRenderer("Soil", visual.Root, PlazaDepth.FlatPropOrder + 11);
        for (int i = 0; i < PlantsPerField; i++)
            visual.Plants[i] = NewRenderer("Plant", visual.Root, 0);
        visual.Sparkle = NewRenderer("Ready", visual.Root, 0);
        visual.Sparkle.sprite = Resources.Load<Sprite>("UI/Celebrate/Twinkle");
        visual.Sparkle.color = new Color(1f, 0.95f, 0.6f, 0.9f);
        visual.Age = Random.Range(0f, 3f);
        return visual;
    }

    private static SpriteRenderer NewRenderer(string name, Transform parent, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        return renderer;
    }

    private static void Fit(SpriteRenderer renderer, Vector2 size, Vector2 local)
    {
        var b = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, b.x), size.y / Mathf.Max(0.01f, b.y), 1f);
        renderer.transform.localPosition = local - (Vector2)(renderer.sprite.bounds.center * renderer.transform.localScale.x);
    }

    private void AnimateFields(float dt)
    {
        foreach (var visual in _fields.Values)
        {
            visual.Age += dt;
            if (visual.Sparkle.enabled)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(visual.Age * 5f);
                visual.Sparkle.transform.localScale = Vector3.one * (0.35f + 0.15f * pulse);
                visual.Sparkle.transform.localRotation = Quaternion.Euler(0f, 0f, visual.Age * 60f);
            }
            // 바람에 살랑
            for (int i = 0; i < PlantsPerField; i++)
            {
                if (visual.Plants[i].enabled)
                    visual.Plants[i].transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(visual.Age * 1.8f + i) * 3f);
            }
        }
    }

    private void RefreshCrates()
    {
        var layout = DecorManager.Instance.GetLayout(_board.Board);
        _gone.Clear();
        _gone.AddRange(_crates.Keys);
        int capacity = _manager.CrateCapacity;
        foreach (var crate in _manager.Farm.Crates)
        {
            if (!layout.TryGet(crate.instanceId, out var placed))
                continue;
            _gone.Remove(crate.instanceId);
            if (!_crates.TryGetValue(crate.instanceId, out var visual))
                _crates[crate.instanceId] = visual = CreateCrate();
            var rect = _board.AreaWorldRect(placed.Area);
            visual.Root.position = new Vector3(rect.center.x, rect.yMax + 0.9f, 0f);
            int count = PlazaFarm.Count(crate);
            visual.Count.text = $"{count}/{capacity}";
            visual.Count.color = count >= capacity ? new Color(1f, 0.45f, 0.4f) : Color.white;
            var top = crate.items.Count > 0 ? _manager.FindCrop(crate.items[0].id) : null;
            var icon = top != null && top.grownSprites != null && top.grownSprites.Length > 0 ? top.grownSprites[0] : null;
            visual.Icon.enabled = icon != null;
            if (icon != null)
            {
                visual.Icon.sprite = icon;
                visual.Icon.transform.localScale = Vector3.one * (0.6f / Mathf.Max(0.01f, icon.bounds.size.y));
            }
            int order = PlazaDepth.SortingOrderFor(rect.yMin) + 20;
            visual.Count.sortingOrder = order;
            visual.Icon.sortingOrder = order;
        }
        foreach (int id in _gone)
        {
            Destroy(_crates[id].Root.gameObject);
            _crates.Remove(id);
        }
    }

    private CrateVisual CreateCrate()
    {
        var visual = new CrateVisual { Root = new GameObject("FarmCrate").transform };
        visual.Root.SetParent(transform, false);
        visual.Icon = NewRenderer("Icon", visual.Root, 0);
        visual.Icon.transform.localPosition = new Vector3(-0.45f, 0f, 0f);
        var text = new GameObject("Count").AddComponent<TextMeshPro>();
        text.transform.SetParent(visual.Root, false);
        text.transform.localPosition = new Vector3(0.25f, 0f, 0f);
        if (RuntimeUIKit.Style.TitleFont != null)
            text.font = RuntimeUIKit.Style.TitleFont;
        text.fontSize = 4.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(75, 46, 34, 255);
        text.rectTransform.sizeDelta = new Vector2(2.4f, 1f);
        visual.Count = text;
        return visual;
    }

    private void ClearVisuals()
    {
        if (_fields.Count == 0 && _crates.Count == 0)
            return;
        foreach (var v in _fields.Values)
            Destroy(v.Root.gameObject);
        foreach (var v in _crates.Values)
            Destroy(v.Root.gameObject);
        _fields.Clear();
        _crates.Clear();
        _worker = null;
    }

    #endregion

    #region 스프링클러 물방울

    private void UpdateSprinklers(float dt)
    {
        _sprinklerTimer -= dt;
        if (_sprinklerTimer <= 0f)
        {
            _sprinklerTimer = 0.12f;
            var layout = DecorManager.Instance.GetLayout(_board.Board);
            foreach (var sprinkler in _manager.FarmParts(FarmPartKind.Sprinkler))
            {
                var rect = _board.AreaWorldRect(sprinkler.Area);
                var drop = NewRenderer("Drop", transform, PlazaDepth.SortingOrderFor(rect.yMin) + 30);
                drop.sprite = _dot;
                drop.color = new Color(0.55f, 0.82f, 1f, 0.9f);
                drop.transform.position = new Vector3(rect.center.x, rect.yMin + rect.height * 0.9f, 0f);
                drop.transform.localScale = Vector3.one * 0.12f;
                float a = Random.Range(0f, Mathf.PI * 2f);
                _drops.Add((drop, new Vector2(Mathf.Cos(a) * 2.2f, Random.Range(2.5f, 3.5f)), 0f));
            }
        }
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var (drop, velocity, age) = _drops[i];
            age += dt;
            velocity.y -= 9f * dt;
            drop.transform.position += (Vector3)(velocity * dt);
            var c = drop.color;
            c.a = 0.9f * (1f - age / 0.7f);
            drop.color = c;
            if (age >= 0.7f)
            {
                Destroy(drop.gameObject);
                _drops.RemoveAt(i);
            }
            else
            {
                _drops[i] = (drop, velocity, age);
            }
        }
    }

    #endregion

    #region 탭

    private void HandleTaps()
    {
        if (DecorModePresenter.IsActive || RelocationModePresenter.IsOpen || (_sheet != null && _sheet.IsOpen)
            || (GameManager.Instance != null && GameManager.Instance.IsModalOpen))
            return;
        if (!PlazaTapInput.TryGetTap(out var world))
            return;
        var cell = _board.WorldToCell(world);
        var placed = DecorManager.Instance.GetLayout(_board.Board).GetAt(cell);
        if (placed == null || !(placed.Decor is BuildingDefinition building))
            return;
        var record = _manager.BuildingRecordOf(placed.InstanceId);
        if (record != null && !record.Built)
            return;
        if (building.FarmPart == FarmPartKind.Field && _manager.Farm.Field(placed.InstanceId) != null)
        {
            if (_sheet == null)
                _sheet = new GameObject(nameof(PlazaFarmSheet)).AddComponent<PlazaFarmSheet>();
            _sheet.Open(_manager, placed.InstanceId);
        }
        else if (building.FarmPart == FarmPartKind.Crate)
        {
            var taken = _manager.CollectCrate(placed.InstanceId);
            if (taken.Count == 0)
                GameNotices.Post(new GameNotice(PlazaFarm.Count(_manager.Farm.Crate(placed.InstanceId)) > 0
                    ? "가방이 가득해서 못 거뒀어요" : "상자가 비어 있어요"));
        }
    }

    private void HandleCollected(int crateId, List<FarmStackSaveData> taken)
    {
        if (!_crates.TryGetValue(crateId, out var visual))
            return;
        int total = 0;
        foreach (var stack in taken)
            total += stack.count;
        FloatText(visual.Root.position + Vector3.up * 0.4f, $"+{total}");
    }

    private void FloatText(Vector3 at, string text)
    {
        var tmp = new GameObject("Float").AddComponent<TextMeshPro>();
        tmp.transform.SetParent(transform, false);
        tmp.transform.position = at;
        if (RuntimeUIKit.Style.TitleFont != null)
            tmp.font = RuntimeUIKit.Style.TitleFont;
        tmp.text = text;
        tmp.fontSize = 6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(75, 46, 34, 255);
        tmp.sortingOrder = 30000;
        tmp.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
        tmp.gameObject.AddComponent<PlazaFarmFloat>();
    }

    #endregion

    #region 농부

    private enum WorkPhase { Idle, ToField, Harvesting, ToCrate, Waiting }

    private OtterWanderAgent _worker;
    private QuarterRigView _workerRig;
    private SpriteRenderer _carried;
    private WorkPhase _phase;
    private int _targetField;
    private int _targetCrate;
    private float _phaseTimer;
    private string _carryId;
    private int _carryAmount;

    private void UpdateWorker(float dt)
    {
        var agent = _manager.HasFarmWorker ? _plaza.FindAgent(_manager.FarmConfig.FarmerOtterId) : null;
        if (agent != _worker)
        {
            _worker = agent;
            _workerRig = agent != null ? agent.GetComponentInChildren<QuarterRigView>() : null;
            _phase = WorkPhase.Idle;
            if (agent != null)
            {
                agent.TaskSpeed = pos => FieldWorkSpeed * (_manager.IsPathCell(_board.WorldToCell(pos)) ? _manager.PathSpeed : 1f);
                if (_carried == null)
                {
                    _carried = NewRenderer("Carried", transform, 0);
                    _carried.enabled = false;
                }
            }
        }
        if (_worker == null)
            return;

        _phaseTimer -= dt;
        switch (_phase)
        {
            case WorkPhase.Idle:
                if (_phaseTimer > 0f)
                    break;
                _phaseTimer = 1f;
                if (TryPickField(out _targetField, out var stand, out var look) && FreeCrate(out _))
                {
                    _phase = WorkPhase.ToField;
                    _worker.AssignTask(stand, look);
                }
                else
                {
                    _worker.ClearTask();
                }
                break;

            case WorkPhase.ToField:
                if (!_manager.Farm.Field(_targetField)?.ready ?? true)
                {
                    _phase = WorkPhase.Idle;
                    break;
                }
                if (_worker.IsOnTask)
                {
                    _phase = WorkPhase.Harvesting;
                    _phaseTimer = _manager.FarmConfig.HarvestSeconds;
                    var crop = _manager.FindCrop(_manager.Farm.Field(_targetField).cropId);
                    if (_workerRig != null && crop != null && crop.grownSprites != null && crop.grownSprites.Length > 0)
                        _workerRig.PlayHarvest(crop.grownSprites[0]);
                }
                break;

            case WorkPhase.Harvesting:
                if (_phaseTimer > 0f)
                    break;
                if (_manager.HarvestField(_targetField, out var harvested, out int amount))
                {
                    _carryId = harvested.cropId;
                    _carryAmount = amount;
                    _carried.sprite = harvested.grownSprites != null && harvested.grownSprites.Length > 0 ? harvested.grownSprites[0] : null;
                    _carried.enabled = _carried.sprite != null;
                    GoToCrate();
                }
                else
                {
                    _phase = WorkPhase.Idle;
                }
                break;

            case WorkPhase.ToCrate:
                if (_worker.IsOnTask)
                {
                    int put = _manager.DepositToCrate(_targetCrate, _carryId, _carryAmount);
                    _carryAmount -= put;
                    if (_carryAmount <= 0)
                    {
                        _carried.enabled = false;
                        _phase = WorkPhase.Idle;
                        _phaseTimer = 0.3f;
                    }
                    else
                    {
                        GoToCrate();
                    }
                }
                break;

            case WorkPhase.Waiting:
                if (_phaseTimer <= 0f)
                    GoToCrate();
                break;
        }

        // 든 작물은 머리 위
        if (_carried != null && _carried.enabled)
        {
            _carried.transform.position = _worker.transform.position + new Vector3(0f, 1.9f + Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.06f, 0f);
            _carried.transform.localScale = Vector3.one * (0.55f / Mathf.Max(0.01f, _carried.sprite.bounds.size.y));
            _carried.sortingOrder = PlazaDepth.SortingOrderFor(_worker.transform.position.y) + 50;
        }
    }

    private void GoToCrate()
    {
        if (FreeCrate(out _targetCrate) && TryStandOf(_targetCrate, out var stand, out var look))
        {
            _phase = WorkPhase.ToCrate;
            _worker.AssignTask(stand, look);
        }
        else
        {
            _phase = WorkPhase.Waiting;
            _phaseTimer = 2f;
            _worker.ClearTask();
        }
    }

    // 농부에게서 가장 가까운 다 자란 밭
    private bool TryPickField(out int fieldId, out Vector2 stand, out Vector2 look)
    {
        fieldId = 0;
        stand = look = default;
        float best = float.MaxValue;
        foreach (var field in _manager.Farm.Fields)
        {
            if (!field.ready || !TryStandOf(field.instanceId, out var s, out var l))
                continue;
            float d = ((Vector2)_worker.transform.position - s).sqrMagnitude;
            if (d < best)
            {
                best = d;
                fieldId = field.instanceId;
                stand = s;
                look = l;
            }
        }
        return fieldId != 0;
    }

    // 농부에게서 가장 가까운 자리가 남은 상자
    private bool FreeCrate(out int crateId)
    {
        crateId = 0;
        float best = float.MaxValue;
        int capacity = _manager.CrateCapacity;
        foreach (var crate in _manager.Farm.Crates)
        {
            if (PlazaFarm.Count(crate) >= capacity || !TryStandOf(crate.instanceId, out var s, out _))
                continue;
            float d = ((Vector2)_worker.transform.position - s).sqrMagnitude;
            if (d < best)
            {
                best = d;
                crateId = crate.instanceId;
            }
        }
        return crateId != 0;
    }

    // 부품 앞 (아래쪽 가운데)에 서서 부품을 봄
    private bool TryStandOf(int instanceId, out Vector2 stand, out Vector2 look)
    {
        stand = look = default;
        if (!DecorManager.Instance.GetLayout(_board.Board).TryGet(instanceId, out var placed))
            return false;
        var rect = _board.AreaWorldRect(placed.Area);
        look = rect.center;
        // 아래 → 양옆 → 위 순서로, 해달이 설 수 있는 가장 가까운 곳 (부품 둘레는 몸 너비만큼 막혀 있음)
        foreach (float gap in StandGaps)
        {
            foreach (var point in new[]
            {
                new Vector2(rect.center.x, rect.yMin - gap),
                new Vector2(rect.xMin - gap, rect.center.y),
                new Vector2(rect.xMax + gap, rect.center.y),
                new Vector2(rect.center.x, rect.yMax + gap),
            })
            {
                if (_board.IsWalkable(point))
                {
                    stand = point;
                    return true;
                }
            }
        }
        return false;
    }

    private static readonly float[] StandGaps = { 0.5f, 0.75f, 1f, 1.5f, 2f };

    #endregion
}

/// <summary>떠올랐다 사라지는 글자 (거둔 수)</summary>
public class PlazaFarmFloat : MonoBehaviour
{
    private float _age;
    private TextMeshPro _text;

    private void Awake() => _text = GetComponent<TextMeshPro>();

    private void Update()
    {
        _age += Time.deltaTime;
        transform.position += Vector3.up * Time.deltaTime * 1.2f;
        var c = _text.color;
        c.a = 1f - Mathf.Clamp01((_age - 0.6f) / 0.5f);
        _text.color = c;
        if (_age > 1.1f)
            Destroy(gameObject);
    }
}
