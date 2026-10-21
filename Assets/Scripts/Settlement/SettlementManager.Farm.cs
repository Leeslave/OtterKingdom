using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광장 농장 잇기 (Docs/농장_설계형_기획.md): 광장 격자에 다 지은 농장 부품(밭 칸 · 상자 · 길 · 스프링클러)을 규칙(PlazaFarm)에 맞추고,
/// 밭을 자라게 하고(흙 · 스프링클러 · 농업 구역), 심기(씨앗) · 농부 수확 · 상자 넣기 · 거두기(가방) · 수레 · 흙 업그레이드 · 꺼 둔 동안 · 옛 밭 옮기기.
/// 기능은 건물을 지으면 열림 (농업 창고 → 흙길 · 기름진 흙 · 큰 상자, 목공소 → 스프링클러, 채석 작업소 → 돌길, 건설소 → 빨리 짓기 · 수레, 성 → 황금 흙)
/// </summary>
public partial class SettlementManager
{
    private const string FarmlandDevelopment = "farmland";
    private const string FarmDiscoverDevelopment = "farm_discovered";
    private const string FarmPathDevelopment = "farm_path_open";
    private const string FarmWorkingDevelopment = "farm_working";
    private const float FarmSyncSeconds = 0.5f;
    private const float OfflineStepSeconds = 5f;
    private const double OfflineMaxSeconds = 12 * 3600;

    private float _farmSyncTimer;
    private bool _farmLoaded;
    private readonly List<int> _farmFieldIds = new List<int>();
    private readonly List<int> _farmCrateIds = new List<int>();

    /// <summary>밭 · 상자 상태가 바뀜 (자람 · 심기 · 수확 · 넣기 · 거두기)</summary>
    public event Action OnFarmChanged;
    /// <summary>상자를 거둠 (상자 배치 ID, 가방에 들어간 것) — 광장이 아이콘을 날림</summary>
    public event Action<int, List<FarmStackSaveData>> OnCrateCollected;

    public PlazaFarmConfig FarmConfig => _config.Farm;
    public PlazaFarm Farm => Settlement.Farm;

    /// <summary>광장 농장이 열림 (밭 개간 = 농업 지역에서 Lv.10)</summary>
    public bool FarmOpen => FarmConfig != null && IsLoaded && HasDevelopment(FarmlandDevelopment);

    #region 해금 (건물 보상)

