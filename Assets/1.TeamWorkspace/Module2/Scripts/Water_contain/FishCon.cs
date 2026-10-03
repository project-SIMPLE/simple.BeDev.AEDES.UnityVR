using UnityEngine;

public class FishCon : MonoBehaviour, IBreedingSite
{
    public GameObject fishs;
    public int scoreValue = 10;

    private bool _hasFish = false;

    private void Start()
    {
        if (fishs != null) fishs.SetActive(false);

        if (M2Manager.Instance != null) M2Manager.Instance.RegisterBreedingSite(this);
    }

    /// <summary>The fish are already in and nobody is credited: how a round shows a bowl it is not using.</summary>
    public void ShowResolved()
    {
        _hasFish = true;
        if (fishs != null) fishs.SetActive(true);

        // The ball is only there to be put in the bowl. With no bowl left that needs one it is a prop
        // that does nothing when dropped in, so take it away rather than let it mislead.
        foreach (var other in FindObjectsByType<FishCon>(FindObjectsSortMode.None))
            if (other != this && !other._hasFish) return;
        foreach (var ball in GameObject.FindGameObjectsWithTag("FishBall")) ball.SetActive(false);
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
