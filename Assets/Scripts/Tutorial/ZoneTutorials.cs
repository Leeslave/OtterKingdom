using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장소(씬 이름)별 첫 방문 튜토리얼 내용. 대상은 튜토리얼을 시작할 때 씬에서 찾고,
/// 없는 대상(아직 안 열린 NPC, 이 씬에 없는 버튼 등)을 가리키는 장은 자동으로 빠진다.
/// </summary>
public static class ZoneTutorials
{
    public const string Plaza = "Plaza";
    public const string Farm = "Farm";
    public const string Fishing = "Fishing";
    public const string Mine = "Mine";

    public static readonly string[] AllIds = { Plaza, Farm, Fishing, Mine };

    // 기능 튜토리얼: 장소의 첫 방문과 따로, 그 기능이 열린 뒤에 한 번 (완료 기록은 장소 튜토리얼과 같은 목록)
    // 요정 상점은 실제로 상점을 열어야 완료 (설명만 넘겨서는 완료가 아님)
    public const string FairyShop = "FairyShop";
    // 건설 모드: 처음 건물 자리를 고를 때 (첫 집). 실제로 공사를 시작해야 완료
    public const string Build = "Build";
    public static readonly string[] FeatureIds = { FairyShop, Build };

    // 건설 모드 설명을 끝까지 봤음 (완료가 아님). 다음에 자리를 고를 때는 설명 없이 [확인] 강조만
    public const string BuildIntro = "Build.Intro";
    public const string BuildPointerMessage = "자리를 정했으면 [확인]을 눌러 공사를 시작해요!";

    // 요정 상점 소개 설명을 끝까지 봤음 (완료가 아님). 다음 광장 방문부터는 설명 없이 요정 강조만 다시 보여 줌.
    // 이 기록이 없는 세이브(이전 버전)는 처음 보는 것으로 = 기본값 "안 봄"
    public const string FairyShopIntro = "FairyShop.Intro";

    public const string FairyShopPointerMessage = "요정을 눌러 상점을 열어 보세요!";

    public static bool Has(string sceneName) => System.Array.IndexOf(AllIds, sceneName) >= 0;

    public static List<TutorialStep> For(string sceneName, GameUI gameUI)
    {
        switch (sceneName)
        {
            case Plaza: return PlazaSteps();
            case Farm: return FarmSteps(gameUI);
            case Fishing: return FishingSteps(gameUI);
            case Mine: return MineSteps(gameUI);
            default: return new List<TutorialStep>();
        }
    }

    #region 장소별

    private static List<TutorialStep> PlazaSteps()
    {
        var topBar = Object.FindAnyObjectByType<TopBarView>();
        var navBar = Object.FindAnyObjectByType<NavBarView>();
        var board =Object.FindAnyObjectByType<SettlementBoardPropView>();
        var otter = NearestToScreenCenter(Object.FindObjectsByType<OtterWanderAgent>(FindObjectsSortMode.None));
        // Gold의 CurrencyID는 "Gold"가 아니라서 GameManager가 쓰는 에셋과 직접 비교
        var gold = GameManager.Instance.GoldCurrency;
        var goldPill = FindPill(c => c == gold);
        var gemPill = FindPill(c => c.CurrencyID == "Gem");

        return new List<TutorialStep>
        {
            TutorialStep.Info("해달 왕국에 온 걸 환영해요!",
                "여기는 왕국의 한가운데, 광장이에요.\n해달들이 모여 쉬고 노는 곳이에요.\n하나씩 둘러볼까요?"),
            TutorialStep.At("왕국 정보",
                "왕국의 이름과 레벨이에요.\n퀘스트를 끝내고 경험치를 모으면\n왕국 레벨이 올라가요.",
                () => TutorialTargets.Ui(topBar != null ? topBar.ProfileArea : null)),
            TutorialStep.At("골드",
                "작물, 물고기, 광석을 팔면 골드를 벌어요.\n밭을 늘리거나 도구를 강화할 때 써요.",
                () => TutorialTargets.Ui(goldPill)),
            TutorialStep.At("조개",
                "귀한 재화예요.\n[+]를 누르면 재화 상점이 열려요.",
                () => TutorialTargets.Ui(gemPill)),
            TutorialStep.At("도감",
                "지금까지 모은 작물, 물고기, 해달을 볼 수 있어요.\n처음 얻은 것은 여기 새로 등록돼요.",
                () => TutorialTargets.Ui(navBar != null ? navBar.CodexButton : null)),
            TutorialStep.At("퀘스트",
                "해 볼 만한 일들이 적혀 있어요.\n다 하면 여기서 보상을 받아요!",
                () => TutorialTargets.Ui(navBar != null ? navBar.QuestButton : null)),
            TutorialStep.At("꾸미기",
                "장난감과 소품을 놓아 장소를 꾸며요.\n놓아 둔 장난감은 해달들이 가지고 놀아요.",
                () => TutorialTargets.Ui(navBar != null ? navBar.DecorateButton : null)),
            TutorialStep.At("가방",
                "모은 물건이 전부 여기 들어가요.\n가방에서 물건을 팔아 골드로 바꿀 수 있어요.",
                () => TutorialTargets.Ui(navBar != null ? navBar.BagButton : null)),
            TutorialStep.At("이동",
                "밭, 낚시터, 광산 같은\n다른 장소로 갈 수 있어요.\n잠긴 곳은 왕국이 커지면 열려요.",
                () => TutorialTargets.Ui(navBar != null ? navBar.TravelButton : null)),
            // 요정 상점은 밭이 열린 뒤에 나타나므로 따로 안내 (FairyShopSteps)
            TutorialStep.AtWorld("해달 친구들",
                "광장을 돌아다니는 해달들이에요.\n가끔은 처음 보는 해달이 놀러 오기도 해요!",
                otter),
            TutorialStep.Info("광장 둘러보기",
                "화면을 끌면 광장 여기저기를 볼 수 있어요.\n두 손가락을 벌리거나 모으면\n크게, 작게 볼 수 있어요."),
            TutorialStep.TryIt("해달 게시판",
                "해달들의 부탁과 방명록이 붙어 있어요.\n게시판을 눌러 첫 부탁을 확인해 봐요!",
                () => TutorialTargets.World(board), board),
        };
    }

