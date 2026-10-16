using UnityEngine;

/// <summary>무엇을 여는 시설인지</summary>
public enum FacilityKind
{
    Milestone, // 접수소: 큰 부탁 화면
    TownHall,  // 마을회관: 발전 현황 화면
}

/// <summary>
/// 광장의 마을 시설 (접수소, 마을회관). 탭하면 큰 부탁 화면이나 발전 현황 화면을 연다.
/// 보일지는 DevelopmentGate가 정하고(접수소 → 마을회관은 같은 부지), 여기는 탭만 받는다.
/// </summary>
public class SettlementFacilityPropView : MonoBehaviour
{
    [SerializeField] private FacilityKind _kind;

    [Tooltip("접수소가 여는 큰 부탁")]
    [SerializeField] private MilestoneGroupDefinition _group;

    [Tooltip("탭을 받을 영역 (건물)")]
    [SerializeField] private Collider2D _tapArea;

    private void Update()
    {
        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded || !PlazaTapInput.TryGetTap(out Vector2 world) || !_tapArea.OverlapPoint(world))
            return;

        if (_kind == FacilityKind.TownHall)
            manager.RequestTownHall();
        else if (_group != null)
            manager.RequestMilestone(_group);
    }
}
