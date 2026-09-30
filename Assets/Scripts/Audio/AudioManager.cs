using UnityEngine;

/// <summary>
/// 배경음/효과음 재생. 음량은 환경설정(GameSettings)을 따르고, 설정이 바뀌면 바로 반영한다.
/// 전역 UI 루트에 붙어 씬을 넘어 유지된다 (씬을 옮겨도 배경음이 끊기지 않음).
/// </summary>
// 설정(SettingsManager, -80)이 준비된 뒤, 화면들보다는 먼저
[DefaultExecutionOrder(-70)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("배경음")]
    [Tooltip("시작할 때 트는 곡. 비우면 코드로 만든 임시 배경음 (실제 곡이 생기기 전 확인용)")]
    [SerializeField] private AudioClip _defaultBgm;

    [Header("효과음")]
    [Tooltip("설정 화면에서 효과음 크기를 바꿀 때 들려줄 소리. 비우면 임시 \"띵\"")]
    [SerializeField] private AudioClip _previewSfx;

    [Tooltip("미리듣기 최소 간격(초). 슬라이더를 끄는 동안 너무 자주 울리지 않게")]
    [SerializeField] private float _previewInterval = 0.12f;

    private AudioSource _bgmSource;
    private AudioSource _sfxSource;
    private GameSettings _settings;
    private float _lastPreviewTime = float.NegativeInfinity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;

        if (_defaultBgm == null)
            _defaultBgm = PlaceholderAudio.CreateBgm();
        if (_previewSfx == null)
            _previewSfx = PlaceholderAudio.CreateDing();
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        _settings = SettingsManager.Instance.Settings;
        _settings.OnChanged += ApplyVolumes;
        ApplyVolumes();
    }

    private void Start()
    {
        if (Instance == this)
            PlayBgm(_defaultBgm);
    }

    private void OnDisable()
    {
        if (_settings != null)
            _settings.OnChanged -= ApplyVolumes;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>배경음 바꾸기. 같은 곡이 이미 나오고 있으면 처음부터 다시 틀지 않는다</summary>
    public void PlayBgm(AudioClip clip)
    {
        if (clip == null)
            throw new System.ArgumentNullException(nameof(clip));

        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
            return;

        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    public void StopBgm() => _bgmSource.Stop();

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null)
            throw new System.ArgumentNullException(nameof(clip));

        _sfxSource.PlayOneShot(clip);
    }

    /// <summary>효과음 크기 미리듣기 (간격 안에 또 부르면 무시)</summary>
    public void PlaySfxPreview()
    {
        if (Time.unscaledTime - _lastPreviewTime < _previewInterval)
            return;

        _lastPreviewTime = Time.unscaledTime;
        PlaySfx(_previewSfx);
    }

    private void ApplyVolumes()
    {
        _bgmSource.volume = _settings.BgmVolume;
        _sfxSource.volume = _settings.SfxVolume;
    }
}
