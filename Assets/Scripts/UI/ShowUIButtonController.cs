using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace YubiSoccer.UI
{
    /// <summary>
    /// トリガーボタンを押したときに指定した UI ルートと内部のボタンを表示する簡易コントローラ。
    /// 使用方法:
    ///  - このスクリプトを空の GameObject にアタッチする。
    ///  - `triggerButton` に押下トリガーとなる Button を割り当てる。
    ///  - `uiRoot` に表示したい UI のルート GameObject を割り当てる。
    ///  - `uiButton` に UI 内で表示したい Button を割り当てる（任意）。
    ///  - `startHidden` が true の場合、Awake で UI を非表示にします。
    /// </summary>
    public class ShowUIButtonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button triggerButton;
        [SerializeField] private GameObject uiRoot;
        [SerializeField] private Button uiButton;
        [Header("Option Buttons")]
        [Tooltip("First option button. Will hide UI when pressed. If Scene Name is set, that scene will be loaded.")]
        [SerializeField] private Button optionButtonA;
        [Tooltip("Second option button. Will hide UI when pressed. If Scene Name is set, that scene will be loaded.")]
        [SerializeField] private Button optionButtonB;
        [Tooltip("Scene name to load when Option A is selected. Leave empty to skip scene load.")]
        [SerializeField] private string sceneToLoadOnOptionA = "";
        [Tooltip("Scene name to load when Option B is selected. Leave empty to skip scene load.")]
        [SerializeField] private string sceneToLoadOnOptionB = "";
        [Tooltip("(Optional) GameObject to hide when the triggerButton is pressed.")]
        [SerializeField] private GameObject attachedObjectToHide;

        [Header("Options")]
        [SerializeField] private bool startHidden = true;
        [Tooltip("If true, hide `attachedObjectToHide` when showing UI. Hide will be reverted when HideUI() is called.")]
        [SerializeField] private bool hideAttachedOnShow = true;

        private void Awake()
        {
            if (startHidden)
            {
                if (uiRoot != null) uiRoot.SetActive(false);
                if (uiButton != null) uiButton.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (triggerButton != null) triggerButton.onClick.AddListener(OnTriggerPressed);
            if (optionButtonA != null) optionButtonA.onClick.AddListener(OnOptionASelected);
            if (optionButtonB != null) optionButtonB.onClick.AddListener(OnOptionBSelected);
            // Back-compat: if legacy uiButton assigned, also hide UI when clicked
            if (uiButton != null) uiButton.onClick.AddListener(HideUI);
        }

        private void OnDisable()
        {
            if (triggerButton != null) triggerButton.onClick.RemoveListener(OnTriggerPressed);
            if (optionButtonA != null) optionButtonA.onClick.RemoveListener(OnOptionASelected);
            if (optionButtonB != null) optionButtonB.onClick.RemoveListener(OnOptionBSelected);
            if (uiButton != null) uiButton.onClick.RemoveListener(HideUI);
        }

        private void OnTriggerPressed()
        {
            ShowUI();
            if (hideAttachedOnShow && attachedObjectToHide != null)
            {
                try { attachedObjectToHide.SetActive(false); } catch { }
            }
        }

        private void OnOptionASelected()
        {
            // hide UI first
            HideUI();
            if (!string.IsNullOrEmpty(sceneToLoadOnOptionA))
            {
                try { SceneManager.LoadScene(sceneToLoadOnOptionA); } catch { }
            }
        }

        private void OnOptionBSelected()
        {
            HideUI();
            if (!string.IsNullOrEmpty(sceneToLoadOnOptionB))
            {
                try { SceneManager.LoadScene(sceneToLoadOnOptionB); } catch { }
            }
        }

        /// <summary>
        /// UI を表示して、内部ボタンを有効化する。
        /// </summary>
        public void ShowUI()
        {
            if (uiRoot != null) uiRoot.SetActive(true);
            if (uiButton != null)
            {
                uiButton.gameObject.SetActive(true);
                uiButton.interactable = true;
            }
        }

        /// <summary>
        /// UI を非表示にする（必要ならば外部から呼ぶ）。
        /// </summary>
        public void HideUI()
        {
            if (uiRoot != null) uiRoot.SetActive(false);
            if (uiButton != null) uiButton.gameObject.SetActive(false);
            // If we hid an attached object when showing, restore its active state here.
            if (hideAttachedOnShow && attachedObjectToHide != null)
            {
                try { attachedObjectToHide.SetActive(true); } catch { }
            }
        }
    }
}
