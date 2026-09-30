using NUnit.Framework;
using UnityEngine;

public class PlaceholderAudioTests
{
    [Test]
    public void CreateBgm_IsAudibleLoopWithoutClipping()
    {
        var clip = PlaceholderAudio.CreateBgm();
        try
        {
            Assert.AreEqual(1, clip.channels);
            Assert.That(clip.length, Is.InRange(15f, 25f), "약 20초 반복");

            var samples = new float[clip.samples];
            clip.GetData(samples, 0);
            float peak = 0f;
            foreach (var s in samples)
                peak = Mathf.Max(peak, Mathf.Abs(s));

            Assert.That(peak, Is.InRange(0.1f, 1f), "들리되 찢어지지 않음");
            Assert.Less(Mathf.Abs(samples[samples.Length - 1]), 0.05f, "반복 이음새에서 딸깍 소리가 나지 않게 끝이 0 근처");
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void CreateDing_IsShort()
    {
        var clip = PlaceholderAudio.CreateDing();
        try
        {
            Assert.Less(clip.length, 0.5f);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }
}
