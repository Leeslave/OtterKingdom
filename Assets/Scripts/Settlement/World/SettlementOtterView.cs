using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 광장의 정착 해달 한 마리에 붙는 말풍선·표시 (SettlementPlazaView가 내보낼 때 붙임).
/// - 탭하면 한마디 (정착 후보는 처음 누르면 "저도 여기 살고 싶어요!" → 그 해달의 집 부탁이 열림)
/// - 광장에 와 있는 전문 해달(광부·농부)은 탭하면 "일하고 싶어요" + [배치] 확인 → 일할 곳에 배치
/// - 만난 관리 해달은 탭하면 "게시판 일을 도와도 될까요?" + [게시판 관리 맡기기] 확인 → 근무 자리로 감.
///   역할을 맡은 뒤에는 탭하면 한마디 + 게시판의 부탁 탭이 열림
/// - 새 집이 생겨 찾아온 이웃(P3)은 탭하면 "저 집에서 살아도 될까요?" + [입주] 확인 → 그 집에 입주
/// - 할 말이 있는 정착 후보·배치를 기다리는 전문 해달·역할을 기다리는 관리 해달은 머리 위에 "!"
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

        // 광산·밭을 치우고 정비하는 동안은 말을 걸지 않음 (장애물을 톡톡 누르다 해달이 말하지 않게)
        if (ZoneClearingView.IsWaiting)
        {
            if (_attention.gameObject.activeSelf)
                _attention.gameObject.SetActive(false);
            return;
        }

        bool attention = !speaking && (manager.HasPendingIntro(_otter) || manager.CanAssignSpecialist(_otter) || manager.CanAssignRole(_otter)
            || manager.CanMoveIn(_otter));
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
        var assignedRole = manager.AssignedRoleOf(_otter);
        if (manager.CanMoveIn(_otter))
        {
            line = string.IsNullOrEmpty(_otter.MoveInLine) ? PickLine() : _otter.MoveInLine;
            AskToMoveIn(manager);
        }
        else if (manager.CanAssignSpecialist(_otter))
        {
            line = string.IsNullOrEmpty(_otter.AssignLine) ? PickLine() : _otter.AssignLine;
            AskToAssign(manager);
        }
        else if (manager.CanAssignRole(_otter))
        {
            var role = manager.FindRole(_otter);
            line = string.IsNullOrEmpty(role.AskLine) ? PickLine() : role.AskLine;
            AskToAssignRole(manager, role);
        }
        else if (assignedRole != null)
        {
            // 근무 중인 관리 해달: 한마디 하고 게시판의 부탁 탭을 엶
            line = PickLine(assignedRole.WorkingLines);
            manager.RequestBoard(true);
        }
        else if (manager.TryHearIntro(_otter))
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

    // 전문 해달: [예]를 누르면 배치 (배치가 저장의 원본. 여러 번 눌러도 한 번만 됨)
    private void AskToAssign(SettlementManager manager)
    {
        var game = GameManager.Instance;
        if (game == null)
        {
            manager.TryAssignSpecialist(_otter);
            return;
        }
        string name = _otter.DisplayName;
        string place = _otter.WorkRegion.DisplayName;
        game.ShowConfirm($"{place}에 배치",
            $"{name}{KoreanParticle.ObjectParticle(name)} {place}에 배치할까요?\n배치하면 {place}에서만 일해요.",
            () => manager.TryAssignSpecialist(_otter));
    }

    // 관리 해달: [예]를 누르면 역할을 맡김 (역할 기록이 저장의 원본. 여러 번 눌러도 한 번만 됨)
    private void AskToAssignRole(SettlementManager manager, ManagementRoleDefinition role)
    {
        var game = GameManager.Instance;
        if (game == null)
        {
            manager.TryAssignRole(role);
            return;
        }
        string name = _otter.DisplayName;
        string roleName = role.DisplayName;
        string title = string.IsNullOrEmpty(role.ConfirmLabel) ? $"{roleName} 맡기기" : role.ConfirmLabel;
        game.ShowConfirm(title,
            $"{name}에게 {roleName}{KoreanParticle.ObjectParticle(roleName)} 맡길까요?\n맡기면 게시판 옆에서 일해요.",
            () => manager.TryAssignRole(role));
    }

    // 새 이웃: [예]를 누르면 그 집에 입주 (집의 입주민 기록이 원본. 여러 번 눌러도 한 번만 됨)
    private void AskToMoveIn(SettlementManager manager)
    {
        var game = GameManager.Instance;
        if (game == null)
        {
            manager.TryMoveIn(_otter);
            return;
        }
        string name = _otter.DisplayName;
        game.ShowConfirm("새 이웃의 입주",
            $"{name}{KoreanParticle.SubjectParticle(name)} 새 집에 입주할까요?\n입주하면 마을 주민이 되어 함께 일해요.",
            () => manager.TryMoveIn(_otter));
    }

    // 같은 말을 연달아 하지 않게
    private string PickLine() => PickLine(_otter.Lines);

    private string PickLine(IReadOnlyList<string> lines)
    {
        int count = lines.Count;
        if (count == 0)
            return PickFallbackLine();
        int index = Random.Range(0, count);
        if (count > 1 && index == _lastLine)
            index = (index + 1) % count;
        _lastLine = index;
        return lines[index];
    }

    private string PickFallbackLine() => _otter.Lines.Count > 0 ? _otter.Lines[0] : string.Empty;
}
