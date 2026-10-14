using System.Collections.Generic;
using UnityEngine;

// Hand-made stereo for the 2D sounds out in the world (the cars): louder the closer the mosquito is,
// panned to the side they are on. The player's own sounds (wing buzz on the camera, rain on the rig)
// are left alone.
public class StereoSoundListener : MonoBehaviour
{
    public AudioSource[] Source;
    public float HearimgDistance;

    // Each source's volume as authored: how loud it is right next to the player.
    private readonly Dictionary<AudioSource, float> _fullVolume = new Dictionary<AudioSource, float>();

    private void Start()
    {
        var world = new List<AudioSource>();
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (source.transform.IsChildOf(transform.root)) continue;
            // 3D sources (frogs, dragonflies) are placed by Unity itself.
            if (source.spatialBlend > 0f) continue;
            world.Add(source);
            _fullVolume[source] = source.volume;
        }
        Source = world.ToArray();
    }

    // Distance only sets the volume. Switching a source off out of range restarted a one-shot clip
    // every time the player came back, left it silent once it had played through, and fought
    // MovingCar, which switches a car's sound off while the car waits between passes.
    private void Update()
    {
        foreach (AudioSource source in Source)
        {
            if (source == null) continue;
            Vector3 toSource = source.transform.position - transform.position;
            float dis = toSource.magnitude;
            source.volume = _fullVolume[source] * Mathf.InverseLerp(HearimgDistance, 0, dis);
            // panStereo +1 is the right ear. This used to be negated, swapping left and right.
            if (dis > 0.001f) source.panStereo = Vector3.Dot(transform.right, toSource / dis);
        }
    }
}
