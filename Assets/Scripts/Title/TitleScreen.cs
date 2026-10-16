using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 화면. "Touch To Start"를 깜빡이다가 화면 아무 곳이나 누르면 어두워진 뒤 광장 씬으로 넘어간다.
/// 화면 전체를 덮는 투명 Image(Raycast Target)에 붙인다. 씬 구성은 OtterKingdom > Tools > Setup Title Scene.
/// </summary>
public class TitleScreen : MonoBehaviour, IPointerClickHandler
{
    [Header("이동")]
    [Tooltip("누르면 불러올 씬 이름 (Build Settings에 있어야 함)")]
    [SerializeField] private string _nextScene = "Plaza";

    [Header("구성 요소")]
    [SerializeField] private TMP_Text _touchText;
    [SerializeField] private ScreenFader _fader;

    [Header("연출")]
    [Tooltip("Touch To Start가 한 번 흐려졌다 돌아오는 시간(초)")]
    [SerializeField] private float _blinkPeriod = 1.6f;
    [Tooltip("가장 흐릴 때의 투명도")]
    [SerializeField, Range(0f, 1f)] private float _minAlpha = 0.25f;

    private bool _starting;

    private void Update()
    {
        if (_touchText == null || _starting) return;

        float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / _blinkPeriod);
        _touchText.alpha = Mathf.Lerp(_minAlpha, 1f, wave);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_starting) return;

        if (!Application.CanStreamedLevelBeLoaded(_nextScene))
        {
            Debug.LogError($"[TitleScreen] '{_nextScene}' 씬이 Build Settings에 없습니다.", this);
            return;
        }

        StartCoroutine(StartRoutine());
    }

    private IEnumerator StartRoutine()
    {
        _starting = true;
        if (_touchText != null) _touchText.alpha = 1f;

        if (_fader != null) yield return _fader.FadeOut();
        SceneManager.LoadScene(_nextScene);
    }
}
