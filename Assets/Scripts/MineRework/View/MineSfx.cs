using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광산 개편 테스트 씬 효과음. 음원이 없어 코드로 만든 임시 소리 (톡 · 쾅 · 와르르 · 뽁 · 띠링 · 반짝 · 팡파르).
/// 같은 소리를 빠르게 여러 번 낼 수 있게 소스 몇 개를 돌려 쓰고, 치는 소리는 콤보마다 조금씩 높아진다.
/// </summary>
public class MineSfx : MonoBehaviour
{
    public enum Kind { Hit, Crit, Break, Pop, Collect, Rare, Discover, Jackpot, Craft, Boulder, Tap, Cheer, Sell, Open }

    private const int SampleRate = 44100;
    private const int Voices = 6;

    [Header("소리")]
    [Tooltip("전체 크기")]
    [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;

    private readonly Dictionary<Kind, AudioClip> _clips = new Dictionary<Kind, AudioClip>();
    private readonly Dictionary<Kind, float> _last = new Dictionary<Kind, float>();
    private AudioSource[] _sources;
    private int _next;

    private void Awake() => Ensure();

    // 다른 것의 Awake에서 먼저 불려도 되게 (MineReworkGame.Awake가 꺼 둔 동안 창을 띄울 때)
    private void Ensure()
    {
        if (_sources != null)
            return;
        _sources = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            _sources[i] = gameObject.AddComponent<AudioSource>();
            _sources[i].playOnAwake = false;
        }
        foreach (Kind kind in Enum.GetValues(typeof(Kind)))
            _clips[kind] = Create(kind);
    }

    /// <summary>소리를 낸다. pitch = 음 높이 배수, minGap = 같은 소리 최소 간격(초)</summary>
    public void Play(Kind kind, float pitch = 1f, float volume = 1f, float minGap = 0.03f)
    {
        Ensure();
        float now = Time.unscaledTime;
        if (_last.TryGetValue(kind, out float last) && now - last < minGap)
            return;
        _last[kind] = now;
        var source = _sources[_next];
        _next = (_next + 1) % _sources.Length;
        source.pitch = pitch;
        source.PlayOneShot(_clips[kind], _volume * volume);
    }

    #region 소리 만들기

    private delegate float Wave(float phase);

    private static float Sine(float p) => Mathf.Sin(p * Mathf.PI * 2f);
    private static float Triangle(float p) => 1f - 4f * Mathf.Abs(p - Mathf.Floor(p + 0.5f));
    private static float Square(float p) => p - Mathf.Floor(p) < 0.5f ? 0.6f : -0.6f;
    private static readonly System.Random NoiseRng = new System.Random(7);
    private static float Noise(float p) => (float)(NoiseRng.NextDouble() * 2.0 - 1.0);

