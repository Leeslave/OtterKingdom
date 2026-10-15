using System;
using System.Collections.Generic;

/// <summary>용도를 정한 구역 블록 하나 (구역·이사)</summary>
[Serializable]
public class DistrictSaveData
{
    public string blockId;
    public DistrictType type;
}

/// <summary>
/// 구역 (구역·이사): 블록마다 정한 용도. 고를 수 있는지는 DistrictRules가 정하고, 여기는 기록만 한다.
/// 중앙 공터처럼 용도가 정해진 블록은 기록하지 않는다 (데이터가 원본)
/// </summary>
public partial class Settlement
{
    private readonly Dictionary<string, DistrictType> _districts = new Dictionary<string, DistrictType>();

    /// <summary>용도를 정한 블록 (블록 ID → 용도)</summary>
    public IReadOnlyDictionary<string, DistrictType> DistrictDesignations => _districts;

    /// <summary>그 블록에 정한 용도 (안 정했으면 None)</summary>
    public DistrictType GetDistrict(string blockId) =>
        !string.IsNullOrEmpty(blockId) && _districts.TryGetValue(blockId, out var type) ? type : DistrictType.None;

    /// <returns>바뀌었으면 true (None이면 정한 것을 지움)</returns>
    public bool SetDistrict(string blockId, DistrictType type)
    {
        if (string.IsNullOrEmpty(blockId))
            throw new ArgumentNullException(nameof(blockId));
        if (GetDistrict(blockId) == type)
            return false;
        if (type == DistrictType.None)
            _districts.Remove(blockId);
        else
            _districts[blockId] = type;
        OnChanged?.Invoke();
        return true;
    }

    private void LoadDistricts(SettlementSaveData saved)
    {
        _districts.Clear();
        if (saved.districts == null)
            return;
        foreach (var d in saved.districts)
        {
            // 알 수 없는 용도(더 새 버전의 세이브)·중복은 버림
            if (d == null || string.IsNullOrEmpty(d.blockId) || d.type == DistrictType.None
                || !Enum.IsDefined(typeof(DistrictType), d.type) || _districts.ContainsKey(d.blockId))
                continue;
            _districts[d.blockId] = d.type;
        }
    }

    private void WriteDistricts(SettlementSaveData result)
    {
        result.districts = new List<DistrictSaveData>();
        foreach (var pair in _districts)
            result.districts.Add(new DistrictSaveData { blockId = pair.Key, type = pair.Value });
        result.districts.Sort((a, b) => string.CompareOrdinal(a.blockId, b.blockId));
    }
}
