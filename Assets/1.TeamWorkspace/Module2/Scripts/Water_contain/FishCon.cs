using UnityEngine;

public class FishCon : MonoBehaviour
{
    public GameObject fishs;
    public int scoreValue = 10;

    private void Start()
    {
        fishs.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("FishBall"))
        {
            fishs.SetActive(true);
            M2Manager.Instance.UpdateScore(scoreValue);

            Destroy(other.gameObject);
        }
    }
}
