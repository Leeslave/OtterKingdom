using UnityEngine;

/// <summary>
/// 플레이어 정보(PlayerProfile)의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// 게임 쪽이 이름·레벨을 정해 넣는다 (SetName / SetLevel). 세이브 연결 전까지는 기본 이름, Lv.1로 시작한다.
/// </summary>
// 상단바 뷰의 OnEnable보다 먼저 준비
[DefaultExecutionOrder(-80)]
public class ProfileManager : MonoBehaviour
{
    public static ProfileManager Instance { get; private set; }

    [Header("기본값")]
    [Tooltip("이름을 정하기 전(세이브가 없을 때) 보일 이름")]
    [SerializeField] private string _defaultName = "해달왕";

    public PlayerProfile Profile { get; private set; }

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        Profile = new PlayerProfile(_defaultName);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
