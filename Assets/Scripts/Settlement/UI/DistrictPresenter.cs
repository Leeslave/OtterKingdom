using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>구역 표지와 회관 카드에서 같은 용도 선택 화면을 연다. 확인 순간에 규칙을 다시 검사한다.</summary>
public class DistrictPresenter : MonoBehaviour
{
    [SerializeField] private DistrictPopupView _popup;
    private SettlementManager _manager;
    private DistrictBlockDefinition _block;
    public DistrictPopupView Popup => _popup;

    private void Start()
    {
        _manager = SettlementManager.Instance;
        if (_manager == null) return;
        PrepareGlyphs();
        _manager.OnDistrictRequested += Open;
        _manager.OnChanged += Refresh;
        _popup.OnBrowse += Browse;
        _popup.OnChosen += Choose;
        SceneNavigator.BeforeLeave += Hide;
    }

    private void OnDestroy()
    {
        if (_manager != null)
        {
            _manager.OnDistrictRequested -= Open;
            _manager.OnChanged -= Refresh;
        }
        if (_popup != null)
        {
            _popup.OnBrowse -= Browse;
            _popup.OnChosen -= Choose;
        }
        SceneNavigator.BeforeLeave -= Hide;
    }

    private void Hide() { if (_popup.IsOpen) _popup.Hide(); }

    private void PrepareGlyphs()
    {
        var characters = new StringBuilder("0123456789 /()·!?이 땅을 뭘로 쓸까요?추천아직 쓸 곳이 없어요.아래 안내에 따라 필요한 시설을 열어 주세요.용도를 정하면 이 땅이 왕국의 구역이 돼요.용도는 나중에 다시 바꿀 수 있어요.현재 용도다른 용도를 눌러 바꿀 수 있어요.건물은 그대로 남아요.");
        foreach (var block in _manager.DistrictBlocks)
            if (block != null) characters.Append(block.DisplayName);
        foreach (var type in Districts.Assignable)
        {
            characters.Append(Districts.DisplayName(type));
            foreach (DistrictDesignateBlock reason in System.Enum.GetValues(typeof(DistrictDesignateBlock)))
                characters.Append(DistrictGuideRules.LockedReason(_manager.Config, type, reason));
        }
        var fonts = new HashSet<TMP_FontAsset>();
        foreach (var label in _popup.GetComponentsInChildren<TMP_Text>(true)) fonts.Add(label.font);
        foreach (var font in fonts)
            if (font != null) font.TryAddCharacters(characters.ToString(), out _);
    }

    private void Open(DistrictBlockDefinition block)
    {
        if (!_manager.IsAdministrator) return;
        var pending = _manager.EditableDistrictBlocks();
        if (pending.Count == 0) return;
        var first = DistrictGuideRules.FirstDistrictBlock(_manager.Config, _manager.Settlement);
        _block = pending.Contains(block) ? block : first != null && pending.Contains(first) ? first : pending[0];
        Draw(pending);
    }

    private void Refresh()
    {
        if (!_popup.IsOpen) return;
        var pending = _manager.EditableDistrictBlocks();
        if (pending.Count == 0) { Hide(); return; }
        if (!pending.Contains(_block)) _block = pending[0];
        Draw(pending);
    }

    private void Draw(List<DistrictBlockDefinition> pending) => _popup.Show(_manager, _block, pending.IndexOf(_block), pending.Count);

    private void Browse(int delta)
    {
        var pending = _manager.EditableDistrictBlocks();
        if (pending.Count == 0) { Hide(); return; }
        int index = Mathf.Max(0, pending.IndexOf(_block));
        _block = pending[(index + delta + pending.Count) % pending.Count];
        Draw(pending);
    }

    private void Choose(DistrictType type)
    {
        var target = _block;
        var game = GameManager.Instance;
        if (game == null || target == null) return;
        game.ShowConfirm("구역 정하기", $"{target.DisplayName}{KoreanParticle.ObjectParticle(target.DisplayName)} {Districts.DisplayName(type)}으로 정할까요?\n기존 건물은 그대로 두고 용도만 바꿔요.", () =>
        {
            var result = _manager.TryDesignateDistrict(target, type);
            if (result != DistrictDesignateBlock.None)
            {
                game.ShowAlert(DistrictGuideRules.LockedReason(_manager.Config, type, result));
                Refresh();
                return;
            }
            Hide();
            game.ShowGuide($"{target.DisplayName}{KoreanParticle.SubjectParticle(target.DisplayName)} {Districts.DisplayName(type)}이 되었어요!\n왕국 질서도 {_manager.DistrictOrder.Score} / 100");
        });
    }
}