    /// <summary>
    /// 요정 상점 소개 설명 (밭이 열리면 광장에 요정이 옴). 광장 첫 튜토리얼과 따로.
    /// 설명이 끝나면 화면을 막지 않는 요정 강조(TutorialPointer)로 넘어가고, 실제로 상점을 열어야 완료된다
    /// </summary>
    public static List<TutorialStep> FairyShopSteps(FairyNpcView fairy)
    {
        return new List<TutorialStep>
        {
            TutorialStep.At("요정이 찾아왔어요!",
                "밭이 열리자 모종을 파는 요정이\n광장에 찾아왔어요.\n모종과 왕국에 필요한 물건을 살 수 있어요.",
                () => TutorialTargets.World(fairy)),
        };
    }

    /// <summary>
    /// 건설 모드 설명 (첫 건물 자리 고르기). 설명이 끝나면 화면을 막지 않는 [확인] 강조(TutorialPointer)로 넘어가고,
    /// 실제로 공사를 시작해야 완료된다
    /// </summary>
    public static List<TutorialStep> BuildSteps(DecorModePresenter decorMode)
    {
        return new List<TutorialStep>
        {
            TutorialStep.At("건설 모드",
                "건물을 지을 자리를 직접 골라요.\n초록 칸이면 지을 수 있고,\n빨간 칸이면 지을 수 없어요.",
                () => TutorialTargets.World(decorMode.GhostTarget)),
            TutorialStep.At("자리 옮기기",
                "건물을 꾹 누른 채 끌면 옮겨져요.\n빈 땅을 눌러도 그 자리로 와요.\n화면을 끌면 광장을 둘러볼 수 있어요.",
                () => TutorialTargets.World(decorMode.GhostTarget)),
            TutorialStep.At("공사 시작",
                "자리를 정했으면 [확인]을 눌러요.\n재료를 내면 그 자리에 공사가 시작돼요!\n다 지은 건물은 꾸미기에서 옮길 수 있어요.",
                () => TutorialTargets.Ui(decorMode.ConfirmButton)),
        };
    }