    private static AudioClip Create(Kind kind)
    {
        float[] s;
        switch (kind)
        {
            case Kind.Hit: // 톡: 단단한 돌 소리 + 짧은 잡음
                s = Buffer(0.12f);
                Note(s, 0f, 0.12f, 52, 0.55f, Triangle, 38f, -6f);
                Note(s, 0f, 0.05f, 60, 0.45f, Noise, 70f);
                Note(s, 0f, 0.08f, 76, 0.18f, Sine, 50f);
                break;
            case Kind.Crit: // 쾅: 낮고 굵게 + 쇳소리
                s = Buffer(0.3f);
                Note(s, 0f, 0.28f, 40, 0.6f, Triangle, 14f, -10f);
                Note(s, 0f, 0.08f, 60, 0.55f, Noise, 40f);
                Note(s, 0.01f, 0.28f, 88, 0.16f, Sine, 12f);
                Note(s, 0.01f, 0.28f, 95, 0.1f, Sine, 12f);
                break;
            case Kind.Break: // 와르르
                s = Buffer(0.45f);
                Note(s, 0f, 0.35f, 60, 0.55f, Noise, 9f);
                Note(s, 0f, 0.4f, 36, 0.5f, Triangle, 9f, -6f);
                Note(s, 0.05f, 0.1f, 60, 0.25f, Noise, 40f);
                Note(s, 0.12f, 0.1f, 60, 0.2f, Noise, 40f);
                break;
            case Kind.Pop: // 뽁
                s = Buffer(0.12f);
                Note(s, 0f, 0.12f, 72, 0.45f, Sine, 26f, 9f);
                break;
            case Kind.Collect: // 띡
                s = Buffer(0.12f);
                Note(s, 0f, 0.12f, 88, 0.3f, Sine, 30f);
                Note(s, 0.03f, 0.09f, 95, 0.2f, Sine, 30f);
                break;
            case Kind.Rare: // 반짝반짝
                s = Buffer(0.8f);
                int[] rare = { 84, 88, 91, 96, 100 };
                for (int i = 0; i < rare.Length; i++)
                    Note(s, i * 0.055f, 0.8f - i * 0.055f, rare[i], 0.22f, Sine, 6f);
                break;
            case Kind.Discover: // 새 광석: 올라가고 화음
                s = Buffer(1.2f);
                int[] up = { 72, 76, 79, 84 };
                for (int i = 0; i < up.Length; i++)
                    Note(s, i * 0.09f, 0.25f, up[i], 0.3f, Triangle, 9f);
                foreach (int n in new[] { 84, 88, 91, 96 })
                    Note(s, 0.36f, 0.84f, n, 0.18f, Sine, 3.5f);
                break;
            case Kind.Jackpot: // 대박 팡파르
                s = Buffer(1.4f);
                int[] fan = { 72, 76, 79, 84, 79, 84, 88, 91 };
                for (int i = 0; i < fan.Length; i++)
                    Note(s, i * 0.08f, 0.22f, fan[i], 0.28f, Square, 10f);
                foreach (int n in new[] { 84, 88, 91, 96 })
                    Note(s, 0.64f, 0.76f, n, 0.16f, Triangle, 3f);
                break;
            case Kind.Craft: // 땅! 땅! 반짝
                s = Buffer(1.0f);
                for (int i = 0; i < 2; i++)
                {
                    Note(s, i * 0.18f, 0.25f, 64, 0.45f, Triangle, 14f);
                    Note(s, i * 0.18f, 0.25f, 88, 0.2f, Sine, 10f);
                    Note(s, i * 0.18f, 0.05f, 60, 0.3f, Noise, 60f);
                }
                foreach (int n in new[] { 79, 84, 88, 91 })
                    Note(s, 0.4f, 0.6f, n, 0.18f, Sine, 4f);
                break;
            case Kind.Boulder: // 쿵
                s = Buffer(0.35f);
                Note(s, 0f, 0.35f, 31, 0.7f, Triangle, 10f, -8f);
                Note(s, 0f, 0.1f, 60, 0.45f, Noise, 30f);
                break;
            case Kind.Tap: // 톡
                s = Buffer(0.07f);
                Note(s, 0f, 0.07f, 79, 0.35f, Sine, 60f);
                break;
            case Kind.Cheer: // 힘내!
                s = Buffer(0.4f);
                Note(s, 0f, 0.15f, 76, 0.3f, Triangle, 12f);
                Note(s, 0.1f, 0.3f, 83, 0.3f, Triangle, 10f);
                break;
            case Kind.Sell: // 짤랑
                s = Buffer(0.4f);
                Note(s, 0f, 0.1f, 88, 0.35f, Sine, 18f);
                Note(s, 0.06f, 0.34f, 95, 0.35f, Sine, 10f);
                Note(s, 0.12f, 0.28f, 100, 0.2f, Sine, 10f);
                break;
            default: // Open: 창 열림 휙
                s = Buffer(0.18f);
                Note(s, 0f, 0.18f, 67, 0.25f, Sine, 16f, 12f);
                break;
        }
        Normalize(s, 0.85f);
        var clip = AudioClip.Create("Mine_" + kind, s.Length, 1, SampleRate, false);
        clip.SetData(s, 0);
        return clip;
    }

    private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

    // midi 음, 감쇠(클수록 빨리 사라짐), glide = 음이 미끄러지는 반음 수 (+ 위로)
    private static void Note(float[] s, float start, float length, int midi, float amp, Wave wave, float decay, float glide = 0f)
    {
        int from = Mathf.Clamp((int)(start * SampleRate), 0, s.Length);
        int count = Mathf.Min(s.Length - from, (int)(length * SampleRate));
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float freq = 440f * Mathf.Pow(2f, (midi - 69 + glide * Mathf.Clamp01(t / Mathf.Max(0.01f, length))) / 12f);
            phase += freq / SampleRate;
            float attack = Mathf.Clamp01(t / 0.004f);
            s[from + i] += wave(phase) * amp * attack * Mathf.Exp(-decay * t);
        }
    }

    private static void Normalize(float[] s, float peak)
    {
        float max = 0f;
        foreach (float v in s)
            max = Mathf.Max(max, Mathf.Abs(v));
        if (max <= 0f)
            return;
        float k = Mathf.Min(1f, peak / max);
        for (int i = 0; i < s.Length; i++)
            s[i] *= k;
    }

    #endregion
}
