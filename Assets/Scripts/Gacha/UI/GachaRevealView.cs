using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보물 조개 뽑기 연출 (Docs/장난감_뽑기_기획.md 4장).
/// 1회: 파도가 조개를 해달 배 위로 → 톡톡 세 번 두드리면 금이 가며 빛이 샘 (빛 색 = 등급: 하양 · 파랑 · 금) →
///      가끔 빛 색이 한 단계 오름(승급 — 에픽은 밤바다로, 픽업 에픽은 벚꽃잎) → 쩍 갈라지며 장난감이 튀어 오름 → 결과 카드.
/// 10회: 해달 열 마리 뗏목, 한 번 누르면 차례로 열림 (가장 좋은 것은 마지막) → 에픽이면 큰 연출 한 번 → 결과 10칸.
/// 결과는 연출 전에 이미 가방에 들어가 있다 (GachaManager). 건너뛰기·뒤로 가기는 언제든 결과로 바로.
/// 순서는 GachaRevealView.Sequence.cs, 여기는 화면 · 계속 도는 움직임 · 빛 효과
/// </summary>
public partial class GachaRevealView : MonoBehaviour
{
    /// <summary>연출 중 (알림 · 방문 연출이 기다림 — PresentationGate)</summary>
    public static bool IsPlaying { get; private set; }

    private static GachaRevealView _current;

    // 조개 모습 (그림 배열 순서)
    private const int LookCommon = 0;
    private const int LookRare = 1;
    private const int LookEpic = 2;
    private const int LookPickup = 3;

    private const int StarCount = 16;
    private const int PetalCount = 18;

    [Header("화면")]
    [Tooltip("전체 투명도 (열고 닫을 때)")]
    [SerializeField] private CanvasGroup _group;

    [Tooltip("밤바다 (밤 배경 + 별). 에픽 빛이 나면 나타남")]
    [SerializeField] private CanvasGroup _night;

    [Tooltip("밤하늘 별이 생기는 곳")]
    [SerializeField] private RectTransform _starLayer;

    [Tooltip("벚꽃잎이 흩날리는 곳 (픽업 에픽)")]
    [SerializeField] private RectTransform _petalLayer;

    [Tooltip("흔들리는 무대 (배경 빼고 전부)")]
    [SerializeField] private RectTransform _shakeRoot;

    [Tooltip("번쩍 (흰 화면)")]
    [SerializeField] private Image _flash;

    [Tooltip("반짝임이 생기는 곳 (맨 앞)")]
    [SerializeField] private RectTransform _sparkleLayer;

    [SerializeField] private Button _skipButton;

    [Tooltip("화면 어디를 눌러도 톡 (투명 버튼)")]
    [SerializeField] private Button _tapArea;

    [Tooltip("\"톡톡!\" 안내 말풍선 (보이고 숨음)")]
    [SerializeField] private RectTransform _tapHintRoot;

    [SerializeField] private TextMeshProUGUI _tapHint;

    [Header("1회")]
    [SerializeField] private RectTransform _single;

    [Tooltip("둥실거리는 해달 (조개가 배 위에 얹힘)")]
    [SerializeField] private RectTransform _otterRoot;

    [SerializeField] private Image _otter;

    [Tooltip("배 위 조개 자리 (해달 기준)")]
    [SerializeField] private RectTransform _shellRoot;

    [Tooltip("조개 뒤 빛 번짐 (등급 색)")]
    [SerializeField] private Image _shellGlow;

    [SerializeField] private Image _shell;

    [SerializeField] private Image _crackGlow;

    [SerializeField] private Image _crackCore;

    [Tooltip("갈라진 왼쪽 반쪽")]
    [SerializeField] private Image _shellLeft;

    [Tooltip("갈라진 오른쪽 반쪽")]
    [SerializeField] private Image _shellRight;

    [Tooltip("해달 앞 물결 (바다 색)")]
    [SerializeField] private Image _water;

