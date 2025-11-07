using UnityEngine;
using System.Collections.Generic; // Listを使うために必要

// Assets/Create メニューから "Sound/SoundDatabase" を選べるようにする
[CreateAssetMenu(menuName = "Sound/SoundDatabase", fileName = "SoundDatabase")]
public class SoundDatabase : ScriptableObject
{
    // Inspectorで編集可能なリスト
    public List<SoundData> seList;
    public List<SoundData> bgmList;
}