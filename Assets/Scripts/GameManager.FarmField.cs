using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 밭이 처음 열리면(Lv.10 밭 개간 뒤 광장 농장) 첫 밭 칸을 놓고 심기까지 안내한다.
// 첫 밭 칸은 공짜 · 바로 지어진다 (BuildingRules.IsFreeFirstField). 이미 놓았으면 놓기는 건너뛰고, 무언가 심었으면 끝.
public partial class GameManager
{
    private const string FirstFieldPlaceTutorial = "Farm.FirstFieldPlace";
    private const string FirstFieldPlantTutorial = "Farm.FirstFieldPlant";
    // 놓기 화면을 그냥 닫았으면 이만큼 뒤에 다시 권함
    private const float FirstFieldRetrySeconds = 60f;
    private const float FirstFieldSettleSeconds = 1.5f;
    private TutorialPointer firstFieldPointer;

    private IEnumerator PlayFirstFieldGuideWhenReady()
    {
        if (save.tutorialsDone.Contains(FirstFieldPlantTutorial)) yield break;
        while (SettlementManager.Instance == null || !SettlementManager.Instance.IsLoaded) yield return null;
        var manager = SettlementManager.Instance;
        var navigator = FindAnyObjectByType<SceneNavigator>();
        var fieldBuilding = manager.FarmPartDefinition(FarmPartKind.Field);
        if (fieldBuilding == null) yield break;

        // 1) 첫 밭 칸 놓기: 설명 → 그 밭 칸을 놓는 화면을 바로 열고 ✓를 가리킴
        float nextOffer = 0f;
        while (manager.FarmParts(FarmPartKind.Field, false).Count == 0)
        {
            yield return new WaitForSecondsRealtime(FeatureTutorialPollSeconds);
            if (Time.unscaledTime < nextOffer || !FirstFieldGuideIdle(manager, navigator)) continue;
            // 밭이 열린 완료 팝업("농부 해달이 찾아왔어요")이 다음 프레임에 뜰 수도 있어 조금 기다렸다 다시 봄
            yield return new WaitForSecondsRealtime(FirstFieldSettleSeconds);
            if (!FirstFieldGuideIdle(manager, navigator)) continue;
            bool finished = false, skipped = false;
            TutorialOverlay.Play(new List<TutorialStep>
            {
                TutorialStep.Info("밭이 열렸어요!", "농업 지역에 밭 칸을 놓으면 작물을 키울 수 있어요.\n첫 밭 칸은 공짜로 바로 지어져요!"),
            }, wasSkipped => { finished = true; skipped = wasSkipped; });
            while (!finished) yield return null;
            nextOffer = Time.unscaledTime + FirstFieldRetrySeconds;
            if (skipped) continue;

            DecorModePresenter.RequestBuild(fieldBuilding);
            ShowFirstFieldPointer(() =>
            {
                var mode = FindAnyObjectByType<DecorModePresenter>();
                return mode != null && mode.IsPlacingBuilding && !IsModalOpen && !TutorialOverlay.IsShowing
                    ? TutorialTargets.Ui(mode.ConfirmButton) : null;
            }, "농업 지역의 초록 자리에서 ✓ 누르기");
            yield return new WaitForSecondsRealtime(1f);
            // 놓았거나 그냥 닫을 때까지
            while (DecorModePresenter.IsActive && manager.FarmParts(FarmPartKind.Field, false).Count == 0) yield return null;
            CloseFirstFieldPointer();
        }
        if (!save.tutorialsDone.Contains(FirstFieldPlaceTutorial))
        {
            save.tutorialsDone.Add(FirstFieldPlaceTutorial);
            SaveNow();
        }

        // 2) 심기: 밭 칸을 직접 눌러 보게 한 뒤, 심을 때까지 밭 칸을 가리킴
        bool explained = false;
        while (!AnyFieldPlanted(manager))
        {
            yield return new WaitForSecondsRealtime(FeatureTutorialPollSeconds);
            if (!manager.FarmOpen || !DistrictGuideIdle(navigator)) continue;
            var view = FirstFieldView(manager);
            if (view == null) continue;
            if (!explained)
            {
                bool finished = false;
                TutorialOverlay.Play(new List<TutorialStep>
                {
                    TutorialStep.TryIt("밭 칸에 심어 봐요", "밭 칸을 눌러 심을 작물을 골라요.", () => TutorialTargets.World(view), view),
                }, _ => finished = true);
                while (!finished) yield return null;
                explained = true;
            }
            if (firstFieldPointer == null)
                ShowFirstFieldPointer(() =>
                {
                    var target = FirstFieldView(manager);
                    if (target == null || AnyFieldPlanted(manager) || TutorialOverlay.IsShowing || DecorModePresenter.IsActive) return null;
                    var sheet = FindAnyObjectByType<PlazaFarmSheet>();
                    return sheet != null && sheet.IsOpen ? null : TutorialTargets.World(target);
                }, "밭 칸 누르기 → 심을 작물 고르기");
        }
        CloseFirstFieldPointer();

        while (!DistrictGuideIdle(navigator)) yield return new WaitForSecondsRealtime(FeatureTutorialPollSeconds);
        var farmer = manager.FarmConfig != null ? manager.Config.FindOtter(manager.FarmConfig.FarmerOtterId) : null;
        string harvester = manager.HasFarmWorker && farmer != null ? $"{farmer.DisplayName}{KoreanParticle.SubjectParticle(farmer.DisplayName)}" : "농부 해달이";
        bool done = false;
        // 농부가 밭을 맡기 전에는 작물이 자라지 않음 (SettlementManager.TickFarm)
        string grow = manager.HasFarmWorker ? $"다 자라면 {harvester} 거둬 상자에 넣어요."
            : $"{harvester} 밭을 맡으면 자라기 시작해요. 말을 걸어 밭에 배치해요!";
        TutorialOverlay.Play(new List<TutorialStep>
        {
            TutorialStep.Info("첫 작물을 심었어요!", $"{grow}\n밭 칸은 꾸미기 → 건물에서 더 지을 수 있어요."),
        }, () => done = true);
        while (!done) yield return null;
        save.tutorialsDone.Add(FirstFieldPlantTutorial);
        SaveNow();
    }

