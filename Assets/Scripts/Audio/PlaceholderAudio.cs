using System;
using UnityEngine;

/// <summary>
/// 실제 음원이 생기기 전 확인용으로 코드로 만드는 임시 소리 (에셋 파일 없음).
/// 배경음: C–Am–F–G 아르페지오 + 베이스, 약 20초 반복. 효과음: 짧은 "띵" + 종류별 짧은 소리(CreateSfx).
/// </summary>
public static class PlaceholderAudio
{
    private const int SampleRate = 22050;
    private const float Bpm = 96f;
    private const int BeatsPerChord = 8; // 2마디
    private const float Peak = 0.6f;

    // 코드 구성음 (MIDI 번호)
    private static readonly int[][] Chords =
    {
        new[] { 60, 64, 67, 72 }, // C
        new[] { 57, 60, 64, 69 }, // Am
        new[] { 53, 57, 60, 65 }, // F
        new[] { 55, 59, 62, 67 }, // G
    };

    // 8분음표마다 짚을 구성음 순서
    private static readonly int[] ArpPattern = { 0, 1, 2, 3, 2, 1, 2, 1 };

    public static AudioClip CreateBgm()
    {
        float beat = 60f / Bpm;
        float eighth = beat / 2f;
        int totalBeats = Chords.Length * BeatsPerChord;
        var samples = new float[Mathf.CeilToInt(totalBeats * beat * SampleRate)];

        for (int c = 0; c < Chords.Length; c++)
        {
            var chord = Chords[c];
            float chordStart = c * BeatsPerChord * beat;

            // 베이스: 박마다 근음 (두 옥타브 아래, 부드러운 삼각파)
            for (int b = 0; b < BeatsPerChord; b++)
                AddNote(samples, chordStart + b * beat, beat * 0.9f, chord[0] - 24, 0.35f, Triangle, 0.8f);

            // 아르페지오: 8분음표 (한 옥타브 위, 사인파, 짧게 감쇠)
            for (int e = 0; e < BeatsPerChord * 2; e++)
            {
                int note = chord[ArpPattern[e % ArpPattern.Length]] + 12;
                AddNote(samples, chordStart + e * eighth, eighth * 1.6f, note, 0.22f, Sine, 4f);
            }
        }

        Normalize(samples, Peak);
        var clip = AudioClip.Create("PlaceholderBgm", samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>효과음 크기 확인용 "띵"</summary>
    public static AudioClip CreateDing()
    {
        const float length = 0.18f;
        var samples = new float[Mathf.CeilToInt(length * SampleRate)];
        AddNote(samples, 0f, length, 84, 0.5f, Sine, 18f);  // C6
        AddNote(samples, 0.03f, length - 0.03f, 91, 0.3f, Sine, 18f); // G6

        Normalize(samples, Peak);
        var clip = AudioClip.Create("PlaceholderDing", samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>효과음 종류마다 다른 짧은 임시 소리 (실제 음원이 생기면 AudioManager 목록에 넣어 대신함)</summary>
    public static AudioClip CreateSfx(SfxKind kind)
    {
        float length;
        float[] samples;
        switch (kind)
        {
            case SfxKind.Click: // 아주 짧고 부드러운 톡
                samples = Buffer(length = 0.06f);
                AddNote(samples, 0f, length, 79, 0.4f, Sine, 70f);
                break;
            case SfxKind.Harvest: // 뽁: 아래에서 위로 두 음
                samples = Buffer(length = 0.2f);
                AddNote(samples, 0f, 0.09f, 72, 0.5f, Triangle, 25f);
                AddNote(samples, 0.06f, length - 0.06f, 79, 0.45f, Sine, 20f);
                break;
            case SfxKind.Fish: // 첨벙: 잡음이 퍼지고 맑은 음 하나
                samples = Buffer(length = 0.3f);
                AddNote(samples, 0f, 0.2f, 60, 0.35f, Noise, 22f);
                AddNote(samples, 0.08f, length - 0.08f, 83, 0.35f, Sine, 14f);
                break;
            case SfxKind.Find: // 반짝: 빠른 세 음
                samples = Buffer(length = 0.32f);
                AddNote(samples, 0f, 0.2f, 84, 0.35f, Sine, 16f);
                AddNote(samples, 0.06f, 0.2f, 88, 0.35f, Sine, 16f);
                AddNote(samples, 0.12f, length - 0.12f, 91, 0.35f, Sine, 12f);
                break;
            case SfxKind.Coin: // 띠링
                samples = Buffer(length = 0.28f);
                AddNote(samples, 0f, 0.08f, 88, 0.4f, Sine, 20f);
                AddNote(samples, 0.07f, length - 0.07f, 93, 0.4f, Sine, 12f);
                break;
            case SfxKind.Upgrade: // 올라가는 네 음
                samples = Buffer(length = 0.45f);
                for (int i = 0; i < 4; i++)
                    AddNote(samples, i * 0.07f, length - i * 0.07f, new[] { 72, 76, 79, 84 }[i], 0.3f, Triangle, 9f);
                break;
            case SfxKind.LevelUp: // 짧은 팡파르: 올라가는 음 + 마지막 화음
                samples = Buffer(length = 0.9f);
                AddNote(samples, 0f, 0.15f, 72, 0.3f, Triangle, 8f);
                AddNote(samples, 0.12f, 0.15f, 76, 0.3f, Triangle, 8f);
                AddNote(samples, 0.24f, 0.15f, 79, 0.3f, Triangle, 8f);
                foreach (int note in new[] { 72, 76, 79, 84 })
                    AddNote(samples, 0.36f, length - 0.36f, note, 0.22f, Sine, 4f);
                break;
            case SfxKind.Complete: // 맑은 화음 하나
                samples = Buffer(length = 0.7f);
                foreach (int note in new[] { 79, 84, 88 })
                    AddNote(samples, 0f, length, note, 0.25f, Sine, 5f);
                break;
            case SfxKind.ShellTap: // 톡: 짧고 단단한 소리
                samples = Buffer(length = 0.09f);
                AddNote(samples, 0f, length, 76, 0.45f, Triangle, 45f);
                AddNote(samples, 0f, 0.04f, 60, 0.3f, Noise, 80f);
                break;
            case SfxKind.ShellCrack: // 쩍: 잡음이 터지고 낮은 음
                samples = Buffer(length = 0.32f);
                AddNote(samples, 0f, 0.18f, 60, 0.5f, Noise, 18f);
                AddNote(samples, 0f, length, 55, 0.35f, Triangle, 10f);
                break;
            case SfxKind.RevealCommon: // 퐁: 맑은 두 음
                samples = Buffer(length = 0.4f);
                AddNote(samples, 0f, 0.2f, 79, 0.35f, Sine, 10f);
                AddNote(samples, 0.08f, length - 0.08f, 84, 0.35f, Sine, 8f);
                break;
            case SfxKind.RevealRare: // 반짝반짝: 올라가는 다섯 음
                samples = Buffer(length = 0.7f);
                for (int i = 0; i < 5; i++)
                    AddNote(samples, i * 0.06f, length - i * 0.06f, new[] { 79, 83, 86, 91, 95 }[i], 0.24f, Sine, 7f);
                break;
            case SfxKind.RevealEpic: // 팡파르: 올라가는 음 + 큰 화음 (효과음은 1초 안)
                samples = Buffer(length = 0.95f);
                for (int i = 0; i < 4; i++)
                    AddNote(samples, i * 0.07f, 0.18f, new[] { 72, 76, 79, 84 }[i], 0.28f, Triangle, 7f);
                foreach (int note in new[] { 72, 79, 84, 88, 91 })
                    AddNote(samples, 0.3f, length - 0.3f, note, 0.2f, Sine, 3.2f);
                break;
            case SfxKind.RevealUpgrade: // 승급: 빠르게 올라가는 음
                samples = Buffer(length = 0.6f);
                for (int i = 0; i < 8; i++)
                    AddNote(samples, i * 0.045f, length - i * 0.045f, 72 + i * 3, 0.2f, Sine, 9f);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Normalize(samples, kind == SfxKind.Click ? Peak * 0.5f : Peak);
        var clip = AudioClip.Create($"PlaceholderSfx_{kind}", samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

    // 음 하나를 더함: 5ms 동안 올라가고 exp(-decay·t)로 감쇠, 끝 10ms는 0으로 모아 딸깍 소리 방지
    private static void AddNote(float[] samples, float start, float duration, int midi, float gain, Func<float, float> wave, float decay)
    {
        float frequency = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        int from = Mathf.FloorToInt(start * SampleRate);

        // 곡 끝을 넘는 음은 끝에서 줄여서 끊음 (반복 이음새에서 딸깍 소리 방지)
        duration = Mathf.Min(duration, (float)(samples.Length - from) / SampleRate);
        int count = Mathf.CeilToInt(duration * SampleRate);
        const float attack = 0.005f;
        const float release = 0.01f;

        for (int i = 0; i < count; i++)
        {
            int index = from + i;
            if (index >= samples.Length)
                break;

            float t = (float)i / SampleRate;
            float envelope = Mathf.Min(1f, t / attack) * Mathf.Exp(-decay * t) * Mathf.Clamp01((duration - t) / release);
            float phase = t * frequency;
            samples[index] += wave(phase - Mathf.Floor(phase)) * gain * envelope;
        }
    }

    private static float Sine(float phase01) => Mathf.Sin(phase01 * 2f * Mathf.PI);
    private static float Triangle(float phase01) => 1f - 4f * Mathf.Abs(phase01 - 0.5f);

    // 첨벙 소리용 잡음 (늘 같은 소리가 나게 위상으로 정해지는 의사 난수)
    private static float Noise(float phase01)
    {
        float x = Mathf.Sin(phase01 * 12.9898f + _noiseStep++ * 78.233f) * 43758.5453f;
        return (x - Mathf.Floor(x)) * 2f - 1f;
    }

    private static int _noiseStep;

    private static void Normalize(float[] samples, float peak)
    {
        float max = 0f;
        foreach (var s in samples)
            max = Mathf.Max(max, Mathf.Abs(s));
        if (max <= 0f)
            return;

        float scale = peak / max;
        for (int i = 0; i < samples.Length; i++)
            samples[i] *= scale;
    }
}
