using UnityEngine;
using YubiSoccer.Game;
using YubiSoccer.Field;

namespace YubiSoccer.UI
{
    /// <summary>
    /// ゴール発生をトリガーに、指定した MissionUIController を達成状態に切り替えるブリッジ。
    /// - 第一ミッションとは完全に独立して動作します。
    /// - Inspector でターゲットの MissionUIController を割り当ててください。
    /// </summary>
    public class GoalToMissionBridge : MonoBehaviour
    {
        [Tooltip("ゴール時に達成状態にする MissionUIController（第二ミッション用）")]
        public MissionUIController targetMissionUIController;

        [Tooltip("ゴール時に表示する SinglePageTutorial があればここに割り当てる（任意）")]
        public SinglePageTutorial optionalTutorialToShow;

        private void Start()
        {
            // 他コンポーネントの Start が終わるまで待ってからリセットする（複数フレーム待ち）
            StartCoroutine(ResetAfterFrames());
        }

        private System.Collections.IEnumerator ResetAfterFrames()
        {
            // Wait a couple frames to allow other scripts to run their Start()/SetCompleted calls
            yield return null;
            yield return null;
            if (targetMissionUIController != null)
            {
                try
                {
                    targetMissionUIController.ResetCompleted();
                    Debug.Log("GoalToMissionBridge: targetMissionUIController.ResetCompleted() called (delayed) to hide second mission on load.");
                }
                catch { }
            }
        }

        private void OnEnable()
        {
            GoalTrigger.OnGoalScored += HandleGoal;
        }

        private void OnDisable()
        {
            GoalTrigger.OnGoalScored -= HandleGoal;
        }

        private void HandleGoal(YubiSoccer.Game.Team team)
        {
            Debug.Log($"GoalToMissionBridge: Goal scored for {team}. Marking target mission completed.");
            // If a tutorial is assigned, show it and wait for it to be closed before marking mission complete.
            if (optionalTutorialToShow != null)
            {
                try
                {
                    optionalTutorialToShow.Show();
                    // subscribe one-shot to OnClosed
                    System.Action handler = null;
                    handler = () =>
                    {
                        try
                        {
                            if (targetMissionUIController != null) targetMissionUIController.SetCompleted(true);
                        }
                        catch { }
                        try { optionalTutorialToShow.OnClosed -= handler; } catch { }
                    };
                    try { optionalTutorialToShow.OnClosed += handler; } catch { }
                }
                catch { }
            }
            else
            {
                if (targetMissionUIController != null)
                {
                    try { targetMissionUIController.SetCompleted(true); } catch { }
                }
            }
        }
    }
}
