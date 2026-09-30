using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>설정 화면 아래쪽의 메뉴 버튼</summary>
public enum SettingsMenu
{
    AccountLink,
    Coupon,
    Support,
    Terms,
}

/// <summary>
/// 설정 화면: 배경음/효과음 슬라이더, 진동/푸시 스위치, 언어 선택, 메뉴 버튼 4개, 버전.
/// 받은 값을 그리고 바뀐 것을 알리기만 한다 (GameSettings를 모름).
/// </summary>
public class SettingsView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("소리")]
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("스위치")]
    [SerializeField] private ToggleSwitchView _vibrationSwitch;
    [SerializeField] private ToggleSwitchView _pushSwitch;

    [Header("언어")]
    [SerializeField] private TMP_Dropdown _languageDropdown;

    [Header("메뉴")]
    [SerializeField] private Button _accountButton;
    [SerializeField] private Button _couponButton;
    [SerializeField] private Button _supportButton;
    [SerializeField] private Button _termsButton;
    [SerializeField] private Button _closeButton;

    [Header("기타")]
    [Tooltip("\"버전 0.1.0 · 해달 왕국\"")]
    [SerializeField] private TextMeshProUGUI _versionText;

    [Header("잠깐 뜨는 안내 (예: 준비 중)")]
    [SerializeField] private CanvasGroup _notice;
    [SerializeField] private TextMeshProUGUI _noticeText;
    [SerializeField] private float _noticeDuration = 1.6f;

    public bool IsOpen => _animator.IsOpen;

    public event Action<float> OnBgmVolumeChanged;
    public event Action<float> OnSfxVolumeChanged;
    public event Action<bool> OnVibrationToggled;
    public event Action<bool> OnPushToggled;
    public event Action<GameLanguage> OnLanguageChanged;
    public event Action<SettingsMenu> OnMenuClicked;

    /// <summary>닫힘 연출이 끝났을 때 (저장 시점)</summary>
    public event Action OnHidden
    {
        add => _animator.OnHidden += value;
        remove => _animator.OnHidden -= value;
    }

    private Coroutine _noticeRoutine;
    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    public void Show() => _animator.Show();
    public void Hide() => _animator.Hide();

    /// <summary>이벤트 없이 표시만 바꿈 (프레젠터가 상태를 반영할 때)</summary>
    public void SetValues(float bgm, float sfx, bool vibration, bool push, GameLanguage language)
    {
        // 프레젠터의 OnEnable이 이 오브젝트의 Awake보다 먼저 불릴 수 있으므로 여기서도 초기화
        EnsureInitialized();

        _bgmSlider.SetValueWithoutNotify(bgm);
        _sfxSlider.SetValueWithoutNotify(sfx);
        _vibrationSwitch.SetOn(vibration);
        _pushSwitch.SetOn(push);
        _languageDropdown.SetValueWithoutNotify((int)language);
    }

    public void SetVersion(string text) => _versionText.text = text;

    public void ShowNotice(string message)
    {
        _noticeText.text = message;
        if (_noticeRoutine != null)
            StopCoroutine(_noticeRoutine);
        _noticeRoutine = StartCoroutine(NoticeRoutine());
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        var languages = new List<string>();
        foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            languages.Add(language.ToDisplayName());
        _languageDropdown.ClearOptions();
        _languageDropdown.AddOptions(languages);

        _bgmSlider.onValueChanged.AddListener(v => OnBgmVolumeChanged?.Invoke(v));
        _sfxSlider.onValueChanged.AddListener(v => OnSfxVolumeChanged?.Invoke(v));
        _vibrationSwitch.OnToggled += on => OnVibrationToggled?.Invoke(on);
        _pushSwitch.OnToggled += on => OnPushToggled?.Invoke(on);
        _languageDropdown.onValueChanged.AddListener(i => OnLanguageChanged?.Invoke((GameLanguage)i));

        _accountButton.onClick.AddListener(() => OnMenuClicked?.Invoke(SettingsMenu.AccountLink));
        _couponButton.onClick.AddListener(() => OnMenuClicked?.Invoke(SettingsMenu.Coupon));
        _supportButton.onClick.AddListener(() => OnMenuClicked?.Invoke(SettingsMenu.Support));
        _termsButton.onClick.AddListener(() => OnMenuClicked?.Invoke(SettingsMenu.Terms));
        _closeButton.onClick.AddListener(Hide);

        _notice.alpha = 0f;
        _isInitialized = true;
    }

    private IEnumerator NoticeRoutine()
    {
        _notice.alpha = 1f;
        yield return new WaitForSecondsRealtime(_noticeDuration);

        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            _notice.alpha = 1f - t / 0.25f;
            yield return null;
        }
        _notice.alpha = 0f;
        _noticeRoutine = null;
    }
}