    [SerializeField] private Image _foam;

    [Tooltip("튀어나온 장난감 (해달 위에서 둥실)")]
    [SerializeField] private RectTransform _toyRoot;

    [Tooltip("장난감 뒤 빛살 (해달 뒤에 그림)")]
    [SerializeField] private Image _rays;

    [Tooltip("장난감 뒤 빛 번짐 (해달 뒤에 그림)")]
    [SerializeField] private Image _toyGlow;

    [Tooltip("갈라질 때 퍼지는 빛 고리")]
    [SerializeField] private Image _ring;

    [SerializeField] private Image _toy;

    [SerializeField] private GachaResultCardView _card;

    [Header("10회")]
    [SerializeField] private RectTransform _raft;

    [Tooltip("뗏목의 해달 10마리")]
    [SerializeField] private GachaRaftSlotView[] _raftSlots = new GachaRaftSlotView[10];

    [SerializeField] private GachaResultsView _results;

    [Header("그림")]
    [Tooltip("누워 쉬는 해달 (돌을 쥠)")]
    [SerializeField] private Sprite _otterRest;

    [Tooltip("돌을 들고 두드리려는 해달")]
    [SerializeField] private Sprite _otterReady;

    [Tooltip("놀란 해달")]
    [SerializeField] private Sprite _otterSurprise;

    [Tooltip("조개: 흔함 · 레어 · 에픽 · 픽업 순")]
    [SerializeField] private Sprite[] _shellSprites = new Sprite[4];

    [Tooltip("조개 왼쪽 반쪽 (같은 순서)")]
    [SerializeField] private Sprite[] _shellLeftSprites = new Sprite[4];

    [Tooltip("조개 오른쪽 반쪽 (같은 순서)")]
    [SerializeField] private Sprite[] _shellRightSprites = new Sprite[4];

    [Tooltip("금: 1 · 2 · 3단계")]
    [SerializeField] private Sprite[] _crackCores = new Sprite[3];

    [Tooltip("금 사이로 새는 빛: 1 · 2 · 3단계")]
    [SerializeField] private Sprite[] _crackGlows = new Sprite[3];

    [Tooltip("별빛 · 반짝임")]
    [SerializeField] private Sprite _twinkle;

    [Tooltip("벚꽃잎")]
    [SerializeField] private Sprite[] _petals = new Sprite[0];

    [Header("색")]
    [Tooltip("빛 색: 흔함 · 레어 · 에픽")]
    [SerializeField] private Color[] _lightColors =
    {
        new Color(1f, 0.97f, 0.86f, 1f),
        new Color(0.5f, 0.78f, 1f, 1f),
        new Color(1f, 0.8f, 0.3f, 1f),
    };

    [Tooltip("픽업 에픽의 빛 (벚꽃 금빛)")]
    [SerializeField] private Color _pickupLight = new Color(1f, 0.74f, 0.82f, 1f);

    [Tooltip("낮 바다 물결 색 (배경 바다와 같게)")]
    [SerializeField] private Color _dayWater = new Color(0.43f, 0.78f, 0.84f, 1f);

    [Tooltip("밤바다 물결 색")]
    [SerializeField] private Color _nightWater = new Color(0.18f, 0.27f, 0.47f, 1f);

    /// <summary>결과에서 [확인]을 눌러 닫혔을 때</summary>
    public event Action OnClosed;

    /// <summary>결과에서 [한 번 더]를 눌렀을 때 (같은 횟수로 다시)</summary>
    public event Action OnAgain;

    private GachaPullReport _report;
    private GachaAgainInfo _again;
    private Func<GachaPull, (string hint, Sprite portrait)> _describe;

