using UnityEngine;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// シーン上で `MissionUIController` と `SinglePageTutorial` を紐づける簡単なブリッジ。
    /// - Inspector に両方を割り当ててください。
    /// - Mission のアナウンス完了時に SinglePageTutorial を表示します。
    /// </summary>
    public class MissionAnnouncementBridge : MonoBehaviour
    {
        public MissionUIController missionUIController;
        public SinglePageTutorial singlePageTutorial;
        [Tooltip("最初の TutorialSequence（もう一度読むボタンを無効化する対象）")]
        public YubiSoccer.UI.TutorialSequence firstTutorialSequence;

        [Header("Direct Buttons (optional)")]
        [Tooltip("最初のチュートリアルの“もう一度読む”ボタンを直接指定する場合はこちらに割り当ててください。指定があれば優先して使用されます。")]
        public Button firstReadAgainButton;
        [Tooltip("第二のチュートリアルの“もう一度読む”ボタンを直接指定する場合はこちらに割り当ててください。指定があれば優先して使用されます。")]
        public Button secondReadAgainButton;

        [Tooltip("第二チュートリアルの readAgainButton をミッション達成時に有効化するかを制御するフラグ（デフォルト true）")]
        public bool enableSecondReadAgainOnMission = true;

        private void Start()
        {
            // Start 中に他コンポーネントの初期化順でうまく設定されないことがあるため
            // 1フレーム待ってから初期化を行うコルーチンを使う
            StartCoroutine(InitializeDelayed());
        }

        private System.Collections.IEnumerator InitializeDelayed()
        {
            // 1フレーム待つことで Inspector での割当や他コンポーネントの Awake/Start を待つ
            yield return null;
            // 追加で1フレーム待つことで UI の初期化順をさらに安定させる
            yield return null;

            // フォールバック: missionUIController が未割当ならシーンから探す
            if (missionUIController == null)
            {
                try { missionUIController = FindObjectOfType<MissionUIController>(); } catch { }
            }

            // 初期状態: 最初の「もう一度読む」は有効、2つ目は無効にする
            Debug.Log("MissionAnnouncementBridge: InitializeDelayed running. firstReadAgainButton=" + (firstReadAgainButton != null) + ", secondReadAgainButton=" + (secondReadAgainButton != null) + ", firstTutorialSequence=" + (firstTutorialSequence != null) + ", singlePageTutorial=" + (singlePageTutorial != null));
            if (firstReadAgainButton != null)
            {
                try { firstReadAgainButton.interactable = true; Debug.Log("MissionAnnouncementBridge: firstReadAgainButton set interactable=true"); } catch { }
            }
            else if (firstTutorialSequence != null && firstTutorialSequence.missionReadAgainButton != null)
            {
                try { firstTutorialSequence.missionReadAgainButton.interactable = true; Debug.Log("MissionAnnouncementBridge: firstTutorialSequence.missionReadAgainButton set interactable=true"); } catch { }
            }

            if (secondReadAgainButton != null)
            {
                try
                {
                    secondReadAgainButton.interactable = false;
                    secondReadAgainButton.gameObject.SetActive(false);
                    Debug.Log("MissionAnnouncementBridge: secondReadAgainButton set inactive and interactable=false");
                }
                catch { }
            }
            else if (singlePageTutorial != null && singlePageTutorial.readAgainButton != null)
            {
                try
                {
                    singlePageTutorial.readAgainButton.interactable = false;
                    singlePageTutorial.readAgainButton.gameObject.SetActive(false);
                    Debug.Log("MissionAnnouncementBridge: singlePageTutorial.readAgainButton set inactive and interactable=false");
                }
                catch { }
            }

            // Announcement 完了時の購読（まだなら）
            if (missionUIController != null)
            {
                try { missionUIController.OnAnnouncementFinished -= OnAnnouncementFinished; } catch { }
                try { missionUIController.OnAnnouncementFinished += OnAnnouncementFinished; } catch { }
            }
        }

        private void OnDestroy()
        {
            if (missionUIController != null)
            {
                missionUIController.OnAnnouncementFinished -= OnAnnouncementFinished;
            }
        }

        private void OnAnnouncementFinished()
        {
            Debug.Log("MissionAnnouncementBridge: OnAnnouncementFinished called");
            try
            {
                // 表示：第二チュートリアルは Announcement 終了時に表示
                if (singlePageTutorial != null) singlePageTutorial.Show();
                // 第二チュートリアル表示時は、既存のミッション完了 UI を非表示にする
                if (missionUIController != null && missionUIController.completedRoot != null)
                {
                    try
                    {
                        missionUIController.completedRoot.SetActive(false);
                        Debug.Log("MissionAnnouncementBridge: missionUIController.completedRoot hidden on second tutorial show");
                    }
                    catch { }
                }
                // ミッション達成時に、最初のチュートリアルの「もう一度読む」ボタンを無効化（直接ボタンが指定されていれば優先）
                if (firstReadAgainButton != null)
                {
                    try
                    {
                        firstReadAgainButton.interactable = false;
                        firstReadAgainButton.gameObject.SetActive(false);
                        Debug.Log("MissionAnnouncementBridge: firstReadAgainButton set inactive and interactable=false on announcement");
                    }
                    catch { }
                }
                else if (firstTutorialSequence != null && firstTutorialSequence.missionReadAgainButton != null)
                {
                    try
                    {
                        firstTutorialSequence.missionReadAgainButton.interactable = false;
                        firstTutorialSequence.missionReadAgainButton.gameObject.SetActive(false);
                        Debug.Log("MissionAnnouncementBridge: firstTutorialSequence.missionReadAgainButton set inactive and interactable=false on announcement");
                    }
                    catch { }
                }

                // 第二チュートリアルの「もう一度読む」ボタンを有効化（直接ボタンが指定されていれば優先）
                if (enableSecondReadAgainOnMission)
                {
                    if (secondReadAgainButton != null)
                    {
                        try
                        {
                            secondReadAgainButton.gameObject.SetActive(true);
                            secondReadAgainButton.interactable = true;
                            Debug.Log("MissionAnnouncementBridge: secondReadAgainButton activated and interactable=true on announcement");
                        }
                        catch { }
                    }
                    else if (singlePageTutorial != null && singlePageTutorial.readAgainButton != null)
                    {
                        try
                        {
                            singlePageTutorial.readAgainButton.gameObject.SetActive(true);
                            singlePageTutorial.readAgainButton.interactable = true;
                            Debug.Log("MissionAnnouncementBridge: singlePageTutorial.readAgainButton activated and interactable=true on announcement");
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
