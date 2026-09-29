using UnityEngine;

/// <summary>
/// 전역 UI 연결: 네비게이션 바 ↔ 가방 화면 / 이동 팝업 ↔ SceneNavigator.
/// 어떤 화면이 열려 있는지(바 버튼 선택 표시)는 여기 한 곳에서만 관리한다.
/// </summary>
public class GlobalUIPresenter : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private GlobalUIRoot _root;
    [SerializeField] private SceneNavigator _navigator;
    [SerializeField] private NavBarView _navBar;
    [SerializeField] private TravelPopupView _travelPopup;

    [Header("가방")]
    [SerializeField] private InventoryPresenter _inventory;
    [Tooltip("가방 화면 루트의 연출 (닫힘을 알아채 바 선택 표시를 끄기 위함)")]
    [SerializeField] private UIPopupAnimator _inventoryScreen;

    private void OnEnable()
    {
        _navBar.OnBagClicked += OpenBag;
        _navBar.OnTravelClicked += ToggleTravel;
        _travelPopup.OnZoneSelected += HandleZoneSelected;
        _travelPopup.OnHidden += HandleTravelHidden;
        _inventoryScreen.OnHidden += HandleBagHidden;
    }

    private void OnDisable()
    {
        _navBar.OnBagClicked -= OpenBag;
        _navBar.OnTravelClicked -= ToggleTravel;
        _travelPopup.OnZoneSelected -= HandleZoneSelected;
        _travelPopup.OnHidden -= HandleTravelHidden;
        _inventoryScreen.OnHidden -= HandleBagHidden;
    }

    #region 가방

    private void OpenBag()
    {
        if (_navigator.IsTraveling)
            return;

        _travelPopup.Hide();
        _navBar.SetBagSelected(true);
        _inventory.Open();
    }

    private void HandleBagHidden()
    {
        _navBar.SetBagSelected(false);
    }

    #endregion

    #region 이동

    private void ToggleTravel()
    {
        if (_navigator.IsTraveling)
            return;

        if (_travelPopup.IsOpen)
        {
            _travelPopup.Hide();
            return;
        }

        _navBar.SetTravelSelected(true);
        _travelPopup.Show(ZoneLookup.Sorted(_root.Zones), _navigator.CurrentZone);
    }

    private void HandleTravelHidden()
    {
        _navBar.SetTravelSelected(false);
    }

    private void HandleZoneSelected(ZoneDefinition zone)
    {
        // 현재 위치를 다시 누르면 그냥 닫기
        if (zone == _navigator.CurrentZone)
        {
            _travelPopup.Hide();
            return;
        }

        if (_navigator.TryGo(zone))
            _travelPopup.Hide();
    }

    #endregion
}
