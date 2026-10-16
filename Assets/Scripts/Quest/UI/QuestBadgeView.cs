using TMPro;
using UnityEngine;

/// <summary>
/// 네비게이션 바 퀘스트 버튼의 숫자 뱃지. 보상을 받을 수 있는 퀘스트 수를 보여주고, 0이면 숨긴다.
/// </summary>
public class QuestBadgeView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("켜고 끌 뱃지 오브젝트 (이 컴포넌트가 붙은 오브젝트와 달라야 함)")]
    [SerializeField] private GameObject _badge;
    [SerializeField] private TextMeshProUGUI _countText;

    private QuestManager _manager;

    private void OnEnable()
    {
        _manager = QuestManager.Instance;
        _manager.OnChanged += Refresh;

        // 꺼져 있던 동안 바뀐 상태 반영
        Refresh();
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        int count = _manager.ClaimableCount;
        _badge.SetActive(count > 0);
        _countText.text = count > 9 ? "9+" : count.ToString();
    }
}
