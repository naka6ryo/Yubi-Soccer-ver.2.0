using System;
using UnityEngine;
using YubiSoccer.UI;

namespace YubiSoccer.Game
{
    /// <summary>
    /// 指定したボール位置から半径内にプレイヤーが入ったらミッションを達成済みに切り替える。
    /// - シーン内のボールや Player タグを使って自動で検索しますが、Inspector で明示的に割当ても可能です。
    /// - 1回だけ達成を通知します（再度リセットしたい場合は ResetCompleted() を呼んでください）。
    /// </summary>

    public class MissionProximityChecker : MonoBehaviour
    {
        [Tooltip("監視対象のボール Transform。未設定時はタグ 'Ball' / 名前 'ball' を検索します。")]
        public Transform ballTransform;

        [Tooltip("プレイヤーを識別するタグ（シーンのプレイヤーにこのタグを設定してください）。")]
        public string playerTag = "Player";

        [Tooltip("判定半径（メートル）")]
        public float radius = 1.5f;

        [Tooltip("ミッションUIコントローラ（任意）。未設定時はシーン内の GameObject 名で検索を試みます。")]
        public MissionUIController missionUIController;

        [Tooltip("ミッションパネルの GameObject 名（missionUIController 未割当時に探すための補助）")]
        public string missionPanelObjectName = "MissionPanel";

        [Tooltip("プレイヤー検出に物理判定（OverlapSphere）を使う場合はレイヤーマスクを設定してください（未設定ならタグ判定にフォールバックします）。")]
        public LayerMask playerLayerMask = 0;

        public bool completed { get; private set; } = false;

        public event Action OnCompleted;

        [Header("Startup")]
        [Tooltip("シーン起動直後に判定を無効化する待ち時間（秒）。短時間の初期配置で誤検出するのを防ぐために使用します。")]
        public float detectionDelay = 0.5f;

        private float timeSinceStart = 0f;

        private void Start()
        {
            // ボール Transform を自動検索（タグ -> 名前に 'ball' を含むオブジェクト -> Rigidbody を持つ最初の ball 名称）
            if (ballTransform == null)
            {
                try
                {
                    var ball = GameObject.FindWithTag("Ball");
                    if (ball != null) ballTransform = ball.transform;
                }
                catch { /* FindWithTag が例外を投げる可能性を防ぐ */ }

                if (ballTransform == null)
                {
                    // 名前に 'ball' が含まれるオブジェクトを探す
                    var all = GameObject.FindObjectsOfType<GameObject>();
                    foreach (var go in all)
                    {
                        if (go == null) continue;
                        if (go.name != null && go.name.ToLower().Contains("ball"))
                        {
                            ballTransform = go.transform;
                            break;
                        }
                    }
                }

                if (ballTransform == null)
                {
                    Debug.LogWarning("MissionProximityChecker: ballTransform が見つかりません。Inspector に割り当ててください。");
                }
            }

            if (missionUIController == null && !string.IsNullOrEmpty(missionPanelObjectName))
            {
                var go = GameObject.Find(missionPanelObjectName);
                if (go != null) missionUIController = go.GetComponent<MissionUIController>();
                if (missionUIController == null && go != null)
                {
                    Debug.LogWarning($"MissionProximityChecker: '{missionPanelObjectName}' に MissionUIController が見つかりません。コンポーネントを追加してください。");
                }
            }
        }

        private void Update()
        {
            // Update elapsed time and defer detection for a short startup period
            timeSinceStart += Time.deltaTime;
            if (timeSinceStart < detectionDelay) return;
            if (completed) return;
            if (ballTransform == null) return;

            Vector3 ballPos = ballTransform.position;

            bool found = false;

            // まずレイヤーマスクが指定されている場合は OverlapSphere を使う（より確実）
            if (playerLayerMask != 0)
            {
                var cols = Physics.OverlapSphere(ballPos, radius, playerLayerMask);
                if (cols != null && cols.Length > 0)
                {
                    foreach (var c in cols)
                    {
                        if (c == null) continue;
                        // タグが Player であるか、またはコライダの GameObject がプレイヤーを含むか
                        if (string.IsNullOrEmpty(playerTag) || c.CompareTag(playerTag) || c.gameObject.GetComponentInParent<Transform>() != null)
                        {
                            found = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                // レイヤーマスクが未設定ならタグ検索（既存の実装）を実行
                var players = GameObject.FindGameObjectsWithTag(playerTag);
                if (players != null && players.Length > 0)
                {
                    float r2 = radius * radius;
                    foreach (var p in players)
                    {
                        if (p == null) continue;
                        var d2 = (p.transform.position - ballPos).sqrMagnitude;
                        if (d2 <= r2)
                        {
                            found = true;
                            break;
                        }
                    }
                }
                else
                {
                    // タグ検索で見つからない場合は OverlapSphere でタグを持つコライダを探すフォールバック
                    var cols = Physics.OverlapSphere(ballPos, radius);
                    if (cols != null && cols.Length > 0)
                    {
                        foreach (var c in cols)
                        {
                            if (c == null) continue;
                            if (!string.IsNullOrEmpty(playerTag) && c.CompareTag(playerTag))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                }
            }

            if (found)
            {
                completed = true;
                Debug.Log("MissionProximityChecker: プレイヤーが判定範囲に入り、ミッション完了を設定しました。");
                if (missionUIController != null)
                {
                    missionUIController.SetCompleted(true);
                }
                OnCompleted?.Invoke();
            }
        }

        /// <summary>
        /// 内部状態をリセットして再度判定できるようにします。
        /// </summary>
        public void ResetCompleted()
        {
            completed = false;
            if (missionUIController != null) missionUIController.SetCompleted(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (ballTransform == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ballTransform.position, radius);
        }

    }
}
