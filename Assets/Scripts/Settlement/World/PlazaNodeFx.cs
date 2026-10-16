using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 광장 바위·나무의 손맛 연출: 튀는 조각(돌 조각·나뭇잎·떨어지는 나뭇가지·열매)과 떠오르는 글자("+3 돌").
/// 조각은 미리 만든 SpriteRenderer를 돌려 쓴다. 바닥(groundY)에 닿으면 한 번 튕기고 사라진다.
/// 나무 베기·바위 캐기: 누른 자리로 도끼·곡괭이가 내리찍히고(Swing), 맞은 자리 반짝(Impact), 먼지구름(Puff)
/// </summary>
public class PlazaNodeFx : MonoBehaviour
{
    private const int PiecePool = 32;
    private const int TextPool = 3;
    private const float Gravity = 14f;
    private const float TextSeconds = 0.9f;
    private const float TextRise = 0.8f;

    [Header("구성 요소")]
    [Tooltip("떠오르는 글자 견본 (평소엔 꺼 둠, 복제해서 씀)")]
    [SerializeField] private TextMeshPro _textTemplate;

    [Tooltip("조각이 그려질 순서 (주변 소품보다 위)")]
    [SerializeField] private int _sortingOrder = 30;

    [Header("손맛 그림 (나무 베기 · 바위 캐기)")]
    [Tooltip("나무를 칠 때 휘두르는 도끼 (세로 그림, 손잡이 끝이 아래 가운데)")]
    [SerializeField] private Sprite _axe;

    [Tooltip("바위를 칠 때 휘두르는 곡괭이 (세로 그림, 손잡이 끝이 아래 가운데)")]
    [SerializeField] private Sprite _pickaxe;

    [Tooltip("맞은 자리에서 반짝 터지는 별")]
    [SerializeField] private Sprite _impact;

    [Tooltip("먼지구름 (건설 연출과 같은 그림)")]
    [SerializeField] private Sprite _dust;

    [Tooltip("나무를 칠 때 튀는 나무 조각")]
    [SerializeField] private Sprite _woodChip;

    [Tooltip("휘두르는 도구 길이 (월드 단위)")]
    [SerializeField] private float _toolSize = 1.15f;

    [Tooltip("먼지구름에 곱하는 색 (흰색 = 그림 그대로)")]
    [SerializeField] private Color _dustColor = new Color(1f, 1f, 1f, 0.9f);

    // 조각의 움직임: 떨어짐(튕김) · 나뭇잎(흔들리며 내려옴) · 먼지(커지며 흐려짐) · 반짝(커졌다 작아짐)
    private enum Motion
    {
        Fall,
        Flutter,
        Puff,
        Pop,
    }

    private class Piece
    {
        public SpriteRenderer Renderer;
        public Vector3 Velocity;
        public float Spin;
        public float Age;
        public float Life;
        public float GroundY;
        public bool Bounced;
        public Motion Motion;
        public Vector3 BaseScale;
        public float BaseAlpha = 1f;
    }

    // 휘두르는 도구: 손 위치를 축으로 들어 올렸다가 맞은 자리로 내리찍음
    private const float StrikeSeconds = 0.07f;
    private const float HoldSeconds = 0.07f;
    private const float LiftSeconds = 0.16f;
    private const float RaisedAngle = -18f;
    private const float StrikeAngle = 62f;

    private Transform _toolRoot;
    private SpriteRenderer _tool;
    private float _toolAge = -1f;

    public Sprite WoodChip => _woodChip;

    private class FloatingText
    {
        public TextMeshPro Text;
        public Vector3 Start;
        public float Age = TextSeconds;
    }

    private readonly List<Piece> _pieces = new List<Piece>();
    private readonly List<FloatingText> _texts = new List<FloatingText>();

    private void Awake()
    {
        for (int i = 0; i < PiecePool; i++)
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = _sortingOrder;
            go.SetActive(false);
            _pieces.Add(new Piece { Renderer = renderer });
        }

        var toolRoot = new GameObject("Tool");
        toolRoot.transform.SetParent(transform, false);
        _toolRoot = toolRoot.transform;
        var tool = new GameObject("Sprite");
        tool.transform.SetParent(_toolRoot, false);
        _tool = tool.AddComponent<SpriteRenderer>();
        _tool.sortingOrder = _sortingOrder + 1;
        toolRoot.SetActive(false);

