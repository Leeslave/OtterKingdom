using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 켜기/끄기 스위치 (초록 알약 + 좌우로 움직이는 동그라미). 누르면 알리기만 하고, 상태는 SetOn으로 받는다.
/// </summary>
public class ToggleSwitchView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [Tooltip("좌우로 움직이는 동그라미 (가운데 기준 앵커)")]
    [SerializeField] private RectTransform _knob;

    [Header("모양")]
    [SerializeField] private Sprite _onSprite;
    [SerializeField] private Sprite _offSprite;
    [Tooltip("동그라미 가운데의 x 위치 (켜짐 / 꺼짐)")]
    [SerializeField] private float _knobOnX = 36f;
    [SerializeField] private float _knobOffX = -36f;

    public bool IsOn { get; private set; }

    /// <summary>눌렀을 때 바뀔 값과 함께</summary>
    public event Action<bool> OnToggled;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnToggled?.Invoke(!IsOn));
    }

    public void SetOn(bool on)
    {
        IsOn = on;
        _background.sprite = on ? _onSprite : _offSprite;
        _knob.anchoredPosition = new Vector2(on ? _knobOnX : _knobOffX, _knob.anchoredPosition.y);
    }
}
