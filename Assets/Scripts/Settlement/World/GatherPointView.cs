using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 광장에서 주울 수 있는 재료 한 무더기 (나뭇가지 → 목재, 돌무더기 → 돌).
/// 탭하면 줍고 사라졌다가 시간이 지나면 다시 생긴다 (세이브에 남음). 줍는 순간 "+2 목재"가 떠올랐다 사라진다.
/// 아이템·개수·시간을 비우면 SettlementConfig의 기본값(나뭇가지)을 쓴다.
/// </summary>
public class GatherPointView : MonoBehaviour, IDecorBlocker
{
    private const float PopupSeconds = 0.9f;
    private const float PopupRise = 0.8f;

    [Header("식별")]
    [Tooltip("세이브에 저장되는 자리 ID (광장 안에서 겹치지 않게)")]
    [SerializeField] private string _pointId;

    [Header("주는 것 (비우면 설정의 기본값)")]
    [Tooltip("주는 아이템 (비우면 목재)")]
    [SerializeField] private ItemDefinition _item;

    [Tooltip("한 번에 주는 개수 (0이면 기본값)")]
    [SerializeField] private int _amount;

    [Tooltip("다시 생기기까지 (초, 0이면 기본값)")]
    [SerializeField] private float _cooldownSeconds;

    [Header("구성 요소")]
    [Tooltip("나뭇가지 그림 (주우면 숨김)")]
    [SerializeField] private GameObject _visual;

    [Tooltip("탭을 받을 영역")]
    [SerializeField] private Collider2D _tapArea;

    [Tooltip("\"+2 목재\" 글자 (평소엔 꺼 둠)")]
    [SerializeField] private TextMeshPro _popup;

    public string PointId => _pointId;

    private Vector3 _popupBase;
    private Coroutine _popupRoutine;

    private void Awake()
    {
        _popupBase = _popup.transform.localPosition;
        _popup.gameObject.SetActive(false);
    }

    private void Update()
    {
        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded)
            return;

        bool ready = manager.IsGatherReady(_pointId);
        if (_visual.activeSelf != ready)
            _visual.SetActive(ready);

        if (ready && PlazaTapInput.TryGetTap(out Vector2 world) && _tapArea.OverlapPoint(world))
            Gather(manager);
    }

    private void Gather(SettlementManager manager)
    {
        var config = manager.Config;
        var item = _item != null ? _item : config.GatherItem;
        int amount = _amount > 0 ? _amount : config.GatherAmount;
        float cooldown = _cooldownSeconds > 0f ? _cooldownSeconds : config.GatherCooldownSeconds;

        int added = manager.TryGather(_pointId, item, amount, cooldown);
        if (added <= 0)
        {
            ShowPopup("가방이 꽉 찼어요");
            return;
        }

        _visual.SetActive(false);
        ShowPopup($"+{added} {item.DisplayName}");
        RewardFly.FromWorld(item.Icon, transform.position + Vector3.up * 0.4f, RewardTarget.Bag, added);
    }

    private void ShowPopup(string text)
    {
        if (_popupRoutine != null)
            StopCoroutine(_popupRoutine);
        _popupRoutine = StartCoroutine(Popup(text));
    }

    private IEnumerator Popup(string text)
    {
        _popup.text = text;
        _popup.gameObject.SetActive(true);
        var color = _popup.color;
        for (float t = 0f; t < PopupSeconds; t += Time.deltaTime)
        {
            float k = t / PopupSeconds;
            _popup.transform.localPosition = _popupBase + Vector3.up * (PopupRise * k);
            color.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            _popup.color = color;
            yield return null;
        }
        color.a = 1f;
        _popup.color = color;
        _popup.gameObject.SetActive(false);
        _popupRoutine = null;
    }

    // 꾸미기: 이 자리(탭 영역)에는 물건·건물을 놓지 못하게. 주워서 그림이 꺼진 뒤에도 같은 자리를 비워 두도록 처음 범위를 기억
    private Rect? _decorBlock;

    public bool TryGetDecorBlock(out Rect worldRect)
    {
        if (_decorBlock == null && _tapArea != null && _tapArea.isActiveAndEnabled)
        {
            var bounds = _tapArea.bounds;
            if (bounds.size.x > 0f && bounds.size.y > 0f)
                _decorBlock = new Rect(bounds.min, bounds.size);
        }
        worldRect = _decorBlock ?? default;
        return _decorBlock != null;
    }
}
