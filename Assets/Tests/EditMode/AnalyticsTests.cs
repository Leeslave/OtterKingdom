using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AnalyticsTests
{
    private class ListSink : IAnalyticsSink
    {
        public readonly List<string> Lines = new List<string>();
        public void Write(string jsonLine) => Lines.Add(jsonLine);
    }

    [Serializable]
    private class Parsed
    {
        public string @event;
        public string eventId;
        public string utc;
        public string installId;
        public string sessionId;
        public bool dev;
        public string itemId;
        public int amount;
        public string note;
    }

    private IAnalyticsSink _previousSink;
    private TimeSpan _previousOffset;

    [SetUp]
    public void SetUp()
    {
        _previousSink = AnalyticsLog.Sink;
        _previousOffset = GameClock.DevOffset;
    }

    [TearDown]
    public void TearDown()
    {
        AnalyticsLog.Sink = _previousSink;
        GameClock.DevOffset = _previousOffset;
    }

    [Test]
    public void Track_WritesOneJsonLine_WithCommonFields()
    {
        var sink = new ListSink();
        AnalyticsLog.Sink = sink;

        AnalyticsLog.Track("reward_claimed", ("itemId", "crop_carrot"), ("amount", 3), ("note", "따옴표\" 줄\n바꿈"));

        Assert.AreEqual(1, sink.Lines.Count);
        StringAssert.DoesNotContain("\n", sink.Lines[0], "한 줄");
        var parsed = JsonUtility.FromJson<Parsed>(sink.Lines[0]);
        Assert.AreEqual("reward_claimed", parsed.@event);
        Assert.AreEqual("crop_carrot", parsed.itemId);
        Assert.AreEqual(3, parsed.amount);
        Assert.AreEqual("따옴표\" 줄\n바꿈", parsed.note);
        Assert.IsTrue(parsed.dev, "에디터는 개발 기록");
        Assert.IsNotEmpty(parsed.eventId);
        Assert.AreEqual(AnalyticsLog.SessionId, parsed.sessionId);
        Assert.AreEqual(AnalyticsLog.InstallId, parsed.installId);
    }

    [Test]
    public void Track_EventIdsAreUnique()
    {
        var sink = new ListSink();
        AnalyticsLog.Sink = sink;
        AnalyticsLog.Track("a");
        AnalyticsLog.Track("a");

        var first = JsonUtility.FromJson<Parsed>(sink.Lines[0]);
        var second = JsonUtility.FromJson<Parsed>(sink.Lines[1]);
        Assert.AreNotEqual(first.eventId, second.eventId);
    }

    [Test]
    public void GameClock_FollowsDevOffset()
    {
        GameClock.DevOffset = TimeSpan.FromHours(5);
        double ahead = (GameClock.UtcNow - DateTime.UtcNow).TotalHours;
        Assert.AreEqual(5, ahead, 0.01);
    }

    [Test]
    public void SaveStamp_KeepsLaterTime_WhenClockTurnedBack()
    {
        var now = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
        string future = now.AddHours(8).ToString("o");

        Assert.AreEqual(future, SaveService.Stamp(future, now, true), "되돌린 시계: 앞서 저장한 시각을 유지");
        Assert.AreEqual(now.ToString("o"), SaveService.Stamp(future, now, false), "에디터: 그대로 지금");
        Assert.AreEqual(now.ToString("o"), SaveService.Stamp(now.AddHours(-1).ToString("o"), now, true), "보통: 지금");
        Assert.AreEqual(now.ToString("o"), SaveService.Stamp(null, now, true));
    }
}
