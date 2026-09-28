using UnityEngine;

public class FishCon : MonoBehaviour
{
    public GameObject fishs;
    public int scoreValue = 10;

    private bool _hasFish = false;

    private void Start()
    {
        if (fishs != null) fishs.SetActive(false);

        if (M2Manager.Instance != null) M2Manager.Instance.RegisterBreedingSite(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasFish || !other.gameObject.CompareTag("FishBall")) return;

        _hasFish = true;
        if (fishs != null) fishs.SetActive(true);

        if (M2Manager.Instance != null)
        {
            M2Manager.Instance.UpdateScore(scoreValue);
            M2Manager.Instance.NeutralizeBreedingSite(this);
        }

        Destroy(other.gameObject);
    }
}
