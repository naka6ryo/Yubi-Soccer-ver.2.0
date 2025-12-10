using System.Collections;
using UnityEngine;
using Cinemachine;

namespace YubiSoccer.UI
{
    /// <summary>
    /// Simple title camera switcher that can be attached to UI Buttons.
    /// - Assign `camA` and `camB` (CinemachineVirtualCamera).
    /// - Call `SwitchToB()` from the Play button OnClick to move to camB.
    /// - Call `SwitchToA()` from the Back/Home button OnClick to move back to camA.
    /// Implements priority-based switching and optionally overrides the brain default blend for the transition.
    /// </summary>
    public class TitleCameraMove : MonoBehaviour
    {
        [Header("Cameras (assign two)")]
        public CinemachineVirtualCamera camA;
        public CinemachineVirtualCamera camB;

        [Header("Priority Settings")]
        public int inactivePriority = 10;
        public int activePriority = 20;

        [Header("Blend Override (optional)")]
        [Tooltip("If > 0, temporarily override CinemachineBrain.defaultBlend for the transition (seconds).")]
        public float overrideBlendTime = 0.25f;
        public CinemachineBlendDefinition.Style overrideBlendStyle = CinemachineBlendDefinition.Style.EaseInOut;

        [Header("Brain (optional)")]
        public CinemachineBrain brain;

        // tracks which side is currently active (true = A active, false = B active)
        private bool isAActive = true;

        void Awake()
        {
            if (brain == null)
            {
                if (Camera.main != null) brain = Camera.main.GetComponent<CinemachineBrain>();
                if (brain == null) brain = Object.FindObjectOfType<CinemachineBrain>();
            }

            // Initialize priorities so camA is active if present
            TryInitPriorities();
        }

        private void TryInitPriorities()
        {
            if (camA != null) camA.Priority = isAActive ? activePriority : inactivePriority;
            if (camB != null) camB.Priority = isAActive ? inactivePriority : activePriority;
        }

        /// <summary>
        /// Call from Play button OnClick to switch to camB (forward).
        /// </summary>
        public void SwitchToB()
        {
            if (camA == null || camB == null)
            {
                Debug.LogWarning("TitleCameraMove: camA or camB not assigned.");
                return;
            }
            if (!isAActive)
            {
                // already B
                return;
            }
            StartCoroutine(CoSwitch(camB));
            isAActive = false;
        }

        /// <summary>
        /// Call from Back/Home button OnClick to switch back to camA (reverse).
        /// </summary>
        public void SwitchToA()
        {
            if (camA == null || camB == null)
            {
                Debug.LogWarning("TitleCameraMove: camA or camB not assigned.");
                return;
            }
            if (isAActive)
            {
                // already A
                return;
            }
            StartCoroutine(CoSwitch(camA));
            isAActive = true;
        }

        private IEnumerator CoSwitch(CinemachineVirtualCamera target)
        {
            if (target == null)
                yield break;

            // If we have a brain and overrideBlendTime > 0, temporarily replace default blend
            CinemachineBlendDefinition old = default;
            bool replaced = false;
            if (brain != null && overrideBlendTime > 0f)
            {
                try
                {
                    old = brain.m_DefaultBlend;
                    brain.m_DefaultBlend = new CinemachineBlendDefinition(overrideBlendStyle, overrideBlendTime);
                    replaced = true;
                }
                catch { }
            }

            // Apply priorities
            if (camA != null) camA.Priority = (camA == target) ? activePriority : inactivePriority;
            if (camB != null) camB.Priority = (camB == target) ? activePriority : inactivePriority;

            // Wait until brain reports the target as active or until timeout
            if (brain != null)
            {
                float timeout = Mathf.Max(0.5f, overrideBlendTime * 3f);
                float t = 0f;
                while (t < timeout)
                {
                    try
                    {
                        var active = brain.ActiveVirtualCamera as CinemachineVirtualCamera;
                        var blend = brain.ActiveBlend;
                        if (active == target && (blend == null || !blend.IsValid))
                        {
                            break; // settled
                        }
                    }
                    catch { }
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                // No brain — still wait a short moment so UI feels smoother
                yield return new WaitForSeconds(Mathf.Max(0.05f, overrideBlendTime));
            }

            // restore blend
            if (replaced)
            {
                try { brain.m_DefaultBlend = old; } catch { }
            }
        }
    }
}
