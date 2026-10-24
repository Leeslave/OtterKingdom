using System;
using System.Collections.Generic;

/// <summary>순간포착 장면의 종류 (기획: Docs/캐릭터행동기획/해달왕국_캐릭터행동과_순간포착.md)</summary>
public enum PlazaMomentKind
{
    SleepyBench, // 꾸벅이 — 졸음은 옮는 거야
    SecretTable, // 재잘이 — 우리끼리 비밀인데
    NightNotes,  // 끄적이 — 오늘의 모델
    LampMap,     // 두리번 — 아 이쪽이었구나
}

/// <summary>장면 하나: 주인공 해달이 어느 자리에 있을 때, 동료 몇 마리를 불러 어떤 말을 주고받는지</summary>
public sealed class PlazaMoment
{
    /// <summary>도감 항목 ID (순간 탭)</summary>
    public string EntryId { get; }
    /// <summary>주인공 해달 ID</summary>
    public string OtterId { get; }
    public PlazaMomentKind Kind { get; }
    /// <summary>주인공이 머물러야 하는 자리 (동료도 같은 자리로 옴)</summary>
    public PlazaSpotKind Spot { get; }
    /// <summary>불러올 동료 수</summary>
    public int Companions { get; }
    /// <summary>차례로 하는 말 (0 = 주인공, 1~ = 동료). 마지막 말과 함께 포착할 수 있게 됨</summary>
    public IReadOnlyList<(int speaker, string line)> Beats { get; }

    public PlazaMoment(string entryId, string otterId, PlazaMomentKind kind, PlazaSpotKind spot, int companions,
        params (int speaker, string line)[] beats)
    {
        EntryId = entryId;
        OtterId = otterId;
        Kind = kind;
        Spot = spot;
        Companions = companions;
        Beats = beats;
    }
}

/// <summary>
/// 순간포착 규칙 (순수 C#). 사진관을 다 지은 뒤(photo_studio_built)부터, 주인공이 좋아하는 자리에 머무는 동안
/// 확률로 장면이 시작되고, 동료가 모여 말을 주고받은 마지막에 카메라 말풍선을 누르면 도감에 남는다.
/// 장면은 다시 일어나고 도감에는 첫 발견(함께한 해달 · 날짜)만 남는다
/// </summary>
public static class PlazaMomentRules
{
    /// <summary>확인 간격 (초)</summary>
    public const float CheckSeconds = 3f;
    /// <summary>주인공이 그 자리에 있고 조건이 맞을 때, 확인마다 장면이 시작될 확률</summary>
    public const float StartChance = 0.35f;
    /// <summary>주인공이 한가할 때, 확인마다 좋아하는 자리로 가 볼 확률</summary>
    public const float NudgeChance = 0.06f;
    /// <summary>장면 하나가 끝나고 다음 장면까지 쉬는 시간 (초)</summary>
    public const float CooldownSeconds = 90f;
    /// <summary>동료가 자리에 오기를 기다리는 최대 시간 (초). 넘으면 장면 취소</summary>
    public const float ArriveTimeoutSeconds = 18f;
    /// <summary>말 하나가 떠 있는 시간 (초)</summary>
    public const float BeatSeconds = 2.4f;
    /// <summary>카메라 말풍선을 누를 수 있는 시간 (초)</summary>
    public const float CaptureSeconds = 10f;

    public static readonly PlazaMoment[] All =
    {
        new PlazaMoment("moment_sleepy_bench", "otter_sleepy", PlazaMomentKind.SleepyBench, PlazaSpotKind.Sit, 1,
            (1, "하암…"), (0, "쿨…"), (1, "나도 조금만… 쿨…")),
        new PlazaMoment("moment_secret_table", "otter_toy_jaejal", PlazaMomentKind.SecretTable, PlazaSpotKind.Eat, 2,
            (0, "있잖아, 이건 우리끼리 비밀인데…"), (1, "정말?!"), (0, "하하하!"), (2, "뭔데? 뭔데?")),
        new PlazaMoment("moment_night_notes", "otter_toy_kkeujeok", PlazaMomentKind.NightNotes, PlazaSpotKind.Gather, 1,
            (1, "뭐 적어요?"), (1, "어? 이거 나잖아!"), (0, "가만히! 그 표정 그대로!")),
        new PlazaMoment("moment_lamp_map", "otter_toy_duribeon", PlazaMomentKind.LampMap, PlazaSpotKind.Gather, 1,
            (0, "음… 여기가 어디지…"), (1, "지도가 거꾸로예요!"), (0, "아, 이쪽이었구나!")),
    };

    /// <summary>사진관을 다 지었는지 (순간포착이 열림)</summary>
    public static bool IsOpen(Func<string, bool> hasDevelopment)
    {
        if (hasDevelopment == null)
            throw new ArgumentNullException(nameof(hasDevelopment));
        return hasDevelopment(StoryArrivalRules.PhotoStudioBuiltDevelopment);
    }

    /// <summary>
    /// 지금 이 장면을 시작할 수 있는지: 주인공이 그 종류의 자리에 머물고, 그 자리에 동료가 앉을 빈자리가 있고, 부를 동료가 충분함
    /// </summary>
    /// <param name="protagonistSpot">주인공이 머무는 자리의 종류 (없으면 null)</param>
    public static bool CanStart(PlazaMoment moment, PlazaSpotKind? protagonistSpot, int freeSeats, int freeCompanions)
    {
        if (moment == null)
            throw new ArgumentNullException(nameof(moment));
        return protagonistSpot == moment.Spot && freeSeats >= moment.Companions && freeCompanions >= moment.Companions;
    }

    /// <summary>도감에 남는 첫 발견 날짜 (2026.10.24)</summary>
    public static string FormatDate(DateTime date) => date.ToString("yyyy.MM.dd");

    /// <summary>주인공 해달의 장면 (없으면 null)</summary>
    public static PlazaMoment ForOtter(string otterId)
    {
        foreach (var moment in All)
        {
            if (moment.OtterId == otterId)
                return moment;
        }
        return null;
    }
}
