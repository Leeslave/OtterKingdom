using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도감 항목의 이야기(컷씬): 일러스트 컷 몇 장과 대사 몇 줄. 얼굴·이름표 없이 컷 위에 대사만 나온다.
/// 대사의 {이름}은 재생할 때 플레이어 이름으로 바뀐다.
/// </summary>
[CreateAssetMenu(fileName = "CollectionStory", menuName = "Game Data/Collection/Collection Story")]
public class CollectionStory : ScriptableObject
{
    /// <summary>대사에서 플레이어 이름으로 바뀌는 자리</summary>
    public const string NameToken = "{이름}";

    [Tooltip("일러스트 컷 (0번 = A컷). 칸이 비어 있으면 항목 그림을 크게 띄운 임시 컷")]
    [SerializeField]
    private List<Sprite> _cuts = new List<Sprite>();

    [Tooltip("대사 (위에서부터 순서대로). 한 줄은 대사창 두 줄 이내")]
    [SerializeField]
    private List<StoryLine> _lines = new List<StoryLine>();

    public IReadOnlyList<Sprite> Cuts => _cuts;
    public IReadOnlyList<StoryLine> Lines => _lines;

    /// <summary>대사의 {이름}을 플레이어 이름으로 바꾼다</summary>
    public static string FormatLine(string text, string playerName)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        return text.Replace(NameToken, playerName ?? "");
    }

    private void OnValidate()
    {
        if (_lines.Count == 0)
            Debug.LogWarning($"[{name}] 대사가 없습니다.", this);

        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (line == null)
                continue;
            if (string.IsNullOrWhiteSpace(line.Text))
                Debug.LogWarning($"[{name}] {i + 1}번째 대사가 비어 있습니다.", this);
            if (line.Cut < 0 || (_cuts.Count > 0 && line.Cut >= _cuts.Count))
                Debug.LogWarning($"[{name}] {i + 1}번째 대사의 컷 번호({line.Cut})가 컷 목록 밖입니다.", this);
        }
    }
}

/// <summary>대사 한 줄: 보일 컷 번호와 문장</summary>
[Serializable]
public class StoryLine
{
    [Tooltip("이 줄이 나올 때 보일 컷 번호 (0 = A컷)")]
    [SerializeField]
    private int _cut;

    [TextArea(2, 3)]
    [SerializeField]
    private string _text;

    public int Cut => _cut;
    public string Text => _text;
}
