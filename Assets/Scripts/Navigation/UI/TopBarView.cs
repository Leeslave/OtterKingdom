using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 상단바의 프로필(아바타, 이름, 레벨, 진행 바)과 설정 버튼. 재화 칸은 CurrencyPillView가 따로 그린다.
/// 프로필은 ProfileManager의 값을 그리기만 하고, 아바타를 누르면 대표 해달 고르기(ProfilePortraitPicker)를 연다.
/// 레벨 칩 · 경험치 막대를 누르면 왕국 화면 [발전] 탭. 다음 레벨이 큰 부탁에 막혀 멈추면 칩에 자물쇠 + 알림 한 번
/// </summary>
public class TopBarView : MonoBehaviour
{
    [Header("프로필")]
    [SerializeField] private TextMeshProUGUI _nameText;
    [Tooltip("\"Lv.5\"")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [Tooltip("다음 레벨까지 진행")]
    [SerializeField] private ProgressBarView _expBar;
    [Tooltip("아바타 (누르면 대표 해달 고르기)")]
    [SerializeField] private Button _profileButton;
    [Tooltip("아바타 안의 해달 얼굴 (고른 대표 해달)")]
    [SerializeField] private Image _portrait;

    [Header("버튼")]
    [SerializeField] private Button _settingsButton;

    /// <summary>튜토리얼이 프로필 위치를 강조할 때 사용</summary>
    public RectTransform ProfileArea => (RectTransform)_profileButton.transform;

    /// <summary>아바타를 눌렀을 때 (대표 해달 고르기 창이 열림)</summary>
    public event Action OnProfileClicked;

    /// <summary>설정 버튼을 눌렀을 때 (설정 화면이 생기면 연결)</summary>
    public event Action OnSettingsClicked;

    private PlayerProfile _profile;
    private GameObject _levelLock;
    // 막힘 알림은 막히는 순간 한 번 (켜질 때 이미 막혀 있으면 자물쇠만)
    private bool _blocked;
    private bool _blockKnown;

    private void Awake()
    {
        _profileButton.onClick.AddListener(() => OnProfileClicked?.Invoke());
        _settingsButton.onClick.AddListener(() => OnSettingsClicked?.Invoke());
        OnProfileClicked += OpenPortraitPicker;
        MakeTappable(_levelText.transform.parent.gameObject);
        MakeTappable(_expBar.gameObject);
        _levelLock = CreateLevelLock((RectTransform)_levelText.transform.parent);
    }

    // 레벨 칩 · 경험치 막대: 왕국 화면 (마을회관 전에는 레벨 조건만 알림)
    private void MakeTappable(GameObject target)
    {
        var graphic = target.GetComponent<Graphic>();
        if (graphic == null)
        {
            var image = target.AddComponent<Image>();
            image.color = Color.clear;
            graphic = image;
        }
        graphic.raycastTarget = true;
        var button = target.GetComponent<Button>();
        if (button == null)
            button = target.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            var settlement = SettlementManager.Instance;
            if (settlement != null)
                settlement.OpenKingdomOrHint();
        });
    }

    private static GameObject CreateLevelLock(RectTransform chip)
    {
        var go = new GameObject("LevelLock", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(chip, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(40f, 40f);
        rect.anchoredPosition = new Vector2(14f, 14f);
        var image = go.GetComponent<Image>();
        image.sprite = RuntimeUIKit.Style != null ? RuntimeUIKit.Style.LockIcon : null;
        image.preserveAspect = true;
        image.raycastTarget = false;
        go.SetActive(false);
        return go;
    }

    private void OpenPortraitPicker()
    {
        var picker = GetComponent<ProfilePortraitPicker>();
        if (picker == null)
            picker = gameObject.AddComponent<ProfilePortraitPicker>();
        picker.Open(_profileButton.image.sprite);
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
        _nameText.richText = false;
        _nameText.enableAutoSizing = true;
        _nameText.fontSizeMin = 20f;
        _nameText.fontSizeMax = 32f;
        _nameText.textWrappingMode = TextWrappingModes.NoWrap;
        _nameText.overflowMode = TextOverflowModes.Ellipsis;
        _nameText.text = _profile.Name;
        _levelText.text = $"Lv.{_profile.Level}";
        _expBar.SetRatio(_profile.ExpRatio);

        var settlement = SettlementManager.Instance;
        RefreshLevelLock(settlement);
        var otter = settlement != null ? ProfilePortraitRules.Resolve(settlement.Config, _profile.PortraitOtterId) : null;
        if (_portrait != null && otter != null && otter.Portrait != null)
            _portrait.sprite = otter.Portrait;
    }

    private void RefreshLevelLock(SettlementManager settlement)
    {
        // 정착 기록을 불러오기 전에는 판단하지 않음 (불러오는 순간 "막힘"으로 보여 알림이 잘못 뜨지 않게)
        if (settlement == null || !settlement.IsLoaded)
        {
            _levelLock.SetActive(false);
            return;
        }
        bool blocked = settlement.TryGetLevelGate(out var gate) && gate.Blocked;
        _levelLock.SetActive(blocked);
        if (blocked && _blockKnown && !_blocked && settlement.TryGetLevelGate(out var now))
            GameNotices.Post(new GameNotice($"경험치가 가득 찼어요!\n부탁 \"{now.Request.Title}\"을(를) 끝내면 Lv.{now.NextLevel}"));
        _blocked = blocked;
        _blockKnown = true;
    }
}
