using Photon.Pun;
using UnityEngine;
using YubiSoccer.Player;

namespace YubiSoccer.Network
{
    /// <summary>
    /// Sync player's charge state (0..1) across the network using PUN's OnPhotonSerializeView.
    /// - Attach to a child of the player prefab (same root that has PhotonView and PlayerKickController).
    /// - Owner writes current charge each network tick; remote clients receive and update visuals.
    /// </summary>
    public class NetworkChargeSync : MonoBehaviourPun, IPunObservable
    {
        [Tooltip("Smoothing factor for received charge values (0=no smoothing, 1=fully smoothed).")]
        [Range(0f, 1f)] public float smoothing = 0.2f;

        private PlayerKickController kickController;
        private KickRadiusIndicator radiusIndicator;

        // received remote state
        private float remoteCharge = 0f;
        private float displayedCharge = 0f;
        private bool remoteIsCharging = false;

        void Awake()
        {
            kickController = GetComponentInParent<PlayerKickController>();
            radiusIndicator = GetComponentInParent<KickRadiusIndicator>();
            // if not found on parent, try children
            if (radiusIndicator == null)
                radiusIndicator = GetComponentInChildren<KickRadiusIndicator>(true);
        }

        void Update()
        {
            if (photonView.IsMine)
            {
                // Owner handles its own visuals via PlayerKickController; nothing to do here
                return;
            }

            // Remote: smooth toward remoteCharge
            displayedCharge = Mathf.Lerp(displayedCharge, remoteCharge, Mathf.Clamp01(1f - smoothing));

            // Update remote visuals: pulse effect on indicator if available
            if (radiusIndicator != null && kickController != null)
            {
                float targetRadius = kickController.ComputeRadiusForCharge(displayedCharge);
                // compute color using owner's logic so remotes match
                Color c = kickController.ComputeIndicatorColor(displayedCharge);

                // show or hide base ring depending on whether there's a charge
                if (displayedCharge > 0.01f)
                {
                    try { radiusIndicator.Show(); } catch { }
                    try { radiusIndicator.SetRadius(targetRadius); } catch { }
                }
                else
                {
                    try { radiusIndicator.Hide(); } catch { }
                }

                // ensure color is set and update pulse
                try { radiusIndicator.SetColor(c); } catch { }
                radiusIndicator.UpdatePulseEffect(targetRadius, c, displayedCharge, Time.deltaTime);
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // owner -> send current charge
                float c = 0f;
                bool charging = false;
                if (kickController != null)
                {
                    try { c = kickController.NetworkCharge01; charging = (c > 0f); } catch { }
                }
                stream.SendNext(c);
                stream.SendNext(charging);
            }
            else
            {
                // remote -> receive
                try
                {
                    float c = (float)stream.ReceiveNext();
                    bool charging = (bool)stream.ReceiveNext();
                    remoteCharge = Mathf.Clamp01(c);
                    remoteIsCharging = charging;
                }
                catch
                {
                    // ignore malformed
                }
            }
        }
    }
}
