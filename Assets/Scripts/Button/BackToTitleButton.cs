using UnityEngine;
using UnityEngine.UI;

public class BackToTitleButton : MonoBehaviour
{
    [SerializeField] private Button button;

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClicked);
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClicked);
    }

    void OnClicked()
    {　
        var nm = FindObjectOfType<NetworkManager>();
        if (nm != null)
        {
            nm.ReturnToTitleAndDisconnect();
        }
        else
        {
            Debug.LogError("[BackToTitleButton] NetworkManager not found.");
        }
    }
}