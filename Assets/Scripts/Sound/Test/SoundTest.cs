using UnityEngine;

public class SoundTest : MonoBehaviour
{
    private void Update()
    {
        // 1キーを押したらSEを再生
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SoundManager.Instance.PlaySE("決定");
        }

        // 2キーを押したらBGMを再生
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SoundManager.Instance.PlayBGM("タイトル");
        }

        // 3キーを押したらSEを再生
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SoundManager.Instance.PlayBGM("試合中");
        }

        // 4キーを押したらBGMを停止
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SoundManager.Instance.StopBGM();
        }
    }
}