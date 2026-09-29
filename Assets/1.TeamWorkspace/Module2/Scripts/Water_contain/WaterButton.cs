using UnityEngine;

public class WaterButton : MonoBehaviour
{
    [Header("Reference")]
    public AudioClip waterSplash;
    public GameObject waterPrefab;
    public GameObject waterSplashEffect;
    public LayerMask groundLayerMask;

    [Header("Game Setting")]
    public int getScore = 10;
    public bool isWaterActive;
    [Tooltip("A 'save' container holds no standing water and does not count as a breeding site.")]
    public bool isSave;
    [Tooltip("Degrees from upright past which the water spills out. Tipping is judged by tilt alone: the old test cast along the container's up axis for 1.2 m against groundLayerMask only, and the house floor is on the Wall layer, so an indoor vase only emptied when held inverted below knee height.")]
    public float pourAngle = 110f;

    private AudioSource _source;

    private void Start()
    {
        _source = GetComponent<AudioSource>();
        isWaterActive = true;

        if (!isSave && M2Manager.Instance != null) M2Manager.Instance.RegisterBreedingSite(this);
    }

    void Update()
    {
        waterOut();
    }

    private void waterOut()
    {
        if (!isWaterActive) return;
        if (Vector3.Angle(transform.up, Vector3.up) < pourAngle) return;

        isWaterActive = false;

        if (waterPrefab != null) waterPrefab.SetActive(false);
        if (_source != null && waterSplash != null) _source.PlayOneShot(waterSplash);

        // The splash lands on whatever is under the container: the ground mask first, then any
        // non-trigger surface (house floor, furniture) that is not the container itself.
        // PF_VaseWithFlowers was serialised before waterSplashEffect existed, so it can be null.
        if (waterSplashEffect != null && FindSplashPoint(out RaycastHit hit))
            Instantiate(waterSplashEffect, hit.point, Quaternion.LookRotation(hit.normal));

        if (M2Manager.Instance == null) return;
        M2Manager.Instance.UpdateScore(getScore);
        if (!isSave) M2Manager.Instance.NeutralizeBreedingSite(this);
    }

    bool FindSplashPoint(out RaycastHit best)
    {
        best = default;
        if (Physics.Raycast(transform.position, Vector3.down, out best, 5f, groundLayerMask, QueryTriggerInteraction.Ignore))
            return true;
        float nearest = float.MaxValue;
        foreach (var h in Physics.RaycastAll(transform.position, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.distance >= nearest) continue;
            nearest = h.distance;
            best = h;
        }
        return nearest < float.MaxValue;
    }
}
