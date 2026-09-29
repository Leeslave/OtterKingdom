using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 상단바의 프로필(아바타, 이름, 레벨, 진행 바)과 설정 버튼. 재화 칸은 CurrencyPillView가 따로 그린다.
/// 프로필은 ProfileManager의 값을 그리기만 하고, 버튼은 클릭을 알리기만 한다.
/// </summary>
public class TopBarView : MonoBehaviour
{
    [Header("프로필")]
    [SerializeField] private TextMeshProUGUI _nameText;
    [Tooltip("\"Lv.5\"")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [Tooltip("다음 레벨까지 진행")]
    [SerializeField] private ProgressBarView _expBar;
    [Tooltip("아바타 (누르면 프로필 알림)")]
    [SerializeField] private Button _profileButton;

    [Header("버튼")]
    [SerializeField] private Button _settingsButton;

    /// <summary>아바타를 눌렀을 때 (왕국 정보 화면 등이 생기면 연결)</summary>
    public event Action OnProfileClicked;

    /// <summary>설정 버튼을 눌렀을 때 (설정 화면이 생기면 연결)</summary>
    public event Action OnSettingsClicked;

    private PlayerProfile _profile;

    private void Awake()
    {
        _profileButton.onClick.AddListener(() => OnProfileClicked?.Invoke());
        _settingsButton.onClick.AddListener(() => OnSettingsClicked?.Invoke());
    }

    private void OnEnable()
    {
        _profile = ProfileManager.Instance.Profile;
        _profile.OnChanged += Refresh;

        // 꺼져 있던 동안 바뀐 값 반영
        Refresh();
    }

    private void OnDisable()
    {
        if (_profile != null)
            _profile.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        _nameText.text = _profile.Name;
        _levelText.text = $"Lv.{_profile.Level}";
        _expBar.SetRatio(_profile.ExpRatio);
    }
}
