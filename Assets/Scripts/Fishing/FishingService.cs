using UnityEngine;

// Fishing rules with no scene dependencies: whether the fishing otter is on
// duty (idle fishing), the bite delay, and what an idle catch is. Whether
// fishing is active is saved so the otter goes straight back to its spot on
// the next visit. Catches in progress are not saved — leaving the scene
// mid-catch throws that catch away, same as the player stopping it.
//
// 바다 낚시 (낚시 개편, Docs/낚시_개편_기획.md): 손으로 던지는 낚시의 규칙(FishingReworkModel)과
// 강화 세 가지(줄 길이 · 찌 무게 · 릴)도 여기서 가짐. 방치 낚시 · 꺼 둔 동안도 같은 강화로
// 닿는 물고기 중에서 낚음. 옛 낚싯대 레벨(1~20)은 처음 한 번 줄 · 찌 · 릴 단계로 옮김 (Migrate)
public class FishingService
{
    // 방치 낚시(낚시 해달 · 꺼 둔 동안)에 쓰레기가 걸릴 확률
    public const float TrashChance = 0.1f;

    private readonly SaveData save;
    private readonly FishingBalanceData balance;
    private readonly FishingReworkConfig config;

    public FishingService(SaveData save, FishingBalanceData balance, FishingReworkConfig config = null, IFishingHost host = null,
        System.Random random = null)
    {
        this.save = save;
        this.balance = balance;
        this.config = config;
        save.rodLevel = Mathf.Clamp(save.rodLevel, 1, MaxOldRodLevel);
        if (save.fishing == null)
            save.fishing = new FishingReworkSave();
        if (config != null)
        {
            Migrate(save, config);
            Model = new FishingReworkModel(config, save.fishing, random ?? new System.Random(), host);
        }
    }

    public FishingBalanceData Balance => balance;
    public FishingReworkConfig Config => config;

    /// <summary>바다 낚시 규칙 (본 게임 연결 전 · 설정이 없으면 null)</summary>
    public FishingReworkModel Model { get; }

    /// <summary>바다 낚시 화면이 열려 있음 → 그동안 선착장의 낚시 해달(방치)은 쉼 (바다에서 해달이 대신 던짐)</summary>
    public bool HandFishing { get; set; }

    public bool IsActive => save.fishingActive;
    public void SetActive(bool active) => save.fishingActive = active;

    private int MaxOldRodLevel => balance.MaxRodLevel;

    /// <summary>줄 · 찌 · 릴 강화한 횟수의 합</summary>
    public int UpgradeSteps => StepsOf(save.fishing);

    private static int StepsOf(FishingReworkSave fishing) =>
        fishing == null ? 0 : fishing.lineLevel + fishing.sinkerLevel + fishing.reelLevel;

    /// <summary>예전 "낚싯대 레벨" 자리 (1 + 강화 횟수). 퀘스트 · 개발 도구 · 꺼 둔 동안 해금이 씀</summary>
    public int RodLevel => config != null ? 1 + UpgradeSteps : save.rodLevel;

    /// <summary>
    /// 옛 낚싯대 레벨 → 줄 · 찌 · 릴 단계 (바다 낚시를 처음 쓸 때 한 번). 강화한 만큼(레벨 − 1)을
    /// 줄 → 찌 → 릴 순서로 하나씩 나눠 줌 (각 끝 단계까지). 예: Lv.10 → 9번 → 3 · 3 · 3
    /// </summary>
    public static void Migrate(SaveData save, FishingReworkConfig config)
    {
        var fishing = save.fishing;
        if (fishing.started || StepsOf(fishing) > 0)
            return;
        int points = Mathf.Max(0, save.rodLevel - 1);
        var order = new[] { FishingUpgrade.Line, FishingUpgrade.Sinker, FishingUpgrade.Reel };
        for (int guard = 0; points > 0 && guard < 100; guard++)
        {
            var upgrade = order[guard % order.Length];
            int max = config.Track(upgrade).MaxLevel;
            switch (upgrade)
            {
                case FishingUpgrade.Line: if (fishing.lineLevel < max) { fishing.lineLevel++; points--; } break;
                case FishingUpgrade.Sinker: if (fishing.sinkerLevel < max) { fishing.sinkerLevel++; points--; } break;
                default: if (fishing.reelLevel < max) { fishing.reelLevel++; points--; } break;
            }
        }
    }

    public float RollBiteDelaySec()
    {
        Vector2 range = balance.biteDelaySec;
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    // Returns the item id of what was hooked: 바다 낚시 강화로 낚을 수 있는 물고기 중에서 (가끔 쓰레기).
    public string RollCatch()
    {
        if (config == null)
            return balance.RollCatch(save.rodLevel, Random.value, Random.value);
        return RollCatch(config, save.fishing, balance.trashItemId, Random.value, Random.value);
    }

    /// <summary>방치 낚시 한 번 (낚시 해달 · 꺼 둔 동안 같은 규칙)</summary>
    public static string RollCatch(FishingReworkConfig config, FishingReworkSave levels, string trashItemId, double trashRoll, double fishRoll)
    {
        if (trashRoll < TrashChance)
            return trashItemId;
        var fish = config.RollCatchable(levels.lineLevel, levels.sinkerLevel, levels.reelLevel, fishRoll);
        return fish != null && fish.Item != null ? fish.Item.ItemId : trashItemId;
    }

    /// <summary>꺼 둔 동안도 낚시하는지 (바다 낚시: 강화를 한 번이라도 하면, 예전: 낚싯대 Lv.2부터)</summary>
    public static bool OfflineUnlocked(SaveData save, FishingBalanceData balance, FishingReworkConfig config) =>
        config != null ? StepsOf(save.fishing) >= 1 : save.rodLevel >= balance.offlineUnlockRodLevel;

    public bool IsFish(string itemId) => (config != null && config.FindByItem(itemId) != null) || balance.IsFish(itemId);
}
