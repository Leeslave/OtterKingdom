using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 광산 월드 효과 (모아 둔 그림을 돌려 씀): 파편 · 불꽃 · 빛 고리 · 빛 퍼짐, 바위에서 튀어나와 땅에 통 튀는 광석,
/// 떠오르는 글자(쾅! · 콤보 · +3), 카메라 흔들림. 규칙은 모른다
/// </summary>
public class MineFx : MonoBehaviour
{
    private const int ParticleOrder = 900;

    private class Particle
    {
        public SpriteRenderer Renderer;
        public Vector2 Velocity;
        public float Gravity;
        public float Spin;
        public float Age;
        public float Life;
        public float StartScale;
        public float EndScale;
        public Color Color;
        public bool Grow;
    }

    private class OrePop
    {
        public SpriteRenderer Renderer;
        public Vector2 Velocity;
        public float GroundY;
        public float Age;
        public int Bounces;
        public float Size;
        public Action<Vector3> Landed;
        public bool Done;
    }

    private class Floater
    {
        public TextMeshPro Text;
        public float Age;
        public float Life;
        public Vector3 Start;
        public float Rise;
        public float Scale;
    }

    private MineReworkArt _art;
    private Camera _camera;
    private Vector3 _cameraBase;
    private float _shake;
    private TMP_FontAsset _font;
    private readonly List<Particle> _particles = new List<Particle>();
    private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
    private readonly List<OrePop> _pops = new List<OrePop>();
    private readonly List<Floater> _floaters = new List<Floater>();
    private readonly Stack<TextMeshPro> _textPool = new Stack<TextMeshPro>();

    public void Setup(MineReworkArt art, Camera camera, TMP_FontAsset font)
    {
        _art = art;
        _camera = camera;
        _cameraBase = camera.transform.position;
        _font = font;
    }

    /// <summary>카메라 기준 위치가 바뀜 (화면 맞춤)</summary>
    public void SetCameraBase(Vector3 position) => _cameraBase = position;

    #region 파편 · 빛

    /// <summary>바위를 칠 때 튀는 돌 파편</summary>
    public void Chips(Vector2 at, Color color, int count, float power = 1f)
    {
        for (int i = 0; i < count; i++)
        {
            float a = UnityEngine.Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(2.5f, 5f) * power;
            Emit(_art.Chip, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, 14f, UnityEngine.Random.Range(-720f, 720f),
                UnityEngine.Random.Range(0.45f, 0.7f), UnityEngine.Random.Range(0.6f, 1.1f), 0.4f, color);
        }
    }

    /// <summary>번쩍이는 불꽃 (치명타 · 반짝 광맥 · 희귀)</summary>
    public void Sparks(Vector2 at, Color color, int count, float power = 1f, float size = 0.5f)
    {
        for (int i = 0; i < count; i++)
        {
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float speed = UnityEngine.Random.Range(1.5f, 4f) * power;
            Emit(_art.Spark, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, 0f, UnityEngine.Random.Range(-180f, 180f),
                UnityEngine.Random.Range(0.35f, 0.6f), size * UnityEngine.Random.Range(0.7f, 1.3f), 0f, color);
        }
    }

    /// <summary>퍼지는 빛 고리</summary>
    public void Ring(Vector2 at, Color color, float size = 2.5f, float life = 0.45f)
    {
        var p = Emit(_art.Ring, at, Vector2.zero, 0f, 0f, life, 0.3f, size, color);
        p.Grow = true;
    }

    /// <summary>잠깐 번지는 빛</summary>
    public void Glow(Vector2 at, Color color, float size = 3f, float life = 0.35f)
    {
        var p = Emit(_art.Glow, at, Vector2.zero, 0f, 0f, life, size * 0.6f, size, color);
        p.Grow = true;
    }

