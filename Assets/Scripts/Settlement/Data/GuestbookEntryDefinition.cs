using UnityEngine;

/// <summary>
/// 방명록 기록 하나. 세이브에는 문장 대신 EntryId만 남는다.
/// </summary>
[CreateAssetMenu(fileName = "GuestbookEntry", menuName = "Game Data/Settlement/Guestbook Entry")]
public class GuestbookEntryDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 저장되는 ID (예: gb_first_arrival)")]
    [SerializeField] private string _entryId;

    [Header("내용")]
    [Tooltip("기록을 남긴 해달 (얼굴 그림)")]
    [SerializeField] private SettlementOtterDefinition _otter;

    [Tooltip("카드 위 작은 표시 (예: 첫 방문, 정착)")]
    [SerializeField] private string _tag;

    [Tooltip("해달이 남긴 말")]
    [TextArea]
    [SerializeField] private string _message;

    public string EntryId => _entryId;
    public SettlementOtterDefinition Otter => _otter;
    public string Tag => _tag;
    public string Message => _message;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_entryId))
            Debug.LogWarning($"[{name}] EntryId가 비어 있습니다.", this);
    }
}
