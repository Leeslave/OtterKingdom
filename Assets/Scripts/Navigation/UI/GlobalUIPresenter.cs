using UnityEngine;

/// <summary>
/// 전역 UI 연결: 네비게이션 바 ↔ 도감 / 퀘스트 / 꾸미기 모드 / 가방 화면 / 이동 팝업 ↔ SceneNavigator.
/// 어떤 화면이 열려 있는지(바 버튼 선택 표시)는 여기 한 곳에서만 관리한다.
/// </summary>
public class GlobalUIPresenter : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private GlobalUIRoot _root;
    [SerializeField] private SceneNavigator _navigator;
    [SerializeField] private NavBarView _navBar;
    [SerializeField] private TravelPopupView _travelPopup;

    [Header("도감")]
    [SerializeField] private CollectionPresenter _collection;
    [Tooltip("도감 화면 루트의 연출 (닫힘을 알아채 바 선택 표시를 끄기 위함)")]
    [SerializeField] private UIPopupAnimator _collectionScreen;

    [Header("퀘스트")]
    [SerializeField] private QuestPresenter _quest;
    [Tooltip("퀘스트 화면 루트의 연출 (닫힘을 알아채 바 선택 표시를 끄기 위함)")]
    [SerializeField] private UIPopupAnimator _questScreen;

    [Header("꾸미기")]
    [SerializeField] private DecorModePresenter _decorMode;

    [Header("가방")]
    [SerializeField] private InventoryPresenter _inventory;
    [Tooltip("가방 화면 루트의 연출 (닫힘을 알아채 바 선택 표시를 끄기 위함)")]
    [SerializeField] private UIPopupAnimator _inventoryScreen;

    private void OnEnable()
    {
        _navBar.OnCodexClicked += OpenCollection;
        _navBar.OnQuestClicked += OpenQuest;
        _navBar.OnDecorateClicked += OpenDecorMode;
        _navBar.OnBagClicked += OpenBag;
        _navBar.OnTravelClicked += ToggleTravel;
        _travelPopup.OnZoneSelected += HandleZoneSelected;
        _travelPopup.OnHidden += HandleTravelHidden;
        _inventoryScreen.OnHidden += HandleBagHidden;
        _collectionScreen.OnHidden += HandleCollectionHidden;
        _questScreen.OnHidden += HandleQuestHidden;
    }

    private void OnDisable()
    {
        _navBar.OnCodexClicked -= OpenCollection;
        _navBar.OnQuestClicked -= OpenQuest;
        _navBar.OnDecorateClicked -= OpenDecorMode;
        _navBar.OnBagClicked -= OpenBag;
        _navBar.OnTravelClicked -= ToggleTravel;
        _travelPopup.OnZoneSelected -= HandleZoneSelected;
        _travelPopup.OnHidden -= HandleTravelHidden;
        _inventoryScreen.OnHidden -= HandleBagHidden;
        _collectionScreen.OnHidden -= HandleCollectionHidden;
        _questScreen.OnHidden -= HandleQuestHidden;
    }

    #region 다른 화면에서 열기 (마을회관의 "모두 마쳤어요" 버튼)

    /// <summary>이동 팝업을 엶 (생산하러 밭·광산으로)</summary>
    public void OpenTravel()
    {
        if (!_travelPopup.IsOpen)
            ToggleTravel();
    }

    public void OpenDecor() => OpenDecorMode();

    public void OpenCodex() => OpenCollection();

    #endregion

    #region 도감

    private void OpenCollection()
    {
        if (_navigator.IsTraveling)
            return;

        _travelPopup.Hide();
        _navBar.SetCodexSelected(true);
        _collection.Open();
    }

    private void HandleCollectionHidden()
    {
        _navBar.SetCodexSelected(false);
    }

    #endregion

    #region 퀘스트

    private void OpenQuest()
    {
        if (_navigator.IsTraveling)
            return;

        _travelPopup.Hide();
        _navBar.SetQuestSelected(true);
        _quest.Open();
    }

    private void HandleQuestHidden()
    {
        _navBar.SetQuestSelected(false);
    }

    #endregion

    #region 꾸미기

    // 꾸미기 격자가 없는 씬(테스트 씬 등)에서는 아무 일도 하지 않음
    private void OpenDecorMode()
    {
        if (_navigator.IsTraveling || !_decorMode.CanEnter)
            return;

        _travelPopup.Hide();
        _decorMode.Enter();
    }

    #endregion

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
