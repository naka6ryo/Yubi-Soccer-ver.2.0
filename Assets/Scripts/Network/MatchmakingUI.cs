using UnityEngine;
using UnityEngine.UI;

// Simple UI glue: wires a Button to NetworkManager. Attach this to a Canvas GameObject.
public class MatchmakingUI : MonoBehaviour
{
    public NetworkManager networkManager;
    public Button quickMatchButton;

    void Start()
    {
        if (quickMatchButton != null)
        {
            quickMatchButton.onClick.AddListener(OnQuickMatchClicked);
        }
    }

    void OnQuickMatchClicked()
    {
        if (networkManager != null) networkManager.QuickMatch();
    }
}
