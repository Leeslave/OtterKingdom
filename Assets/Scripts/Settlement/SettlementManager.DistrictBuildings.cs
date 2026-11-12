using UnityEngine;

/// <summary>
/// 특성 건물은 맞는 구역에만 (구역·이사 5막): 행정 해달이 생긴 뒤, 구역이 정해진 특성 건물(가게 · 공방 · 채석장 …)을 새로 지을 때
/// 그 용도 구역의 열린 땅 안에만 놓을 수 있다. 집 · 의자 같은 게시판 부탁 건물과 이미 지은 건물(옮기기는 이사 · 민원)은 제한하지 않는다.
/// 중앙(행정)은 플레이어가 정하는 용도가 아니라 제한하지 않는다
/// </summary>
public partial class SettlementManager
{
    /// <summary>이 물건을 새로 지을 때 들어가야 하는 구역 (제한 없으면 None)</summary>
    public DistrictType RequiredDistrictFor(DecorDefinition decor)
    {
        if (!IsAdministrator || !(decor is BuildingDefinition building))
            return DistrictType.None;
        var type = building.District;
        return type == DistrictType.Central ? DistrictType.None : type;
    }

    /// <summary>그 용도의 구역이 있는지 (없으면 짓기 전에 구역부터)</summary>
    public bool HasDistrictFor(DistrictType type) => type == DistrictType.None || DistrictRules.HasDistrict(_config, Settlement, type);

    /// <summary>이 칸(격자 칸, 세이브 좌표 아님)에 놓으면 들어가야 할 구역 안인지</summary>
    public bool FitsRequiredDistrict(DecorDefinition decor, RectInt gridArea)
    {
        var type = RequiredDistrictFor(decor);
        var board = PlazaBoard;
        if (type == DistrictType.None || board == null)
            return true;
        var block = DistrictRules.BlockAt(_config, Settlement, new RectInt(gridArea.position - board.SaveOrigin, gridArea.size));
        return block != null && DistrictRules.TypeOf(block, Settlement) == type;
    }

    /// <summary>새 특성 건물을 처음 놓아 볼 자리: 맞는 구역의 빈 부지 칸 (격자 칸, 없으면 false)</summary>
    public bool TryFindDistrictSpot(DecorDefinition decor, Vector2Int footprint, out Vector2Int gridOrigin)
    {
        gridOrigin = default;
        var type = RequiredDistrictFor(decor);
        var board = PlazaBoard;
        if (type == DistrictType.None || board == null)
            return false;
        var slots = RelocationRules.FreeSlots(_config, Settlement, type, footprint, PlazaOccupied());
        var layout = DecorManager.Instance.GetLayout(board);
        foreach (var slot in slots)
        {
            var candidate = DistrictRules.OriginIn(slot.Slot, footprint) + board.SaveOrigin;
            if (layout.Check(decor, candidate, DecorRotation.R0) != DecorPlacementResult.Ok) continue;
            gridOrigin = candidate;
            return true;
        }
        return false;
    }

    public TerritoryExpansionDefinition NextDistrictExpansion(DistrictType type)
    {
        TerritoryExpansionDefinition best = null;
        int bestScore = int.MaxValue;
        foreach (var block in _config.DistrictBlocks)
        {
            if (block == null) continue;
            var current = DistrictRules.TypeOf(block, Settlement);
            if (current != DistrictType.None && current != type) continue;
            int open = DistrictRules.OpenStages(block, Settlement);
            if (open >= block.Stages.Count) continue;
            foreach (var territory in _config.Territories)
            {
                if (territory == null || territory.ExpansionId != block.Stages[open].Development
                    || HasDevelopment(territory.ExpansionId) || !ScoutingRules.CanOpenRoute(Settlement, territory)) continue;
                int score = open * 10 + (block.SuggestedType == type ? 0 : 5);
                if (score >= bestScore) continue;
                best = territory; bestScore = score;
            }
        }
        return best;
    }

    public string DistrictSpaceHint(DistrictType type)
    {
        foreach (var block in DistrictRules.PendingBlocks(_config, Settlement))
            return $"왕국 화면의 구역 탭에서 {block.DisplayName}을 {Districts.DisplayName(type)}으로 정해 주세요.";
        var territory = NextDistrictExpansion(type);
        return territory != null
            ? $"{territory.DisplayName}을 정찰·개간하면 {Districts.DisplayName(type)}에 쓸 땅이 넓어져요. 왕국 화면의 섬 지도를 열어 보세요."
            : $"{Districts.DisplayName(type)}에 빈자리가 부족해요. 물건을 옮기거나 섬 지도에서 새 땅을 열어 주세요.";
    }

    public void OpenDistrictExpansionMap(DistrictType type)
    {
        var territory = NextDistrictExpansion(type);
        OpenIslandMap();
        if (territory != null && _islandMap != null) _islandMap.Focus(territory.Direction);
    }
}
