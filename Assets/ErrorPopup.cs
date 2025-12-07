using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ErrorPopup : MonoBehaviour
{
    public static ErrorPopup Instance;

    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        Instance = this;

        if (panel != null)
            panel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    public void Show(string message)
    {
        if (messageText != null)
            messageText.text = message;

        if (panel != null)
            panel.SetActive(true);
    }
}