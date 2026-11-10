using System;
using System.Collections.Generic;

/// <summary>가공 레시피 (순수 값). 데이터는 ProcessingRecipeDefinition</summary>
public readonly struct ProcessingRecipe
{
    /// <summary>가공 줄 ID — 세이브 · 수레가 이것으로 찾음 (시설의 첫 줄은 시설 ID, 정밀 공방처럼 둘째 줄은 따로)</summary>
    public readonly string ProcessorId;
    public readonly string InputId;
    public readonly int InputCount;
    public readonly string OutputId;
    public readonly float CycleSeconds;
    /// <summary>창고에서 바로 쓰는 둘째 재료 (없으면 null)</summary>
    public readonly string ExtraInputId;
    public readonly int ExtraInputCount;
    /// <summary>재료를 넣어 두지 않고 만들 때마다 왕국 창고에서 꺼냄 (정밀 공방: 판재 + 주괴)</summary>
    public readonly bool FromWarehouse;

    public ProcessingRecipe(string processorId, string inputId, int inputCount, string outputId, float cycleSeconds,
        string extraInputId = null, int extraInputCount = 0, bool fromWarehouse = false)
    {
        if (string.IsNullOrEmpty(processorId) || string.IsNullOrEmpty(inputId) || string.IsNullOrEmpty(outputId))
            throw new ArgumentException("레시피의 가공 줄 · 재료 ID가 비었어요");
        if (inputCount <= 0 || cycleSeconds <= 0f || extraInputCount < 0)
            throw new ArgumentOutOfRangeException(nameof(inputCount), "재료 수 · 시간은 0보다 커야 해요");
        ProcessorId = processorId;
        InputId = inputId;
        InputCount = inputCount;
        OutputId = outputId;
        CycleSeconds = cycleSeconds;
        ExtraInputId = string.IsNullOrEmpty(extraInputId) || extraInputCount <= 0 ? null : extraInputId;
        ExtraInputCount = ExtraInputId != null ? extraInputCount : 0;
        FromWarehouse = fromWarehouse;
    }

    /// <summary>창고에서 꺼내는 한 번 몫 (창고 재료 레시피)</summary>
    public List<(string itemId, int amount)> WarehouseCosts()
    {
        var costs = new List<(string, int)> { (InputId, InputCount) };
        if (ExtraInputId != null)
            costs.Add((ExtraInputId, ExtraInputCount));
        return costs;
    }

    /// <summary>1시간에 최대 몇 개 (재료가 충분할 때)</summary>
    public float OutputPerHour => 3600f / CycleSeconds;

    /// <summary>1시간에 최대 쓰는 재료 수</summary>
    public float InputPerHour => OutputPerHour * InputCount;

    /// <summary>넣어 둘 수 있는 재료 = 이만큼 만들 몫</summary>
    public int InputCapacity => InputCount * ProcessingRules.BufferCycles;

    /// <summary>시설에 쌓아 둘 수 있는 가공품 = 8시간치 (시설 생산과 같은 상한)</summary>
    public int TrayCapacity => (int)Math.Ceiling(OutputPerHour * ProductionRules.MaxStoredHours);
}

/// <summary>
/// 가공 (순수 C#, 기획서 6.2): 시설에 넣은 원재료로 한 번에 하나씩 만들어 시설에 쌓고(tray), 수령 · 수레가 창고로 옮긴다.
/// 원재료가 모자라면 기다리고(진행은 한 개 몫까지만 쌓임), 시설이 가득 차면 멈춤. 넣은 재료는 만들 때만 빠짐 (없어지지 않음)
/// </summary>
public static class ProcessingRules
{
    /// <summary>재료를 미리 넣어 둘 수 있는 몫 (만들 횟수)</summary>
    public const int BufferCycles = 10;

    /// <summary>표준 공구(I04): 가공 슬롯 +1 → 두 개씩 나란히</summary>
    public const float StandardToolsSlots = 2f;

    /// <summary>수력 기계(I05): 가공 속도 +15%</summary>
    public const float WaterPowerBonus = 0.15f;

    /// <summary>협업 생산(I07): 켜 둔 가공 줄이 하나 늘 때마다 +5%, 최대 +20%</summary>
    public const float CooperationPerLine = 0.05f;
    public const float CooperationMax = 0.2f;