    private bool _waitingTap;
    private int _taps;
    private bool _finished;
    private bool _toyFloating;
    private float _shellGlowScale = 1f;
    private float _raysAlpha;
    private Vector2 _otterHome;
    private Vector2 _otterPunch;
    private Vector2 _shellRest;
    private Vector2 _toyRest;
    private Vector2 _waterRest;
    private Vector2 _foamRest;
    private bool _restKnown;
    private float _shakeStrength;
    private float _shakeTime;
    private float _shakeDuration;

    private readonly List<Star> _stars = new List<Star>();
    private readonly List<Petal> _petalsLive = new List<Petal>();
    private bool _petalsOn;

    private class Star
    {
        public Image Image;
        public float Phase;
        public float Speed;
    }

    private class Petal
    {
        public RectTransform Rect;
        public Vector2 Velocity;
        public float Spin;
        public float Sway;
        public float Phase;
    }

    private void Awake()
    {
        _tapArea.onClick.AddListener(HandleTap);
        _skipButton.onClick.AddListener(Skip);
        RememberRest();
    }

    private void OnEnable()
    {
        _card.OnOk += Close;
        _card.OnAgain += HandleAgain;
        _results.OnOk += Close;
        _results.OnAgain += HandleAgain;
    }

    private void OnDisable()
    {
        _card.OnOk -= Close;
        _card.OnAgain -= HandleAgain;
        _results.OnOk -= Close;
        _results.OnAgain -= HandleAgain;
        if (_current == this)
            IsPlaying = false;
    }

    #region 바깥에서

    /// <summary>
    /// 연출을 처음부터 (다시 뽑기면 다시 시작)
    /// </summary>
    /// <param name="again">결과 화면 [한 번 더] 버튼 모양</param>
    /// <param name="describe">결과 카드의 안내 (광장에 두면 누가 오는지 · 한정 해달 얼굴)</param>
    public void Play(GachaPullReport report, GachaAgainInfo again, Func<GachaPull, (string hint, Sprite portrait)> describe)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        if (report.Pulls.Count == 0)
            throw new ArgumentException("결과가 없는 뽑기입니다.", nameof(report));

