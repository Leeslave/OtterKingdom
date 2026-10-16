using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 이야기(컷씬) 재생 화면: 일러스트 컷 위에 대사가 한 글자씩 나온다. 얼굴·이름표는 없다.
/// 탭: 글자가 나오는 중이면 그 줄을 바로 끝까지, 다 나왔으면 다음 줄. 건너뛰기: 즉시 끝.
/// 받은 컷과 문장만 그리고 끝났음을 알리기만 한다 (도감 모델을 모름).
/// </summary>
public class CollectionStoryPlayerView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("컷")]
    [Tooltip("지금 보이는 컷")]
    [SerializeField] private Image _cutFront;
    [Tooltip("컷이 바뀔 때 뒤에서 겹쳐 사라지는 이전 컷")]
    [SerializeField] private Image _cutBack;
    [SerializeField] private float _crossfadeDuration = 0.25f;

    [Header("대사")]
    [SerializeField] private TextMeshProUGUI _lineText;
    [Tooltip("줄이 다 나왔을 때 까딱이는 ▼")]
    [SerializeField] private GameObject _nextIndicator;
    [Tooltip("초당 글자 수")]
    [SerializeField] private float _charactersPerSecond = 20f;

    [Header("진행 점 (몇 번째 줄인지)")]
    [SerializeField] private List<Image> _dots = new List<Image>();
    [SerializeField] private Color _dotOnColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    [SerializeField] private Color _dotOffColor = new Color32(0xE9, 0xD0, 0xB2, 0xFF);

    [Header("버튼")]
    [Tooltip("화면 전체 (탭하면 진행)")]
    [SerializeField] private Button _tapArea;
    [SerializeField] private Button _skipButton;

    /// <summary>끝까지 봤거나 건너뛰었을 때 (닫힘 연출 시작 시점)</summary>
    public event Action OnFinished;

    public bool IsPlaying { get; private set; }

    private readonly List<Sprite> _cuts = new List<Sprite>();
    private readonly List<string> _texts = new List<string>();
    private readonly List<int> _cutIndices = new List<int>();
    private int _lineIndex;
    private int _shownCut = -1;
    private Coroutine _typing;
    private Coroutine _fading;
    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <param name="cuts">컷 그림 (이미 임시 컷으로 채워진 목록)</param>
    /// <param name="lines">(보일 컷 번호, 이름이 들어간 문장)</param>
    public void Play(IReadOnlyList<Sprite> cuts, IReadOnlyList<(int cut, string text)> lines)
    {
        if (cuts == null) throw new ArgumentNullException(nameof(cuts));
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        if (lines.Count == 0) throw new ArgumentException("대사가 없습니다.", nameof(lines));

        EnsureInitialized();

        _cuts.Clear();
        _cuts.AddRange(cuts);
        _texts.Clear();
        _cutIndices.Clear();
        foreach (var (cut, text) in lines)
        {
            _cutIndices.Add(cut);
            _texts.Add(text);
        }

        for (int i = 0; i < _dots.Count; i++)
            _dots[i].gameObject.SetActive(i < _texts.Count);

        _shownCut = -1;
        _cutBack.enabled = false;
        IsPlaying = true;
        _animator.Show();
        ShowLine(0);
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        _tapArea.onClick.AddListener(HandleTap);
        _skipButton.onClick.AddListener(Finish);
        _isInitialized = true;
    }

    private void HandleTap()
    {
        if (!IsPlaying)
            return;

        // 글자가 나오는 중 → 그 줄을 바로 끝까지
        if (_typing != null)
        {
            StopCoroutine(_typing);
            _typing = null;
            _lineText.maxVisibleCharacters = int.MaxValue;
            _nextIndicator.SetActive(true);
            return;
        }

        if (_lineIndex + 1 < _texts.Count)
            ShowLine(_lineIndex + 1);
        else
            Finish();
    }

    private void ShowLine(int index)
    {
        _lineIndex = index;
        ShowCut(Mathf.Clamp(_cutIndices[index], 0, Mathf.Max(0, _cuts.Count - 1)));

        for (int i = 0; i < _dots.Count; i++)
            _dots[i].color = i <= index ? _dotOnColor : _dotOffColor;

        _lineText.text = _texts[index];
        _lineText.maxVisibleCharacters = 0;
        _nextIndicator.SetActive(false);
        if (_typing != null)
            StopCoroutine(_typing);
        _typing = StartCoroutine(TypeRoutine());
    }

    private void ShowCut(int index)
    {
        if (index == _shownCut || _cuts.Count == 0)
            return;

        bool first = _shownCut < 0;
        _shownCut = index;

        if (first)
        {
            _cutFront.sprite = _cuts[index];
            _cutFront.color = Color.white;
            return;
        }

        // 이전 컷을 뒤로 보내고 새 컷을 서서히 드러냄
        _cutBack.sprite = _cutFront.sprite;
        _cutBack.enabled = true;
        _cutFront.sprite = _cuts[index];
        if (_fading != null)
            StopCoroutine(_fading);
        _fading = StartCoroutine(CrossfadeRoutine());
    }

    private IEnumerator TypeRoutine()
    {
        // 한 글자씩: 태그를 뺀 실제 글자 수를 알기 위해 메시를 먼저 갱신
        _lineText.ForceMeshUpdate();
        int total = _lineText.textInfo.characterCount;
        float shown = 0f;

        while (shown < total)
        {
            shown += _charactersPerSecond * Time.unscaledDeltaTime;
            _lineText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
            yield return null;
        }

        _lineText.maxVisibleCharacters = int.MaxValue;
        _nextIndicator.SetActive(true);
        _typing = null;
    }

    private IEnumerator CrossfadeRoutine()
    {
        for (float t = 0f; t < _crossfadeDuration; t += Time.unscaledDeltaTime)
        {
            _cutFront.color = new Color(1f, 1f, 1f, t / _crossfadeDuration);
            yield return null;
        }

        _cutFront.color = Color.white;
        _cutBack.enabled = false;
        _fading = null;
    }

    private void Finish()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;
        if (_typing != null)
            StopCoroutine(_typing);
        _typing = null;

        _animator.Hide();
        OnFinished?.Invoke();
    }
}