    /// <summary>왕립 산업단지(I08 공사)를 마치면 가공 속도 +15%</summary>
    public const float IndustrialParkBonus = 0.15f;

    /// <summary>자동 공급(L04)이 가방에 남겨 두는 기본 수 (희귀 광석을 다 쓰지 않게)</summary>
    public const int DefaultKeep = 10;

    /// <summary>가공 속도 배수 (기술에 따라: 표준 공구 ×2 · 수력 기계 ×1.15)</summary>
    public static float Speed(Func<string, bool> hasTech)
    {
        if (hasTech == null)
            throw new ArgumentNullException(nameof(hasTech));
        float speed = hasTech(TechIds.StandardTools) ? StandardToolsSlots : 1f;
        return hasTech(TechIds.WaterPower) ? speed * (1f + WaterPowerBonus) : speed;
    }

    /// <summary>협업 생산(I07) 보너스: 켜 둔 가공 줄 수 → 더하는 비율 (한 줄이면 0)</summary>
    public static float CooperationBonus(int activeLines) =>
        activeLines <= 1 ? 0f : Math.Min(CooperationMax, CooperationPerLine * (activeLines - 1));

    /// <summary>자동 공급(L04): 가방에서 넣을 수 — 넣을 자리만큼, 가방에 keep개는 남김</summary>
    public static int AutoSupplyAmount(ProcessorSaveData processor, ProcessingRecipe recipe, int inBag, int keep)
    {
        if (inBag < 0 || keep < 0)
            throw new ArgumentOutOfRangeException(nameof(inBag));
        return Math.Max(0, Math.Min(recipe.InputCapacity - processor.input, inBag - keep));
    }

    public static ProcessorSaveData Processor(IndustrySaveData data, string buildingId)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        var processor = data.processors.Find(p => p.buildingId == buildingId);
        if (processor == null)
            data.processors.Add(processor = new ProcessorSaveData { buildingId = buildingId });
        return processor;
    }

    /// <summary>가공을 켜고 끔. 처음 켜면 한 개를 바로 줌 (체험)</summary>
    /// <returns>처음 켜서 체험 한 개를 줬나</returns>
    public static bool SetOn(ProcessorSaveData processor, ProcessingRecipe recipe, bool on)
    {
        processor.on = on;
        if (!on || processor.trialGiven)
            return false;
        processor.trialGiven = true;
        processor.tray = Math.Min(recipe.TrayCapacity, processor.tray + 1);
        return true;
    }

    /// <summary>원재료를 넣음 (넣을 자리만큼)</summary>
    /// <returns>실제로 넣은 수 — 나머지는 부르는 쪽이 가방 등으로</returns>
    public static int Feed(ProcessorSaveData processor, ProcessingRecipe recipe, int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        int taken = Math.Min(amount, Math.Max(0, recipe.InputCapacity - processor.input));
        processor.input += taken;
        return taken;
    }

    /// <summary>seconds 동안 만듦 (꺼져 있으면 안 만듦). speed = 가공 속도 배수 (Speed)</summary>
    /// <returns>새로 만든 수</returns>
    public static int Run(ProcessorSaveData processor, ProcessingRecipe recipe, double seconds, float speed = 1f)
    {
        if (!processor.on || seconds <= 0 || double.IsNaN(seconds))
            return 0;
        if (speed <= 0f)
            throw new ArgumentOutOfRangeException(nameof(speed));
        double progress = Math.Max(0, processor.progress) + seconds * speed;
        int byTime = (int)Math.Floor(progress / recipe.CycleSeconds);
        int byInput = processor.input / recipe.InputCount;
        int byTray = Math.Max(0, recipe.TrayCapacity - processor.tray);
        int made = Math.Min(byTime, Math.Min(byInput, byTray));
        processor.input -= made * recipe.InputCount;
        processor.tray += made;
        progress -= made * recipe.CycleSeconds;
        // 재료가 없거나 가득 차서 멈춘 동안은 한 개 몫까지만 쌓임 (재료가 오면 바로 하나)
        processor.progress = (float)Math.Min(progress, recipe.CycleSeconds);
        return made;
    }

    /// <summary>
    /// 창고 재료 레시피: seconds 동안 만들 때마다 왕국 창고에서 한 번 몫을 꺼냄 (모자라면 기다림 · 진행은 한 개 몫까지).
    /// 시설이 가득 차면 멈춤 (꺼내지 않음)
    /// </summary>
    /// <returns>새로 만든 수</returns>
    public static int RunFromWarehouse(ProcessorSaveData processor, ProcessingRecipe recipe, double seconds, float speed, KingdomWarehouse warehouse)
    {
        if (!recipe.FromWarehouse)
            throw new ArgumentException("창고 재료 레시피가 아니에요", nameof(recipe));
        if (!processor.on || seconds <= 0 || double.IsNaN(seconds))
            return 0;
        if (speed <= 0f)
            throw new ArgumentOutOfRangeException(nameof(speed));
        double progress = Math.Max(0, processor.progress) + seconds * speed;
        int byTime = (int)Math.Floor(progress / recipe.CycleSeconds);
        int limit = Math.Min(byTime, Math.Max(0, recipe.TrayCapacity - processor.tray));
        var costs = recipe.WarehouseCosts();
        int made = 0;
        while (made < limit && warehouse.TryConsume(costs, "processing"))
            made++;
        processor.tray += made;
        progress -= made * recipe.CycleSeconds;
        processor.progress = (float)Math.Min(progress, recipe.CycleSeconds);
        return made;
    }

    /// <summary>시설에 쌓인 가공품을 창고로 (칸만큼)</summary>
    /// <returns>옮긴 수</returns>
    public static int Collect(ProcessorSaveData processor, ProcessingRecipe recipe, KingdomWarehouse warehouse, string reason)
    {
        if (processor.tray <= 0)
            return 0;
        int moved = warehouse.Add(recipe.OutputId, processor.tray, reason);
        processor.tray -= moved;
        return moved;
    }

    /// <summary>지금 1시간에 실제로 만드는 양: 들어오는 재료(시간당)로 만들 수 있는 만큼 (레시피 최대 × 속도를 넘지 않음)</summary>
    public static float ActualPerHour(ProcessingRecipe recipe, double inputPerHour, float speed = 1f) =>
        (float)Math.Min(recipe.OutputPerHour * speed, Math.Max(0, inputPerHour) / recipe.InputCount);
}

