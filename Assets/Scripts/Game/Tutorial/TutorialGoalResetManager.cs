using UnityEngine;
using YubiSoccer.Field;

namespace YubiSoccer.Game
{
    /// <summary>
    /// チュートリアル用のゴールリセット。ScoreManager を使わず、直接 GoalTrigger イベントを購読する。
    /// </summary>
    public class TutorialGoalResetManager : GoalResetManager
    {
        protected override void SubscribeGoalEvents()
        {
            GoalTrigger.OnGoalScored += HandleGoalScored;
        }

        protected override void UnsubscribeGoalEvents()
        {
            GoalTrigger.OnGoalScored -= HandleGoalScored;
        }

        private void HandleGoalScored(Team team)
        {
            TriggerGoalReset(team);
        }
    }
}
