using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 해달 명부 (가챠 개편 3단계, Docs/가챠개편_특수해달조각_장신구_기획.md 3 · 4장). 런타임에 그리는 화면 (RuntimeUIKit):
/// - 특수 해달: 조각 · 별 · 버프, [해금] · [별 올리기]
/// - 편성: 열린 칸(1~3)에 해금한 해달 넣기 · 빼기, 지금 받는 버프 합
/// - 장신구: 가진 장신구 · 강화 · 낀 해달, [끼우기] → 주민 해달 고르기 · [빼기]
/// 바꾸면 뽑기 매니저가 바로 저장. 뽑기 화면의 [명부] 버튼이 연다
/// </summary>
public class CompanionScreen : MonoBehaviour
{
    private const int SortingOrder = 240;
    private const float PanelWidth = 960f;

    private enum Tab { Otters, Formation, Accessories }

    private Canvas _canvas;
    private Tab _tab;
    private int _pickSlot = -1;
    private AccessoryDefinition _pickAccessory;

    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

    public void Open()
    {
        if (_canvas == null)
        {
            _canvas = RuntimeUIKit.CreateCanvas("해달 명부", SortingOrder, false);
            _canvas.transform.SetParent(transform, false);
        }
        _canvas.gameObject.SetActive(true);
        _pickSlot = -1;
        _pickAccessory = null;
        Draw();
    }

    public void Close()
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private static GachaManager Manager => GachaManager.Instance;
    private static CompanionCollection Companions => Manager.Companions;
    private static GachaRewardConfig Config => Manager.RewardConfig;

    private void Draw(string hint = "")
    {
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        PrepareGlyphs(hint);
        var panel = RuntimeUIKit.CreatePanel(RuntimeUIKit.CreateDim(_canvas.transform), PanelWidth);
        RuntimeUIKit.CreateLabel(panel, "해달 명부", 44, true);

        var tabs = RuntimeUIKit.CreateRow(panel, 96);
        TabButton(tabs, "특수 해달", Tab.Otters);
        TabButton(tabs, "편성", Tab.Formation);
        TabButton(tabs, "장신구", Tab.Accessories);

        if (Config == null)
            RuntimeUIKit.CreateLabel(panel, "아직 특수 해달을 만날 수 없어요", 28);
        else if (_tab == Tab.Otters)
            DrawOtters(panel);
        else if (_tab == Tab.Formation)
            DrawFormation(panel);
        else
            DrawAccessories(panel);

        if (!string.IsNullOrEmpty(hint))
            RuntimeUIKit.CreateLabel(panel, hint, 26);
        RuntimeUIKit.CreateButton(panel, "닫기", Close, RuntimeUIKit.ButtonKind.Secondary);
    }

    private void TabButton(Transform row, string text, Tab tab)
    {
        RuntimeUIKit.CreateButton(row, text, () =>
        {
            _tab = tab;
            _pickSlot = -1;
            _pickAccessory = null;
            Draw();
        }, _tab == tab ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Option, flexible: true);
    }

    #region 특수 해달

