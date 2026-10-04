using UnityEngine;

/// <summary>
/// 공통 대기 규칙: 입력을 차지하는 화면(화면 전환 구름, 완료 팝업, 레벨업, 튜토리얼, 장소의 확인·알림 팝업)이 떠 있는 동안
/// 새 알림 띠·요정 등장·모임 연출 같은 것은 기다린다. 새 해달 방문(SettlementPlazaView)과 같은 기준.
/// </summary>
public static class PresentationGate
{
    private static SceneNavigator _navigator;

    public static bool IsBusy
    {
        get
        {
            if (_navigator == null)
                _navigator = Object.FindAnyObjectByType<SceneNavigator>();
            if (_navigator != null && _navigator.IsTraveling)
                return true;
            if (SettlementPresenter.IsCelebrating || LevelUpPresenter.IsBusy || TutorialOverlay.IsShowing || GatheringDirector.IsPlaying)
                return true;
            return GameManager.Instance != null && GameManager.Instance.IsModalOpen;
        }
    }
}
