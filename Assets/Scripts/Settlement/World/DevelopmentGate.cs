using System.Collections;
using UnityEngine;

/// <summary>
/// 왕국 발전에 따라 켜지거나 꺼지는 광장 오브젝트 (집, 벤치, 막힌 길의 잡목 등).
/// 오브젝트를 지우지 않고 켜고 끈다. 판단·적용은 SettlementPlazaView가 한다 (꺼진 오브젝트는 스스로 깨어나지 않으므로).
/// </summary>
public class DevelopmentGate : MonoBehaviour
{
    /// <summary>새로 생길 때 통 튀는 시간 (걷기 영역은 이 뒤에 다시 계산 — 튀는 동안 크기가 0에서 시작하므로)</summary>
    public const float PopSeconds = 0.35f;

    [Header("조건")]
    [Tooltip("이 발전 ID (예: house_1, farmland)")]
    [SerializeField] private string _developmentId;

    [Tooltip("켜면 발전 후에 보임 (집). 끄면 발전 후에 사라짐 (개간으로 치우는 잡목·돌)")]
    [SerializeField] private bool _showWhenUnlocked = true;

    [Tooltip("새로 보이게 될 때 광장 카메라가 이쪽을 비춤 (집, 밭 표지판)")]
    [SerializeField] private bool _focusOnUnlock;

    [Tooltip("이 발전이 열리면 숨김 (같은 자리가 더 큰 건물로 바뀔 때: 접수소 → 마을회관). 비우면 없음")]
    [SerializeField] private string _supersededBy;

    [Tooltip("켜면 통 튀지 않고 바로 나타남 (넓은 바닥 조각처럼 튀면 어색한 것)")]
    [SerializeField] private bool _noPop;

    public string DevelopmentId => _developmentId;
    public bool FocusOnUnlock => _focusOnUnlock && _showWhenUnlocked;

    public bool ShouldBeVisible(bool unlocked) => unlocked == _showWhenUnlocked;

    /// <param name="has">발전이 열렸는지</param>
    /// <param name="animate">새로 생길 때 통통 튀어나오는 연출</param>
    public void Apply(System.Func<string, bool> has, bool animate)
    {
        bool superseded = !string.IsNullOrEmpty(_supersededBy) && has(_supersededBy);
        SetVisible(ShouldBeVisible(has(_developmentId)) && !superseded, animate);
    }

    /// <param name="animate">새로 생길 때 통통 튀어나오는 연출</param>
    public void Apply(bool unlocked, bool animate) => SetVisible(ShouldBeVisible(unlocked), animate);

    private void SetVisible(bool visible, bool animate)
    {
        if (gameObject.activeSelf == visible)
            return;

        gameObject.SetActive(visible);
        // 부모가 꺼져 있으면(다른 발전 오브젝트 아래) 연출 없이
        if (visible && animate && !_noPop && gameObject.activeInHierarchy)
            StartCoroutine(Pop());
    }

    private IEnumerator Pop()
    {
        var target = transform.localScale;
        for (float t = 0f; t < PopSeconds; t += Time.deltaTime)
        {
            float k = t / PopSeconds;
            // 0 → 1.12 → 1 (살짝 넘쳤다 돌아옴)
            float s = k < 0.7f ? Mathf.Lerp(0f, 1.12f, k / 0.7f) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
            transform.localScale = target * s;
            yield return null;
        }
        transform.localScale = target;
    }
}