    /// <summary>이 건물을 다 지었는지 (농장 기능 해금)</summary>
    public bool HasBuilt(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId))
            return true;
        foreach (var record in Settlement.Buildings)
        {
            if (record.Built && record.BuildingId == buildingId)
                return true;
        }
        return false;
    }

    /// <summary>이 농장 부품을 열어 주는 건물 (흙길 · 스프링클러, 없으면 null)</summary>
    private FarmUnlock FarmPartUnlock(BuildingDefinition building)
    {
        if (FarmConfig == null || building == null)
            return null;
        var unlock = building.FarmPart == FarmPartKind.Path ? FarmConfig.PathUnlock
            : building.FarmPart == FarmPartKind.Sprinkler ? FarmConfig.SprinklerUnlock : null;
        return unlock != null && !string.IsNullOrEmpty(unlock.RequiredBuilding) ? unlock : null;
    }

    private bool Unlocked(FarmUnlock unlock) => unlock != null && !string.IsNullOrEmpty(unlock.RequiredBuilding) && HasBuilt(unlock.RequiredBuilding);

    public int CrateCapacity => FarmConfig == null ? 0 : FarmConfig.CrateCapacity * (Unlocked(FarmConfig.BigCrate) ? 2 : 1);
    public bool CartActive => FarmConfig != null && Unlocked(FarmConfig.Cart);
    public bool StonePath => FarmConfig != null && Unlocked(FarmConfig.StonePath);
    public float PathSpeed => FarmConfig == null ? 1f : StonePath ? FarmConfig.StonePathSpeed : FarmConfig.PathSpeed;

    /// <summary>밭 칸 짓는 시간 (건설소가 있으면 절반)</summary>
    private float BuildSecondsOf(BuildingDefinition building) =>
        building.FarmPart == FarmPartKind.Field && FarmConfig != null && Unlocked(FarmConfig.FastBuild) ? building.BuildSeconds * 0.5f : building.BuildSeconds;

    /// <summary>더 지을 수 있는 밭 칸 수 (짓는 중 포함)</summary>
    public int FarmFieldsLeft => FarmConfig == null ? 0 : FarmConfig.FieldsAt(PlayerLevel) - CountFarmParts(FarmPartKind.Field, false);

    #endregion

    #region 놓인 부품

    /// <summary>광장에 놓인 이 농장 부품들 (builtOnly = 다 지은 것만)</summary>
    public List<PlacedDecor> FarmParts(FarmPartKind kind, bool builtOnly = true)
    {
        var result = new List<PlacedDecor>();
        var board = PlazaBoard;
        var decor = DecorManager.Instance;
        if (board == null || decor == null)
            return result;
        foreach (var placed in decor.GetLayout(board).Placed)
        {
            if (!(placed.Decor is BuildingDefinition building) || building.FarmPart != kind)
                continue;
            if (builtOnly)
            {
                var record = Settlement.FindBuilding(placed.InstanceId);
                if (record != null && !record.Built)
                    continue;
            }
            result.Add(placed);
        }
        return result;
    }

    private int CountFarmParts(FarmPartKind kind, bool builtOnly) => FarmParts(kind, builtOnly).Count;

    /// <summary>이 격자 칸에 길이 깔렸는지</summary>
    public bool IsPathCell(Vector2Int cell)
    {
        var board = PlazaBoard;
        var decor = DecorManager.Instance;
        if (board == null || decor == null)
            return false;
        var placed = decor.GetLayout(board).GetAt(cell);
        return placed != null && placed.Decor is BuildingDefinition b && b.FarmPart == FarmPartKind.Path;
    }

    private void SyncFarm()
    {
        _farmFieldIds.Clear();
        foreach (var p in FarmParts(FarmPartKind.Field))
            _farmFieldIds.Add(p.InstanceId);
        _farmCrateIds.Clear();
        foreach (var p in FarmParts(FarmPartKind.Crate))
            _farmCrateIds.Add(p.InstanceId);
        if (Farm.Sync(_farmFieldIds, _farmCrateIds))
            OnFarmChanged?.Invoke();
        // 고랑 열기 퀘스트(Lv.11~13) = 처음 밭 칸 3개를 넘긴 수
        if (QuestManager.Instance != null && FarmConfig != null)
            QuestManager.Instance.SetOpenedFurrows(Mathf.Max(0, _farmFieldIds.Count - FarmConfig.FieldsAt(10)));
    }

    #endregion

    #region 자람

    /// <summary>이 밭 칸의 자람 속도 배수 (흙 · 스프링클러 · 농업 구역)</summary>
    public float FieldSpeed(FarmFieldSaveData field)
    {
        var soil = SoilOf(field);
        float speed = 1f / (soil != null ? soil.DurationMultiplier : 1f);
        speed *= 1f + SprinklerBonusAt(field.instanceId);
        speed *= (100f + BonusPercent(KingdomBonusKind.GrowSpeed)) / 100f;
        return speed;
    }

    public FarmSoilTier SoilOf(FarmFieldSaveData field)
    {
        var soils = FarmConfig != null ? FarmConfig.Soils : null;
        if (soils == null || soils.Count == 0)
            return null;
        return soils[Mathf.Clamp(field.soil, 0, soils.Count - 1)];
    }

    /// <summary>둘레 스프링클러가 주는 자람 + (겹치면 최대까지)</summary>
    public float SprinklerBonusAt(int fieldId)
    {
        var board = PlazaBoard;
        var decor = DecorManager.Instance;
        if (FarmConfig == null || board == null || decor == null)
            return 0f;
        var layout = decor.GetLayout(board);
        if (!layout.TryGet(fieldId, out var field))
            return 0f;
        int range = FarmConfig.SprinklerRange;
        float bonus = 0f;
        foreach (var sprinkler in FarmParts(FarmPartKind.Sprinkler))
        {
            if (CellDistance(field.Area, sprinkler.Area) <= range)
                bonus += FarmConfig.SprinklerBonus;
        }
        return Mathf.Min(bonus, FarmConfig.SprinklerMax);
    }

    // 두 칸 영역 사이 거리 (겹치면 0, 칸 단위, 대각선도 한 칸)
    private static int CellDistance(RectInt a, RectInt b)
    {
        int dx = Mathf.Max(0, Mathf.Max(a.xMin - (b.xMax - 1), b.xMin - (a.xMax - 1)));
        int dy = Mathf.Max(0, Mathf.Max(a.yMin - (b.yMax - 1), b.yMin - (a.yMax - 1)));
        return Mathf.Max(dx, dy);
    }

    // Update에서 (SettlementManager.cs)
    private void TickFarm(float dt)
    {
        OpenPlazaFarm();
        if (!FarmOpen)
            return;
        if (!_farmLoaded)
        {
            _farmLoaded = true;
            SyncFarm();
            MigrateOldFarm();
            ApplyFarmOffline();
        }
        _farmSyncTimer -= dt;
        if (_farmSyncTimer <= 0f)
        {
            _farmSyncTimer = FarmSyncSeconds;
            SyncFarm();
            // 밭 씬이 없어져 밭 안내가 없음: 농부를 배치하면 바로 광장 밭에서 일함
            var farmer = FarmConfig != null ? _config.FindOtter(FarmConfig.FarmerOtterId) : null;
            if (farmer != null && SettlementRegionRules.StartWork(farmer, Settlement, NowTicks))
            {
                Settlement.UnlockDevelopment(FarmWorkingDevelopment);
                SaveRequested?.Invoke();
            }
            ResumeWaitingFields();
        }
        if (Farm.Grow(dt, FieldSpeed) > 0)
            OnFarmChanged?.Invoke();
        Farm.Data.lastUtcTicks = NowTicks;

        if (CartActive)
        {
            Farm.Data.cartTimer += dt;
            if (Farm.Data.cartTimer >= FarmConfig.CartSeconds)
            {
                Farm.Data.cartTimer = 0f;
                foreach (var crate in new List<FarmCrateSaveData>(Farm.Crates))
                {
                    if (PlazaFarm.Count(crate) > 0)
                        CollectCrate(crate.instanceId);
                }
            }
        }
    }

    #endregion

    // 밭이 광장으로 옮겨 와 밭 씬 개간(장애물 치우기 · 주민 밭 갈기)이 없음:
    // 농업 지역이 생겨 밭을 발견하면 바로 밭 칸을 지을 수 있게 (밭 개간 부탁도 이것으로 끝남)
    private void OpenPlazaFarm()
    {
        if (FarmConfig == null || !IsLoaded || HasDevelopment(FarmlandDevelopment))
            return;
        if (!HasDevelopment(FarmDiscoverDevelopment) || !DistrictRules.HasDistrict(_config, Settlement, DistrictType.Agricultural))
            return;
        Settlement.UnlockDevelopment(FarmPathDevelopment);
        Settlement.UnlockDevelopment(FarmlandDevelopment);
        GameNotices.Post(new GameNotice("농업 지역에 밭을 지을 수 있어요! (꾸미기 → 밭 칸)"));
        SaveRequested?.Invoke();
    }

    #region 심기

    public IReadOnlyList<CropDefinition> FarmCrops => GameManager.Instance != null ? GameManager.Instance.Crops : Array.Empty<CropDefinition>();

    public CropDefinition FindCrop(string cropId)
    {
        foreach (var crop in FarmCrops)
        {
            if (crop != null && crop.cropId == cropId)
                return crop;
        }
        return null;
    }

    /// <summary>씨앗이 따로 필요 없는 작물 (당근 같은 영구 씨앗)</summary>
    public static bool FreeSeed(CropDefinition crop) => crop != null && crop.seedType == SeedType.Permanent;

    public int SeedCount(CropDefinition crop)
    {
        var game = GameManager.Instance;
        if (crop == null || game == null)
            return 0;
        return game.TryFindItem(crop.SeedItemId, out var seed) ? game.Bag.GetCount(seed) : 0;
    }

    private bool ConsumeSeed(CropDefinition crop)
    {
        if (FreeSeed(crop))
            return true;
        var game = GameManager.Instance;
        return game != null && game.TryFindItem(crop.SeedItemId, out var seed) && game.Bag.TryRemove(seed, 1, ItemChangeReason.Plant);
    }

    /// <summary>이 밭에 심는다 (씨앗 하나). 못 심으면 false</summary>
    public bool PlantField(int fieldId, CropDefinition crop)
    {
        if (crop == null || Farm.Field(fieldId) == null || !ConsumeSeed(crop))
            return false;
        Farm.Plant(fieldId, crop.cropId, crop.baseDurationSec);
        OnFarmChanged?.Invoke();
        SaveRequested?.Invoke();
        return true;
    }

    public void ClearField(int fieldId)
    {
        Farm.Clear(fieldId);
        OnFarmChanged?.Invoke();
        SaveRequested?.Invoke();
    }

    #endregion

    #region 농부 (광장 PlazaFarmWorker가 부름)

    /// <summary>밭을 거둔다: 작물 개수(흙에 따라 하나 더) → 같은 작물 다시 심기(씨앗이 없으면 기다림). 거둔 작물 · 개수</summary>
    public bool HarvestField(int fieldId, out CropDefinition crop, out int amount)
    {
        amount = 0;
        var field = Farm.Field(fieldId);
        string cropId = Farm.Harvest(fieldId);
        crop = FindCrop(cropId);
        if (crop == null)
            return false;
        var soil = SoilOf(field);
        amount = Mathf.Max(1, crop.yieldCount) + (soil != null && UnityEngine.Random.value < soil.BonusChance ? 1 : 0);
        amount = KingdomBonus.Amount(KingdomBonusKind.HarvestYield, amount);
        if (ConsumeSeed(crop))
            Farm.Plant(fieldId, crop.cropId, crop.baseDurationSec);
        else
            field.waitingCropId = crop.cropId;
        OnFarmChanged?.Invoke();
        return true;
    }

    /// <summary>씨앗을 기다리던 밭에 씨앗이 생겼으면 심음</summary>
    public void ResumeWaitingFields()
    {
        foreach (var field in Farm.Fields)
        {
            if (string.IsNullOrEmpty(field.waitingCropId) || !string.IsNullOrEmpty(field.cropId))
                continue;
            var crop = FindCrop(field.waitingCropId);
            if (crop != null && ConsumeSeed(crop))
                Farm.Plant(field.instanceId, crop.cropId, crop.baseDurationSec);
        }
    }

    /// <summary>상자에 넣는다. 넣은 수</summary>
    public int DepositToCrate(int crateId, string itemId, int amount)
    {
        int n = Farm.Deposit(crateId, itemId, amount, CrateCapacity);
        if (n > 0)
        {
            OnFarmChanged?.Invoke();
            SaveRequested?.Invoke();
        }
        return n;
    }

    /// <summary>상자를 거둬 가방으로 (가방이 차면 남음)</summary>
    public List<FarmStackSaveData> CollectCrate(int crateId)
    {
        var game = GameManager.Instance;
        var taken = Farm.Collect(crateId, (id, count) =>
        {
            if (game == null || !game.TryFindItem(id, out var item))
                return 0;
            return game.Bag.Add(item, count, ItemChangeReason.Harvest);
        });
        if (taken.Count > 0)
        {
            OnCrateCollected?.Invoke(crateId, taken);
            OnFarmChanged?.Invoke();
            SaveRequested?.Invoke();
        }
        return taken;
    }

    #endregion

    #region 흙 업그레이드

    /// <summary>이 밭의 다음 흙 (없으면 null) · 열렸는지</summary>
    public FarmSoilTier NextSoil(FarmFieldSaveData field, out bool unlocked)
    {
        unlocked = false;
        var soils = FarmConfig != null ? FarmConfig.Soils : null;
        if (field == null || soils == null || field.soil + 1 >= soils.Count)
            return null;
        var next = soils[field.soil + 1];
        unlocked = HasBuilt(next.RequiredBuilding) && (string.IsNullOrEmpty(next.RequiredDevelopment) || HasDevelopment(next.RequiredDevelopment));
        return next;
    }

    public bool TryUpgradeSoil(int fieldId, out string reason)
    {
        reason = null;
        var field = Farm.Field(fieldId);
        var next = NextSoil(field, out bool unlocked);
        if (next == null)
        {
            reason = "가장 좋은 흙이에요";
            return false;
        }
        if (!unlocked)
        {
            reason = SoilLockText(next);
            return false;
        }
        if (GoldBalance < next.Gold)
        {
            reason = "골드가 모자라요";
            return false;
        }
        foreach (var cost in next.Items)
        {
            if (cost != null && cost.Item != null && ItemCount(cost.Item) < cost.Amount)
            {
                reason = $"{cost.Item.DisplayName}이(가) 모자라요";
                return false;
            }
        }
        PayCost(next.Gold, new List<ItemAmount>(next.Items));
        Farm.SetSoil(fieldId, field.soil + 1);
        OnFarmChanged?.Invoke();
        SaveRequested?.Invoke();
        return true;
    }

    /// <summary>잠긴 흙 안내 (예: 농업 창고를 지으면 열려요)</summary>
    public string SoilLockText(FarmSoilTier soil)
    {
        if (!string.IsNullOrEmpty(soil.RequiredBuilding) && !HasBuilt(soil.RequiredBuilding))
        {
            var building = FindBuilding(soil.RequiredBuilding);
            return $"{EulReul(building != null ? building.DisplayName : soil.RequiredBuilding)} 지으면 열려요";
        }
        return "왕국이 더 커지면 열려요";
    }

    #endregion

    #region 꺼 둔 동안

    // 꺼 둔 동안: 밭이 자라고, 다 자란 밭은 농부가 (걷는 시간만큼 덜) 상자에 넣고 다시 심음. 상자가 차면 멈춤
    private void ApplyFarmOffline()
    {
        long last = Farm.Data.lastUtcTicks;
        Farm.Data.lastUtcTicks = NowTicks;
        if (last <= 0 || !HasFarmWorker)
            return;
        double seconds = Math.Min(OfflineMaxSeconds, (NowTicks - last) / (double)TimeSpan.TicksPerSecond);
        if (seconds < 1)
            return;
        float efficiency = FarmConfig.OfflineEfficiency;
        for (double t = 0; t < seconds; t += OfflineStepSeconds)
        {
            Farm.Grow((float)Math.Min(OfflineStepSeconds, seconds - t) * efficiency, FieldSpeed);
            foreach (var field in Farm.Fields)
            {
                if (!field.ready)
                    continue;
                var crate = FirstCrateWithSpace();
                if (crate < 0)
                    break;
                if (HarvestField(field.instanceId, out var crop, out int amount))
                    DepositToCrate(crate, crop.cropId, amount);
            }
        }
        OnFarmChanged?.Invoke();
    }

    private int FirstCrateWithSpace()
    {
        foreach (var crate in Farm.Crates)
        {
            if (PlazaFarm.Count(crate) < CrateCapacity)
                return crate.instanceId;
        }
        return -1;
    }

    /// <summary>농부(광장 밭 일꾼)가 있음 — 밭에 배치된 농부 해달</summary>
    public bool HasFarmWorker
    {
        get
        {
            return FarmConfig != null && Settlement.GetSpecialistState(FarmConfig.FarmerOtterId) >= SpecialistState.Assigned;
        }
    }

    #endregion

    #region 옛 밭 옮기기

    // 옛 세이브(밭 씬 밭 3 × 고랑 3): 열린 고랑 수만큼 밭 칸을 농업 지역에 놓고(자라던 작물 그대로) 상자 하나. 농업 지역이 없으면 기다림
    private void MigrateOldFarm()
    {
        if (Farm.Data.migrated)
            return;
        var game = GameManager.Instance;
        var service = game != null ? game.FarmService : null;
        var fieldDef = FarmPartDefinition(FarmPartKind.Field);
        var crateDef = FarmPartDefinition(FarmPartKind.Crate);
        if (service == null || fieldDef == null || crateDef == null || !DistrictRules.HasDistrict(_config, Settlement, DistrictType.Agricultural))
            return;
        Farm.Data.migrated = true;
        if (Farm.Fields.Count > 0)
            return;

        var slots = new List<(string crop, float remaining, bool ready)>();
        for (int p = 0; p < service.Plots.Count; p++)
        {
            if (!service.IsPlotUnlocked(p))
                continue;
            for (int s = 0; s < service.Plots[p].SlotCount; s++)
            {
                if (!service.IsSlotOpen(p, s))
                    continue;
                var crop = service.GetSlotCrop(p, s);
                var state = service.GetSlotState(p, s);
                slots.Add((crop != null ? crop.cropId : null, service.GetSlotRemainingSec(p, s), state == FurrowSlotState.AwaitingHarvest));
            }
        }
        int soil = service.FarmLevel > 1 ? 1 : 0;
        int count = Mathf.Min(slots.Count, FarmConfig.FieldsAt(Mathf.Max(PlayerLevel, 10)));
        PlaceFarmPartFree(crateDef);
        for (int i = 0; i < count; i++)
        {
            int id = PlaceFarmPartFree(fieldDef);
            if (id <= 0)
                break;
            SyncFarm();
            var (crop, remaining, ready) = slots[i];
            if (!string.IsNullOrEmpty(crop))
                Farm.Plant(id, crop, ready ? 0f : remaining);
            Farm.SetSoil(id, soil);
        }
        SyncFarm();
        GameNotices.Post(new GameNotice("밭이 광장 농업 지역으로 옮겨 왔어요!"));
        SaveRequested?.Invoke();
    }

    private BuildingDefinition FarmPartDefinition(FarmPartKind kind)
    {
        foreach (var building in BuildingCatalog)
        {
            if (building != null && building.FarmPart == kind)
                return building;
        }
        return null;
    }

    // 농업 지역의 빈 자리에 공짜로 다 지어 놓음 (옛 세이브 옮기기). 놓은 배치 ID (못 놓으면 0)
    private int PlaceFarmPartFree(BuildingDefinition building)
    {
        var board = PlazaBoard;
        var decor = DecorManager.Instance;
        if (board == null || decor == null || !TryFindFarmSpot(building, out var origin))
            return 0;
        if (decor.TryPlaceBuilding(board, building, origin, out var placed) != DecorPlacementResult.Ok)
            return 0;
        BuildingRules.Start(Settlement, building, placed.InstanceId, NowTicks, 0);
        FinishDueBuildings(false);
        return placed.InstanceId;
    }

    /// <summary>농업 지역 열린 땅에서 이 부품이 들어갈 빈 자리 (격자 칸)</summary>
    public bool TryFindFarmSpot(BuildingDefinition building, out Vector2Int origin)
    {
        origin = default;
        var board = PlazaBoard;
        var decor = DecorManager.Instance;
        if (board == null || decor == null)
            return false;
        var layout = decor.GetLayout(board);
        foreach (var block in _config.DistrictBlocks)
        {
            if (block == null || DistrictRules.TypeOf(block, Settlement) != DistrictType.Agricultural)
                continue;
            int open = DistrictRules.OpenStages(block, Settlement);
            for (int s = 0; s < open && s < block.Stages.Count; s++)
            {
                foreach (var area in block.Stages[s].Areas)
                {
                    for (int y = area.yMax - building.Footprint.y; y >= area.yMin; y--)
                    {
                        for (int x = area.xMin; x <= area.xMax - building.Footprint.x; x++)
                        {
                            var cell = new Vector2Int(x, y) + board.SaveOrigin;
                            if (layout.Check(building, cell, DecorRotation.R0) == DecorPlacementResult.Ok)
                            {
                                origin = cell;
                                return true;
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    #endregion

    /// <summary>이름 + 을/를 (받침 있으면 을)</summary>
    public static string EulReul(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;
        char last = word[word.Length - 1];
        bool batchim = last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
        return word + (batchim ? "을" : "를");
    }
}
