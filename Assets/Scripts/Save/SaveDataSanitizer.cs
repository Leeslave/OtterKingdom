using System.Collections.Generic;

/// <summary>
/// 불러온 세이브의 값 검사 (상세기획서 11.4 · 변경명세 8.2).
/// 파일이 깨진 건 아니지만 값이 이상한 경우(빠진 목록, 음수·NaN 시간, 0 레벨)를 게임이 쓰기 전에 고친다.
/// 가방·재화는 각자 불러올 때 다시 걸러낸다 (InventorySaveConverter, CurrencyManager.SetBalance).
/// </summary>
public static class SaveDataSanitizer
{
    /// <summary>고친 곳의 설명 목록 (비어 있으면 손댈 곳이 없었음)</summary>
    public static List<string> Sanitize(SaveData save)
    {
        if (save == null)
            throw new System.ArgumentNullException(nameof(save));

        var fixes = new List<string>();

        // JsonUtility는 빠진 필드를 기본값으로 채우지만, 손으로 고친 파일 등에서 null이 들어올 수 있음
        if (save.tutorialsDone == null) { save.tutorialsDone = new List<string>(); fixes.Add("tutorialsDone"); }
        if (save.plots == null) { save.plots = new List<PlotSaveData>(); fixes.Add("plots"); }
        if (save.inventory == null) { save.inventory = new List<ItemStack>(); fixes.Add("inventory"); }
        if (save.seeds == null) { save.seeds = new List<ItemStack>(); fixes.Add("seeds"); }
        if (save.starterSeedsGranted == null) { save.starterSeedsGranted = new List<string>(); fixes.Add("starterSeedsGranted"); }
        if (save.offlineFarmSlots == null) { save.offlineFarmSlots = new List<OfflineFarmSlotSaveData>(); fixes.Add("offlineFarmSlots"); }
        if (save.otters == null) { save.otters = new List<OtterSaveData>(); fixes.Add("otters"); }
        if (save.currencies == null) { save.currencies = new List<CurrencyBalance>(); fixes.Add("currencies"); }
        if (save.collection == null) { save.collection = new List<CollectionSaveEntry>(); fixes.Add("collection"); }
        if (save.quests == null) { save.quests = new List<QuestSaveEntry>(); fixes.Add("quests"); }
        if (save.farmerWork == null) { save.farmerWork = new FarmerWorkSaveData(); fixes.Add("farmerWork"); }
        if (save.farmNotice == null) { save.farmNotice = new FarmNoticeSaveData(); fixes.Add("farmNotice"); }
        if (save.profile == null) { save.profile = new ProfileSaveData(); fixes.Add("profile"); }
        if (save.settlement == null) { save.settlement = new SettlementSaveData(); fixes.Add("settlement"); }
        if (save.decor == null) { save.decor = new DecorSaveData(); fixes.Add("decor"); }

        // 빈 줄은 버림 (읽는 쪽이 null을 가정하지 않음)
        if (save.plots.RemoveAll(p => p == null) > 0) fixes.Add("plots: null");
        if (save.otters.RemoveAll(o => o == null) > 0) fixes.Add("otters: null");
        if (save.currencies.RemoveAll(c => c == null) > 0) fixes.Add("currencies: null");

        save.farmLevel = AtLeastOne(save.farmLevel, "farmLevel", fixes);
        save.rodLevel = AtLeastOne(save.rodLevel, "rodLevel", fixes);
        save.pickaxeLevel = AtLeastOne(save.pickaxeLevel, "pickaxeLevel", fixes);
        save.profile.level = AtLeastOne(save.profile.level, "profile.level", fixes);
        save.profile.exp = NotNegative(save.profile.exp, "profile.exp", fixes);
        save.lifetimeSales = NotNegative(save.lifetimeSales, "lifetimeSales", fixes);
        save.inventoryCapacity = NotNegative(save.inventoryCapacity, "inventoryCapacity", fixes);

        save.offlineFishingProgressSec = Seconds(save.offlineFishingProgressSec, "offlineFishingProgressSec", fixes);
        save.offlineMiningProgressSec = Seconds(save.offlineMiningProgressSec, "offlineMiningProgressSec", fixes);
        save.miningSecToNextFind = Seconds(save.miningSecToNextFind, "miningSecToNextFind", fixes);
        save.offlineOtterVisitProgressSec = Seconds(save.offlineOtterVisitProgressSec, "offlineOtterVisitProgressSec", fixes);
        save.farmerWork.remainingSec = Seconds(save.farmerWork.remainingSec, "farmerWork.remainingSec", fixes);

        for (int p = 0; p < save.plots.Count; p++)
        {
            var plot = save.plots[p];
            if (plot.slots == null) { plot.slots = PlotSaveData.CreateEmptySlots(); fixes.Add($"plots[{p}].slots"); }
            for (int s = 0; s < plot.slots.Count; s++)
            {
                var slot = plot.slots[s];
                if (slot == null)
                {
                    plot.slots[s] = new FurrowSlotSaveData { state = FurrowSlotState.Empty };
                    fixes.Add($"plots[{p}].slots[{s}]");
                    continue;
                }
                slot.remainingSec = Seconds(slot.remainingSec, $"plots[{p}].slots[{s}].remainingSec", fixes);
                slot.durationSec = Seconds(slot.durationSec, $"plots[{p}].slots[{s}].durationSec", fixes);
            }
        }

        for (int i = 0; i < save.offlineFarmSlots.Count; i++)
        {
            if (save.offlineFarmSlots[i] == null)
            {
                // 자리 번호가 의미 있는 목록이라 지우지 않고 빈 등록으로
                save.offlineFarmSlots[i] = new OfflineFarmSlotSaveData();
                fixes.Add($"offlineFarmSlots[{i}]");
                continue;
            }
            save.offlineFarmSlots[i].progressSec = Seconds(save.offlineFarmSlots[i].progressSec, $"offlineFarmSlots[{i}].progressSec", fixes);
        }

        return fixes;
    }

    private static int AtLeastOne(int value, string name, List<string> fixes)
    {
        if (value >= 1)
            return value;
        fixes.Add($"{name}: {value}");
        return 1;
    }

    private static int NotNegative(int value, string name, List<string> fixes)
    {
        if (value >= 0)
            return value;
        fixes.Add($"{name}: {value}");
        return 0;
    }

    // 남은 시간·진행 시간: 음수, NaN, 무한대는 0으로
    private static float Seconds(float value, string name, List<string> fixes)
    {
        if (value >= 0f && !float.IsInfinity(value))
            return value;
        fixes.Add($"{name}: {value}");
        return 0f;
    }
}
