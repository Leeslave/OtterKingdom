using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 광산 밖(광장·밭 등)에 있을 때 광산에서 캔 것을 알림으로 보여 준다 (전역 UI 루트에 붙음).
/// 광산 씬에서는 광부 해달의 말풍선이 보여 주므로 알림을 띄우지 않는다. 오프라인 채굴은 복귀 팝업이 보여 준다.
/// </summary>
public class MiningToastPresenter : MonoBehaviour
{
    private const string MineScene = "Mine";

    [Header("화면")]
    [SerializeField] private FindToastView _toast;

    private void OnEnable()
    {
        GameManager.MiningFound += HandleMiningFound;
    }

    private void OnDisable()
    {
        GameManager.MiningFound -= HandleMiningFound;
    }

    private void HandleMiningFound(ItemDefinition item)
    {
        if (SceneManager.GetActiveScene().name == MineScene)
            return;

        _toast.Show(item.Icon, $"광산 · {item.DisplayName} +1");
    }
}
