using UnityEngine;

// 오래된 갱도(광산 미로) 발견: 왕국 Lv.5 + 곡괭이 강화 1회 + 광부가 채굴 중 + 또박이를 게시판에 배치한 뒤면
// 깡깡이가 갱도를 찾는다 (MineMazeRules). 어느 장소에서든 1초마다 확인하고, 찾으면 발전으로 남겨 저장하고
// 광산에서 연락이 온다. 또박이 배치 뒤의 안내(행정 해달 · 첫 구역)가 떠 있는 동안은 기다린다.
// 광산 씬에서는 광산 카트가 입구가 되고(MineMazeEntranceView), 메인 Lv.5 "오래된 갱도"가 이 발전을 센다.
public partial class GameManager
{
    private const float MineMazeCheckSeconds = 1f;
    private float mineMazeCheckTimer;

    private void TickMineMazeDiscovery(float dt)
    {
        mineMazeCheckTimer -= dt;
        if (mineMazeCheckTimer > 0f) return;
        mineMazeCheckTimer = MineMazeCheckSeconds;

        var settlement = SettlementManager.Instance;
        if (settlement == null || !settlement.IsLoaded) return;
        // 또박이를 막 배치했으면 행정 해달 안내(승격)가 먼저
        if (IsModalOpen || TutorialOverlay.IsShowing || DistrictGuideRules.CanPromote(settlement.Settlement)) return;
        int level = ProfileManager.Instance != null ? ProfileManager.Instance.Level : 1;
        if (!MineMazeRules.ShouldDiscover(settlement.HasDevelopment(MineMazeRules.FoundDevelopment), level,
                miningService.PickaxeLevel, miningBalance.offlineUnlockPickaxeLevel, IsMiningActive, CanProduceIn(MineZoneId),
                settlement.Settlement.IsCompleted(MineMazeRules.BoardOtterRequest)))
            return;

        settlement.Settlement.UnlockDevelopment(MineMazeRules.FoundDevelopment);
        SaveNow();
        AnalyticsLog.Track("mine_maze_found", ("level", level), ("pickaxeLevel", miningService.PickaxeLevel));

        const string found = "광산에서 연락이 왔어요!\n깡깡이: 광산 안쪽에서 누군가 쓰던 오래된 갱도를 찾았어요!";
        if (CurrentZoneId == MineZoneId)
        {
            GameNotices.Post(new GameNotice(found + "\n반짝이는 광산 카트를 눌러 들어가 봐요"));
            return;
        }
        var link = MineMazeLinkConfig.Instance;
        GameNotices.Post(link != null && link.MineZone != null
            ? new GameNotice(found + "\n광산에서 반짝이는 광산 카트를 눌러 봐요", "광산 가기", () =>
            {
                var navigator = FindAnyObjectByType<SceneNavigator>();
                if (navigator != null) navigator.TryGo(link.MineZone);
            })
            : new GameNotice(found));
    }
}
