using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
    private SettlementManager districtGuideManager;
    private TutorialPointer districtPointer;

    // 광장 첫 방문·기존 팝업을 기다린 뒤 승격 → 첫 구역 설명 → 실제 주거 지정 순서로 안내한다.
    private IEnumerator PlayDistrictGuideWhenReady()
    {
        while (SettlementManager.Instance == null || !SettlementManager.Instance.IsLoaded) yield return null;
        districtGuideManager = SettlementManager.Instance;
        districtGuideManager.OnDistrictDesignated += HandleDistrictDesignated;
        var navigator = FindAnyObjectByType<SceneNavigator>();
        while (!districtGuideManager.IsAdministrator)
        {
            yield return new WaitForSecondsRealtime(FeatureTutorialPollSeconds);
            if (!DistrictGuideIdle(navigator) || !DistrictGuideRules.CanPromote(districtGuideManager.Settlement)) continue;
            SettlementOtterView receptionist = null;
            foreach (var view in FindObjectsByType<SettlementOtterView>(FindObjectsSortMode.None))
                if (view.Otter != null && view.Otter.OtterId == DistrictGuideRules.Receptionist) receptionist = view;
            if (receptionist == null) continue;
            bool finished = false;
            TutorialOverlay.Play(new List<TutorialStep>
            {
                TutorialStep.AtWorld("또박이 · 행정 해달", "음… 좀 정신없네요.\n마을회관도 생겼으니 제가 왕국의 정리를 맡을게요!", receptionist),
                TutorialStep.Info("왕국을 정돈해 봐요", "또박이가 행정 해달이 되었어요.\n개간한 땅의 용도를 정하고,\n마을회관에서 왕국 질서도를 확인해요."),
            }, () =>
            {
                districtGuideManager.TryPromoteAdministrator();
                finished = true;
            });
            while (!finished) yield return null;
        }

        while (!save.tutorialsDone.Contains(ZoneTutorials.District))
        {
            yield return new WaitForSecondsRealtime(FeatureTutorialPollSeconds);
            // 옛 세이브에서 이미 정한 구역은 튜토리얼 때문에 다시 고르게 하지 않는다.
            if (DistrictGuideRules.FirstDistrictConfigured(districtGuideManager.Config, districtGuideManager.Settlement)) yield break;
            if (!DistrictGuideIdle(navigator) || !DistrictGuideRules.NeedsFirstDistrict(districtGuideManager.Config, districtGuideManager.Settlement)) continue;
            break;
        }
        if (save.tutorialsDone.Contains(ZoneTutorials.District)) yield break;
        if (!save.tutorialsDone.Contains(ZoneTutorials.DistrictIntro))
        {
            bool finished = false, skipped = false;
            var map = FindAnyObjectByType<DistrictMapView>();
            var first = DistrictGuideRules.FirstDistrictBlock(districtGuideManager.Config, districtGuideManager.Settlement);
            if (map != null) map.FocusGuide(first);
            yield return new WaitForSecondsRealtime(0.6f);
            TutorialOverlay.Play(ZoneTutorials.DistrictSteps(map, first), wasSkipped => { finished = true; skipped = wasSkipped; });
            while (!finished) yield return null;
            if (skipped) yield break; // 이번 방문만 쉬고 다음 방문에 다시 안내
            save.tutorialsDone.Add(ZoneTutorials.DistrictIntro);
            SaveNow();
        }
        if (save.tutorialsDone.Contains(ZoneTutorials.District)) yield break;
        districtGuideManager.RequestDistrict(DistrictGuideRules.FirstDistrictBlock(districtGuideManager.Config, districtGuideManager.Settlement));
        var presenter = FindAnyObjectByType<DistrictPresenter>();
        districtPointer = TutorialPointer.Show(() =>
            presenter != null && presenter.Popup.IsOpen && !IsModalOpen && !TutorialOverlay.IsShowing
                ? TutorialTargets.Ui(presenter.Popup.ResidentialButton) : null,
            "[주거 지역]을 눌러 첫 구역을 정해 봐요!", TutorialPointer.AboveGlobalUI);
    }

    private bool DistrictGuideIdle(SceneNavigator navigator)
    {
        if (zoneTutorialPending || IsModalOpen || (navigator != null && navigator.IsTraveling)
            || SettlementPresenter.IsCelebrating || LevelUpPresenter.IsBusy || TutorialOverlay.IsShowing || DecorModePresenter.IsActive)
            return false;
        foreach (var popup in FindObjectsByType<UIPopupAnimator>(FindObjectsSortMode.None))
            if (popup.IsOpen) return false;
        return true;
    }

    private void HandleDistrictDesignated(DistrictBlockDefinition block)
    {
        var first = DistrictGuideRules.FirstDistrictBlock(districtGuideManager.Config, districtGuideManager.Settlement);
        if ((first != null && first.BlockId == DistrictRules.CenterBlockId && first != block)
            || districtGuideManager.DistrictTypeOf(block) != DistrictType.Residential
            || save.tutorialsDone.Contains(ZoneTutorials.District)) return;
        save.tutorialsDone.Add(ZoneTutorials.District);
        if (districtPointer != null) districtPointer.Close();
        districtPointer = null;
        SaveNow();
    }

    private void CloseDistrictGuide()
    {
        if (districtGuideManager != null) districtGuideManager.OnDistrictDesignated -= HandleDistrictDesignated;
        if (districtPointer != null) districtPointer.Close();
        districtPointer = null;
    }
}
