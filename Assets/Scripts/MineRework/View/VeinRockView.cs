using UnityEngine;

/// <summary>
/// 광맥 바위 하나의 모습: 바위(체력에 따라 금 가는 3단계) + 박힌 광맥 + 그림자, 반짝 광맥이면 뒤에서 금빛이 숨 쉼.
/// 맞으면 찌그러졌다 돌아오고 흔들리며 하얗게 번쩍, 체력 막대가 잠깐 보인다. 깨지면 잔해가 남았다 사라지고, 새로 생기면 땅에서 쑥.
/// 이 오브젝트 위치 = 바위 발밑. 규칙은 모른다 (MineReworkGame이 칸의 상태를 넘겨줌)
/// </summary>
public class VeinRockView : MonoBehaviour
{
    private const float PunchSeconds = 0.16f;
    private const float SpawnSeconds = 0.38f;
    private const float BreakSeconds = 0.55f;
    private const float BarSeconds = 1.4f;
    private static readonly Color ShinyGlow = new Color(1f, 0.85f, 0.35f, 0.9f);

    private MineReworkArt _art;
    private int _order;
    private Transform _body;
    private SpriteRenderer _rock;
    private SpriteRenderer _vein;
    private SpriteRenderer _flash;
    private SpriteRenderer _shadow;
    private SpriteRenderer _glow;
    private SpriteRenderer _rubble;
    private SpriteRenderer _barBack;
    private SpriteRenderer _barFill;
    private Color _tint = Color.white;

    private float _punch = -1f;
    private float _punchStrength;
    private float _spawn = -1f;
    private float _break = -1f;
    private float _bar = -1f;
    private float _barValue = 1f;
    private float _barShown = 1f;
    private float _idle;
    private float _wiggle;
    private bool _present;
    private bool _shiny;

    public bool Present => _present;
    public bool Shiny => _shiny;
    /// <summary>바위 가운데 (효과 · 광석이 튀어나오는 곳)</summary>
    public Vector2 Center => (Vector2)transform.position + Vector2.up * (0.6f * transform.lossyScale.y);
    /// <summary>체력 막대 위치</summary>
    public Vector2 Top => (Vector2)transform.position + Vector2.up * (1.75f * transform.lossyScale.y);

    public void Setup(MineReworkArt art, Material flash, Sprite white, int order)
    {
        _art = art;
        _order = order;
        _idle = Random.Range(0f, 4f);
        _shadow = Child("Shadow", transform, art.Glow, order - 2);
        _shadow.color = new Color(0.15f, 0.08f, 0.05f, 0.35f);
        _shadow.transform.localScale = new Vector3(1.05f, 0.32f, 1f);
        _shadow.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        _glow = Child("Glow", transform, art.Glow, order - 1);
        _glow.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        _body = new GameObject("Body").transform;
        _body.SetParent(transform, false);
        _rock = Child("Rock", _body, art.RockStage(0), order);
        _vein = Child("Vein", _body, null, order + 1);
        _flash = Child("Flash", _body, art.RockStage(0), order + 2);
        if (flash != null)
            _flash.sharedMaterial = flash;
        _flash.enabled = false;
        _rubble = Child("Rubble", transform, art.Rubble, order);
        _rubble.enabled = false;
        _barBack = Child("BarBack", transform, white, order + 10);
        _barFill = Child("BarFill", transform, white, order + 11);
        _barBack.color = new Color(0.29f, 0.18f, 0.13f, 0.85f);
        _barBack.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        _barBack.transform.localScale = new Vector3(1.3f, 0.16f, 1f);
        _barFill.transform.localPosition = new Vector3(-0.62f, 1.75f, 0f);
        SetBarVisible(false);
        SetVisible(false);
    }

