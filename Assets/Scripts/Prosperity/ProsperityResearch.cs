using System;
using System.Collections.Generic;

[Serializable]
public class ProsperitySaveData
{
    public bool introCompleted;
    public int rp;
    public List<string> learned = new List<string>();
    public List<string> rpGrants = new List<string>();
    public List<string> advanceCompleted = new List<string>();
    public ProsperityAdvanceSaveData advance;
    public List<string> marketAccepted = new List<string>();
    public List<string> marketCompleted = new List<string>();
}

public sealed class ProsperityTech
{
    public string Id { get; }
    public string Name { get; }
    public string Effect { get; }
    public int Level { get; }
    public int Cost { get; }
    public IReadOnlyList<string> Prerequisites { get; }

    public ProsperityTech(string id, string name, string effect, int level, int cost, params string[] prerequisites)
    {
        Id = id; Name = name; Effect = effect; Level = level; Cost = cost;
        Prerequisites = Array.AsReadOnly(prerequisites);
    }
}

public enum ResearchResult { Success, UnknownTech, AlreadyLearned, NeedsIntro, NeedsLevel, NeedsPrerequisite, NotEnoughRp, NotImplemented }

/// <summary>Unity·가방·창고와 분리된 연구 및 지급 기록.</summary>
public sealed partial class ProsperityResearch
{
    public const int LastResearchLevel = 50;

    public static readonly IReadOnlyList<ProsperityTech> Techs = Array.AsReadOnly(new[]
    {
        new ProsperityTech(TechIds.Sawmill, "제재 기술", "목공소에서 판재를 만들어요", 20, 1),
        new ProsperityTech(TechIds.Warehouse, "공동 창고", "왕국 자재 보관 공간을 늘려요", 20, 1, TechIds.Sawmill),
        new ProsperityTech(TechIds.Market, "장터 주문", "가방 재료로 장터 주문을 납품해요", 21, 1, TechIds.Sawmill),
        new ProsperityTech(TechIds.TownPlanning, "마을 정비", "왕국 표지판과 가로등을 만들어요", 22, 1, TechIds.Sawmill),
        new ProsperityTech(TechIds.StoneCutting, "석재 규격화", "채석 작업소에서 석재 블록을 만들어요", 23, 2, TechIds.Sawmill),
        new ProsperityTech(TechIds.Cart, "운송 수레", "시설 한 곳의 가공품을 자동 수거해요", 24, 2, TechIds.Warehouse),
        new ProsperityTech(TechIds.BetterHouses, "주거 개량 1차", "기존 집을 개량 주택으로 바꿔요", 25, 2, TechIds.TownPlanning, TechIds.Warehouse),

        // P1: 산업 · 물류 · 도시
        new ProsperityTech(TechIds.Smelting, "금속 제련", "건설소에서 조개 구리를 금속 주괴로 가공해요", 28, 2, TechIds.StoneCutting),
        new ProsperityTech(TechIds.Dispatch, "운송 배차", "수레 경로를 세 곳까지 연결해요", 29, 1, TechIds.Cart),
        new ProsperityTech(TechIds.StandardTools, "표준 공구", "가공 속도가 두 배가 돼요", 30, 1, TechIds.Smelting),
        new ProsperityTech(TechIds.AutoSupply, "원료 자동 공급", "가방의 원료를 시설에 자동으로 넣어요", 31, 1, TechIds.Dispatch),
        new ProsperityTech(TechIds.Paving, "도로 포장", "중앙 광장 돌길에서 왕국 큰길을 이을 수 있어요", 32, 1, TechIds.TownPlanning),
        new ProsperityTech(TechIds.WaterPower, "수력 기계", "가공 속도가 15% 더 빨라져요", 33, 1, TechIds.StandardTools),
        new ProsperityTech(TechIds.Depot, "집산소", "왕국 창고의 보관 공간을 400칸으로 늘려요", 34, 1, TechIds.Warehouse),
        new ProsperityTech(TechIds.PublicSpaces, "공공 시설", "창고 자재로 광장 분수와 돌 꽃밭을 만들어요", 35, 1, TechIds.Paving, TechIds.Smelting),
        new ProsperityTech(TechIds.PrecisionWork, "정밀 공방", "목공소에서 판재와 주괴로 정밀 부품을 가공해요", 36, 2, TechIds.Smelting, TechIds.Warehouse),
        new ProsperityTech(TechIds.BetterCart, "개량 수레", "수레 경로를 다섯 곳까지 연결해요", 38, 1, TechIds.Dispatch),
        new ProsperityTech(TechIds.Administration, "왕국 행정", "개량 주택을 마을 주택으로 한 번 더 개량해요", 40, 2, TechIds.BetterHouses, TechIds.Smelting),
        new ProsperityTech(TechIds.IndustrialLogistics, "산업 물류 체계", "일터에서 가공 줄별 상태와 생산량을 확인해요", 42, 2, TechIds.PrecisionWork, TechIds.BetterCart),

        // P2: 협업 생산 · 왕국 공사
        new ProsperityTech(TechIds.CoProduction, "협업 생산", "가동 중인 가공 줄마다 생산 속도가 5%씩 올라요", 43, 1, TechIds.IndustrialLogistics),
        new ProsperityTech(TechIds.RoyalIndustry, "왕립 산업단지", "왕립 산업단지 공사를 열어요", 45, 2, TechIds.CoProduction, TechIds.PrecisionWork),
        new ProsperityTech(TechIds.Academy, "왕립 학술원", "기록관을 왕립 학술원으로 바꾸는 공사를 열어요", 46, 2, TechIds.Administration, TechIds.PrecisionWork),
        new ProsperityTech(TechIds.Capital, "수도 계획", "중앙 광장을 꾸미는 수도 광장 공사를 열어요", 48, 1, TechIds.PublicSpaces, TechIds.Administration),
        new ProsperityTech(TechIds.Palace, "왕궁 증축", "성을 왕궁으로 바꾸는 공사를 열어요", LastResearchLevel, 2, TechIds.Academy, TechIds.Capital),
    });

