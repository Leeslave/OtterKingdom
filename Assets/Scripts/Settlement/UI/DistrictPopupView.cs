using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>열린 땅의 용도 선택. 잠긴 용도도 이유와 함께 보여 준다.</summary>
public class DistrictPopupView : MonoBehaviour
{
    [SerializeField] private UIPopupAnimator _animator;
    [SerializeField] private TextMeshProUGUI _blockName;
    [SerializeField] private TextMeshProUGUI _note;
    [SerializeField] private Button[] _choices;
    [SerializeField] private TextMeshProUGUI[] _titles;
    [SerializeField] private TextMeshProUGUI[] _reasons;
    [SerializeField] private Button _previous;
    [SerializeField] private Button _next;
    [SerializeField] private Button _close;
    public bool IsOpen => _animator != null && _animator.IsOpen;
    public RectTransform ResidentialButton => (RectTransform)_choices[0].transform;
    public event Action<DistrictType> OnChosen;
    public event Action<int> OnBrowse;

    private void Awake()
    {
        for (int i = 0; i < _choices.Length; i++)
        {
            var type = Districts.Assignable[i];
            _choices[i].onClick.AddListener(() => OnChosen?.Invoke(type));
        }
        _previous.onClick.AddListener(() => OnBrowse?.Invoke(-1));
        _next.onClick.AddListener(() => OnBrowse?.Invoke(1));
        _close.onClick.AddListener(Hide);
    }

    public void Show(SettlementManager manager, DistrictBlockDefinition block, int index, int count)
    {
        _blockName.text = $"{block.DisplayName}  ({index + 1} / {count})";
        bool any = manager.DistrictChoices(block).Count > 0;
        var current = manager.Settlement.GetDistrict(block.BlockId);
        _note.text = current != DistrictType.None
            ? $"현재 용도: {Districts.DisplayName(current)}\n다른 용도를 눌러 바꿀 수 있어요. 건물은 그대로 남아요."
            : any ? "용도를 정하면 이 땅이 왕국의 구역이 돼요.\n용도는 나중에 다시 바꿀 수 있어요."
            : "아직 쓸 곳이 없어요.\n아래 안내에 따라 필요한 시설을 열어 주세요.";
        for (int i = 0; i < _choices.Length; i++)
        {
            var type = Districts.Assignable[i];
            var reason = DistrictRules.CheckDesignate(manager.Config, manager.Settlement, block, type);
            bool first = current == DistrictType.None && (block.BlockId == DistrictRules.CenterBlockId || DistrictRules.DesignatedCount(manager.Config, manager.Settlement) == 0);
            bool recommended = first ? type == manager.Config.FirstDistrict : type == block.SuggestedType;
            _titles[i].text = Districts.DisplayName(type) + (current == type ? " · 현재" : recommended ? " · 추천" : "");
            _reasons[i].text = DistrictGuideRules.LockedReason(manager.Config, type, reason);
            _choices[i].interactable = reason == DistrictDesignateBlock.None;
        }
        _previous.gameObject.SetActive(count > 1);
        _next.gameObject.SetActive(count > 1);
        if (!IsOpen) _animator.Show();
    }

    public void Hide() => _animator.Hide();
}
