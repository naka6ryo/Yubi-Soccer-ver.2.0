using UnityEngine;


public class Confetti : MonoBehaviour
{
    ParticleSystem ps;
    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        Hide();
    }

    public void Show()
    {
        this.gameObject.SetActive(true);
        ps.Play();
    }

    public void Hide()
    {
        ps.Stop();
        this.gameObject.SetActive(false);
    }
}