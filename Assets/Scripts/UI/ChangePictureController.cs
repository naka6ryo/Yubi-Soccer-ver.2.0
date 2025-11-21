using UnityEngine;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    public class ChangePictureController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("押されたら処理される Button (OriginalBattleButton)")]
        [SerializeField] private Button originalBattleButton;

        [Tooltip("隠す対象の BattleMode GameObject (パネル等)")]
        [SerializeField] private GameObject hiddenObject;
        [SerializeField] private Button hiddenButton;
        [SerializeField] private Button hiddenButton2;

        [Tooltip("表示する Image (Inspectorで GameObject を割当て)")]
        [SerializeField] private Image addedImage;

        [SerializeField] private Button addedButton;
        [SerializeField] private Button addedButton2;
        [SerializeField] private Button addedButton3;
        [SerializeField] private GameObject addObject;
        [SerializeField] private GameObject addObject2;


        private void Awake()
        {
            // 初期は追加UIを非表示にする
            if (addedImage != null) addedImage.gameObject.SetActive(false);
            if (addedButton != null) addedButton.gameObject.SetActive(false);
            if (addedButton2 != null) addedButton2.gameObject.SetActive(false);
            if (addedButton3 != null) addedButton3.gameObject.SetActive(false);
            if (addObject != null) addObject.gameObject.SetActive(false);
            if (addObject2 != null) addObject2.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (originalBattleButton != null)
            {
                originalBattleButton.onClick.AddListener(OnClicked);
            }
        }

        public void OnClicked()
        {
            if (hiddenObject != null) hiddenObject.SetActive(false);
            if (hiddenButton != null) hiddenButton.gameObject.SetActive(false);
            if (hiddenButton2 != null) hiddenButton2.gameObject.SetActive(false);
            if (addedImage != null) addedImage.gameObject.SetActive(true);
            if (addedButton != null) addedButton.gameObject.SetActive(true);
            if (addedButton2 != null) addedButton2.gameObject.SetActive(true);
            if (addedButton3 != null) addedButton3.gameObject.SetActive(true);
            if (addObject != null) addObject.gameObject.SetActive(true);
            if (addObject2 != null) addObject2.gameObject.SetActive(true);
        }
    }
}