        _textTemplate.gameObject.SetActive(false);
        for (int i = 0; i < TextPool; i++)
        {
            var text = Instantiate(_textTemplate, _textTemplate.transform.parent);
            _texts.Add(new FloatingText { Text = text });
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (var p in _pieces)
        {
            if (!p.Renderer.gameObject.activeSelf)
                continue;
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                p.Renderer.gameObject.SetActive(false);
                continue;
            }

            var t = p.Renderer.transform;
            float k = p.Age / p.Life;
            if (p.Motion == Motion.Puff)
            {
                // 먼지: 살짝 떠오르며 커지고 흐려짐
                t.position += p.Velocity * dt;
                p.Velocity *= 1f - 2.5f * dt;
                t.localScale = p.BaseScale * (1f + 0.8f * Mathf.Sqrt(k));
                SetAlpha(p.Renderer, p.BaseAlpha * (1f - k) * (1f - k));
                continue;
            }
            if (p.Motion == Motion.Pop)
            {
                // 반짝: 빠르게 커졌다가 작아지며 돎
                t.localScale = p.BaseScale * Mathf.Sin(Mathf.Min(1f, k * 1.1f) * Mathf.PI) * 1.2f;
                t.Rotate(0f, 0f, 220f * dt);
                SetAlpha(p.Renderer, 1f - k * k);
                continue;
            }
            if (p.Motion == Motion.Flutter)
            {
                // 나뭇잎: 천천히 흔들리며 내려옴
                p.Velocity.y = Mathf.Max(p.Velocity.y - Gravity * 0.12f * dt, -1.2f);
                p.Velocity.x = Mathf.Sin(p.Age * 6f + p.Spin) * 0.8f;
            }
            else
            {
                p.Velocity.y -= Gravity * dt;
            }

            var position = t.position + p.Velocity * dt;
            if (p.Motion == Motion.Fall && position.y < p.GroundY && p.Velocity.y < 0f)
            {
                position.y = p.GroundY;
                if (!p.Bounced)
                {
                    p.Velocity = new Vector3(p.Velocity.x * 0.4f, -p.Velocity.y * 0.3f, 0f);
                    p.Bounced = true;
                }
                else
                {
                    p.Velocity = Vector3.zero;
                    p.Spin = 0f;
                }
            }
            t.position = position;
            t.Rotate(0f, 0f, p.Spin * dt);

            float fade = Mathf.Clamp01((p.Life - p.Age) / 0.25f);
            var color = p.Renderer.color;
            color.a = fade;
            p.Renderer.color = color;
        }

        AnimateTool(dt);

