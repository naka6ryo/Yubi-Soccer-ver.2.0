// CM2_SwitchNearEndSmooth.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CM2_SwitchNearEndSmooth : MonoBehaviour
{
    [Header("0=1番, 1=2番, 2=3番")]
    public List<CinemachineVirtualCamera> cams = new List<CinemachineVirtualCamera>(3);

    [Header("Priority")]
    public int lowPriority = 10;
    public int highPriority = 20;

    [Header("トリガー時期（“直前”の定義）")]
    [Tooltip("1→2ブレンドの進捗(0〜1)。これを超えたら 3 へ切替。例: 0.85〜0.95")]
    [Range(0f, 1f)] public float progressThreshold = 0.90f;

    [Header("2→3 のブレンド設定（瞬間だけ適用）")]
    public float twoToThreeBlendTime = 0.25f; // 0.15〜0.4 あたりが“止まらず滑らか”
    public CinemachineBlendDefinition.Style twoToThreeStyle =
        CinemachineBlendDefinition.Style.EaseInOut;

    [Header("参照（未設定なら自動取得）")]
    public CinemachineBrain brain;

    bool _running;

    /// <summary>UIボタンの OnClick から呼ぶ</summary>
    public void Play()
    {
        if (!_running) StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        _running = true;

        if (cams.Count < 3 || cams[0] == null || cams[1] == null || cams[2] == null)
        { Debug.LogWarning("cams[0..2] に vcam を割り当ててください。"); _running = false; yield break; }

        if (brain == null) brain = Camera.main ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain == null) { Debug.LogWarning("CinemachineBrain が見つかりません。"); _running = false; yield break; }

        // まず 2 を最優先にして 1→2 ブレンド開始
        SetHighest(1);

        // ブレンド開始を待機
        float t = 0f, safety = 3f;
        while ((brain.ActiveBlend == null || !brain.ActiveBlend.IsValid) && t < safety)
        { t += Time.deltaTime; yield return null; }

        // “直前”で 3 へ切替（その瞬間だけ 2→3 のブレンド時間を短く強制）
        if (brain.ActiveBlend != null && brain.ActiveBlend.IsValid)
        {
            while (true)
            {
                var b = brain.ActiveBlend;
                if (b == null || !b.IsValid) break;

                float progress = (b.Duration > 0f) ? (b.TimeInBlend / b.Duration) : 1f;
                if (progress >= progressThreshold)
                {
                    // 一時的に DefaultBlend を置き換え
                    var old = brain.m_DefaultBlend;
                    brain.m_DefaultBlend = new CinemachineBlendDefinition(twoToThreeStyle, twoToThreeBlendTime);

                    SetHighest(2); // 3 を最優先 → 2→3 ブレンドがすぐ始まる

                    // 指定時間＋少し待ってから元のブレンド設定に戻す
                    yield return new WaitForSeconds(twoToThreeBlendTime + 0.05f);
                    brain.m_DefaultBlend = old;
                    break;
                }
                yield return null;
            }
        }
        else
        {
            // Cut などでブレンドが検出できなかったときのフォールバック
            var old = brain.m_DefaultBlend;
            brain.m_DefaultBlend = new CinemachineBlendDefinition(twoToThreeStyle, twoToThreeBlendTime);
            SetHighest(2);
            yield return new WaitForSeconds(twoToThreeBlendTime + 0.05f);
            brain.m_DefaultBlend = old;
        }

        _running = false;
    }

    void SetHighest(int index)
    {
        for (int i = 0; i < cams.Count; i++)
        {
            var v = cams[i];
            if (v == null) continue;
            v.Priority = (i == index) ? highPriority : lowPriority;
        }
    }
}
