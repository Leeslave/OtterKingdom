using System;

/// <summary>
/// 처음 나오는 네 해달: 시작 해달이 → 광산 길을 열면 깡깡이 → 깡깡이를 광산에 배치하면 물감이 · 뚝딱이.
/// 게시판을 보강해 또박이가 올 때(board_upgraded)까지는 이 넷만 광장에 나온다 (다른 해달은 기록만 남고 기다림).
/// </summary>
public static class EarlyOtterRules
{
    public const string OpenDevelopment = DistrictGuideRules.TownHall;

    private static readonly string[] EarlyOtterIds = { "otter_first", "otter_miner", "otter_painter", "otter_builder" };

    // 소개 대기 줄 없이 바로 광장에 있는 해달 (광산 배치로 함께 오는 물감이 · 뚝딱이, 생산 필수 해달)
    private static readonly string[] ImmediateOtterIds = { "otter_miner", "otter_farmer", "otter_painter", "otter_builder" };

    public static bool IsEarly(string otterId) => Array.IndexOf(EarlyOtterIds, otterId) >= 0;

    public static bool IsImmediate(string otterId) => Array.IndexOf(ImmediateOtterIds, otterId) >= 0;

    public static bool IsVillageOpen(Settlement settlement) =>
        settlement != null && settlement.HasDevelopment(OpenDevelopment);

    public static bool CanAppear(string otterId, Settlement settlement) =>
        IsEarly(otterId) || IsVillageOpen(settlement);
}
