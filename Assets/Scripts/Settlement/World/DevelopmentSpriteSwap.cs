using System.Collections;
using UnityEngine;

/// <summary>
/// 발전이 열리면 같은 오브젝트의 그림을 바꾼다 (게시판 보강: 게시판 오브젝트가 그대로 보강된 게시판 그림으로).
/// 탭·발자국·앞뒤 정렬은 그대로 두고 그림만 바꾸므로, 공사 중에도 게시판을 계속 쓸 수 있다.
/// 상태는 발전 기록에서 읽기만 한다 (그림이 바뀌어야 저장되는 구조가 아님).
/// </summary>
public class DevelopmentSpriteSwap : MonoBehaviour
{
    private const float PopSeconds = 0.35f;

    [Tooltip("이 발전이 열리면 (예: board_upgraded)")]
    [SerializeField] private string _developmentId;

    [SerializeField] private SpriteRenderer _renderer;

    [Tooltip("발전 뒤 그림")]
    [SerializeField] private Sprite _unlockedSprite;

    private Sprite _baseSprite;
    private bool _applied;
    private bool _unlocked;

    private void Awake()
    {
        _baseSprite = _renderer.sprite;
    }

    private void Update()
    {
        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded)
            return;
        bool unlocked = manager.HasDevelopment(_developmentId);
        if (_applied && unlocked == _unlocked)
            return;

        // 씬을 열 때는 바로, 보는 중에 열리면 통 튀어나오게
        bool animate = _applied && unlocked;
        _applied = true;
        _unlocked = unlocked;
        _renderer.sprite = unlocked && _unlockedSprite != null ? _unlockedSprite : _baseSprite;
        if (animate)
            StartCoroutine(Pop());
    }

    private IEnumerator Pop()
    {
        var target = transform.localScale;
        for (float t = 0f; t < PopSeconds; t += Time.deltaTime)
        {
            float k = t / PopSeconds;
            float s = k < 0.6f ? Mathf.Lerp(0.85f, 1.08f, k / 0.6f) : Mathf.Lerp(1.08f, 1f, (k - 0.6f) / 0.4f);
            transform.localScale = new Vector3(target.x, target.y * s, target.z);
            yield return null;
        }
        transform.localScale = target;
    }
}
