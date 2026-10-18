using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
    private SettlementManager districtGuideManager;
    private TutorialPointer districtPointer;
    private TutorialPointer territoryPointer;

    private IEnumerator PlayTerritoryGuideWhenReady()
    {
        const string introId = "Territory.Intro";
        const string doneId = "Territory.FirstExpansion";
        if (save.tutorialsDone.Contains(doneId)) yield break;
        while (SettlementManager.Instance == null || !SettlementManager.Instance.IsLoaded) yield return null;
        var manager = SettlementManager.Instance;
        var navigator = FindAnyObjectByType<SceneNavigator>();
        while (!manager.Settlement.HasDevelopment(TerritoryRules.TutorialReady))
        {
            // 이미 확장한 저장은 첫 개간을 다시 시키지 않는다.
            if (manager.Settlement.HasDevelopment(TerritoryRules.FirstDevelopment)) yield break;
            yield return new WaitForSecondsRealtime(FeatureTutorialPollSeconds);
        }

        TerritorySiteView site = null;
        foreach (var candidate in FindObjectsByType<TerritorySiteView>(FindObjectsSortMode.None))
            if (candidate.Direction == TerritoryDirection.NorthEast) site = candidate;
        if (site == null) yield break;

        while (!manager.Settlement.HasDevelopment(TerritoryRules.TutorialDone))
        {
            yield return new WaitForSecondsRealtime(FeatureTutorialPollSeconds);
            var territory = manager.CurrentTerritory(TerritoryDirection.NorthEast);
            var task = manager.NextTerritoryClearing(territory);
            if (task == null || manager.GetTaskJob(task) != null) continue;
            if (!DistrictGuideIdle(navigator)) continue;
            if (!save.tutorialsDone.Contains(introId))
            {
                bool finished = false, skipped = false;
                TutorialOverlay.Play(new List<TutorialStep>
                {
                    TutorialStep.AtWorld("오른쪽 땅을 넓혀 봐요", "게시판을 넓혔으니 마을도 넓혀 볼까요?\n오른쪽 숲에 해달을 보내 개간할 수 있어요.", site),
                    TutorialStep.Info("첫 개간은 한 번만", "해달을 고르고 [작업 시작]을 눌러 주세요.\n이번에는 개간 한 번이 끝나면\n추가 납품 없이 바로 땅이 넓어져요!"),
                    TutorialStep.TryIt("숲의 개간 표시 누르기", "강조된 표시를 눌러 해달을 보내 봐요!", () => TutorialTargets.World(site), site),
                }, wasSkipped => { finished = true; skipped = wasSkipped; });
                while (!finished) yield return null;
                if (skipped) yield break;
                save.tutorialsDone.Add(introId);
                SaveNow();
            }
            if (territoryPointer == null)
                territoryPointer = TutorialPointer.Show(() =>
                {
                    if (manager.Settlement.HasDevelopment(TerritoryRules.TutorialDone) || manager.GetTaskJob(task) != null
                        || IsModalOpen || TutorialOverlay.IsShowing) return null;
                    var popup = FindAnyObjectByType<SettlementTaskPopupView>();
                    if (popup != null && popup.IsOpen && popup.Task == task)
                        return TutorialTargets.Ui(popup.StartButton);
                    return DistrictGuideIdle(navigator) ? TutorialTargets.World(site) : null;
                }, "개간 표시 → 해달 선택 → [작업 시작]", TutorialPointer.AboveGlobalUI);
        }
        if (territoryPointer != null) territoryPointer.Close();
        territoryPointer = null;
        while (!DistrictGuideIdle(navigator)) yield return new WaitForSecondsRealtime(FeatureTutorialPollSeconds);
        bool acknowledged = false;
        TutorialOverlay.Play(new List<TutorialStep>
        {
            TutorialStep.Info("마을이 넓어졌어요!", "오른쪽 땅을 사용할 수 있게 되었어요.\n다음부터는 Lv.6부터 레벨마다 개간 기회가 생겨요.\n각 땅의 개간을 모두 마치고 확장 미션을 완료해 주세요."),
        }, () => acknowledged = true);
        while (!acknowledged) yield return null;
        save.tutorialsDone.Add(doneId);
        SaveNow();
    }

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
        if (territoryPointer != null) territoryPointer.Close();
        territoryPointer = null;
        if (districtGuideManager != null) districtGuideManager.OnDistrictDesignated -= HandleDistrictDesignated;
        if (districtPointer != null) districtPointer.Close();
        districtPointer = null;
    }
}