    private void DrawOtters(Transform panel)
    {
        var otters = SortedOtters();
        int unlocked = otters.FindAll(o => Companions.IsUnlocked(o)).Count;
        RuntimeUIKit.CreateLabel(panel, $"만난 특수 해달 {unlocked}/{otters.Count} · 조각은 뽑기와 교환소에서 모아요", 26);
        foreach (var otter in otters)
        {
            int stars = Companions.Stars(otter);
            int need = CompanionCollection.NextStarCost(otter, stars);
            string ready = Companions.CanStarUp(otter) ? (stars <= 0 ? "  <color=#E5484D>해금 가능!</color>" : "  <color=#E5484D>별 올리기 가능!</color>") : "";
            string title = $"<b>{otter.DisplayName}</b>  {GachaRewardText.TierName(GachaRewardKind.Fragments, otter.Tier)} · "
                + $"{(stars > 0 ? $"<color=#E5A21A>{new string('★', stars)}</color>" : "잠김")}{ready}";
            string bonus = GachaRewardText.Bonus(otter.Bonus, otter.PercentAt(Mathf.Max(1, stars)));
            string progress = need > 0 ? $"조각 {Companions.Fragments(otter)}/{need}" : "5성 완성!";
            var row = Row(panel, 130, Manager.IconOf(otter), $"{title}\n{bonus}{(stars > 0 ? "" : " (해금하면)")} · {progress}");
            string action = stars <= 0 ? "해금" : stars >= SpecialOtterDefinition.MaxStars ? "완성" : "별 올리기";
            var button = RuntimeUIKit.CreateButton(row, action, () =>
            {
                if (Manager.TryStarUp(otter))
                {
                    AudioManager.Play(SfxKind.RevealRare);
                    Draw(stars <= 0 ? $"{otter.DisplayName}{KoreanParticle.SubjectParticle(otter.DisplayName)} 명부에 들어왔어요! 편성에서 응원을 맡겨 보세요"
                        : $"{otter.DisplayName} {stars + 1}성! {GachaRewardText.Bonus(otter.Bonus, otter.PercentAt(stars + 1))}");
                }
                else
                    Draw("조각이 모자라요. 뽑기 · 교환소에서 모아요");
            }, Companions.CanStarUp(otter) ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Option, 200f);
            button.interactable = stars < SpecialOtterDefinition.MaxStars;
        }
    }

    private static List<SpecialOtterDefinition> SortedOtters()
    {
        var result = new List<SpecialOtterDefinition>();
        for (int tier = GachaTable.Epic; tier >= GachaTable.Rare; tier--)
        {
            foreach (var otter in Config.Otters)
            {
                if (otter != null && otter.Tier == tier)
                    result.Add(otter);
            }
        }
        return result;
    }

    #endregion

    #region 편성

    private void DrawFormation(Transform panel)
    {
        int open = Manager.FormationSlots;
        RuntimeUIKit.CreateLabel(panel, $"응원 칸 {open}/{CompanionCollection.MaxSlots} · 같은 버프는 한 마리만 맡을 수 있어요", 26);
        for (int slot = 0; slot < CompanionCollection.MaxSlots; slot++)
        {
            int s = slot;
            var otter = OtterIn(slot);
            string text = slot >= open ? $"칸 {slot + 1} · 잠김\n{SlotCondition(slot)}"
                : otter == null ? $"칸 {slot + 1} · 비어 있음"
                : $"칸 {slot + 1} · <b>{otter.DisplayName}</b> {Companions.Stars(otter)}성\n{GachaRewardText.Bonus(otter.Bonus, otter.PercentAt(Companions.Stars(otter)))}";
            var row = Row(panel, 120, otter != null ? Manager.IconOf(otter) : null, text);
            if (slot >= open)
                continue;
            RuntimeUIKit.CreateButton(row, otter == null ? "넣기" : "바꾸기", () => { _pickSlot = s; Draw(); },
                _pickSlot == slot ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Option, 170f);
            if (otter != null)
                RuntimeUIKit.CreateButton(row, "빼기", () => { Manager.ClearFormation(s); _pickSlot = -1; Draw(); }, RuntimeUIKit.ButtonKind.Secondary, 140f);
        }

        if (_pickSlot >= 0)
            DrawFormationPicker(panel);
        RuntimeUIKit.CreateLabel(panel, FormationSummary(), 26);
    }

