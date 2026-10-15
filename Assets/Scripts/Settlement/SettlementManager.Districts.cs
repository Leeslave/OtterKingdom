using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 구역 (구역·이사 기획): 블록 용도 정하기 · 왕국 질서도 · 구역 보너스.
/// 건물이 놓인 자리는 광장 꾸미기 격자, 블록 용도는 Settlement가 원본. 계산은 DistrictRules
/// </summary>
public partial class SettlementManager
{
    public bool IsAdministrator => IsLoaded && Settlement.HasDevelopment(DistrictGuideRules.Promotion);
    public event Action<DistrictBlockDefinition> OnDistrictRequested;

    public void RequestDistrict(DistrictBlockDefinition block = null)
    {
        if (IsAdministrator) OnDistrictRequested?.Invoke(block);
    }

    public bool TryPromoteAdministrator()
    {
        if (!IsLoaded || !DistrictGuideRules.Promote(Settlement)) return false;
        SaveRequested?.Invoke();
        return true;
    }

    private DistrictOrder _districtOrder;
    private int _districtOrderFrame = -1;

    /// <summary>블록 용도를 정했을 때 (블록)</summary>
    public event Action<DistrictBlockDefinition> OnDistrictDesignated;

    public IReadOnlyList<DistrictBlockDefinition> DistrictBlocks => _config.DistrictBlocks;

    public DistrictType DistrictTypeOf(DistrictBlockDefinition block) => DistrictRules.TypeOf(block, Settlement);

    /// <summary>열렸지만 아직 용도를 고르지 않은 블록</summary>
    public List<DistrictBlockDefinition> PendingDistrictBlocks() => DistrictRules.PendingBlocks(_config, Settlement);

    public List<DistrictBlockDefinition> EditableDistrictBlocks() => DistrictRules.EditableBlocks(_config, Settlement);

    public List<DistrictType> DistrictChoices(DistrictBlockDefinition block) => DistrictRules.Choices(_config, Settlement, block);

    /// <summary>블록 용도를 정하고 저장 (안 되면 이유, 아무것도 바꾸지 않음)</summary>
    public DistrictDesignateBlock TryDesignateDistrict(DistrictBlockDefinition block, DistrictType type)
    {
        if (!IsLoaded)
            return DistrictDesignateBlock.Unknown;
        var reason = DistrictRules.Designate(_config, Settlement, block, type);
        if (reason != DistrictDesignateBlock.None)
            return reason;
        _districtOrderFrame = -1;
        OnDistrictDesignated?.Invoke(block);
        SaveRequested?.Invoke();
        return reason;
    }

    /// <summary>지금 왕국 질서도 (한 프레임에 한 번만 계산)</summary>
    public DistrictOrder DistrictOrder
    {
        get
        {
            if (!IsLoaded)
                return null;
            int frame = Time.frameCount;
            if (_districtOrder == null || _districtOrderFrame != frame)
            {
                _districtOrder = DistrictRules.Evaluate(_config, Settlement, DistrictPlacements());
                _districtOrderFrame = frame;
            }
            return _districtOrder;
        }
    }

    /// <summary>광장에 놓인 구역 건물들 (자리를 골라 짓는 건물 · 부탁 건물 자리, 세이브 좌표)</summary>
    public List<DistrictPlacement> DistrictPlacements()
    {
        var list = new List<DistrictPlacement>();
        var decor = DecorManager.Instance;
        var board = decor != null ? decor.FindBoard(_config.PlazaBoardId) : null;
        if (board == null)
            return list;
        foreach (var placed in decor.GetLayout(board).Placed)
        {
            if (placed.Decor is IDistrictBuilding building && building.District != DistrictType.None)
            {
                var area = placed.Area;
                list.Add(new DistrictPlacement(building.District, new RectInt(area.position - board.SaveOrigin, area.size)));
            }
        }
        return list;
    }

    /// <summary>구역 보너스 (%). KingdomBonus가 특성 건물 효과와 합쳐 묻는다</summary>
    public int DistrictBonusPercent(KingdomBonusKind kind) =>
        IsLoaded ? DistrictRules.BonusPercent(_config, Settlement, DistrictOrder, kind) : 0;

    // 왕국 보유 효과 합계: 다 지은 특성 건물 + 구역
    private int TotalBonusPercent(KingdomBonusKind kind) => BonusPercent(kind) + DistrictBonusPercent(kind);
}
