using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하단 네비게이션 바 (도감 · 퀘스트 · 꾸미기 · 가방 · 이동). 그리고 클릭을 알리기만 한다.
/// 도감·퀘스트·꾸미기는 기능이 생기면 버튼과 이벤트를 연결한다 (지금은 눌러도 반응 없음).
/// </summary>
public class NavBarView : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button _bagButton;
    [SerializeField] private Button _travelButton;

    [Header("선택 표시 (해당 화면이 열려 있는 동안 켜짐)")]
    [SerializeField] private GameObject _bagSelected;
    [SerializeField] private GameObject _travelSelected;

    public event Action OnBagClicked;
    public event Action OnTravelClicked;

    private void Awake()
    {
        _bagButton.onClick.AddListener(() => OnBagClicked?.Invoke());
        _travelButton.onClick.AddListener(() => OnTravelClicked?.Invoke());

        SetBagSelected(false);
        SetTravelSelected(false);
    }

    public void SetBagSelected(bool selected) => _bagSelected.SetActive(selected);
    public void SetTravelSelected(bool selected) => _travelSelected.SetActive(selected);
}
