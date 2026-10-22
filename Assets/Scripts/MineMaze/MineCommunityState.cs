using System;

/// <summary>One-time deep-mine story. Time away never advances illness or consumes treatment.</summary>
[Serializable]
public class MineCommunityState
{
    public bool started;
    public bool concernAccepted;
    public bool supportReceived;
    public bool treated;
    public bool sourceRemoved;
    public bool exported;

    public const string HerbId = "item_fairy_herb";
    public const string HospitalId = "bld_mine_hospital";
    /// <summary>예전에 따로 짓던 마을 길드 건물 (이제 꾸미기 목록에 없음. 지은 세이브만 길드로 셈)</summary>
    public const string GuildId = "bld_mine_guild";
    /// <summary>병원이 생기면 열림 → 게시판 "우리 마을의 길드"(마을회관 넓히기)가 이걸 조건으로 둠</summary>
    public const string HospitalDevelopment = "mine_hospital";
    public const int HerbsRequired = 10;
    public const float UntreatedMultiplier = .90f;
    public const float TreatedMultiplier = .95f;
    public float ProductionMultiplier => !started || sourceRemoved ? 1f
        : treated ? TreatedMultiplier : UntreatedMultiplier;

    public bool Discover(int floor, bool fishingOpen)
    {
        if (started || !fishingOpen || floor < 3) return false;
        started = true;
        return true;
    }

    public bool AcceptConcern()
    {
        if (!started || concernAccepted) return false;
        concernAccepted = true;
        return true;
    }

    public bool Treat(Inventory bag, ItemDefinition herb)
    {
        if (!concernAccepted || treated || bag == null || herb == null || herb.ItemId != HerbId
            || bag.GetCount(herb) < HerbsRequired) return false;
        if (!bag.TryRemove(herb, HerbsRequired, ItemChangeReason.Delivery)) return false;
        treated = true;
        return true;
    }

    public bool RemoveSource()
    {
        if (!started || !treated || sourceRemoved) return false;
        sourceRemoved = true;
        return true;
    }

    public bool Export(bool hospitalBuilt, bool guildBuilt)
    {
        if (!sourceRemoved || exported || !hospitalBuilt || !guildBuilt) return false;
        exported = true;
        return true;
    }
}
