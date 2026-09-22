using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TestCurrencyUI : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private Currency _goldData;

    [Header("UI 요소")]
    [SerializeField] private TMP_InputField _inputField; 
    [SerializeField] private Button _submitButton;      

    private void Start()
    {
        if (_submitButton != null)
            _submitButton.onClick.AddListener(OnSubmitButtonClicked);
    }

    private void OnSubmitButtonClicked()
    {
        if (int.TryParse(_inputField.text, out int amount))
        {
            var tx = new CurrencyTransaction(_goldData, amount, TransactionSource.TestGet);
            CurrencyManager.Instance.ProcessTransaction(tx);

            _inputField.text = "";
            Debug.Log($"테스트: {amount} 골드 요청 완료!");
        }
        else
            Debug.LogWarning("입력한 값이 숫자가 아닙니다!");
    }
}