    /// <summary>천천히 떠오르는 빛가루 (반짝 광맥 둘레 · 분위기)</summary>
    public void Mote(Vector2 at, Color color, float size = 0.25f, float life = 1.6f)
    {
        Emit(_art.Mote, at, new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), UnityEngine.Random.Range(0.4f, 0.8f)), 0f, 0f,
            life, size, size * 0.3f, color);
    }

    private Particle Emit(Sprite sprite, Vector2 at, Vector2 velocity, float gravity, float spin, float life,
        float startScale, float endScale, Color color)
    {
        var renderer = _pool.Count > 0 ? _pool.Pop() : CreateRenderer("Fx");
        renderer.gameObject.SetActive(true);
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = ParticleOrder;
        renderer.transform.position = new Vector3(at.x, at.y, 0f);
        renderer.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        var p = new Particle
        {
            Renderer = renderer, Velocity = velocity, Gravity = gravity, Spin = spin, Life = life,
            StartScale = startScale, EndScale = endScale, Color = color,
        };
        _particles.Add(p);
        UpdateParticle(p, 0f);
        return p;
    }

    private SpriteRenderer CreateRenderer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.AddComponent<SpriteRenderer>();
    }

    private void UpdateParticle(Particle p, float dt)
    {
        p.Age += dt;
        p.Velocity.y -= p.Gravity * dt;
        var t = p.Renderer.transform;
        t.position += (Vector3)(p.Velocity * dt);
        t.Rotate(0f, 0f, p.Spin * dt);
        float k = Mathf.Clamp01(p.Age / p.Life);
        float scale = p.Grow ? Mathf.Lerp(p.StartScale, p.EndScale, 1f - (1f - k) * (1f - k)) : Mathf.Lerp(p.StartScale, p.EndScale, k * k);
        t.localScale = Vector3.one * scale;
        var c = p.Color;
        c.a *= p.Grow ? 1f - k : 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
        p.Renderer.color = c;
    }

    #endregion

    /// <summary>곡괭이가 바위를 내리침 (탭한 바위 오른쪽 위에서 휙). size = 월드 크기 (그림 해상도와 상관없이)</summary>
    public void Swing(Vector2 at, Sprite pickaxe, float size = 1.1f)
    {
        float native = pickaxe != null ? Mathf.Max(pickaxe.bounds.size.x, pickaxe.bounds.size.y) : 1f;
        float scale = size / Mathf.Max(0.01f, native);
        var p = Emit(pickaxe, at + new Vector2(0.55f, 0.5f), Vector2.zero, 0f, 0f, 0.18f, scale, scale, Color.white);
        p.Renderer.sortingOrder = ParticleOrder + 2;
        p.Renderer.transform.rotation = Quaternion.Euler(0f, 0f, 55f);
        p.Spin = -480f;
    }

    #region 광석이 튀어나옴

    /// <summary>바위에서 광석이 튀어나와 땅에 통통 튀고, 멈추면 landed(그 위치)를 부름 (거기서 UI로 날아감)</summary>
    public void PopOre(Vector2 from, Sprite icon, float delay, Action<Vector3> landed, float size = 0.75f)
    {
        var renderer = _pool.Count > 0 ? _pool.Pop() : CreateRenderer("Ore");
        renderer.gameObject.SetActive(false);
        renderer.sprite = icon;
        renderer.color = Color.white;
        renderer.sortingOrder = ParticleOrder + 1;
        renderer.transform.position = new Vector3(from.x, from.y + 0.4f, 0f);
        renderer.transform.rotation = Quaternion.identity;
        float a = UnityEngine.Random.Range(55f, 125f) * Mathf.Deg2Rad;
        float speed = UnityEngine.Random.Range(4.5f, 6.5f);
        float iconSize = icon != null ? Mathf.Max(icon.bounds.size.x, icon.bounds.size.y) : 1f;
        _pops.Add(new OrePop
        {
            Renderer = renderer,
            Velocity = new Vector2(Mathf.Cos(a) * speed * 0.55f, Mathf.Sin(a) * speed),
            GroundY = from.y - UnityEngine.Random.Range(0.1f, 0.6f),
            Age = -delay,
            Size = size / iconSize,
            Landed = landed,
        });
    }

    private void UpdatePop(OrePop p, float dt)
    {
        p.Age += dt;
        if (p.Age < 0f)
            return;
        var t = p.Renderer.transform;
        if (!p.Renderer.gameObject.activeSelf)
            p.Renderer.gameObject.SetActive(true);
        float popIn = Mathf.Clamp01(p.Age / 0.12f);
        p.Velocity.y -= 22f * dt;
        var pos = t.position + (Vector3)(p.Velocity * dt);
        if (pos.y < p.GroundY && p.Velocity.y < 0f)
        {
            pos.y = p.GroundY;
            p.Bounces++;
            p.Velocity = new Vector2(p.Velocity.x * 0.5f, -p.Velocity.y * 0.38f);
            if (p.Bounces >= 2)
            {
                p.Done = true;
                p.Landed?.Invoke(pos);
                Recycle(p.Renderer);
                return;
            }
        }
        t.position = pos;
        float squash = p.Bounces > 0 ? 1f + Mathf.Sin(Mathf.Min(1f, p.Age * 6f) * Mathf.PI) * 0.08f : 1f;
        t.localScale = new Vector3(p.Size * popIn * squash, p.Size * popIn / squash, 1f);
    }

    #endregion

    #region 글자

    /// <summary>떠오르는 글자 (쾅! · 콤보 · +3)</summary>
    public void Text(Vector2 at, string text, Color color, float size = 6f, float life = 0.8f, float rise = 1.2f)
    {
        var tmp = _textPool.Count > 0 ? _textPool.Pop() : CreateText();
        tmp.gameObject.SetActive(true);
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = size;
        tmp.transform.position = at;
        _floaters.Add(new Floater { Text = tmp, Life = life, Start = at, Rise = rise, Scale = 1f });
    }

    private TextMeshPro CreateText()
    {
        var go = new GameObject("Text");
        go.transform.SetParent(transform, false);
        var tmp = go.AddComponent<TextMeshPro>();
        if (_font != null)
            tmp.font = _font;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(75, 46, 34, 255);
        tmp.sortingOrder = ParticleOrder + 5;
        tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
        return tmp;
    }

    private void UpdateFloater(Floater f, float dt)
    {
        f.Age += dt;
        float k = Mathf.Clamp01(f.Age / f.Life);
        float pop = k < 0.15f ? Mathf.Lerp(0.4f, 1.25f, k / 0.15f) : k < 0.3f ? Mathf.Lerp(1.25f, 1f, (k - 0.15f) / 0.15f) : 1f;
        f.Text.transform.position = f.Start + Vector3.up * (f.Rise * (1f - (1f - k) * (1f - k)));
        f.Text.transform.localScale = Vector3.one * pop;
        var c = f.Text.color;
        c.a = 1f - Mathf.Clamp01((k - 0.65f) / 0.35f);
        f.Text.color = c;
    }

    #endregion

    #region 카메라 흔들림

    public void Shake(float strength) => _shake = Mathf.Max(_shake, strength);

    #endregion

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            UpdateParticle(p, dt);
            if (p.Age >= p.Life)
            {
                Recycle(p.Renderer);
                _particles.RemoveAt(i);
            }
        }
        for (int i = _pops.Count - 1; i >= 0; i--)
        {
            UpdatePop(_pops[i], dt);
            if (_pops[i].Done)
                _pops.RemoveAt(i);
        }
        for (int i = _floaters.Count - 1; i >= 0; i--)
        {
            var f = _floaters[i];
            UpdateFloater(f, dt);
            if (f.Age >= f.Life)
            {
                f.Text.gameObject.SetActive(false);
                _textPool.Push(f.Text);
                _floaters.RemoveAt(i);
            }
        }

        if (_camera != null)
        {
            if (_shake > 0.001f)
            {
                _camera.transform.position = _cameraBase + (Vector3)(UnityEngine.Random.insideUnitCircle * _shake);
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.2f) * 0.86f;
            }
            else
            {
                _camera.transform.position = _cameraBase;
            }
        }
    }

    private void Recycle(SpriteRenderer renderer)
    {
        renderer.gameObject.SetActive(false);
        _pool.Push(renderer);
    }
}
