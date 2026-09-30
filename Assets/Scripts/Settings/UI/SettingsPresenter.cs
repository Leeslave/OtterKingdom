using UnityEngine;

/// <summary>
/// 상단바 설정 버튼 → 설정 화면. 화면에서 바꾼 값을 GameSettings에 넣고, 닫을 때 기기에 저장한다.
/// </summary>
public class SettingsPresenter : MonoBehaviour
{
    private const string ComingSoonMessage = "준비 중이에요";

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
        _view.OnSfxVolumeChanged += _settings.SetSfxVolume;
        _view.OnVibrationToggled += HandleVibrationToggled;
        _view.OnPushToggled += _settings.SetPushEnabled;
        _view.OnLanguageChanged += _settings.SetLanguage;
        _view.OnMenuClicked += HandleMenuClicked;
        _view.OnHidden += HandleHidden;
        _settings.OnChanged += Refresh;

        _view.SetVersion($"버전 {Application.version}  ·  {_gameTitle}");
        Refresh();
    }

    private void OnDisable()
    {
        _topBar.OnSettingsClicked -= Open;
        _view.OnMenuClicked -= HandleMenuClicked;
        _view.OnVibrationToggled -= HandleVibrationToggled;
        _view.OnHidden -= HandleHidden;

        if (_settings == null)
            return;

        _view.OnBgmVolumeChanged -= _settings.SetBgmVolume;
        _view.OnSfxVolumeChanged -= _settings.SetSfxVolume;
        _view.OnPushToggled -= _settings.SetPushEnabled;
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

    private void HandleVibrationToggled(bool enabled)
    {
        _settings.SetVibrationEnabled(enabled);

        // 켰을 때 한 번 울려서 켜졌다는 걸 알려줌
        if (enabled)
            SettingsManager.Instance.Vibrate();
    }

    // 계정·쿠폰·고객센터·약관은 서버/페이지가 생기면 연결
    private void HandleMenuClicked(SettingsMenu menu)
    {
        _view.ShowNotice(ComingSoonMessage);
    }

    private void HandleHidden()
    {
        SettingsManager.Instance.SaveIfDirty();
    }
}
