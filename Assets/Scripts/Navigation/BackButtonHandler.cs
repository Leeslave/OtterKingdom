using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Android 뒤로 가기(에디터에서는 Esc): 맨 위 창부터 하나씩 닫는다 (상세기획서 9.2).
/// 순서: 장소 팝업(GameUI) → 전역 UI의 열린 창 중 맨 위 → 꾸미기 모드 → 아무것도 없으면 "한 번 더 누르면 종료" 안내, 2초 안에 다시 누르면 종료.
/// 씬 전환 중·튜토리얼 중에는 무시한다. 게임이 시작될 때 저절로 하나 만들어져 씬을 넘어 유지된다.
/// </summary>
public class BackButtonHandler : MonoBehaviour
{
    private const float ExitConfirmSeconds = 2f;
    private const string ExitHint = "뒤로 가기를 한 번 더 누르면 종료돼요";

    private float _exitArmedUntil = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject(nameof(BackButtonHandler));
        DontDestroyOnLoad(go);
        go.AddComponent<BackButtonHandler>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;
        HandleBack();
    }

    private void HandleBack()
    {
        var navigator = FindAnyObjectByType<SceneNavigator>();
        if ((navigator != null && navigator.IsTraveling) || TutorialOverlay.IsShowing)
            return;

        var gameUI = FindAnyObjectByType<GameUI>();
        if (gameUI != null && gameUI.CloseTopModal())
            return;
        // 뽑기 연출 중: 결과로 건너뜀 (결과 화면이면 닫음) — 아래 뽑기 화면이 먼저 닫히지 않게
        if (GachaRevealView.TryHandleBack())
            return;
        if (CloseTopGlobalWindow())
            return;
        if (DecorModePresenter.IsActive)
        {
            var decor = FindAnyObjectByType<DecorModePresenter>();
            if (decor != null)
            {
                decor.Exit();
                return;
            }
        }
        ConfirmExit();
    }

    // 전역 UI에서 열려 있는 창 중 맨 위 (계층에서 뒤에 있을수록 위에 그려짐)
    private static bool CloseTopGlobalWindow()
    {
        var root = GlobalUIRoot.Instance;
        if (root == null)
            return false;
        UIPopupAnimator top = null;
        foreach (var popup in root.GetComponentsInChildren<UIPopupAnimator>())
        {
            if (popup.IsOpen && popup.IsDismissable)
                top = popup;
        }
        if (top == null)
            return false;
        top.Hide();
        return true;
    }

    private void ConfirmExit()
    {
        if (Time.unscaledTime <= _exitArmedUntil)
        {
            Application.Quit();
            return;
        }
        _exitArmedUntil = Time.unscaledTime + ExitConfirmSeconds;
        var root = GlobalUIRoot.Instance;
        var toast = root != null ? root.GetComponentInChildren<FindToastView>(true) : null;
        if (toast != null)
            toast.Show(null, ExitHint);
    }
}
