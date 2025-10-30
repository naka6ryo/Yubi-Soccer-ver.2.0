using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Simple UI glue: wires a Button to NetworkManager. Attach this to a Canvas GameObject.
public class MatchmakingUI : MonoBehaviour
{
    public NetworkManager networkManager;
    public Button quickMatchButton;
    public TMP_Text statusText;

    void Start()
    {
        if (quickMatchButton != null)
        {
            quickMatchButton.onClick.AddListener(OnQuickMatchClicked);
        }

        if (networkManager != null && statusText != null)
        {
            // Allow NetworkManager to update the same status text
            networkManager.statusText = statusText;
        }
    }

    void OnQuickMatchClicked()
    {
        if (networkManager != null) networkManager.QuickMatch();
    }
}
