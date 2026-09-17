using NUnit.Framework.Interfaces;
using UnityEngine;

public class StereoSoundListener : MonoBehaviour
{
    public AudioSource[] Source;
    public float HearimgDistance;
    private void Start()
    {
        Source = FindObjectsOfType<AudioSource>();
    }
    private void Update()
    {
        foreach (AudioSource source in Source)
        {
            float dis = Vector3.Distance(transform.position, source.transform.position);
            if (dis < HearimgDistance)
            {
                source.enabled = true;
                source.volume = Mathf.InverseLerp(HearimgDistance, 0, dis);
                float dir = Vector3.Dot(transform.right, (source.transform.position - transform.position).normalized);
                source.panStereo = dir *= -1;
            }
            else if (dis >= HearimgDistance)
            {
                source.enabled = false;
            }
        }
    }
}