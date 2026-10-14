using System.Collections;
using UnityEngine;

/// <summary>
/// A frog croaking now and then, in short bouts of one to three croaks, so the player hears where the
/// frogs (predators) are before seeing them. <see cref="Module1Sounds"/> adds it to every frog.
/// </summary>
public class FrogCroaks : MonoBehaviour
{
    public AudioClip[] Clips;
    public float Volume = 0.35f;
    public Vector2 Interval = new Vector2(3f, 9f);

    AudioSource source;

    public void Init(Module1SoundBank bank)
    {
        Clips = bank.FrogCroaks;
        Volume = bank.FrogVolume;
        Interval = bank.FrogInterval;
        source = Module1Sounds.Make3D(gameObject, bank.MaxDistance);
    }

    IEnumerator Start()
    {
        if (source == null || Clips == null || Clips.Length == 0) yield break;
        // Out of step with the other frogs from the first croak.
        yield return new WaitForSeconds(Random.Range(0f, Interval.y));
        while (true)
        {
            int croaks = Random.Range(1, 4);
            for (int i = 0; i < croaks; i++)
            {
                source.pitch = Random.Range(0.9f, 1.1f);
                source.PlayOneShot(Clips[Random.Range(0, Clips.Length)], Volume);
                yield return new WaitForSeconds(Random.Range(0.35f, 0.6f));
            }
            yield return new WaitForSeconds(Random.Range(Interval.x, Interval.y));
        }
    }
}
