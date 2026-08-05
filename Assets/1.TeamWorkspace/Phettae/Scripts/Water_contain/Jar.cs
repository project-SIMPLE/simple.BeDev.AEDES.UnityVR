using UnityEngine;

public class Jar : MonoBehaviour
{
    [Header("Reference")]
    public GameObject topHolo;
    public GameObject top;
    public GameObject top_grab;

    [Header("Game Setting")]
    public int getScore = 10;

    private bool isInColli = false;

    public void Start()
    {
        top.SetActive(false);
        topHolo.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Jar_Hat"))
        {
            topHolo.SetActive(true);
            isInColli = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Jar_Hat"))
        {
            topHolo.SetActive(false);
            isInColli = false;
        }
    }

    public void PutTop()
    {
        if (isInColli)
        {
            top.SetActive(true);
            topHolo.SetActive(false);
            top_grab.SetActive(false);

            M2Manager.Instance.UpdateScore(getScore);
        }
    }
}
