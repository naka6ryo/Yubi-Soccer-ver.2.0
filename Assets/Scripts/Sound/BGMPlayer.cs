using UnityEngine;

public class BGMPlayer : MonoBehaviour
{
    [SerializeField] private string bgmName = "タイトル";
    private SoundManager soundManager;

    void Start()
    {
        soundManager = SoundManager.Instance;
        soundManager.PlayBGM(bgmName);
        soundManager.SetBGMVolume(0.2f);
    }

    void OnDestroy()
    {
        soundManager.StopBGM();
    }
}