    // 밭 안내는 농부 해달 소개(밭 개간 완료 팝업 → 농부가 광장에 옴)가 끝난 뒤에
    private bool FirstFieldGuideIdle(SettlementManager manager, SceneNavigator navigator)
    {
        if (!manager.FarmOpen || !DistrictGuideIdle(navigator))
            return false;
        var plaza = SettlementPlazaView.Active;
        if (plaza != null && plaza.IsIntroducingVisitor)
            return false;
        string farmerId = manager.FarmConfig != null ? manager.FarmConfig.FarmerOtterId : null;
        return string.IsNullOrEmpty(farmerId) || manager.Config.FindOtter(farmerId) == null || manager.Settlement.HasMet(farmerId);
    }

    private static bool AnyFieldPlanted(SettlementManager manager)
    {
        foreach (var field in manager.Farm.Fields)
            if (!string.IsNullOrEmpty(field.cropId)) return true;
        return false;
    }

    private static PlacedDecorView FirstFieldView(SettlementManager manager)
    {
        var board = DecorBoardView.Active;
        if (board == null) return null;
        foreach (var field in manager.FarmParts(FarmPartKind.Field))
        {
            var view = board.FindView(field.InstanceId);
            if (view != null) return view;
        }
        return null;
    }

    private void ShowFirstFieldPointer(System.Func<Rect?> target, string message)
    {
        CloseFirstFieldPointer();
        firstFieldPointer = TutorialPointer.Show(target, message, TutorialPointer.AboveGlobalUI);
    }

    private void CloseFirstFieldPointer()
    {
        if (firstFieldPointer != null) firstFieldPointer.Close();
        firstFieldPointer = null;
    }
}