    private static SpriteRenderer Child(string name, Transform parent, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    /// <summary>칸 상태대로 바로 맞춤 (애니메이션 없이)</summary>
    public void Show(MineRock rock, Color tint)
    {
        _tint = tint;
        _present = rock.Present;
        _shiny = rock.Present && rock.Shiny;
        SetVisible(_present);
        _break = -1f;
        _rubble.enabled = false;
        if (!_present)
            return;
        _vein.sprite = rock.Vein != null ? rock.Vein.VeinSprite : null;
        _vein.enabled = _vein.sprite != null;
        SetStage(rock);
        _rock.color = tint;
        _barValue = _barShown = rock.MaxHp > 0 ? rock.Hp / (float)rock.MaxHp : 1f;
    }

    /// <summary>새 바위가 땅에서 쑥 올라옴</summary>
    public void PlaySpawn(MineRock rock, Color tint)
    {
        Show(rock, tint);
        _spawn = 0f;
        _body.localScale = new Vector3(1f, 0f, 1f);
    }

    /// <summary>맞음: 찌그러짐 · 흔들림 · 번쩍 · 금 · 체력 막대</summary>
    public void PlayHit(MineRock rock, bool crit)
    {
        _punch = 0f;
        _punchStrength = crit ? 1.8f : 1f;
        _flash.enabled = _flash.sharedMaterial != null;
        _flash.sprite = _rock.sprite;
        SetStage(rock);
        _flash.sprite = _rock.sprite;
        _barValue = rock.MaxHp > 0 ? rock.Hp / (float)rock.MaxHp : 0f;
        _bar = 0f;
        SetBarVisible(true);
    }

    /// <summary>깨짐: 바위는 사라지고 잔해가 잠깐</summary>
    public void PlayBreak()
    {
        _present = false;
        _shiny = false;
        _body.gameObject.SetActive(false);
        _glow.enabled = false;
        _shadow.enabled = false;
        _rubble.enabled = true;
        _rubble.color = _tint;
        _break = 0f;
        _bar = -1f;
        SetBarVisible(false);
    }

    /// <summary>눌렀는지 (바위 모양 타원 안)</summary>
    public bool Contains(Vector2 world)
    {
        if (!_present)
            return false;
        float s = transform.lossyScale.x;
        var d = world - ((Vector2)transform.position + new Vector2(0f, 0.6f * s));
        return (d.x * d.x) / (1.2f * s * 1.2f * s) + (d.y * d.y) / (0.9f * s * 0.9f * s) <= 1f;
    }

    /// <summary>"여기 쳐 봐" 살짝 흔들기</summary>
    public void Wiggle() => _wiggle = 0.5f;

    private void SetStage(MineRock rock)
    {
        float k = rock.MaxHp > 0 ? rock.Hp / (float)rock.MaxHp : 1f;
        int stage = k > 0.66f ? 0 : k > 0.33f ? 1 : 2;
        _rock.sprite = _art.RockStage(stage);
    }

    private void SetVisible(bool visible)
    {
        _body.gameObject.SetActive(visible);
        _shadow.enabled = visible;
        _glow.enabled = visible && _shiny;
    }

    private void SetBarVisible(bool visible)
    {
        _barBack.enabled = visible;
        _barFill.enabled = visible;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _idle += dt;
        float sx = 1f, sy = 1f, rot = 0f, ox = 0f;

        if (_spawn >= 0f)
        {
            _spawn += dt;
            float k = Mathf.Clamp01(_spawn / SpawnSeconds);
            sy = k < 0.6f ? Mathf.Lerp(0f, 1.18f, k / 0.6f) : Mathf.Lerp(1.18f, 1f, (k - 0.6f) / 0.4f);
            sx = k < 0.6f ? Mathf.Lerp(0.7f, 0.92f, k / 0.6f) : Mathf.Lerp(0.92f, 1f, (k - 0.6f) / 0.4f);
            if (k >= 1f)
                _spawn = -1f;
        }
        if (_punch >= 0f)
        {
            _punch += dt;
            float k = Mathf.Clamp01(_punch / PunchSeconds);
            float wave = Mathf.Sin(k * Mathf.PI) * (1f - k * 0.3f);
            sx *= 1f + 0.12f * _punchStrength * wave;
            sy *= 1f - 0.14f * _punchStrength * wave;
            ox = Mathf.Sin(k * Mathf.PI * 5f) * 0.06f * _punchStrength * (1f - k);
            var c = Color.white;
            c.a = (1f - k) * 0.85f;
            _flash.color = c;
            if (k >= 1f)
            {
                _punch = -1f;
                _flash.enabled = false;
            }
        }
        if (_wiggle > 0f)
        {
            _wiggle -= dt;
            rot = Mathf.Sin(_wiggle * 40f) * 3f * (_wiggle / 0.5f);
        }
        if (_present)
        {
            // 숨 쉬듯 아주 조금
            float breathe = 1f + Mathf.Sin(_idle * 2.2f) * 0.012f;
            sy *= breathe;
            _body.localScale = new Vector3(sx, sy, 1f);
            _body.localPosition = new Vector3(ox, 0f, 0f);
            _body.localRotation = Quaternion.Euler(0f, 0f, rot);
            if (_shiny)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_idle * 3.2f);
                var g = ShinyGlow;
                g.a = 0.45f + 0.4f * pulse;
                _glow.color = g;
                _glow.transform.localScale = Vector3.one * (3.2f + 0.4f * pulse);
                _rock.color = Color.Lerp(_tint, new Color(1f, 0.93f, 0.7f), 0.35f + 0.15f * pulse);
            }
        }
        if (_break >= 0f)
        {
            _break += dt;
            float k = Mathf.Clamp01(_break / BreakSeconds);
            var c = _tint;
            c.a = 1f - Mathf.Clamp01((k - 0.4f) / 0.6f);
            _rubble.color = c;
            if (k >= 1f)
            {
                _break = -1f;
                _rubble.enabled = false;
            }
        }
        if (_bar >= 0f)
        {
            _bar += dt;
            _barShown = Mathf.MoveTowards(_barShown, _barValue, dt * 3f);
            float w = 1.24f * Mathf.Clamp01(_barShown);
            _barFill.transform.localScale = new Vector3(w, 0.1f, 1f);
            _barFill.transform.localPosition = new Vector3(-0.62f + w / 2f, 1.75f, 0f);
            _barFill.color = Color.Lerp(new Color(0.95f, 0.45f, 0.35f), new Color(0.55f, 0.85f, 0.42f), _barShown);
            if (_bar > BarSeconds)
            {
                _bar = -1f;
                SetBarVisible(false);
            }
        }
    }
}
