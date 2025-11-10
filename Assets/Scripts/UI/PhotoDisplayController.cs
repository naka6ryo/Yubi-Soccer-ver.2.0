using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 指定のボタンを押すと写真を前面パネルで表示する。
    /// Inspector:
    ///  - openButton: 押すと写真を表示するボタン
    ///  - closeButton: 押すと写真を閉じるボタン（任意）
    ///  - photoPanel: 写真表示用パネル（Canvas内、最前面のCanvas GroupやSortingOrderを推奨）
    ///  - photoImage: RawImage または Image（RaycastTarget を true にしておく）
    ///  - photoSprite: デフォルトで表示する画像（任意）
    /// </summary>
    public class PhotoDisplayController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image photoImage;

        private void Awake()
        {
            if (openButton != null) openButton.gameObject.SetActive(true);
            if (closeButton != null) closeButton.gameObject.SetActive(false);
            if (photoImage != null) photoImage.gameObject.SetActive(false);

            // ホバーで閉じる等の挙動を防ぐため、closeButton に付与された EventTrigger を削除する
            if (closeButton != null)
            {
                var et = closeButton.gameObject.GetComponent<EventTrigger>();
                if (et != null)
                {
                    Destroy(et);
                }
            }
        }

        private void OnEnable()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        // Inspectorで設定した画像を表示する簡易API
        public void Open()
        {
            // openButton を隠して、closeButton と photoImage を表示するだけ
            if (openButton != null) openButton.gameObject.SetActive(false);
            if (closeButton != null) closeButton.gameObject.SetActive(true);
            if (photoImage != null) photoImage.gameObject.SetActive(true);
        }

        // 任意のSpriteを引数で表示するAPI（他スクリプトから呼べる）
        public void OpenWith(Sprite sprite)
        {
            if (photoImage != null)
            {
                if (sprite != null) photoImage.sprite = sprite;
                // サイズ調整等は不要なら行わない（要件に合わせて追加）
                photoImage.gameObject.SetActive(true);
            }
            if (openButton != null) openButton.gameObject.SetActive(false);
            if (closeButton != null) closeButton.gameObject.SetActive(true);
        }

        public void Close()
        {
            // 閉じるときは openButton を再表示して、closeButton と photoImage を隠す
            if (openButton != null) openButton.gameObject.SetActive(true);
            if (closeButton != null) closeButton.gameObject.SetActive(false);
            if (photoImage != null) photoImage.gameObject.SetActive(false);
        }
    }
}