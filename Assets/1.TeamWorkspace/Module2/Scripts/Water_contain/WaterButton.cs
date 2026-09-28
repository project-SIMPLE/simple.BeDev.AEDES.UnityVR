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

        Debug.DrawRay(transform.position, transform.up * 1.2f, Color.red);
    }

    private void waterOut()
    {
        if (!isWaterActive) return;
        if (!Physics.Raycast(transform.position, transform.up, out RaycastHit hit, 1.2f, groundLayerMask)) return;

        isWaterActive = false;

        if (waterPrefab != null) waterPrefab.SetActive(false);
        if (_source != null && waterSplash != null) _source.PlayOneShot(waterSplash);

        // PF_VaseWithFlowers was serialised before this field existed, so it loads as null and the
        // old unguarded Instantiate threw on every tip-out.
        if (waterSplashEffect != null)
            Instantiate(waterSplashEffect, hit.point, Quaternion.LookRotation(hit.normal));

        if (M2Manager.Instance == null) return;
        M2Manager.Instance.UpdateScore(getScore);
        if (!isSave) M2Manager.Instance.NeutralizeBreedingSite(this);
    }
}
