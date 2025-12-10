using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MoveInputFieldOnClick : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform inputFieldRect;

    [Header("Move Options")]
    [SerializeField] private Vector2 anchoredPosition;
    [SerializeField] private RectTransform targetTransform;

    [Header("Activation")]
    [SerializeField] private bool autoActivate = true;
    [SerializeField] private bool selectAllOnActivate = false;
    [SerializeField] private bool returnAfterEdit = true;

    public void Move()
    {
        if (inputFieldRect == null) return;
        SaveOriginalIfNeeded();
        inputFieldRect.anchoredPosition = anchoredPosition;
        TryActivateInput();
    }

    public void MoveToTarget()
    {
        if (inputFieldRect == null || targetTransform == null) return;
        SaveOriginalIfNeeded();
        inputFieldRect.anchoredPosition = targetTransform.anchoredPosition;
        TryActivateInput();
    }

    private void TryActivateInput()
    {
        if (!autoActivate || inputFieldRect == null) return;

        var go = inputFieldRect.gameObject;
        if (go == null) return;

        // Set selected object on EventSystem so UI focus follows
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(go);
        }

        // Try TextMeshPro input first
        var tmp = go.GetComponent<TMP_InputField>();
        if (tmp != null)
        {
            tmp.ActivateInputField();
            if (selectAllOnActivate) tmp.Select();
            if (returnAfterEdit) SubscribeEndEdit(tmp);
            return;
        }

        // Fallback to Unity UI InputField
        var ui = go.GetComponent<InputField>();
        if (ui != null)
        {
            ui.ActivateInputField();
            if (selectAllOnActivate) ui.Select();
            if (returnAfterEdit) SubscribeEndEdit(ui);
        }
    }

    // --- return handling ---
    private Vector2 savedAnchoredPosition;
    private bool hasSavedPosition = false;

    private TMP_InputField cachedTMP;
    private InputField cachedUI;

    private void SaveOriginalIfNeeded()
    {
        if (inputFieldRect == null) return;
        if (!hasSavedPosition)
        {
            savedAnchoredPosition = inputFieldRect.anchoredPosition;
            hasSavedPosition = true;
        }
    }

    private void SubscribeEndEdit(TMP_InputField tmp)
    {
        if (tmp == null) return;
        cachedTMP = tmp;
        tmp.onEndEdit.AddListener(OnEndEditString);
    }

    private void SubscribeEndEdit(InputField ui)
    {
        if (ui == null) return;
        cachedUI = ui;
        ui.onEndEdit.AddListener(OnEndEditString);
    }

    private void UnsubscribeEndEdit()
    {
        if (cachedTMP != null)
        {
            cachedTMP.onEndEdit.RemoveListener(OnEndEditString);
            cachedTMP = null;
        }
        if (cachedUI != null)
        {
            cachedUI.onEndEdit.RemoveListener(OnEndEditString);
            cachedUI = null;
        }
    }

    private void OnEndEditString(string _)
    {
        ReturnToOriginal();
    }

    public void ReturnToOriginal()
    {
        if (!hasSavedPosition || inputFieldRect == null) return;
        inputFieldRect.anchoredPosition = savedAnchoredPosition;
        hasSavedPosition = false;
        // clear selection on next frame so we don't call SetSelectedGameObject
        // while EventSystem is already handling selection (avoids nested selection error)
        UnsubscribeEndEdit();
        if (EventSystem.current != null)
        {
            StartCoroutine(ClearSelectionNextFrame());
        }
    }

    private void OnDestroy()
    {
        UnsubscribeEndEdit();
    }

    private System.Collections.IEnumerator ClearSelectionNextFrame()
    {
        yield return null;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
