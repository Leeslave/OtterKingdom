using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하단 네비게이션 바 (도감 · 퀘스트 · 꾸미기 · 가방 · 이동). 그리고 클릭을 알리기만 한다.
/// 꾸미기(가운데 큰 버튼)는 누르면 꾸미기 모드로 들어간다 (모드 동안 바는 숨겨지므로 선택 표시가 없음).
/// </summary>
public class NavBarView : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button _codexButton;
    [SerializeField] private Button _questButton;
    [SerializeField] private Button _decorateButton;
    [SerializeField] private Button _bagButton;
    [SerializeField] private Button _travelButton;

    [Header("선택 표시 (해당 화면이 열려 있는 동안 켜짐)")]
    [SerializeField] private GameObject _codexSelected;
    [SerializeField] private GameObject _questSelected;
    [SerializeField] private GameObject _bagSelected;
    [SerializeField] private GameObject _travelSelected;

    public event Action OnCodexClicked;
    public event Action OnQuestClicked;
    public event Action OnDecorateClicked;
    public event Action OnBagClicked;
    public event Action OnTravelClicked;

    private void Awake()
    {
        _codexButton.onClick.AddListener(() => OnCodexClicked?.Invoke());
        _questButton.onClick.AddListener(() => OnQuestClicked?.Invoke());
        _decorateButton.onClick.AddListener(() => OnDecorateClicked?.Invoke());
        _bagButton.onClick.AddListener(() => OnBagClicked?.Invoke());
        _travelButton.onClick.AddListener(() => OnTravelClicked?.Invoke());

        SetCodexSelected(false);
        SetQuestSelected(false);
        SetBagSelected(false);
        SetTravelSelected(false);
    }

    public void SetCodexSelected(bool selected) => _codexSelected.SetActive(selected);
    public void SetQuestSelected(bool selected) => _questSelected.SetActive(selected);
    public void SetBagSelected(bool selected) => _bagSelected.SetActive(selected);
    public void SetTravelSelected(bool selected) => _travelSelected.SetActive(selected);
}
