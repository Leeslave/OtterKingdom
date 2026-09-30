using System;
using UnityEngine;

/// <summary>
/// 실제 음원이 생기기 전 확인용으로 코드로 만드는 임시 소리 (에셋 파일 없음).
/// 배경음: C–Am–F–G 아르페지오 + 베이스, 약 20초 반복. 효과음: 짧은 "띵".
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
