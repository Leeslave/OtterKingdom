using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 광장 바위·나무의 손맛 연출: 튀는 조각(돌 조각·나뭇잎·떨어지는 나뭇가지·열매)과 떠오르는 글자("+3 돌").
/// 조각은 미리 만든 SpriteRenderer를 돌려 쓴다. 바닥(groundY)에 닿으면 한 번 튕기고 사라진다.
/// </summary>
public class PlazaNodeFx : MonoBehaviour
{
    private const int PiecePool = 16;
    private const int TextPool = 3;
    private const float Gravity = 14f;
    private const float TextSeconds = 0.9f;
    private const float TextRise = 0.8f;

    [Header("구성 요소")]
    [Tooltip("떠오르는 글자 견본 (평소엔 꺼 둠, 복제해서 씀)")]
    [SerializeField] private TextMeshPro _textTemplate;

    [Tooltip("조각이 그려질 순서 (주변 소품보다 위)")]
    [SerializeField] private int _sortingOrder = 30;

    private class Piece
    {
        public SpriteRenderer Renderer;
        public Vector3 Velocity;
        public float Spin;
        public float Age;
        public float Life;
        public float GroundY;
        public bool Bounced;
        public bool Floaty;
    }

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
            if (p.Floaty)
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
            if (!p.Floaty && position.y < p.GroundY && p.Velocity.y < 0f)
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
        p.Floaty = false;
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
        p.Floaty = true;
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
        p.Renderer.gameObject.SetActive(true);
    }
}
