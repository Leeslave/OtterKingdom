using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모든 UI 버튼에 누름 연출(ButtonPressFeedback)을 붙인다: 전역 UI, 장소 화면, 나중에 생기는 카드·칸까지.
/// 게임이 시작될 때 저절로 하나 만들어져 씬을 넘어 유지되고, 가끔 새 버튼을 찾아 붙인다.
/// </summary>
public class ButtonFeedbackInstaller : MonoBehaviour
{
    private const float ScanSeconds = 0.5f;

    private float _timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject(nameof(ButtonFeedbackInstaller));
        DontDestroyOnLoad(go);
        go.AddComponent<ButtonFeedbackInstaller>();
    }

    private void Update()
    {
        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f)
            return;
        _timer = ScanSeconds;
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!button.TryGetComponent<ButtonPressFeedback>(out _))
                button.gameObject.AddComponent<ButtonPressFeedback>();
        }
    }
}
