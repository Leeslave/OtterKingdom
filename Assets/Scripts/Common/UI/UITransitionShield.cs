using UnityEngine;
using UnityEngine.UI;

/// <summary>창을 닫은 직후의 연타가 뒤 창이나 재화 버튼에 들어가지 않게 한다.</summary>
public class UITransitionShield : MonoBehaviour
{
    private static UITransitionShield _instance;
    private float _until;

    public static bool IsBlocking => _instance != null && Time.unscaledTime < _instance._until;

    public static void Block(float seconds = 0.35f)
    {
        PlazaTapInput.BlockCurrentGesture();
        if (_instance == null)
        {
            var canvas = RuntimeUIKit.CreateCanvas(nameof(UITransitionShield), 32760, true);
            var image = RuntimeUIKit.CreateDim(canvas.transform).GetComponent<Image>();
            image.color = Color.clear;
            _instance = canvas.gameObject.AddComponent<UITransitionShield>();
        }
        _instance._until = Mathf.Max(_instance._until, Time.unscaledTime + seconds);
        _instance.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (Time.unscaledTime >= _until) gameObject.SetActive(false);
    }

    private void OnDestroy() { if (_instance == this) _instance = null; }
}
