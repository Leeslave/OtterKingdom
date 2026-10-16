using UnityEngine;
using UnityEngine.UI;

/// <summary>10회 뽑기 뗏목의 해달 한 마리 (배 위 조개, 빛, 튀어나온 장난감). 움직임은 GachaRevealView가 맡음</summary>
public class GachaRaftSlotView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("흔들리는 몸 (해달 + 조개)")]
    [SerializeField] private RectTransform _body;

    [SerializeField] private Image _otter;

    [Tooltip("조개 뒤 빛 (등급 색)")]
    [SerializeField] private Image _glow;

    [SerializeField] private Image _shell;

    [Tooltip("열린 뒤 해달 위로 튀어나오는 장난감")]
    [SerializeField] private Image _toy;

    [SerializeField] private GameObject _newBadge;

    public RectTransform Body => _body;
    public Image Otter => _otter;
    public Image Glow => _glow;
    public Image Shell => _shell;
    public Image Toy => _toy;
    public GameObject NewBadge => _newBadge;

    /// <summary>흔들림 위상 (마리마다 다르게)</summary>
    public float Phase { get; set; }

    /// <summary>몸이 흔들리는 기준 위치</summary>
    public Vector2 BodyRest { get; set; }
}