    public ProsperitySaveData Data { get; }
    public event Action Changed;
    public event Action<string> Learned;

    public ProsperityResearch(ProsperitySaveData data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Sanitize(data);
    }

    public static bool Sanitize(ProsperitySaveData data)
    {
        bool changed = false;
        if (data.learned == null) { data.learned = new List<string>(); changed = true; }
        if (data.rpGrants == null) { data.rpGrants = new List<string>(); changed = true; }
        if (data.advanceCompleted == null) { data.advanceCompleted = new List<string>(); changed = true; }
        if (data.marketAccepted == null) { data.marketAccepted = new List<string>(); changed = true; }
        if (data.marketCompleted == null) { data.marketCompleted = new List<string>(); changed = true; }
        if (data.rp < 0) { data.rp = 0; changed = true; }
        changed |= Clean(data.learned);
        changed |= Clean(data.rpGrants);
        changed |= Clean(data.advanceCompleted);
        changed |= Clean(data.marketAccepted);
        changed |= Clean(data.marketCompleted);
        changed |= data.marketAccepted.RemoveAll(id => data.marketCompleted.Contains(id)) > 0;
        changed |= SanitizeAdvance(data);
        return changed;
    }

    private static bool Clean(List<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return values.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !seen.Add(id)) > 0;
    }

    public bool HasTech(string id) => !string.IsNullOrEmpty(id) && Data.learned.Contains(id);
    public static ProsperityTech Find(string id)
    {
        foreach (var tech in Techs) if (tech.Id == id) return tech;
        return null;
    }

    public static bool CanEnter(int level, bool sourceRemoved, bool castleBuilt) => level >= 20 && sourceRemoved && castleBuilt;

    public bool CompleteIntro(int level, bool sourceRemoved, bool castleBuilt)
    {
        if (Data.introCompleted || !CanEnter(level, sourceRemoved, castleBuilt)) return false;
        Data.introCompleted = true;
        GrantOnce("prosperity:entry", 2);
        GrantReachedLevels(level);
        Changed?.Invoke();
        return true;
    }

    /// <summary>소급과 정상 레벨업이 동일한 지급 키를 사용한다.</summary>
    public bool SyncLevel(int level)
    {
        if (!Data.introCompleted) return false;
        bool changed = GrantReachedLevels(level);
        if (Data.advance != null && Data.advance.futureLevel <= level)
        {
            // 의뢰 중 목표 레벨에 도달하면 정상 레벨 보상을 받고 의뢰는 종료한다.
            Data.advance = null;
            changed = true;
        }
        if (changed) Changed?.Invoke();
        return changed;
    }

    private bool GrantReachedLevels(int level)
    {
        bool changed = false;
        for (int reached = 21; reached <= Math.Min(level, LastResearchLevel); reached++)
            changed |= GrantOnce($"prosperity:level:{reached}", 1);
        return changed;
    }

    // 지급 키를 사용하는 내부 경로만 허용한다. 외부에서 임의 RP를 생성하지 않는다.
    private bool GrantOnce(string key, int amount)
    {
        if (Data.rpGrants.Contains(key)) return false;
        int next = checked(Data.rp + amount);
        Data.rpGrants.Add(key);
        Data.rp = next;
        return true;
    }

    public ResearchResult Check(string id, int level)
    {
        var tech = Find(id);
        if (tech == null) return ResearchResult.UnknownTech;
        if (HasTech(id)) return ResearchResult.AlreadyLearned;
        if (!Data.introCompleted) return ResearchResult.NeedsIntro;
        if (level < tech.Level) return ResearchResult.NeedsLevel;
        foreach (string prerequisite in tech.Prerequisites)
            if (!HasTech(prerequisite)) return ResearchResult.NeedsPrerequisite;
        if (Data.rp < tech.Cost) return ResearchResult.NotEnoughRp;
        return ResearchResult.Success;
    }

    public ResearchResult TryLearn(string id, int level)
    {
        var result = Check(id, level);
        if (result != ResearchResult.Success) return result;
        Data.rp -= Find(id).Cost;
        Data.learned.Add(id);
        // 전체 상태가 반영된 뒤 저장 요청과 기능 해금 이벤트를 보낸다.
        Changed?.Invoke();
        Learned?.Invoke(id);
        return ResearchResult.Success;
    }
}
