using UnityEngine;

/// <summary>
/// Every Module 1 sound that <see cref="Module1Sounds"/> plays, with its volume, in one place so the mix
/// can be balanced without touching the scene. The team's original three sounds (wing buzz, cars) stay
/// where they were; the rain clip is taken over from the scene so it can fade.
///
/// All clips are CC0 from Freesound - see Team Assets/Sound/Module1/CREDITS.md.
/// Lives at Resources/Module1/Module1SoundBank.
/// </summary>
[CreateAssetMenu(menuName = "AEDES/Module 1 Sound Bank", fileName = "Module1SoundBank")]
public class Module1SoundBank : ScriptableObject
{
    [Header("Atmosphere (2D, on the player)")]
    public AudioClip Ambience;
    [Range(0f, 1f)] public float AmbienceVolume = 0.4f;
    [Tooltip("Ambience level while it rains, as a fraction of AmbienceVolume: the birds go quiet.")]
    [Range(0f, 1f)] public float AmbienceDuringRain = 0.5f;
    [Range(0f, 1f)] public float RainVolume = 0.15f;
    [Tooltip("Seconds the rain takes to fade in or out.")]
    public float RainFade = 2.5f;

    [Header("Creatures (3D, fade to silence at MaxDistance like the cars)")]
    public float MaxDistance = 20f;
    public AudioClip[] FrogCroaks;
    [Range(0f, 1f)] public float FrogVolume = 0.35f;
    [Tooltip("Seconds between a frog's bouts of croaking.")]
    public Vector2 FrogInterval = new Vector2(3f, 9f);
    public AudioClip DragonflyWings;
    [Range(0f, 1f)] public float DragonflyVolume = 0.6f;
    [Tooltip("Wing volume while the dragonfly is chasing the player.")]
    [Range(0f, 1f)] public float DragonflyHuntVolume = 1f;
    [Tooltip("Wing pitch while chasing: faster beats.")]
    public float DragonflyHuntPitch = 1.25f;

    [Header("Feedback (2D, on the player)")]
    public AudioClip SipNectar;
    [Range(0f, 1f)] public float SipNectarVolume = 0.16f;
    public AudioClip DrinkBlood;
    [Range(0f, 1f)] public float DrinkBloodVolume = 0.35f;
    public AudioClip BloodFull;
    [Range(0f, 1f)] public float BloodFullVolume = 0.5f;
    public AudioClip Mate;
    [Range(0f, 1f)] public float MateVolume = 0.32f;
    public AudioClip LayEgg;
    [Range(0f, 1f)] public float LayEggVolume = 0.6f;
    public AudioClip Danger;
    [Range(0f, 1f)] public float DangerVolume = 0.25f;
    [Tooltip("Eaten or run over. Not played when the mosquito starves.")]
    public AudioClip DeathHit;
    [Range(0f, 1f)] public float DeathHitVolume = 0.5f;
    public AudioClip DeathSting;
    [Range(0f, 1f)] public float DeathStingVolume = 0.32f;
}
