using System.Linq;
using UnityEngine;

/// <summary>
/// Ensure there is always exactly one enabled AudioListener in the scene.
/// - If multiple enabled listeners are found, only one will remain enabled.
/// - If none are enabled, a fallback listener will be enabled (prefer MainCamera).
/// PlayerController should register its local child AudioListener with this manager so
/// ownership can be respected.
/// </summary>
public class AudioListenerManager : MonoBehaviour
{
    private static AudioListenerManager _instance;
    public static AudioListenerManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AudioListenerManager>();
                if (_instance == null)
                {
                    var go = new GameObject("AudioListenerManager");
                    _instance = go.AddComponent<AudioListenerManager>();
                    // keep alive across loads in editor/play
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private AudioListener activeListener;

    void Awake()
    {
        if (_instance == null) _instance = this;
        DontDestroyOnLoad(gameObject);
        ReconcileListeners();
    }

    /// <summary>
    /// Called by PlayerController to register its child AudioListener and request ownership.
    /// If isOwner is true, this listener will be enabled and other listeners disabled.
    /// If isOwner is false, this listener will be disabled unless no other enabled listener exists.
    /// </summary>
    public void RegisterLocalListener(AudioListener listener, bool isOwner)
    {
        if (listener == null) return;
        var all = FindObjectsOfType<AudioListener>(true);

        if (isOwner)
        {
            // Owner wants the audio listener: enable it and disable others
            foreach (var al in all)
            {
                al.enabled = (al == listener);
            }
            activeListener = listener;
        }
        else
        {
            // Non-owner: ensure this one is disabled
            listener.enabled = false;
            // If there is no enabled listener anywhere, try to enable one (prefer main camera child's listener)
            if (!all.Any(x => x.enabled))
            {
                EnsureFallbackEnabled(all);
            }
        }
    }

    /// <summary>
    /// Scans listeners and ensures exactly one is enabled. Prefer the one on Camera.main, else the first found.
    /// </summary>
    public void ReconcileListeners()
    {
        var all = FindObjectsOfType<AudioListener>(true);
        if (all == null || all.Length == 0)
        {
            // create fallback on main camera or a new gameobject
            EnableOrCreateFallback();
            return;
        }

        // If more than one enabled, disable all but one (prefer Camera.main)
        var enabled = all.Where(x => x.enabled).ToArray();
        if (enabled.Length > 1)
        {
            AudioListener prefer = enabled.FirstOrDefault(x => x.gameObject.GetComponent<Camera>() == Camera.main) ?? enabled[0];
            foreach (var al in enabled)
            {
                al.enabled = (al == prefer);
            }
            activeListener = prefer;
            return;
        }

        // If none enabled, enable a preferred one
        if (enabled.Length == 0)
        {
            EnsureFallbackEnabled(all);
            return;
        }

        // Exactly one enabled
        activeListener = enabled[0];
    }

    private void EnsureFallbackEnabled(AudioListener[] all)
    {
        // Prefer listener on Camera.main
        if (Camera.main != null)
        {
            var mainAl = Camera.main.GetComponent<AudioListener>();
            if (mainAl != null)
            {
                mainAl.enabled = true;
                activeListener = mainAl;
                return;
            }
        }

        if (all != null && all.Length > 0)
        {
            all[0].enabled = true;
            activeListener = all[0];
            return;
        }

        // nothing to enable: create one
        EnableOrCreateFallback();
    }

    private void EnableOrCreateFallback()
    {
        if (Camera.main != null)
        {
            var main = Camera.main.gameObject;
            var al = main.GetComponent<AudioListener>();
            if (al == null) al = main.AddComponent<AudioListener>();
            al.enabled = true;
            activeListener = al;
            return;
        }
        var go = new GameObject("RuntimeAudioListener");
        var listener = go.AddComponent<AudioListener>();
        listener.enabled = true;
        activeListener = listener;
        DontDestroyOnLoad(go);
    }
}
