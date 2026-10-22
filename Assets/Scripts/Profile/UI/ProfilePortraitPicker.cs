using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단바 아바타를 누르면 뜨는 대표 해달 고르기. 정착한 해달 얼굴을 칸에 늘어놓고, 고른 뒤 [변경]으로 바꾼다.
/// 고를 수 있는 해달은 ProfilePortraitRules가 정한다.
/// </summary>
public class ProfilePortraitPicker : MonoBehaviour
{
    private const int Columns = 4;
    private const float CellSize = 170f;
    private const float FaceSize = 130f;
    private static readonly Color Selected = new Color(1f, 0.82f, 0.38f, 1f);

    private Canvas _canvas;
    private Sprite _frame;
    private string _selected;

    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

    /// <param name="frame">칸 테두리 (상단바 아바타와 같은 둥근 그림)</param>
    public void Open(Sprite frame)
    {
        var profile = ProfileManager.Instance;
        var settlement = SettlementManager.Instance;
        if (profile == null || settlement == null)
            return;
        _frame = frame;
        if (_canvas == null)
        {
            _canvas = RuntimeUIKit.CreateCanvas("대표 해달 고르기", 240, false);
            _canvas.transform.SetParent(transform, false);
        }
        _canvas.gameObject.SetActive(true);
        _selected = ProfilePortraitRules.Resolve(settlement.Config, profile.Profile.PortraitOtterId)?.OtterId;
        Draw();
    }

    public void Close()
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void Draw()
    {
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        var manager = SettlementManager.Instance;
        var choices = ProfilePortraitRules.Choices(manager.Config, manager.IsLoaded ? manager.Settlement : null);
        string current = ProfilePortraitRules.Resolve(manager.Config, ProfileManager.Instance.Profile.PortraitOtterId)?.OtterId;
        PrepareNames(choices);

        var panel = RuntimeUIKit.CreatePanel(RuntimeUIKit.CreateDim(_canvas.transform), 900);
        RuntimeUIKit.CreateLabel(panel, "대표 해달", RuntimeUIKit.TitleFontSize, true);
        RuntimeUIKit.CreateLabel(panel, "정착한 해달 중에서 골라요", 28);

        RectTransform row = null;
        for (int i = 0; i < choices.Count; i++)
        {
            if (i % Columns == 0)
                row = RuntimeUIKit.CreateRow(panel, CellSize + 44f, 22f);
            CreateCell(row, choices[i], choices[i].OtterId == current);
        }

        var buttons = RuntimeUIKit.CreateRow(panel, RuntimeUIKit.ButtonHeight);
        RuntimeUIKit.CreateButton(buttons, "닫기", Close, RuntimeUIKit.ButtonKind.Secondary, flexible: true);
        var apply = RuntimeUIKit.CreateButton(buttons, "변경", Apply, RuntimeUIKit.ButtonKind.Primary, flexible: true);
        apply.interactable = _selected != current;
    }

    private void CreateCell(RectTransform row, SettlementOtterDefinition otter, bool isCurrent)
    {
        var cell = new GameObject(otter.OtterId, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        cell.transform.SetParent(row, false);
        var layout = cell.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.spacing = 4f;
        cell.GetComponent<LayoutElement>().preferredWidth = CellSize;

        var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(Button));
        frameGo.transform.SetParent(cell.transform, false);
        ((RectTransform)frameGo.transform).sizeDelta = new Vector2(CellSize, CellSize);
        var frame = frameGo.GetComponent<Image>();
        frame.sprite = _frame;
        frame.color = otter.OtterId == _selected ? Selected : Color.white;
        var button = frameGo.GetComponent<Button>();
        button.targetGraphic = frame;
        button.onClick.AddListener(() =>
        {
            _selected = otter.OtterId;
            Draw();
        });

        var face = RuntimeUIKit.CreateIcon(frameGo.transform, otter.Portrait, FaceSize);
        var faceRect = (RectTransform)face.transform;
        faceRect.sizeDelta = new Vector2(FaceSize, FaceSize);
        faceRect.anchoredPosition = new Vector2(0f, 6f);

        if (isCurrent)
        {
            var tag = RuntimeUIKit.CreateLabel(frameGo.transform, "현재", 24, true);
            var tagRect = (RectTransform)tag.transform;
            tagRect.anchorMin = tagRect.anchorMax = new Vector2(0.5f, 0f);
            tagRect.sizeDelta = new Vector2(CellSize, 34f);
            tagRect.anchoredPosition = new Vector2(0f, 14f);
        }

        var name = RuntimeUIKit.CreateLabel(cell.transform, otter.DisplayName, 26);
        ((RectTransform)name.transform).sizeDelta = new Vector2(CellSize, 36f);
    }

    private void Apply()
    {
        if (!string.IsNullOrEmpty(_selected))
            ProfileManager.Instance.ChoosePortrait(_selected);
        Close();
    }

    private static void PrepareNames(List<SettlementOtterDefinition> choices)
    {
        var characters = new StringBuilder("대표해달정착한중에서골라요현재닫기변경");
        foreach (var otter in choices)
            characters.Append(otter.DisplayName);
        RuntimeUIKit.PrepareGlyphs(characters.ToString());
    }
}
