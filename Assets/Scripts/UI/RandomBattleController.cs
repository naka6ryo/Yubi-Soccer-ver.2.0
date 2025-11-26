using UnityEngine;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// RandomBattleButton 押下時に BattleMode を非表示にし、
    /// 新しく割り当てた Image と Button を表示します。
    /// Inspector に各参照をセットしてください。
    /// </summary>
    public class RandomBattleController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("押されたら処理される Button (RandomBattleButton)")]
        [SerializeField] private Button randomBattleButton;

        [Tooltip("隠す対象の BattleMode GameObject (パネル等)")]
        [SerializeField] private GameObject battleMode;
        [Tooltip("隠す対象の BattleMode GameObject (パネル等)")]
        [SerializeField] private Button hiddenButton;
        [SerializeField] private Button hiddenButton2;

        [Tooltip("表示する Image (Inspectorで GameObject を割当て)")]
        [SerializeField] private Image addedImage;

        [Tooltip("表示する Button (Inspectorで割当て)")]
        [SerializeField] private Button addedButton;
        [Tooltip("表示する Button (Inspectorで割当て)")]
        [SerializeField] private Button addedButton2;
        [Tooltip("表示する Button (Inspectorで割当て)")]
        [SerializeField] private Button addedButton3;

        private void Awake()
        {
            // 初期は追加UIを非表示にする
            if (addedImage != null) addedImage.gameObject.SetActive(false);
            if (addedButton != null) addedButton.gameObject.SetActive(false);
            if (addedButton2 != null) addedButton2.gameObject.SetActive(false);
            if (addedButton3 != null) addedButton3.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (randomBattleButton != null)
            {
                randomBattleButton.onClick.AddListener(OnRandomBattleClicked);
            }
        }

        /// <summary>
        /// RandomBattleButton のクリックハンドラ
        /// </summary>
        public void OnRandomBattleClicked()
        {
            Debug.Log("[RandomBattleController] OnRandomBattleClicked");
            if (battleMode != null) battleMode.SetActive(false);
            if (hiddenButton != null) hiddenButton.gameObject.SetActive(false);
            if (hiddenButton2 != null) hiddenButton2.gameObject.SetActive(false);
            if (addedImage != null)
            {
                addedImage.gameObject.SetActive(true);
                // Ensure the decorative image doesn't block UI clicks
                try { addedImage.raycastTarget = false; } catch { }
            }
            if (addedButton != null)
            {
                addedButton.gameObject.SetActive(true);
                addedButton.interactable = true;
                try { addedButton.transform.SetAsLastSibling(); } catch { }
                addedButton.onClick.AddListener(() =>
                {
                    Debug.Log("[RandomBattleController] addedButton clicked");
                    var nm = FindObjectOfType<NetworkManager>(); if (nm != null) nm.QuickMatch2Players();
                });
            }
            if (addedButton2 != null)
            {
                addedButton2.gameObject.SetActive(true);
                addedButton2.interactable = true;
                try { addedButton2.transform.SetAsLastSibling(); } catch { }
                addedButton2.onClick.AddListener(() =>
                {
                    Debug.Log("[RandomBattleController] addedButton2 clicked");
                    var nm = FindObjectOfType<NetworkManager>(); if (nm != null) nm.QuickMatch4Players();
                });
            }
            if (addedButton3 != null)
            {
                addedButton3.gameObject.SetActive(true);
                addedButton3.interactable = true;
                try { addedButton3.transform.SetAsLastSibling(); } catch { }
                addedButton3.onClick.AddListener(() =>
                {
                    Debug.Log("[RandomBattleController] addedButton3 clicked");
                    var nm = FindObjectOfType<NetworkManager>(); if (nm != null) nm.QuickMatch6Players();
                });
            }
        }
    }
}