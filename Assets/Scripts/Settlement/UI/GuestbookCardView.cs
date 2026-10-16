using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방명록 카드 하나: 해달 사진, 표시(첫 방문 등), 남긴 말, 이름. 받은 기록만 그린다.
/// </summary>
public class GuestbookCardView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Image _portrait;
    [SerializeField] private TextMeshProUGUI _tagText;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private TextMeshProUGUI _nameText;

    public void Bind(GuestbookEntryDefinition entry)
    {
        var otter = entry.Otter;
        _portrait.sprite = otter != null ? otter.Portrait : null;
        _portrait.enabled = _portrait.sprite != null;
        _tagText.text = entry.Tag;
        _messageText.text = entry.Message;
        _nameText.text = otter != null ? otter.DisplayName : string.Empty;
    }
}
