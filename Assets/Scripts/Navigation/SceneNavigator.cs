using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 장소(씬) 이동. 화면을 어둡게 → BeforeLeave(저장 등) → 씬 불러오기 → Arrived → 화면을 밝게.
/// 게임 쪽(GameManager 등)은 이 클래스를 직접 부르지 않고 이벤트만 구독한다.
/// </summary>
public class SceneNavigator : MonoBehaviour
{
    /// <summary>지금 씬을 떠나기 직전 (화면이 완전히 어두워진 뒤, 아직 옛 씬이 살아 있을 때). 저장은 여기서.</summary>
    public static event Action BeforeLeave;

    /// <summary>새 씬에 도착한 직후 (화면이 밝아지기 전)</summary>
    public static event Action<ZoneDefinition> Arrived;

    [Header("구성 요소")]
    [SerializeField] private GlobalUIRoot _root;
    [SerializeField] private ScreenFader _fader;

    public bool IsTraveling { get; private set; }

    /// <summary>지금 씬에 해당하는 장소. 장소로 등록되지 않은 씬이면 null</summary>
    public ZoneDefinition CurrentZone => ZoneLookup.FindByScene(_root.Zones, SceneManager.GetActiveScene().name);

    /// <returns>이동을 시작했으면 true. 이동 중이거나, 잠긴 장소이거나, 이미 그 장소면 false</returns>
    public bool TryGo(ZoneDefinition zone)
    {
        if (zone == null)
            throw new ArgumentNullException(nameof(zone));

        if (IsTraveling || !zone.IsAvailable || zone == CurrentZone)
            return false;

        if (!Application.CanStreamedLevelBeLoaded(zone.SceneName))
        {
            Debug.LogError($"[SceneNavigator] '{zone.SceneName}' 씬이 Build Settings에 없습니다.", zone);
            return false;
        }

        StartCoroutine(TravelRoutine(zone));
        return true;
    }

    private IEnumerator TravelRoutine(ZoneDefinition zone)
    {
        IsTraveling = true;
        yield return _fader.FadeOut();

        // 구독한 쪽의 예외 때문에 화면이 검은 채로 멈추지 않도록 여기서 끊는다
        try { BeforeLeave?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }

        var loading = SceneManager.LoadSceneAsync(zone.SceneName);
        while (!loading.isDone)
            yield return null;

        try { Arrived?.Invoke(zone); }
        catch (Exception e) { Debug.LogException(e); }

        yield return _fader.FadeIn();
        IsTraveling = false;
    }
}
