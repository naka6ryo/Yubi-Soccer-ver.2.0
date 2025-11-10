using UnityEngine;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// OriginalBattleButton 押下時に BattleMode を非表示にし、
    /// 新しく割り当てた Image と Button を表示します。
    /// Inspector に各参照をセットしてください。
    /// </summary>
    public class OriginalBattleController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("押されたら処理される Button (OriginalBattleButton)")]
        [SerializeField] private Button originalBattleButton;

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
            if (originalBattleButton != null)
            {
                originalBattleButton.onClick.AddListener(OnOriginalBattleClicked);
            }
        }

        /// <summary>
        /// OriginalBattleButton のクリックハンドラ
        /// </summary>
        public void OnOriginalBattleClicked()
        {
            if (battleMode != null) battleMode.SetActive(false);
            if (hiddenButton != null) hiddenButton.gameObject.SetActive(false);
            if (hiddenButton2 != null) hiddenButton2.gameObject.SetActive(false);
            if (addedImage != null) addedImage.gameObject.SetActive(true);
            if (addedButton != null) addedButton.gameObject.SetActive(true);
            if (addedButton2 != null) addedButton2.gameObject.SetActive(true);
            if (addedButton3 != null) addedButton3.gameObject.SetActive(true);
        }
    }
}