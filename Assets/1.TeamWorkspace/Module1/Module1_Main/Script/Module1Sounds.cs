using System.Collections;
using UnityEngine;

/// <summary>
/// Module 1's sounds beyond the team's wing buzz and cars: village ambience, rain that fades, frogs and
/// dragonflies, and feedback for drinking, mating, laying eggs, danger and death. Built from code so the
/// scene never has to change; clips and volumes come from <see cref="Module1SoundBank"/>.
///
/// Feedback is read off PlayerMain and GameManager every frame instead of being called from them, so
/// the team's scripts stay as they are. GameManager adds this component to itself in Awake.
/// </summary>
public class Module1Sounds : MonoBehaviour
{
    const string BankPath = "Module1/Module1SoundBank";
    /// <summary>How fast the feedback loops (sipping, drinking, heartbeat) fade in and out, in seconds.</summary>
    const float LoopFade = 0.15f;

    Module1SoundBank bank;
    GameManager gm;
    PlayerMain player;
    AudioSource ambience, rain, sip, drink, heartbeat, oneShots;

    float lastNectar, lastBlood;
    int lastEggs;
    bool wasMate, wasDead;

    void Start()
    {
        bank = Resources.Load<Module1SoundBank>(BankPath);
        gm = GameManager.instance;
        player = gm != null ? gm.player : null;
        if (bank == null || player == null) { enabled = false; return; }

        // 2D sounds ride on the head, inside the player rig, where StereoSoundListener leaves them alone.
        Transform head = player.mainCamera != null ? player.mainCamera.transform : player.transform;
        ambience = Make2D(head, "Ambience", bank.Ambience, true);
        sip = Make2D(head, "SipNectar", bank.SipNectar, true);
        drink = Make2D(head, "DrinkBlood", bank.DrinkBlood, true);
        heartbeat = Make2D(head, "Heartbeat", bank.Danger, true);
        oneShots = Make2D(head, "Feedback", null, false);
        // PlayOneShot scales by the source's volume, so this one stays at full; each sound brings its own.
        oneShots.volume = 1f;

        // The scene's rain sound sits on the Rain object, which GameManager switches on and off, so it
        // cut in and out at full volume. Take its clip and play it here instead, faded and quieter.
        AudioSource sceneRain = gm.Rain != null ? gm.Rain.GetComponentInChildren<AudioSource>(true) : null;
        rain = Make2D(head, "Rain", sceneRain != null ? sceneRain.clip : null, true);
        if (sceneRain != null) sceneRain.enabled = false;

        ambience.volume = bank.AmbienceVolume;
        if (ambience.clip != null)
        {
            // Start somewhere in the recording so every round does not open on the same bird.
            ambience.time = Random.Range(0f, ambience.clip.length * 0.9f);
            ambience.Play();
        }

        foreach (var danger in FindObjectsByType<DengerSource>(FindObjectsSortMode.None))
        {
            if (danger is DragonFlyRoaminh) danger.gameObject.AddComponent<DragonflyWings>().Init(bank);
            else danger.gameObject.AddComponent<FrogCroaks>().Init(bank);
        }

        lastNectar = player.Current_Nec;
        lastBlood = player.Current_Blood;
        lastEggs = player.EggLayed;
        wasMate = player.isMate;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool alive = !player.Death;

        // Rain fades with GameManager's rain window; the birds go quiet while it rains.
        FadeLoop(rain, gm.IsRain ? bank.RainVolume : 0f, bank.RainVolume / Mathf.Max(0.01f, bank.RainFade) * dt);
        float ambienceTarget = bank.AmbienceVolume * (gm.IsRain ? bank.AmbienceDuringRain : 1f);
        ambience.volume = Mathf.MoveTowards(ambience.volume, ambienceTarget, bank.AmbienceVolume * dt / 2f);

        // Feeding: the bars only go up while the player is drinking.
        bool sipping = alive && player.Current_Nec > lastNectar + 0.0001f;
        bool drinking = alive && player.Current_Blood > lastBlood + 0.0001f;
        FadeLoop(sip, sipping ? bank.SipNectarVolume : 0f, bank.SipNectarVolume * dt / LoopFade);
        FadeLoop(drink, drinking ? bank.DrinkBloodVolume : 0f, bank.DrinkBloodVolume * dt / LoopFade);
        if (lastBlood < player.Max_Blood && player.Current_Blood >= player.Max_Blood)
            Play(bank.BloodFull, bank.BloodFullVolume);

        if (player.isMate && !wasMate) Play(bank.Mate, bank.MateVolume);
        if (player.EggLayed > lastEggs) Play(bank.LayEgg, bank.LayEggVolume);

        bool inDanger = alive && gm.DangerUI != null && gm.DangerUI.activeSelf;
        FadeLoop(heartbeat, inDanger ? bank.DangerVolume : 0f, bank.DangerVolume * dt / 0.3f);

        if (player.Death && !wasDead) StartCoroutine(Died(starved: player.Current_Nec <= 0f));

        lastNectar = player.Current_Nec;
        lastBlood = player.Current_Blood;
        lastEggs = player.EggLayed;
        wasMate = player.isMate;
        wasDead = player.Death;
    }

    IEnumerator Died(bool starved)
    {
        if (!starved)
        {
            Play(bank.DeathHit, bank.DeathHitVolume);
            yield return new WaitForSeconds(0.25f);
        }
        Play(bank.DeathSting, bank.DeathStingVolume);
    }

    void Play(AudioClip clip, float volume)
    {
        if (clip != null) oneShots.PlayOneShot(clip, volume);
    }

    /// <summary>Moves a looping source towards a volume, starting it on the way up and stopping it at silence.</summary>
    static void FadeLoop(AudioSource s, float target, float step)
    {
        if (s.clip == null) return;
        s.volume = Mathf.MoveTowards(s.volume, target, step);
        if (s.volume > 0f && !s.isPlaying) s.Play();
        else if (s.volume <= 0f && s.isPlaying) s.Stop();
    }

    static AudioSource Make2D(Transform parent, string name, AudioClip clip, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        return s;
    }

    /// <summary>
    /// A sound out in the world that Unity places itself: louder near, from the side it is on, silent
    /// beyond <paramref name="maxDistance"/> (the same 20 m as StereoSoundListener's cars).
    /// </summary>
    public static AudioSource Make3D(GameObject host, float maxDistance)
    {
        var s = host.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1f;
        s.maxDistance = maxDistance;
        // These creatures barely move; doppler only warbles the dragonfly as it turns.
        s.dopplerLevel = 0f;
        return s;
    }
}
