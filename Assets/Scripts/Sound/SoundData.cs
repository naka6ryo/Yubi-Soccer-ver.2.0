using UnityEngine;

// class の代わりに struct を使う
[System.Serializable]
public struct SoundData // struct に変更
{
    public string name;
    public AudioClip clip;
}