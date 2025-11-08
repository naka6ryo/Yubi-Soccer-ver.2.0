using UnityEngine;

public class OpeningBGMPlayer : MonoBehaviour
{
    private SoundManager soundManager;

    void Start()
    {
        soundManager = SoundManager.Instance;
        if (soundManager != null)
        {
            soundManager.PlayBGM("タイトル");
        }
    }

    void OnDestroy()
    {
        soundManager.StopBGM();
    }
}