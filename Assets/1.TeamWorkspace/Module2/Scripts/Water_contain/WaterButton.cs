using System.Linq;
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
    public bool isSave;

    private AudioSource _source;
    

    private void Start()
    {
        _source = GetComponent<AudioSource>();
        isWaterActive = true;
    }

    void Update()
    {
        waterOut();

        if (!isSave)
        {
            M2Manager.Instance.notSaveWaterContainer.ToArray();
        }
        else
        {
            M2Manager.Instance.saveWaterContainer.ToArray();
        }

        Debug.DrawRay(transform.position, transform.up * 1.2f, Color.red);
    }

    private void waterOut()
    {
        if (Physics.Raycast(transform.position, transform.up, out RaycastHit hit, 1.2f, groundLayerMask) && isWaterActive)
        {
            isWaterActive = false;
            waterPrefab.SetActive(false);
            _source.PlayOneShot(waterSplash);
            M2Manager.Instance.UpdateScore(getScore);
            Instantiate(waterSplashEffect, hit.point, Quaternion.LookRotation(hit.normal));
        }
    }
}