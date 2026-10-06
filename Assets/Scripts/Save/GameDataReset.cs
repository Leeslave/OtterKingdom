using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어용 데이터 초기화 (설정 화면, 상세기획서 11.6). 확인은 부르는 쪽(SettingsPresenter)이 두 번 받는다.
/// 1) 저장을 막고  2) 세이브 파일을 보관함으로 옮긴 뒤(마지막 초기화 하나만 남김 — 문의가 오면 되살릴 수 있게)
/// 3) 씬을 넘어 사는 오브젝트(전역 UI·재화·가방 등 진행 상태를 가진 것)를 모두 없애고  4) 첫 씬부터 새 게임으로 다시 시작한다.
/// 기기 설정(소리·진동)과 익명 설치 ID는 남는다.
/// </summary>
public static class GameDataReset
{
    private const string SnapshotTag = "player-reset";
    private const int FirstSceneIndex = 0;

    public static bool IsResetting { get; private set; }

    public static void ResetAndRestart()
    {
        if (IsResetting)
            return;
        IsResetting = true;
        AnalyticsLog.Track("data_reset");

        // 없애는 오브젝트들이 사라지면서 옛 진행을 다시 저장하지 않게, 새 씬이 열릴 때까지 막음
        SaveService.WritesFrozen = true;

        var service = new SaveService();
        string kept = service.WipeToSnapshot(SnapshotTag);
        foreach (var name in service.ListSnapshots())
        {
            if (name != kept && name.EndsWith("-" + SnapshotTag))
                service.DeleteSnapshot(name);
        }

        Time.timeScale = 1f;
        foreach (var root in PersistentRoots())
        {
            if (!KeepsThroughReset(root))
                Object.Destroy(root);
        }

        SceneManager.sceneLoaded += HandleFirstSceneLoaded;
        SceneManager.LoadScene(FirstSceneIndex);
    }

    private static void HandleFirstSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleFirstSceneLoaded;
        SaveService.WritesFrozen = false;
        IsResetting = false;
    }

    // DontDestroyOnLoad로 옮겨진 오브젝트들 (그 씬은 직접 찾을 수 없어 임시 오브젝트로 들어가 봄)
    private static List<GameObject> PersistentRoots()
    {
        var probe = new GameObject("ResetProbe");
        Object.DontDestroyOnLoad(probe);
        var roots = new List<GameObject>(probe.scene.GetRootGameObjects());
        roots.Remove(probe);
        Object.Destroy(probe);
        return roots;
    }

    // 진행 상태가 없고, 게임 시작 때 한 번만 저절로 생기는 것들은 남김 (없애면 다시 생기지 않음)
    private static bool KeepsThroughReset(GameObject root)
    {
        return root.GetComponent<BackButtonHandler>() != null
            || root.GetComponent<AnalyticsReporter>() != null
            || root.GetComponent<ButtonFeedbackInstaller>() != null
            || root.GetComponent<GameSoundEvents>() != null;
    }
}
