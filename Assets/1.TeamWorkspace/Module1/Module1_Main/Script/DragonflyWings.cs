using UnityEngine;

/// <summary>
/// A dragonfly's wing drone, louder and faster while it is chasing the player, so the threat can be
/// heard closing in. <see cref="Module1Sounds"/> adds it to every roaming dragonfly.
/// </summary>
public class DragonflyWings : MonoBehaviour
{
    AudioSource source;
    DengerSource hunter;
    float idleVolume, huntVolume, huntPitch;

    public void Init(Module1SoundBank bank)
    {
        hunter = GetComponent<DengerSource>();
        idleVolume = bank.DragonflyVolume;
        huntVolume = bank.DragonflyHuntVolume;
        huntPitch = bank.DragonflyHuntPitch;
        source = Module1Sounds.Make3D(gameObject, bank.MaxDistance);
        source.clip = bank.DragonflyWings;
        source.loop = true;
        source.volume = idleVolume;
        if (source.clip == null) return;
        // Each dragonfly starts at a different point in the recording so three of them do not phase.
        source.time = Random.Range(0f, source.clip.length);
        source.Play();
    }

    void Update()
    {
        if (source == null) return;
        bool hunting = hunter != null && hunter.inRange;
        source.volume = Mathf.MoveTowards(source.volume, hunting ? huntVolume : idleVolume, Time.deltaTime);
        source.pitch = Mathf.MoveTowards(source.pitch, hunting ? huntPitch : 1f, Time.deltaTime);
    }
}