        _report = report;
        _again = again;
        _describe = describe;
        _current = this;
        IsPlaying = true;
        gameObject.SetActive(true);
        StopAllCoroutines();
        ResetStage();
        StartCoroutine(report.Pulls.Count == 1 ? SingleRoutine(report.Pulls[0], false, true) : RaftRoutine());
    }

    /// <summary>[한 번 더]가 막혔을 때 (값이 모자람 등) 결과 화면은 그대로 둠</summary>
    public bool IsShowingResult => _finished;

    /// <summary>Android 뒤로 가기: 연출 중이면 결과로 건너뜀, 결과 화면이면 닫음</summary>
    public static bool TryHandleBack()
    {
        if (!IsPlaying || _current == null)
            return false;
        if (_current._finished)
            _current.Close();
        else
            _current.Skip();
        return true;
    }

    #endregion

    #region 준비 · 닫기

    private void RememberRest()
    {
        if (_restKnown)
            return;
        _otterHome = _otterRoot.anchoredPosition;
        _shellRest = _shellRoot.anchoredPosition;
        _toyRest = _toyRoot.anchoredPosition;
        _waterRest = _water.rectTransform.anchoredPosition;
        _foamRest = _foam.rectTransform.anchoredPosition;
        foreach (var slot in _raftSlots)
            slot.BodyRest = slot.Body.anchoredPosition;
        _restKnown = true;
    }

    private void ResetStage()
    {
        RememberRest();
        _finished = false;
        _waitingTap = false;
        _toyFloating = false;
        _petalsOn = false;
        _otterPunch = Vector2.zero;
        _shellGlowScale = 1f;
        _raysAlpha = 0f;
        _shakeStrength = 0f;

        _group.alpha = 1f;
        _night.alpha = 0f;
        _water.color = _dayWater;
        GachaTween.SetAlpha(_flash, 0f);
        _shakeRoot.anchoredPosition = Vector2.zero;
        ClearChildren(_sparkleLayer);
        foreach (var petal in _petalsLive)
            petal.Rect.gameObject.SetActive(false);

        _single.gameObject.SetActive(false);
        _raft.gameObject.SetActive(false);
        _card.Hide();
        _results.Hide();
        _tapHintRoot.gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(true);

        ResetSingle();
    }

    private void ResetSingle()
    {
        _otter.sprite = _otterRest;
        _otterRoot.localScale = Vector3.one;
        _shellRoot.anchoredPosition = _shellRest;
        _shellRoot.localScale = Vector3.one;
        _shellRoot.localRotation = Quaternion.identity;
        _shellRoot.gameObject.SetActive(false);
        _shell.gameObject.SetActive(true);
        _shell.sprite = _shellSprites[LookCommon];
        _shell.rectTransform.localScale = Vector3.one;
        _shellGlow.gameObject.SetActive(false);
        _crackGlow.gameObject.SetActive(false);
        _crackCore.gameObject.SetActive(false);
        _shellLeft.gameObject.SetActive(false);
        _shellRight.gameObject.SetActive(false);
        foreach (var half in new[] { _shellLeft, _shellRight })
        {
            half.rectTransform.anchoredPosition = Vector2.zero;
            half.rectTransform.localRotation = Quaternion.identity;
            GachaTween.SetAlpha(half, 1f);
        }
        _toyRoot.gameObject.SetActive(false);
        _toyRoot.anchoredPosition = _toyRest;
        _toy.rectTransform.localScale = Vector3.one;
        _rays.gameObject.SetActive(false);
        _ring.gameObject.SetActive(false);
        GachaTween.SetAlpha(_toyGlow, 0f);
    }

    private void HandleAgain() => OnAgain?.Invoke();

    private void Close()
    {
        if (!gameObject.activeInHierarchy)
            return;
        StopAllCoroutines();
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        _waitingTap = false;
        yield return GachaTween.Run(0.22f, t => _group.alpha = 1f - t);
        IsPlaying = false;
        _card.Hide();
        _results.Hide();
        // 끄면 이 코루틴도 멈추므로 알림을 먼저
        OnClosed?.Invoke();
        gameObject.SetActive(false);
    }

    #endregion

    #region 입력

    private void HandleTap()
    {
        if (_waitingTap)
            _taps++;
    }

    private IEnumerator WaitTap(string hint)
    {
        _taps = 0;
        _waitingTap = true;
        _tapHint.text = hint;
        _tapHintRoot.gameObject.SetActive(true);
        while (_taps == 0)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f;
            _tapHintRoot.localScale = Vector3.one * pulse;
            yield return null;
        }
        _waitingTap = false;
        _tapHintRoot.gameObject.SetActive(false);
    }

    #endregion

    #region 계속 도는 움직임

    private void Update()
    {
        if (!IsPlaying)
            return;

        float time = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        // 해달이 물 위에서 둥실 (조개도 배 위에 얹혀 같이 움직임)
        _otterRoot.anchoredPosition = _otterHome + new Vector2(0f, Mathf.Sin(time * 2.4f) * 9f) + _otterPunch;
        _otterRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.2f) * 1.5f);
        var sway = new Vector2(Mathf.Sin(time * 0.7f) * 26f, Mathf.Sin(time * 1.6f) * 5f);
        _water.rectTransform.anchoredPosition = _waterRest + sway;
        _foam.rectTransform.anchoredPosition = _foamRest + sway;

        if (_shellGlow.gameObject.activeSelf)
            _shellGlow.rectTransform.localScale = Vector3.one * (_shellGlowScale + Mathf.Sin(time * 5f) * 0.05f);

        if (_rays.gameObject.activeSelf)
        {
            _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -time * 16f);
            GachaTween.SetAlpha(_rays, _raysAlpha * (0.85f + Mathf.Sin(time * 3f) * 0.15f));
        }
        if (_toyFloating)
            _toyRoot.anchoredPosition = _toyRest + Vector2.up * (Mathf.Sin(time * 1.8f) * 12f);

        foreach (var slot in _raftSlots)
        {
            if (slot.gameObject.activeInHierarchy)
                slot.Body.anchoredPosition = slot.BodyRest + new Vector2(0f, Mathf.Sin(time * 2.2f + slot.Phase) * 6f);
        }

        UpdateShake(dt);
        UpdateStars(time);
        UpdatePetals(dt, time);
    }

    private void UpdateShake(float dt)
    {
        if (_shakeStrength <= 0f)
            return;
        _shakeTime += dt;
        float left = 1f - _shakeTime / _shakeDuration;
        if (left <= 0f)
        {
            _shakeStrength = 0f;
            _shakeRoot.anchoredPosition = Vector2.zero;
            return;
        }
        _shakeRoot.anchoredPosition = UnityEngine.Random.insideUnitCircle * (_shakeStrength * left);
    }

    private void Shake(float strength, float seconds)
    {
        _shakeStrength = Mathf.Max(strength, _shakeStrength * (1f - _shakeTime / Mathf.Max(0.01f, _shakeDuration)));
        _shakeDuration = seconds;
        _shakeTime = 0f;
    }

    #endregion

    #region 밤바다 · 별 · 벚꽃잎

    private IEnumerator GoNight(bool petals, float seconds)
    {
        EnsureStars();
        if (petals)
            StartPetals();
        var from = _water.color;
        yield return GachaTween.Run(seconds, t =>
        {
            float k = GachaTween.InOutSine(t);
            _night.alpha = Mathf.Max(_night.alpha, k);
            _water.color = Color.Lerp(from, _nightWater, k);
        });
    }

    private void SetNightNow(bool petals)
    {
        EnsureStars();
        _night.alpha = 1f;
        _water.color = _nightWater;
        if (petals)
            StartPetals();
    }

    private void EnsureStars()
    {
        if (_stars.Count > 0 || _twinkle == null)
            return;
        var area = _starLayer.rect;
        for (int i = 0; i < StarCount; i++)
        {
            var image = NewImage("Star", _starLayer, _twinkle, UnityEngine.Random.Range(26f, 58f));
            image.rectTransform.anchoredPosition = new Vector2(
                UnityEngine.Random.Range(area.xMin + 40f, area.xMax - 40f),
                UnityEngine.Random.Range(area.yMin + 20f, area.yMax - 20f));
            _stars.Add(new Star { Image = image, Phase = UnityEngine.Random.Range(0f, 6.3f), Speed = UnityEngine.Random.Range(1.6f, 3.4f) });
        }
    }

    private void UpdateStars(float time)
    {
        if (_night.alpha <= 0f)
            return;
        foreach (var star in _stars)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(time * star.Speed + star.Phase);
            star.Image.color = new Color(1f, 0.97f, 0.85f, 0.25f + 0.75f * k);
            star.Image.rectTransform.localScale = Vector3.one * (0.7f + 0.4f * k);
        }
    }

    private void StartPetals()
    {
        if (_petals == null || _petals.Length == 0)
            return;
        var area = _petalLayer.rect;
        while (_petalsLive.Count < PetalCount)
        {
            var image = NewImage("Petal", _petalLayer, _petals[_petalsLive.Count % _petals.Length], UnityEngine.Random.Range(40f, 70f));
            _petalsLive.Add(new Petal { Rect = image.rectTransform });
        }
        foreach (var petal in _petalsLive)
        {
            ResetPetal(petal, area, true);
            petal.Rect.gameObject.SetActive(true);
        }
        _petalsOn = true;
    }

    private static void ResetPetal(Petal petal, Rect area, bool anywhere)
    {
        float y = anywhere ? UnityEngine.Random.Range(area.yMin, area.yMax) : area.yMax + 60f;
        petal.Rect.anchoredPosition = new Vector2(UnityEngine.Random.Range(area.xMin, area.xMax + 300f), y);
        petal.Velocity = new Vector2(UnityEngine.Random.Range(-160f, -70f), UnityEngine.Random.Range(-190f, -110f));
        petal.Spin = UnityEngine.Random.Range(-140f, 140f);
        petal.Sway = UnityEngine.Random.Range(20f, 50f);
        petal.Phase = UnityEngine.Random.Range(0f, 6.3f);
        petal.Rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
    }

    private void UpdatePetals(float dt, float time)
    {
        if (!_petalsOn)
            return;
        var area = _petalLayer.rect;
        foreach (var petal in _petalsLive)
        {
            var p = petal.Rect.anchoredPosition + petal.Velocity * dt;
            p.x += Mathf.Sin(time * 2f + petal.Phase) * petal.Sway * dt;
            petal.Rect.anchoredPosition = p;
            petal.Rect.localRotation = Quaternion.Euler(0f, 0f, petal.Rect.localEulerAngles.z + petal.Spin * dt);
            if (p.y < area.yMin - 80f || p.x < area.xMin - 120f)
                ResetPetal(petal, area, false);
        }
    }

    #endregion

    #region 빛 효과

    private Color LightColor(int light, bool pickup) =>
        light >= 2 && pickup ? _pickupLight : _lightColors[Mathf.Clamp(light, 0, _lightColors.Length - 1)];

    private IEnumerator Flash(float alpha, float seconds)
    {
        yield return GachaTween.Run(seconds, t => GachaTween.SetAlpha(_flash, alpha * (1f - GachaTween.OutCubic(t))));
    }

    /// <summary>한 점에서 별빛이 사방으로 퍼짐 (반짝임 층 기준 위치)</summary>
    private void Burst(Vector3 worldCenter, Color color, int count, float radius, float size)
    {
        if (_twinkle == null)
            return;
        var center = (Vector2)_sparkleLayer.InverseTransformPoint(worldCenter);
        for (int i = 0; i < count; i++)
        {
            var image = NewImage("Sparkle", _sparkleLayer, _twinkle, size * UnityEngine.Random.Range(0.7f, 1.2f));
            float angle = (i + UnityEngine.Random.Range(-0.3f, 0.3f)) / count * Mathf.PI * 2f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float reach = radius * UnityEngine.Random.Range(0.7f, 1.15f);
            StartCoroutine(SparkleRoutine(image, center, direction * reach, color, UnityEngine.Random.Range(0.55f, 0.8f)));
        }
    }

    private static IEnumerator SparkleRoutine(Image image, Vector2 from, Vector2 offset, Color color, float seconds)
    {
        var rect = image.rectTransform;
        yield return GachaTween.Run(seconds, t =>
        {
            rect.anchoredPosition = from + offset * GachaTween.OutCubic(t);
            rect.localScale = Vector3.one * GachaTween.Bump(Mathf.Min(1f, t * 1.3f));
            rect.localRotation = Quaternion.Euler(0f, 0f, t * 90f);
            image.color = GachaTween.WithAlpha(color, 1f - GachaTween.InCubic(t));
        });
        Destroy(image.gameObject);
    }

    private IEnumerator RingRoutine(Vector3 worldCenter, Color color, float toScale, float seconds)
    {
        var rect = _ring.rectTransform;
        rect.position = worldCenter;
        _ring.gameObject.SetActive(true);
        yield return GachaTween.Run(seconds, t =>
        {
            rect.localScale = Vector3.one * Mathf.Lerp(0.3f, toScale, GachaTween.OutCubic(t));
            _ring.color = GachaTween.WithAlpha(color, 1f - t);
        });
        _ring.gameObject.SetActive(false);
    }

    private Image NewImage(string name, RectTransform parent, Sprite sprite, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    #endregion
}