/// <summary>
/// 수레 (순수 C#, 기획서 7.1): 고른 시설의 가공품을 창고로 저절로 옮김. 기술로 다닐 수 있는 시설 수가 늘어남 (L02 1곳 · L03 3곳).
/// 실제 옮기기는 장부 — 화면의 수레는 연출일 뿐 (길이 막혀도 옮기기는 됨)
/// </summary>
public static class CartRules
{
    /// <summary>개량 수레(L06)는 더 많이 실어 다섯 곳까지</summary>
    public const int BetterCartRoutes = 5;

    public static int RouteLimit(Func<string, bool> hasTech) =>
        hasTech(TechIds.BetterCart) ? BetterCartRoutes : hasTech(TechIds.Dispatch) ? 3 : hasTech(TechIds.Cart) ? 1 : 0;

    public static bool HasRoute(IndustrySaveData data, string buildingId) => data.cartRoutes.Contains(buildingId);

    /// <returns>바꿨나 (켜려는데 수레가 모자라면 false)</returns>
    public static bool SetRoute(IndustrySaveData data, string buildingId, bool on, int limit)
    {
        if (!on)
            return data.cartRoutes.Remove(buildingId);
        if (data.cartRoutes.Contains(buildingId))
            return false;
        if (data.cartRoutes.Count >= limit)
            return false;
        data.cartRoutes.Add(buildingId);
        return true;
    }

    /// <summary>수레가 다니는 가공 줄의 가공품을 창고로 (수레 수보다 많이 저장돼 있으면 앞에서부터 그 수만큼만)</summary>
    /// <returns>(가공 줄 ID, 옮긴 수)</returns>
    public static List<(string processorId, int moved)> Deliver(IndustrySaveData data, IReadOnlyList<ProcessingRecipe> recipes,
        KingdomWarehouse warehouse, int limit)
    {
        var result = new List<(string, int)>();
        for (int i = 0; i < data.cartRoutes.Count && i < limit; i++)
        {
            string processorId = data.cartRoutes[i];
            foreach (var recipe in recipes)
            {
                if (recipe.ProcessorId != processorId)
                    continue;
                int moved = ProcessingRules.Collect(ProcessingRules.Processor(data, processorId), recipe, warehouse, "cart");
                if (moved > 0)
                    result.Add((processorId, moved));
            }
        }
        return result;
    }
}
