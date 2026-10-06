using UnityEngine;

/// <summary>
/// 상단바 설정 버튼 → 설정 화면. 화면에서 바꾼 값을 GameSettings에 넣고, 닫을 때 기기에 저장한다.
/// </summary>
public class SettingsPresenter : MonoBehaviour
{
    private const string ComingSoonMessage = "준비 중이에요";
    private const string PushComingSoonMessage = "알림은 다음 업데이트에서 지원해요";

    [Header("구성 요소")]
    [SerializeField] private TopBarView _topBar;
    [SerializeField] private SettingsView _view;

    [Header("표시")]
    [Tooltip("버전 줄의 게임 이름")]
    [SerializeField] private string _gameTitle = "해달 왕국";

    private GameSettings _settings;

    private void OnEnable()
    {
        _settings = SettingsManager.Instance.Settings;

        _topBar.OnSettingsClicked += Open;
        _view.OnBgmVolumeChanged += _settings.SetBgmVolume;
        _view.OnSfxVolumeChanged += HandleSfxVolumeChanged;
        _view.OnVibrationToggled += HandleVibrationToggled;
        _view.OnPushToggled += HandlePushToggled;
        _view.OnLanguageChanged += _settings.SetLanguage;
        _view.OnMenuClicked += HandleMenuClicked;
        _view.OnResetClicked += HandleResetClicked;
        _view.OnHidden += HandleHidden;
        _settings.OnChanged += Refresh;

        _view.SetVersion($"버전 {Application.version}  ·  {_gameTitle}");
        // 예전에 켜 둔 값도 끔 (푸시는 아직 없음)
        _settings.SetPushEnabled(false);
        Refresh();
    }

    private void OnDisable()
    {
        _topBar.OnSettingsClicked -= Open;
        _view.OnMenuClicked -= HandleMenuClicked;
        _view.OnResetClicked -= HandleResetClicked;
        _view.OnSfxVolumeChanged -= HandleSfxVolumeChanged;
        _view.OnVibrationToggled -= HandleVibrationToggled;
        _view.OnPushToggled -= HandlePushToggled;
        _view.OnHidden -= HandleHidden;

        if (_settings == null)
            return;

        _view.OnBgmVolumeChanged -= _settings.SetBgmVolume;
        _view.OnLanguageChanged -= _settings.SetLanguage;
        _settings.OnChanged -= Refresh;
    }

    public void Open()
    {
        Refresh();
        _view.Show();
    }

    private void Refresh()
    {
        _view.SetValues(_settings.BgmVolume, _settings.SfxVolume, _settings.VibrationEnabled,
            _settings.PushEnabled, _settings.Language);
    }

    // 바뀐 크기로 "띵"을 들려줌 (배경음은 계속 나오고 있어서 따로 들려줄 필요 없음)
    private void HandleSfxVolumeChanged(float volume)
    {
        _settings.SetSfxVolume(volume);
        AudioManager.Instance.PlaySfxPreview();
    }

    private void HandleVibrationToggled(bool enabled)
    {
        _settings.SetVibrationEnabled(enabled);

        // 켰을 때 한 번 울려서 켜졌다는 걸 알려줌
        if (enabled)
            SettingsManager.Instance.Vibrate();
    }

    // 푸시 알림은 이번 범위가 아님 (상세기획서 3.3) → 켜지지 않게 되돌리고 안내만
    private void HandlePushToggled(bool enabled)
    {
        _settings.SetPushEnabled(false);
        Refresh();
        if (enabled)
            _view.ShowNotice(PushComingSoonMessage);
    }

    // 계정·쿠폰·고객센터·약관은 서버/페이지가 생기면 연결
    private void HandleMenuClicked(SettingsMenu menu)
    {
        _view.ShowNotice(ComingSoonMessage);
    }

    // 데이터 초기화: 두 번 확인 (상세기획서 11.6). 확인 창은 장소 팝업(GameUI)이라 설정 화면 위에 뜸
    private void HandleResetClicked()
    {
        var gameUI = FindAnyObjectByType<GameUI>();
        if (gameUI == null)
        {
            _view.ShowNotice("장소 안에서만 할 수 있어요");
            return;
        }

        gameUI.ShowChoice("데이터 초기화",
            "왕국·가방·재화·퀘스트·도감까지\n모든 진행이 지워지고 처음부터 시작해요.\n(소리·진동 설정은 그대로예요)",
            "초기화", () => ConfirmReset(gameUI), "취소", null);
    }

    private void ConfirmReset(GameUI gameUI)
    {
        gameUI.ShowChoice("정말 지울까요?",
            "지운 진행은 되돌릴 수 없어요.",
            "지우고 다시 시작", GameDataReset.ResetAndRestart, "취소", null);
    }

    private void HandleHidden()
    {
        SettingsManager.Instance.SaveIfDirty();
    }
}