    private static List<TutorialStep> FarmSteps(GameUI gameUI)
    {
        PlotView firstPlot = null;
        PlotView lockedPlot = null;
        var farm = GameManager.Instance.FarmService;
        foreach (var plot in Object.FindObjectsByType<PlotView>(FindObjectsSortMode.None))
        {
            if (plot.PlotIndex == GameManager.FirstPlantGuidePlotIndex) firstPlot = plot;
            else if (!farm.IsPlotUnlocked(plot.PlotIndex) &&
                     (lockedPlot == null || plot.PlotIndex < lockedPlot.PlotIndex)) lockedPlot = plot;
        }
        var farmer = Object.FindAnyObjectByType<FarmerOtterController>();
        var offlineNpc = Object.FindAnyObjectByType<OfflineFarmNpcView>();
        var navBar = Object.FindAnyObjectByType<NavBarView>();

        return new List<TutorialStep>
        {
            TutorialStep.Info("여기는 밭이에요",
                "작물을 심고 키워서 수확하는 곳이에요."),
            TutorialStep.At("농부 해달",
                "다 자란 작물은 농부 해달이 알아서 수확하고,\n같은 작물을 다시 심어 줘요.\n수확한 작물은 가방에 들어가요.",
                () => TutorialTargets.World(farmer)),
            TutorialStep.At("잠긴 밭",
                "잠긴 밭을 누르면 골드로\n고랑을 한 칸씩 열 수 있어요.\n왕국 레벨이 오를수록 더 열 수 있어요.",
                () => TutorialTargets.World(lockedPlot)),
            TutorialStep.At("밭 강화",
                "골드로 밭을 강화하면\n작물이 더 빨리 자라요.\n레벨이 오르면 게임을 꺼 둔 동안에도 농사를 지어요.",
                () => TutorialTargets.Ui(gameUI.FarmUpgradeButton)),
            TutorialStep.At("오프라인 농사",
                "여기 작물을 등록해 두면\n게임을 꺼 둔 동안에도 농사를 지어요.",
                () => TutorialTargets.World(offlineNpc)),
            TutorialStep.At("수확물 팔기",
                "수확한 작물은 가방에서 팔아\n골드로 바꿀 수 있어요.",
                () => TutorialTargets.Ui(navBar != null ? navBar.BagButton : null)),
            TutorialStep.TryIt("밭고랑",
                "빈 칸을 누르면 심을 작물을 고를 수 있어요.\n모종이 필요한 작물은 가방의 모종을 써요.\n이 고랑을 눌러 심어 봐요!",
                () => TutorialTargets.World(firstPlot), firstPlot),
        };
    }

    private static List<TutorialStep> FishingSteps(GameUI gameUI)
    {
        var spot = Object.FindAnyObjectByType<FishingSpotView>();
        var otter = Object.FindAnyObjectByType<FishingOtterController>();

        return new List<TutorialStep>
        {
            TutorialStep.Info("여기는 낚시터예요",
                "해달이 물고기를 낚아 오는 곳이에요."),
            TutorialStep.At("낚시 해달",
                "낚은 물고기는 가방에 들어가요.\n가끔 쓰레기가 걸리기도 해요!\n낚시 중인 해달을 누르면 낚시를 멈춰요.",
                () => TutorialTargets.World(otter)),
            TutorialStep.At("낚싯대 강화",
                "골드로 낚싯대를 강화하면\n물고기가 더 잘 잡혀요.\n레벨이 오르면 게임을 꺼 둔 동안에도 낚시해요.",
                () => TutorialTargets.Ui(gameUI.RodUpgradeButton)),
            TutorialStep.TryIt("낚시 자리",
                "동그라미를 누르면 해달이 걸어가서\n낚시를 시작해요. 눌러 봐요!",
                () => TutorialTargets.World(spot), spot),
        };
    }

    private static List<TutorialStep> MineSteps(GameUI gameUI)
    {
        var entrance = Object.FindAnyObjectByType<MineEntranceView>();
        var miner = Object.FindAnyObjectByType<MinerOtterController>();

        return new List<TutorialStep>
        {
            TutorialStep.Info("여기는 광산이에요",
                "해달이 광산에 들어가 광석을 캐 오는 곳이에요."),
            TutorialStep.At("광부 해달",
                "채굴하는 동안에는 말풍선으로\n캐낸 광석을 보여 줘요.\n다른 곳에 가 있어도 계속 캐요.\n말풍선을 누르면 채굴을 멈춰요.",
                () => TutorialTargets.World(miner)),
            TutorialStep.At("곡괭이 강화",
                "곡괭이를 강화하면\n다이아몬드가 나올 확률이 올라가요.",
                () => TutorialTargets.Ui(gameUI.PickaxeUpgradeButton)),
            TutorialStep.TryIt("광산 입구",
                "입구의 동그라미를 누르면\n해달이 광산에 들어가 채굴을 시작해요. 눌러 봐요!",
                () => TutorialTargets.World(entrance), entrance),
        };
    }

    #endregion

    #region 대상 찾기

    private static RectTransform FindPill(System.Func<Currency, bool> match)
    {
        foreach (var pill in Object.FindObjectsByType<CurrencyPillView>(FindObjectsSortMode.None))
        {
            if (pill.Currency != null && match(pill.Currency)) return (RectTransform)pill.transform;
        }
        return null;
    }

    // 튜토리얼을 시작할 때 화면 가운데에 가장 가까운 해달 (돌아다녀도 강조는 따라감)
    private static T NearestToScreenCenter<T>(T[] candidates) where T : Component
    {
        var cam = Camera.main;
        if (cam == null) return candidates.Length > 0 ? candidates[0] : null;

        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        T best = null;
        float bestDistance = float.MaxValue;
        foreach (var candidate in candidates)
        {
            float distance = ((Vector2)cam.WorldToScreenPoint(candidate.transform.position) - center).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    #endregion
}
