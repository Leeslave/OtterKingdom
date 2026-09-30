using UnityEngine;

/// <summary>
/// 환경설정(GameSettings)의 주인. 기기(PlayerPrefs)에 저장/불러오기와 진동 요청을 맡는다.
/// 전역 UI 루트에 붙어 씬을 넘어 유지된다 (중복은 GlobalUIRoot가 먼저 정리).
/// </summary>
// 설정 화면(SettingsPresenter)의 OnEnable보다 먼저 값이 준비돼 있어야 함
[DefaultExecutionOrder(-80)]
public class SettingsManager : MonoBehaviour
{
    private const string PrefsKey = "settings";

    public static SettingsManager Instance { get; private set; }

    public GameSettings Settings { get; private set; }

    private bool _isDirty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        Settings = new GameSettings();
        Load();
        Settings.OnChanged += HandleChanged;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Settings.OnChanged -= HandleChanged;
        SaveIfDirty();
        Instance = null;
    }

    // 모바일은 종료 대신 백그라운드로 가면서 끝나는 경우가 많아 여기서도 저장
    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveIfDirty();
    }

    private void OnApplicationQuit() => SaveIfDirty();

    /// <summary>진동이 켜져 있을 때만 진동 (모바일 기기에서만 동작)</summary>
    public void Vibrate()
    {
        if (!Settings.VibrationEnabled)
            return;
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    /// <summary>바뀐 값을 기기에 기록 (슬라이더를 끄는 동안 매번 쓰지 않도록 화면을 닫을 때 호출)</summary>
    public void SaveIfDirty()
    {
        if (!_isDirty)
            return;

        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Settings.ToData()));
        PlayerPrefs.Save();
        _isDirty = false;
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, null);
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            Settings.Apply(JsonUtility.FromJson<SettingsData>(json));
        }
        catch (System.ArgumentException e)
        {
            // 손상된 값은 기본값으로 시작 (다음 변경 때 덮어씀)
            Debug.LogWarning($"[SettingsManager] 저장된 설정을 읽지 못해 기본값을 씁니다: {e.Message}");
        }
    }

    private void HandleChanged() => _isDirty = true;
}
