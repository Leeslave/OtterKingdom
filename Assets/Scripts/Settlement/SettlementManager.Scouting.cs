using System;
using System.Collections.Generic;
using UnityEngine;

public partial class SettlementManager
{
    private IslandMapSheet _islandMap;

    public bool HasIslandMap => IsLoaded && HasDevelopment(ScoutingRules.MapJoined);

    public void OpenIslandMap()
    {
        if (!IsLoaded) return;
        if (_islandMap == null) _islandMap = gameObject.AddComponent<IslandMapSheet>();
        _islandMap.Open(this);
    }

    public void OpenSouthernMap()
    {
        OpenIslandMap();
        if (_islandMap != null) _islandMap.Focus(TerritoryDirection.South);
    }

    public bool TryStartScouting(TerritoryExpansionDefinition territory, IReadOnlyList<string> workers)
    {
        FinishDueWork();
        TickScouting();
        if (!ScoutingRules.CanScout(_config, Settlement, territory)) return false;
        RunProduction();
        bool started = Settlement.Scouting.TryStart(territory.ExpansionId, workers,
            id => _config.FindOtter(id) is SettlementOtterDefinition otter && CanAssign(otter),
            NowTicks, TimeSpan.FromSeconds(DevTimers.Duration((float)ScoutingRules.Duration(territory).TotalSeconds)));
        if (!started) return false;
        OnChanged?.Invoke();
        SaveRequested?.Invoke();
        return true;
    }

    public bool CancelScouting()
    {
        FinishDueWork(); // 귀환 시각을 넘긴 정찰을 회수로 바꾸지 않는다.
        RunProduction();
        if (!Settlement.Scouting.Cancel()) return false;
        OnChanged?.Invoke();
        SaveRequested?.Invoke();
        return true;
    }

    public TerritoryExpansionDefinition ScoutDestination(string id)
    {
        foreach (var territory in _config.Territories)
            if (territory != null && territory.ExpansionId == id) return territory;
        return null;
    }

    private void TickScouting()
    {
        if (!IsLoaded) return;
        bool changed = false;
        if (!HasIslandMap && Settlement.IsCompleted("req_upgrade_board")
            && InventoryManager.Instance?.MineMazeState?.mapOtter == true)
        {
            Settlement.UnlockDevelopment(ScoutingRules.MapJoined);
            GameNotices.Post(new GameNotice("지도 해달이 마을회관에 합류했어요. 섬 지도를 열어 정찰대를 보내 봐요!", "섬 지도", OpenIslandMap));
            changed = true;
        }
        if (Settlement.Scouting.Destination != null && Settlement.Scouting.EndUtcTicks <= NowTicks)
        {
            // 정찰 중에는 생산하지 않는다. 오프라인 8시간 한도를 한 번만 적용해 귀환 전후를 나눈다.
            RunProduction(Settlement.Scouting.EndUtcTicks, NowTicks);
            string discovered = Settlement.Scouting.Tick(NowTicks);
            if (discovered != null)
            {
                _territoryVersion++;
                var territory = ScoutDestination(discovered);
                string report = territory?.Direction == TerritoryDirection.South && territory.Tier == 1
                    ? "남쪽 숲 너머에서 바닷가와 낡은 선착장을 찾았어요! 이제 그쪽 길을 개간해요."
                    : $"정찰대가 {territory?.DisplayName ?? discovered}을 발견하고 돌아왔어요. 주민들이 다시 일을 할 수 있어요.";
                GameNotices.Post(new GameNotice(report, "발견 보고", OpenIslandMap));
                changed = true;
            }
        }
        // 발견 → 남쪽 경로 확보 → 기존 선착장 부탁. 이미 열린 낚시는 그대로 유지한다.
        if (!HasDevelopment("fishing_found") && ScoutingRules.UsesScouting(Settlement))
            foreach (var territory in _config.Territories)
                if (territory != null && territory.Direction == TerritoryDirection.South && territory.Tier == 1
                    && Settlement.Scouting.HasDiscovered(territory.ExpansionId) && TerritoryRules.IsExpanded(territory, Settlement))
                {
                    Settlement.UnlockDevelopment("fishing_found");
                    GameNotices.Post(new GameNotice("바닷가로 가는 길이 열렸어요. 게시판에서 선착장을 복구해요!"));
                    changed = true;
                    break;
                }
        if (changed)
        {
            OnChanged?.Invoke();
            SaveRequested?.Invoke();
        }
    }
}
