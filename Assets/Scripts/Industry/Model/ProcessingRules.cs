using System;
using System.Collections.Generic;

/// <summary>가공 레시피 (순수 값). 데이터는 ProcessingRecipeDefinition</summary>
public readonly struct ProcessingRecipe
{
    public readonly string BuildingId;
    public readonly string InputId;
    public readonly int InputCount;
    public readonly string OutputId;
    public readonly float CycleSeconds;

    public ProcessingRecipe(string buildingId, string inputId, int inputCount, string outputId, float cycleSeconds)
    {
        if (string.IsNullOrEmpty(buildingId) || string.IsNullOrEmpty(inputId) || string.IsNullOrEmpty(outputId))
            throw new ArgumentException("레시피의 시설 · 재료 ID가 비었어요");
        if (inputCount <= 0 || cycleSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(inputCount), "재료 수 · 시간은 0보다 커야 해요");
        BuildingId = buildingId;
        InputId = inputId;
        InputCount = inputCount;
        OutputId = outputId;
        CycleSeconds = cycleSeconds;
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

    /// <summary>seconds 동안 만듦 (꺼져 있으면 안 만듦)</summary>
    /// <returns>새로 만든 수</returns>
    public static int Run(ProcessorSaveData processor, ProcessingRecipe recipe, double seconds)
    {
        if (!processor.on || seconds <= 0 || double.IsNaN(seconds))
            return 0;
        double progress = Math.Max(0, processor.progress) + seconds;
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

    /// <summary>지금 1시간에 실제로 만드는 양: 들어오는 재료(시간당)로 만들 수 있는 만큼 (레시피 최대를 넘지 않음)</summary>
    public static float ActualPerHour(ProcessingRecipe recipe, double inputPerHour) =>
        (float)Math.Min(recipe.OutputPerHour, Math.Max(0, inputPerHour) / recipe.InputCount);
}

/// <summary>
/// 수레 (순수 C#, 기획서 7.1): 고른 시설의 가공품을 창고로 저절로 옮김. 기술로 다닐 수 있는 시설 수가 늘어남 (L02 1곳 · L03 3곳).
/// 실제 옮기기는 장부 — 화면의 수레는 연출일 뿐 (길이 막혀도 옮기기는 됨)
/// </summary>
public static class CartRules
{
    public static int RouteLimit(Func<string, bool> hasTech) =>
        hasTech(TechIds.Dispatch) ? 3 : hasTech(TechIds.Cart) ? 1 : 0;

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

    /// <summary>수레가 다니는 시설의 가공품을 창고로 (수레 수보다 많이 저장돼 있으면 앞에서부터 그 수만큼만)</summary>
    /// <returns>(시설 ID, 옮긴 수)</returns>
    public static List<(string buildingId, int moved)> Deliver(IndustrySaveData data, IReadOnlyList<ProcessingRecipe> recipes,
        KingdomWarehouse warehouse, int limit)
    {
        var result = new List<(string, int)>();
        for (int i = 0; i < data.cartRoutes.Count && i < limit; i++)
        {
            string buildingId = data.cartRoutes[i];
            foreach (var recipe in recipes)
            {
                if (recipe.BuildingId != buildingId)
                    continue;
                int moved = ProcessingRules.Collect(ProcessingRules.Processor(data, buildingId), recipe, warehouse, "cart");
                if (moved > 0)
                    result.Add((buildingId, moved));
            }
        }
        return result;
    }
}
