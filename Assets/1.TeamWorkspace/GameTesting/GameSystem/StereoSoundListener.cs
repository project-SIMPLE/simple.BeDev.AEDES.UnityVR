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
            float dir = Vector3.Dot(transform.right, (source.transform.position - transform.position).normalized);
            source.panStereo = dir*=-1;
            float dis = Vector3.Distance(transform.position, source.transform.position);
            source.volume = Mathf.InverseLerp(HearimgDistance, 0, dis);
        }
    }
}
