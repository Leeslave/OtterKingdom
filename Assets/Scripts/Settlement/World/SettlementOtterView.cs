using TMPro;
using UnityEngine;

/// <summary>
/// 광장의 정착 해달 한 마리에 붙는 말풍선·표시 (SettlementPlazaView가 내보낼 때 붙임).
/// - 탭하면 한마디 (정착 후보는 처음 누르면 "저도 여기 살고 싶어요!" → 그 해달의 집 부탁이 열림)
/// - 할 말이 있는 정착 후보는 머리 위에 "!"
/// - 공사를 맡으면 망치 말풍선
/// </summary>
public class SettlementOtterView : MonoBehaviour
{
    private const float SpeechSeconds = 2.8f;
    private const float BobHeight = 0.08f;
    private const int OverlayOrder = 32000;

    private SettlementOtterDefinition _otter;
    private Collider2D _tapArea;
    private SpriteRenderer _speech;
    private TextMeshPro _speechText;
    private SpriteRenderer _attention;
    private SpriteRenderer _work;
    private Vector3 _attentionBase;
    private float _hideSpeechAt;
    private int _lastLine = -1;

    public SettlementOtterDefinition Otter => _otter;

    /// <param name="height">발밑에서 머리 위까지 (월드 단위)</param>
    public void Init(SettlementOtterDefinition otter, float height, Sprite speechBubble, Sprite alert, Sprite hammer, TMP_FontAsset font)
    {
        _otter = otter;

        // 부모 크기(건설 해달 1.1배 등)와 상관없이 같은 크기로 보이게
        float inverse = 1f / Mathf.Max(0.01f, transform.lossyScale.y);
        float headY = height * inverse;

        _tapArea = GetComponent<Collider2D>();
        if (_tapArea == null)
        {
            var circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = 0.75f * inverse;
            circle.offset = new Vector2(0f, headY * 0.45f);
            _tapArea = circle;
        }

        _speech = CreateSprite("Speech", speechBubble, new Vector3(0f, headY + 0.05f * inverse, 0f), inverse);
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(_speech.transform, false);
        // 말풍선(1.75 높이, 아래 0.35는 꼬리) 몸통 가운데
        textGo.transform.localPosition = new Vector3(0f, 1.02f, 0f);
        _speechText = textGo.AddComponent<TextMeshPro>();
        if (font != null)
            _speechText.font = font;
        _speechText.fontSize = 3f;
        _speechText.enableAutoSizing = true;
        _speechText.fontSizeMin = 2f;
        _speechText.fontSizeMax = 3f;
        _speechText.alignment = TextAlignmentOptions.Center;
        _speechText.color = new Color32(0x4B, 0x2E, 0x22, 0xFF);
        _speechText.rectTransform.sizeDelta = new Vector2(3.2f, 1.1f);
        _speechText.sortingOrder = OverlayOrder + 1;
        _speech.gameObject.SetActive(false);

        _attention = CreateSprite("Attention", alert, new Vector3(0.1f * inverse, headY + 0.05f * inverse, 0f), inverse);
        _attentionBase = _attention.transform.localPosition;
        _attention.gameObject.SetActive(false);

        if (hammer != null)
        {
            _work = CreateSprite("WorkBubble", hammer, new Vector3(0.55f * inverse, headY - 0.1f * inverse, 0f), inverse);
            _work.gameObject.SetActive(false);
        }
    }

    private SpriteRenderer CreateSprite(string name, Sprite sprite, Vector3 localPosition, float inverse)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = Vector3.one * inverse;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = OverlayOrder;
        return renderer;
    }

    /// <summary>공사를 맡았는지 (망치 말풍선)</summary>
    public void SetWorking(bool working)
    {
        if (_work != null && _work.gameObject.activeSelf != working)
            _work.gameObject.SetActive(working);
    }

    private void Update()
    {
        if (_otter == null)
            return;
        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded)
            return;

        bool speaking = _speech.gameObject.activeSelf;
        if (speaking && Time.time >= _hideSpeechAt)
        {
            _speech.gameObject.SetActive(false);
            speaking = false;
        }

        bool attention = !speaking && manager.HasPendingIntro(_otter);
        if (_attention.gameObject.activeSelf != attention)
            _attention.gameObject.SetActive(attention);
        if (attention)
            _attention.transform.localPosition = _attentionBase + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 4f)) * BobHeight);

        if (PlazaTapInput.TryGetTap(out Vector2 world) && _tapArea.OverlapPoint(world))
            Speak(manager);
    }

    private void Speak(SettlementManager manager)
    {
        string line;
        if (manager.TryHearIntro(_otter))
            line = _otter.IntroLine;
        else if (_otter.Lines.Count > 0)
            line = PickLine();
        else
            return;

        _speechText.text = line;
        _speech.gameObject.SetActive(true);
        _attention.gameObject.SetActive(false);
        _hideSpeechAt = Time.time + SpeechSeconds;
    }

    // 같은 말을 연달아 하지 않게
    private string PickLine()
    {
        int count = _otter.Lines.Count;
        int index = Random.Range(0, count);
        if (count > 1 && index == _lastLine)
            index = (index + 1) % count;
        _lastLine = index;
        return _otter.Lines[index];
    }
}
