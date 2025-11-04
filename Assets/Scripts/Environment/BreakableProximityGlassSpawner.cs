using System;
using System.Collections.Generic;
using UnityEngine;

namespace YubiSoccer.Environment
{
    /// <summary>
    /// BreakableProximityGlass を再生成するためのスポーナー。
    /// エントリにPrefabとスポーンTransform(位置/回転/スケール)を設定し、RespawnAll()で再生成する。
    /// 初回からスポーンしたい場合は spawnOnStart をON。
    /// </summary>
    public class BreakableProximityGlassSpawner : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public GameObject prefab;
            public Transform point;
            public Transform parentOverride;
            [NonSerialized] public GameObject lastInstance; // 直近に生成したインスタンスを記録
        }

        [Header("Spawn Entries")]
        public List<Entry> entries = new List<Entry>();

        [Header("Options")]
        [Tooltip("Start時にエントリを生成する")]
        public bool spawnOnStart = false;
        [Tooltip("Respawn時に既存インスタンスが生きていれば破棄する")]
        public bool destroyExistingOnRespawn = true;

        private void Start()
        {
            if (spawnOnStart)
            {
                RespawnAll();
            }
        }

        public void RespawnAll()
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                Respawn(i);
            }
        }

        public void Respawn(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            var e = entries[index];
            if (e == null || e.prefab == null) return;

            // 既存があれば片付け
            if (destroyExistingOnRespawn && e.lastInstance != null)
            {
                Destroy(e.lastInstance);
                e.lastInstance = null;
            }

            Transform parent = e.parentOverride != null ? e.parentOverride : transform;
            Vector3 pos = e.point != null ? e.point.position : transform.position;
            Quaternion rot = e.point != null ? e.point.rotation : transform.rotation;
            Vector3 scale = e.point != null ? e.point.localScale : Vector3.one;

            var go = Instantiate(e.prefab, pos, rot, parent);
            go.transform.localScale = scale;
            e.lastInstance = go;
        }
    }
}
