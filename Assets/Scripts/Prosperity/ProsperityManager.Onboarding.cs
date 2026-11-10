using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class ProsperityManager
{
    private TutorialOverlay _introOverlay;
    private bool _introPlaying;
    private bool _openAfterIntro;
    private float _onboardingPoll;
    private ProsperityFlowStep _lastNudgedStep;

    private bool GasRemoved => _settlement != null && _settlement.IsLoaded
        && (_settlement.HasDevelopment("mine_source_removed") || _settlement.MineCommunity?.sourceRemoved == true);
    private bool CastleBuilt => _settlement != null && _settlement.IsLoaded
        && (_settlement.IsCastleBuilt || _settlement.Settlement.IsCompleted(CastleRules.RequestId));

    public ProsperityFlowStep NextStep => _settlement == null || !_settlement.IsLoaded
        ? ProsperityFlowStep.None
        : ProsperityFlow.Next(Research, _profile.Level, GasRemoved, CastleBuilt,
            _settlement.HasDevelopment("first_plank"),
            _plan != null && CommunityProjectRules.IsCompleted(_plan, _settlement.Settlement));

    private void TickOnboarding()
    {
        if (Research == null) return;
        // 씬 이동으로 안내가 파괴됐으면 완료 처리 없이 다음 광장에서 다시 제안한다.
        if (_introPlaying && _introOverlay == null) _introPlaying = false;
        _onboardingPoll -= Time.unscaledDeltaTime;
        if (_onboardingPoll > 0f) return;
        _onboardingPoll = 0.5f;
        if (GameDataReset.IsResetting || SaveService.WritesFrozen || _introPlaying) return;
        if (!ProsperityFlow.CanPresent(SceneManager.GetActiveScene().name == "Plaza",
            GameManager.Instance != null && _settlement != null && _settlement.IsLoaded,
            PresentationGate.IsBusy, GameNoticeToast.IsShowing || GameNotices.PendingCount > 0)) return;
        if (_openAfterIntro)
        {
            _openAfterIntro = false;
            Open();
            return;
        }
        var step = NextStep;
        if (step == ProsperityFlowStep.Intro)
        {
            PlayIntro();
            return;
        }
        if (step == ProsperityFlowStep.None || step == _lastNudgedStep) return;
        _lastNudgedStep = step;
        GameNotices.Post(new GameNotice(StepMessage(step), StepAction(step), OpenNextStep));
    }

    private void PlayIntro()
    {
        _introPlaying = true;
        _introOverlay = TutorialOverlay.Play(new[]
        {
            TutorialStep.Info("다시 웃는 마을", "깊은 갱도의 가스 원인이 사라졌어요. 해달들이 기운을 되찾고, 함께 세운 성에도 활기가 돌아왔어요."),
            TutorialStep.Info("또박이의 선언", "또박이: 이제 우리가 배운 것을 모아 왕국을 더 풍요롭게 만들어요. 연구를 고르면 일터와 마을이 달라질 거예요!"),
            TutorialStep.Info("번영의 시대", "먼저 제재 기술을 연구하고 목공소에서 첫 판재를 만들어 봐요. 번영 계획 공동사업을 마치면 연구 포인트 3 RP를 더 받을 수 있어요."),
        }, skipped =>
        {
            _introPlaying = false;
            if (Research.CompleteIntro(_profile.Level, GasRemoved, CastleBuilt)) _openAfterIntro = true;
        });
    }

    public bool BeginEra()
    {
        EnsureBindings();
        return Research.CompleteIntro(_profile.Level, GasRemoved, CastleBuilt);
    }

    public void OpenNextStep()
    {
        EnsureBindings();
        switch (NextStep)
        {
            case ProsperityFlowStep.Castle:
                var castle = _settlement.Config.FindRequest(CastleRules.RequestId);
                if (_settlement.IsCastleReady && castle != null) _settlement.RequestOpen(castle);
                else _settlement.RequestTownHall();
                break;
            case ProsperityFlowStep.FirstPlank:
                _settlement.OpenProduction(ProductionSheet.Tab.Processing);
                break;
            case ProsperityFlowStep.Plan:
                OpenPlan();
                break;
            default:
                Open();
                break;
        }
    }

    public static string StepMessage(ProsperityFlowStep step) => step switch
    {
        ProsperityFlowStep.Castle => "또박이: 가스 사건을 해결했어요! 마을회관에서 성 완공을 준비해요. 성을 완공하면 번영의 시대가 시작돼요.",
        ProsperityFlowStep.Sawmill => "첫 연구: 제재 기술을 배워 목공소에서 판재를 만들어 봐요.",
        ProsperityFlowStep.FirstPlank => "목공소의 가공을 켜고 첫 판재를 받아요. 처음 켜면 판재 1개를 바로 받을 수 있어요.",
        ProsperityFlowStep.Plan => "첫 판재를 만들었어요! 번영 계획 수립에 목재 12개·돌 12개를 납품하고 계획을 확정해요. 보상은 3 RP예요.",
        ProsperityFlowStep.Warehouse => "번영 계획을 세웠어요! 공동 창고를 연구하면 왕국 자재 보관 공간이 넓어져요.",
        _ => "왕국의 다음 발전을 준비해요.",
    };

    public static string StepAction(ProsperityFlowStep step) => step switch
    {
        ProsperityFlowStep.Castle => "성 준비 보기",
        ProsperityFlowStep.FirstPlank => "가공 보기",
        ProsperityFlowStep.Plan => "공동사업 보기",
        _ => "연구 보기",
    };
}