    private void DrawFormationPicker(Transform panel)
    {
        var unlocked = SortedOtters().FindAll(o => Companions.IsUnlocked(o));
        if (unlocked.Count == 0)
        {
            RuntimeUIKit.CreateLabel(panel, "해금한 특수 해달이 없어요. 특수 해달 탭에서 조각으로 해금해요", 26);
            return;
        }
        RuntimeUIKit.CreateLabel(panel, $"칸 {_pickSlot + 1}에 누구를 넣을까요?", 28, true);
        RectTransform row = null;
        for (int i = 0; i < unlocked.Count; i++)
        {
            if (i % 2 == 0)
                row = RuntimeUIKit.CreateRow(panel, 96);
            var otter = unlocked[i];
            RuntimeUIKit.CreateButton(row, $"{otter.DisplayName} · {GachaRewardText.Bonus(otter.Bonus, otter.PercentAt(Companions.Stars(otter)))}", () =>
            {
                var outcome = Manager.AssignFormation(_pickSlot, otter);
                _pickSlot = -1;
                Draw(outcome == FormationOutcome.Assigned ? $"{otter.DisplayName}{KoreanParticle.SubjectParticle(otter.DisplayName)} 응원을 맡았어요!"
                    : outcome == FormationOutcome.SameBonus ? "같은 버프를 주는 해달이 이미 다른 칸에 있어요"
                    : "지금은 넣을 수 없어요");
            }, RuntimeUIKit.ButtonKind.Option, flexible: true);
        }
    }

    private static SpecialOtterDefinition OtterIn(int slot)
    {
        string id = Companions.Formation[slot];
        return string.IsNullOrEmpty(id) ? null : Config.FindOtter(id);
    }

    private static string SlotCondition(int slot) => slot == 1 ? "첫 개간을 하면 열려요" : "밭 농사를 시작하면 열려요";

    private static string FormationSummary()
    {
        var text = new StringBuilder("지금 받는 응원: ");
        bool any = false;
        for (int slot = 0; slot < Mathf.Min(CompanionCollection.MaxSlots, Manager.FormationSlots); slot++)
        {
            var otter = OtterIn(slot);
            if (otter == null)
                continue;
            text.Append(any ? " · " : "").Append(GachaRewardText.Bonus(otter.Bonus, otter.PercentAt(Companions.Stars(otter))));
            any = true;
        }
        return any ? text.ToString() : "아직 응원하는 해달이 없어요";
    }

    #endregion

    #region 장신구

    private void DrawAccessories(Transform panel)
    {
        var owned = new List<AccessoryDefinition>();
        for (int tier = GachaTable.Epic; tier >= GachaTable.Common; tier--)
        {
            foreach (var accessory in Config.Accessories)
            {
                if (accessory != null && accessory.Tier == tier && Companions.Owns(accessory))
                    owned.Add(accessory);
            }
        }
        if (owned.Count == 0)
        {
            RuntimeUIKit.CreateLabel(panel, "아직 장신구가 없어요. 뽑기에서 나와요", 28);
            return;
        }
        if (_pickAccessory != null)
        {
            AccessoryRow(panel, _pickAccessory);
            DrawWearerPicker(panel);
            return;
        }
        RuntimeUIKit.CreateLabel(panel, "주민 해달 한 마리에 하나씩. 낀 해달 한 마리만 효과를 받아요", 26);
        foreach (var accessory in owned)
            AccessoryRow(panel, accessory);
    }

    private void AccessoryRow(Transform panel, AccessoryDefinition accessory)
    {
        int plus = Companions.Plus(accessory);
        string wearer = Companions.Wearer(accessory);
        string text = $"<b>{accessory.DisplayName}</b>{(plus > 0 ? $" +{plus}" : "")}\n{GachaRewardText.Effect(accessory.Effect, accessory.PercentAt(plus))}"
            + $" · {(wearer != null ? $"{OtterName(wearer)}{KoreanParticle.SubjectParticle(OtterName(wearer))} 끼고 있어요" : "아무도 안 꼈어요")}";
        var row = Row(panel, 120, Config.IconOf(accessory), text);
        RuntimeUIKit.CreateButton(row, wearer != null ? "옮기기" : "끼우기", () => { _pickAccessory = accessory; Draw(); },
            _pickAccessory == accessory ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Option, 170f);
        if (wearer != null)
            RuntimeUIKit.CreateButton(row, "빼기", () =>
            {
                Manager.UnequipAccessory(accessory);
                _pickAccessory = null;
                Draw($"{accessory.DisplayName}{KoreanParticle.ObjectParticle(accessory.DisplayName)} 뺐어요");
            }, RuntimeUIKit.ButtonKind.Secondary, 140f);
    }