        foreach (var f in _texts)
        {
            if (f.Age >= TextSeconds)
                continue;
            f.Age += dt;
            float k = Mathf.Clamp01(f.Age / TextSeconds);
            f.Text.transform.position = f.Start + Vector3.up * (TextRise * k);
            var color = f.Text.color;
            color.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            f.Text.color = color;
            if (f.Age >= TextSeconds)
                f.Text.gameObject.SetActive(false);
        }
    }

    /// <summary>위로 튀었다가 groundY에 떨어지는 조각 (돌 조각, 나뭇가지, 열매)</summary>
    public void Burst(Sprite sprite, Vector3 from, Vector2 velocity, float size, float groundY, float life)
    {
        var p = Take();
        if (p == null || sprite == null)
            return;
        Show(p, sprite, from, size, life);
        p.Velocity = velocity;
        p.Spin = Random.Range(-360f, 360f);
        p.GroundY = groundY;
        p.Motion = Motion.Fall;
    }

    /// <summary>천천히 흔들리며 내려오는 나뭇잎</summary>
    public void Flutter(Sprite sprite, Vector3 from, float size, float life)
    {
        var p = Take();
        if (p == null || sprite == null)
            return;
        Show(p, sprite, from, size, life);
        p.Velocity = new Vector3(0f, Random.Range(0.2f, 0.8f), 0f);
        p.Spin = Random.Range(0f, 6.28f);
        p.Motion = Motion.Flutter;
    }

    /// <summary>
    /// 누른 자리로 도구를 내리찍음 (나무 = 도끼, 바위 = 곡괭이). 손은 맞은 자리 오른쪽 아래, 도구 머리가 맞은 자리에 닿음
    /// </summary>
    public void Swing(bool axe, Vector3 impact)
    {
        var sprite = axe ? _axe : _pickaxe;
        if (sprite == null)
            return;

        _tool.sprite = sprite;
        _tool.color = Color.white;
        float height = sprite.bounds.size.y;
        float scale = _toolSize / Mathf.Max(0.0001f, height);
        float parentScale = Mathf.Max(0.0001f, transform.lossyScale.x);
        _toolRoot.localScale = Vector3.one / parentScale;
        _tool.transform.localScale = Vector3.one * scale;
        _tool.transform.localPosition = new Vector3(0f, _toolSize * 0.5f, 0f);

        // 머리(손잡이 끝에서 0.8만큼)가 내리찍는 각도에서 맞은 자리에 오도록 손 위치를 정함
        float reach = _toolSize * 0.8f;
        float strike = StrikeAngle * Mathf.Deg2Rad;
        var hand = impact + new Vector3(Mathf.Sin(strike), -Mathf.Cos(strike), 0f) * reach;
        _toolRoot.position = new Vector3(hand.x, hand.y, impact.z);
        _toolRoot.rotation = Quaternion.Euler(0f, 0f, RaisedAngle);
        _toolRoot.gameObject.SetActive(true);
        _toolAge = 0f;
    }

    /// <summary>맞은 자리에서 반짝 (커졌다가 돌며 사라짐)</summary>
    public void Impact(Vector3 at, float size)
    {
        var p = Take();
        if (p == null || _impact == null)
            return;
        Show(p, _impact, at, size, 0.22f);
        p.Motion = Motion.Pop;
        p.BaseScale = p.Renderer.transform.localScale;
        p.Renderer.transform.localScale = Vector3.zero;
    }

    /// <summary>먼지구름 (살짝 떠오르며 커지고 흐려짐)</summary>
    public void Puff(Vector3 at, float size, Vector2 drift)
    {
        var p = Take();
        if (p == null || _dust == null)
            return;
        Show(p, _dust, at, size, Random.Range(0.55f, 0.8f));
        p.Motion = Motion.Puff;
        p.Velocity = drift;
        p.BaseScale = p.Renderer.transform.localScale;
        p.BaseAlpha = _dustColor.a;
        var color = _dustColor;
        p.Renderer.color = color;
    }

    private void AnimateTool(float dt)
    {
        if (_toolAge < 0f)
            return;
        _toolAge += dt;
        float angle;
        float alpha = 1f;
        if (_toolAge < StrikeSeconds)
        {
            float k = _toolAge / StrikeSeconds;
            angle = Mathf.Lerp(RaisedAngle, StrikeAngle, k * k);
        }
        else if (_toolAge < StrikeSeconds + HoldSeconds)
        {
            // 맞고 살짝 되튐
            float k = (_toolAge - StrikeSeconds) / HoldSeconds;
            angle = StrikeAngle - 7f * Mathf.Sin(k * Mathf.PI);
        }
        else
        {
            float k = (_toolAge - StrikeSeconds - HoldSeconds) / LiftSeconds;
            if (k >= 1f)
            {
                _toolRoot.gameObject.SetActive(false);
                _toolAge = -1f;
                return;
            }
            angle = StrikeAngle - 14f * k;
            alpha = 1f - k;
            _toolRoot.position += Vector3.up * (0.6f * dt);
        }
        _toolRoot.rotation = Quaternion.Euler(0f, 0f, angle);
        SetAlpha(_tool, alpha);
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        var color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }

    /// <summary>at에서 떠올랐다 사라지는 글자 (동시에 몇 개까지)</summary>
    public void ShowText(string text, Vector3 at, Color color)
    {
        FloatingText slot = null;
        foreach (var f in _texts)
        {
            if (slot == null || f.Age > slot.Age)
                slot = f;
        }
        slot.Age = 0f;
        slot.Start = at;
        slot.Text.text = text;
        color.a = 1f;
        slot.Text.color = color;
        slot.Text.transform.position = at;
        slot.Text.gameObject.SetActive(true);
    }

    private Piece Take()
    {
        foreach (var p in _pieces)
        {
            if (!p.Renderer.gameObject.activeSelf)
                return p;
        }
        return null;
    }

    private static void Show(Piece p, Sprite sprite, Vector3 from, float size, float life)
    {
        p.Renderer.sprite = sprite;
        p.Renderer.color = Color.white;
        var t = p.Renderer.transform;
        t.position = from;
        t.rotation = Quaternion.identity;
        float scale = size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        t.localScale = Vector3.one * scale / Mathf.Max(0.0001f, t.parent.lossyScale.x);
        p.Age = 0f;
        p.Life = life;
        p.Bounced = false;
        p.BaseAlpha = 1f;
        p.Renderer.gameObject.SetActive(true);
    }
}
