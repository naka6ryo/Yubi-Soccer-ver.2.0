using System.Diagnostics.Contracts;
using UnityEngine;

// AudioSourceコンポーネントが必須であることを示す
[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static SoundManager Instance { get; private set; }

    // サウンドデータベース (Inspectorからアタッチする)
    [SerializeField]
    private SoundDatabase soundDatabase;

    // サウンド再生用のAudioSource
    private AudioSource bgmSource; // BGM用 (ループ再生)
    private AudioSource seSource;  // SE用 (PlayOneShot)

    private void Awake()
    {
        // ▼ シングルトン ＆ 永続化処理 ▼
        if (Instance == null)
        {
            // 自身をインスタンスとして設定
            Instance = this;
            // シーンをまたいでも破棄されないようにする
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 既にインスタンスが存在する場合は、このオブジェクトを破棄する
            Destroy(gameObject);
            return;
        }
        // ▲ シングルトン ＆ 永続化処理 ▲

        // AudioSourceコンポーネントを取得
        // BGM用とSE用でAudioSourceを分けるため、2つ目のAudioSourceを追加する
        bgmSource = GetComponent<AudioSource>();

        // BGMはループ再生を基本とする
        bgmSource.loop = true;
    }

    /// <summary>
    /// SEを再生する
    /// </summary>
    /// <param name="name">SoundDatabaseに登録したSEの名前</param>
    public void PlaySE(string name)
    {
        if (seSource == null || soundDatabase == null)
        {
            Debug.LogWarning("[SoundManager] seSource または soundDatabase が null です。");
            return;
        }

        // データベースから名前で検索
        SoundData data = soundDatabase.seList.Find(x => x.name == name);

        if (data.clip != null)
        {
            // 見つかったら再生 (同時再生可能なPlayOneShot)
            seSource.PlayOneShot(data.clip);
        }
        else
        {
            // 見つからなかったらエラー
            Debug.LogError($"[SoundManager] SE \"{name}\" が見つかりません。");
        }
    }

    /// <summary>
    /// BGMを再生する
    /// </summary>
    /// <param name="name">SoundDatabaseに登録したBGMの名前</param>
    public void PlayBGM(string name)
    {
        if (bgmSource == null || soundDatabase == null)
        {
            Debug.LogWarning("[SoundManager] bgmSource または soundDatabase が null です。");
            return;
        }

        // データベースから名前で検索
        SoundData data = soundDatabase.bgmList.Find(x => x.name == name);

        if (data.clip != null)
        {
            // 既に同じBGMが再生中なら何もしない
            if (bgmSource.clip == data.clip && bgmSource.isPlaying)
            {
                return;
            }

            // 見つかったらクリップを差し替えて再生
            bgmSource.clip = data.clip;
            bgmSource.Play();
        }
        else
        {
            // 見つからなかったらエラー
            Debug.LogError($"[SoundManager] BGM \"{name}\" が見つかりません。");
        }
    }

    /// <summary>
    /// BGMを停止する
    /// </summary>
    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
    }

    /// <summary>
    /// SEを停止する
    /// チャージ中状態のSEを止めるときに使用
    /// </summary>
    /// <returns></returns>
    public bool StopSE()
    {
        if (seSource != null && seSource.isPlaying)
        {
            seSource.Stop();
            return true;
        }
        return false;
    }

    public void SetSEVolume(float volume)
    {
        if (seSource != null)
        {
            seSource.volume = volume;
        }
    }
    
    /// <summary>
    /// BGM の音量を設定する（0.0 - 1.0）
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        if (bgmSource != null)
        {
            bgmSource.volume = Mathf.Clamp01(volume);
        }
    }
}