    private void DrawWearerPicker(Transform panel)
    {
        var settlement = SettlementManager.Instance;
        var residents = settlement != null ? settlement.SettledResidents() : new List<SettlementOtterDefinition>();
        if (residents.Count == 0)
        {
            RuntimeUIKit.CreateLabel(panel, "마을에 사는 주민 해달이 없어요", 26);
            return;
        }
        RuntimeUIKit.CreateLabel(panel, "누구에게 끼울까요?", 28, true);
        RectTransform row = null;
        for (int i = 0; i < residents.Count; i++)
        {
            if (i % 3 == 0)
                row = RuntimeUIKit.CreateRow(panel, 90);
            var otter = residents[i];
            string current = Companions.EquippedOn(otter.OtterId);
            string label = current != null && current != _pickAccessory.AccessoryId ? $"{otter.DisplayName} (바꾸기)" : otter.DisplayName;
            var accessory = _pickAccessory;
            RuntimeUIKit.CreateButton(row, label, () =>
            {
                Manager.EquipAccessory(accessory, otter.OtterId);
                _pickAccessory = null;
                Draw($"{otter.DisplayName}에게 {accessory.DisplayName}{KoreanParticle.ObjectParticle(accessory.DisplayName)} 끼웠어요!");
            }, otter.OtterId == Companions.Wearer(accessory) ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Option, flexible: true);
        }
        RuntimeUIKit.CreateButton(panel, "목록으로", () => { _pickAccessory = null; Draw(); }, RuntimeUIKit.ButtonKind.Secondary);
    }

    private static string OtterName(string otterId) =>
        SettlementManager.Instance != null ? SettlementManager.Instance.OtterName(otterId) : otterId;

    #endregion

    // 그림 + 왼쪽 정렬 글(남는 폭을 다 씀). 버튼은 부르는 쪽이 뒤에 붙임
    private static RectTransform Row(Transform panel, float height, Sprite icon, string text)
    {
        var row = RuntimeUIKit.CreateRow(panel, height);
        if (icon != null)
            RuntimeUIKit.CreateIcon(row, icon, height - 16f);
        var label = RuntimeUIKit.CreateLabel(row, text, 26, false, TextAlignmentOptions.Left);
        label.richText = true;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return row;
    }

    // 처음 보는 한글을 미리 폰트에 (목록을 그리는 도중 글자가 추가되면 D3D12 에디터가 멈춤)
    private static void PrepareGlyphs(string hint)
    {
        var text = new StringBuilder("해달명부특수편성장신구닫기해금별올리기완성잠김조각성모자라요뽑기교환소에서모아요명부에들어왔어요응원을맡겨보세요★가능만난은와")
            .Append("칸비어있음넣기바꾸기빼기같은버프는한마리만맡을수있어요첫개간을하면열려요밭농사를시작하면지금받는아직응원하는해달이없어요")
            .Append("누구를넣을까요해금한탭으로이미다른에있어요넣을주민하나씩낀효과를받아요끼우기옮기기아무도안꼈어요끼고뺐어요누구에게끼울까요")
            .Append("목록으로마을에사는끼웠어요레어에픽흔함이동속도작업시간얻는양작물성장목재돌판매가공사수확량물고기생산완성0123456789+−%·/():!")
            .Append(hint);
        var config = Config;
        if (config != null)
        {
            foreach (var otter in config.Otters)
            {
                if (otter != null)
                    text.Append(otter.DisplayName);
            }
            foreach (var accessory in config.Accessories)
            {
                if (accessory != null)
                    text.Append(accessory.DisplayName);
            }
        }
        var settlement = SettlementManager.Instance;
        if (settlement != null)
        {
            foreach (var otter in settlement.SettledResidents())
                text.Append(otter.DisplayName);
        }
        RuntimeUIKit.PrepareGlyphs(text.ToString());
    }
}
