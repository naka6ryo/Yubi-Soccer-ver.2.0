using UnityEngine;

public class BGMPlayer : MonoBehaviour
{
    [SerializeField] private string bgmName = "タイトル";
    private SoundManager soundManager;

    void Start()
    {
        soundManager = SoundManager.Instance;
        soundManager.PlayBGM(bgmName);
    }

    void OnDestroy()
    {
        if (soundManager != null)
        {
            soundManager.StopBGM();
        }
    }
}