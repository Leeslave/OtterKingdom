#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>
/// 테스트용 게임 속도 (에디터 전용, 빌드에는 없음). 켜 두면 두 시간이 같이 N배로 흐른다:
/// - Unity 시간(Time.timeScale): 밭 자람 · 광산 · 해달 걷기 · 연출
/// - 게임 시계(GameClock): 건설 · 주민 작업 · 이사 · 개간 · 일일 초기화
/// 바꾸기: 메뉴 OtterKingdom/Dev/Game Speed, 구역 · 성 테스트 창. 켜 둔 동안 화면 왼쪽 위에 배속 표시.
/// 앞당겨진 게임 시계는 플레이를 끌 때 Dev/Clock 값으로 남는다 (DevClockMenu) — 세이브 시각이 거꾸로 가지 않게
/// </summary>
public class DevGameSpeed : MonoBehaviour
{
    public const float MaxSpeed = 30f;
    private const float DefaultMaxDeltaTime = 1f / 3f;

    private static DevGameSpeed _instance;
    private float _applied = 1f;
    private GUIStyle _style;

    /// <summary>배속 (1 = 보통)</summary>
    public static float Speed { get; set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;
        var go = new GameObject(nameof(DevGameSpeed)) { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DevGameSpeed>();
    }

    private void Update()
    {
        // 다른 곳이 시간을 멈췄으면(세이브 오류 팝업) 건드리지 않음
        if (Time.timeScale == 0f)
            return;
        float speed = Mathf.Clamp(Speed, 1f, MaxSpeed);
        if (speed != _applied || !Mathf.Approximately(Time.timeScale, speed))
        {
            Time.timeScale = speed;
            // 한 프레임 시간이 상한(1/3초)에 잘려 배속이 덜 나오지 않게
            Time.maximumDeltaTime = DefaultMaxDeltaTime * speed;
            _applied = speed;
        }
        if (speed > 1f)
            GameClock.DevOffset += TimeSpan.FromSeconds((speed - 1f) * Time.unscaledDeltaTime);
    }

    private void OnGUI()
    {
        if (_applied <= 1f)
            return;
        if (_style == null)
            _style = new GUIStyle(GUI.skin.box) { fontSize = 28, fontStyle = FontStyle.Bold };
        GUI.Box(new Rect(12f, 12f, 130f, 48f), $"x{_applied:0} 속도", _style);
    }
}
#endif
