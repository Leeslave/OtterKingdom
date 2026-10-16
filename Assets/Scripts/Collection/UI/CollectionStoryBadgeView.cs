using UnityEngine;

/// <summary>
/// 하단 바 도감 버튼의 "N" 뱃지. 해금했지만 아직 안 본 이야기가 하나라도 있을 때만 보인다.
/// </summary>
public class CollectionStoryBadgeView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("켜고 끌 뱃지 오브젝트 (이 컴포넌트가 붙은 오브젝트와 달라야 함)")]
    [SerializeField] private GameObject _badge;

    private Collection _collection;
    private CollectionDatabase _database;

    private void OnEnable()
    {
        _collection = CollectionManager.Instance.Collection;
        _database = CollectionManager.Instance.Database;
        _collection.OnStoriesChanged += Refresh;

        // 꺼져 있던 동안 바뀐 상태 반영
        Refresh();
    }

    private void OnDisable()
    {
        if (_collection != null)
            _collection.OnStoriesChanged -= Refresh;
    }

    private void Refresh()
    {
        _badge.SetActive(_collection.CountNewStories(_database.Entries) > 0);
    }
}
