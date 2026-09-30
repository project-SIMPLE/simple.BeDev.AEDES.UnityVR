using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class M2Manager : MonoBehaviour
{
    public static M2Manager Instance { get; private set; }

    public enum RoundEndReason { TimeOut, AllSitesCleared, BitesReached }

    [Header("Reference")]
    public TextMeshProUGUI[] socreText;
    public TextMeshProUGUI timerText;
    public GameObject flySwatterPrefabs;
    public GameObject creamPrefabs;
    public Transform pointFontPlayer;
    public GameObject particleSpwn;
    private static readonly int Exposure = Shader.PropertyToID("_Exposure");

    [Header("Reference Canvas")]
    [Tooltip("Legacy scene game-over panel. Only used when there is no Module2HUD.")]
    public GameObject gameOver;

    [Header("Input System")]
    [Tooltip("Primary confirm action. Kept, but no longer the only way to confirm - it fires only intermittently on the headset.")]
    public InputActionReference aButton;
    [Tooltip("Seconds of briefing before the round starts on its own, so a confirm that never arrives cannot soft-lock the game. 0 disables.")]
    public float introAutoStartSeconds = 6f;
    [Tooltip("Ignore confirm presses for this long after the briefing appears, so a button already held does not skip it.")]
    public float introArmDelay = 0.75f;
    [Tooltip("Ignore restart presses for this long after the round ends, so whatever was in your hand cannot reload the scene.")]
    public float endArmDelay = 1.5f;
    [Tooltip("Show a live input readout on the briefing. Useful on-headset diagnostics; turn off for a real build.")]
    public bool showInputDebug = true;

    [Header("Game Setting")]
    public int score = 0;
    public float timer = 180f;
    public int isOver;
    [Tooltip("Awarded when every breeding site has been dealt with before the timer runs out.")]
    public int completionBonus = 100;
    [Tooltip("Points per swatted adult. Small on purpose: the master design keeps clearing the adult swarm as an M2 mechanic, but bite count is secondary and source reduction must stay the winning strategy (A8, B2 KPIs).")]
    public int swatScore = 1;
    [Tooltip("Breeding sites used per round, drawn at random from those in the scene. Keeps a round inside a 2-minute slot and makes repeat plays differ.")]
    public int sitesPerRound = 6;
    [Tooltip("Seconds of extra guidance at the start of a round, ending early once the player grabs anything. Module 1 is flown with a stick and never teaches grabbing, so Module 2 is the first time a child is asked to pick something up.")]
    public float onboardingSeconds = 12f;
    [Tooltip("Hold the timer until the player dismisses the HUD intro card.")]
    public bool showIntro = true;

    [Header("Relay triggers (master design B4)")]
    [Tooltip("Bites that end the pilot's stint. B4: relay trigger is 10 bites or the 3-minute action window.")]
    public int biteLimit = 10;
    [Tooltip("Energy lost per bite. INTERPRETATION: B2 names an Energy bar but does not specify its economy.")]
    public float energyPerBite = 0.12f;
    [Tooltip("Energy regained per second while not being bitten. INTERPRETATION: B2 Scene 3 says 'rest to restore Energy'; there is no rest station in the scene yet.")]
    public float energyRegenPerSecond = 0.02f;
    [Tooltip("Seconds without a bite before energy starts coming back.")]
    public float energyRegenDelay = 4f;
    [Tooltip("Seconds after a bite during which further bites are ignored. Mosquitoes emerge from the very containers the player is working on, so without this a swarm landed 8 bites in 5 seconds while a jar was being covered and ended the round in under 30 seconds.")]
    public float biteGracePeriod = 5f;

    [Header("Repellent")]
    [Tooltip("Seconds of bite protection from one squeeze of repellent cream onto a hand.")]
    public float repellentDuration = 30f;

    [Header("Day Night System")]
    public Light sun;
    public Material skybox;
    public Gradient lightColor;
    public AnimationCurve lightIntensity;
    [Tooltip("Seconds of daylight. Clamped to the round length; half the round if unset or too large.")]
    public float dayDuration = 120.0f;
    public bool isDay, isNight;

    public Module2HUD hud { get; private set; }

    // Breeding sites (jars, open containers, fish bowls) register themselves in their own Start
    // and report back when they are dealt with. Clearing them all ends the round early.
    private readonly HashSet<Component> _openSites = new HashSet<Component>();
    private int _siteCount;

    // Set just before RestartGame reloads the scene, so a retry drops straight back into play
    // instead of making the player read the briefing again. Statics survive a scene load.
    private static bool s_skipIntroOnLoad;

    private bool _started;
    private bool _ended;

    // Confirm input. The serialized aButton action (map "Phettae", action "A Button", bound to
    // <XRController>{RightHand}/{PrimaryButton}) fires only intermittently on the headset, and with
    // the briefing gated on it that intermittency became a soft-lock. So accept a confirm from any
    // plausible source: the Input System actions, and the legacy UnityEngine.XR device API that
    // Module 1's PlayerMain drives its whole game from on this same headset.
    private InputAction[] _introActions;
    private InputAction[] _restartActions;
    private int _introFrame = -1, _restartFrame = -1;
    private bool _introPressed, _restartPressed;
    private bool _legacyAnyLastFrame, _legacyPrimaryLastFrame;
    private float _introShownAt = -1f;
    private float _endShownAt = -1f;
    private int _gamaProjection = -1;
    private bool _subsetChosen;
    private float _roundStartedAt = -1f;
    private float _lastBiteAt = -999f;
    private float _repellentUntil = -1f;
    private bool _repellentWasActive;

    /// <summary>True once the player has picked anything up, which ends the guidance early.</summary>
    public bool HasGrabbed { get; private set; }
    public void NoteFirstGrab() { HasGrabbed = true; }

    /// <summary>Show the extra start-of-round guidance.</summary>
    public bool Onboarding =>
        IsPlaying && !HasGrabbed && _roundStartedAt >= 0f && Time.time - _roundStartedAt < onboardingSeconds;
    private static readonly List<UnityEngine.XR.InputDevice> s_devices = new List<UnityEngine.XR.InputDevice>();

    /// <summary>Live input state, shown on the briefing while showInputDebug is on.</summary>
    public string ConfirmDebug { get; private set; } = "";
    private float _roundLength;
    private float _dayLength;
    private float _skyboxExposureOnLoad;
    private bool _hasSkyboxExposure;

    // ---- state the HUD reads ----
    public bool IsPlaying => _started && !_ended;
    public bool RoundStarted => _started;
    public bool RoundEnded => _ended;
    public RoundEndReason EndReason { get; private set; }
    public int MosquitoesSwatted { get; private set; }
    public int BiteCount { get; private set; }
    public int BiteLimit => Mathf.Max(biteLimit, 1);
    /// <summary>True while repellent protects the player: bites are ignored and mosquitoes veer away.</summary>
    public bool RepellentActive => IsPlaying && Time.time < _repellentUntil;
    public float RepellentRemaining => RepellentActive ? _repellentUntil - Time.time : 0f;
    public int RepellentApplications { get; private set; }

    /// <summary>Energy, 0..1. Drains on bites and comes back after a spell without one.</summary>
    public float Energy { get; private set; } = 1f;

    /// <summary>
    /// The module's exported metric. B2's GAMA hook and A8's "Community Outcome" are both this
    /// number, and it is what squads are ranked by at debrief - not the action score.
    /// Feeds Outbreak_Intensity = (100 - Source_Reduction_Percent) x Population_Density_Factor.
    /// </summary>
    public int SourceReductionPercent =>
        _siteCount > 0 ? Mathf.RoundToInt(100f * SitesCleared / _siteCount) : 0;
    public int TrashBinned { get; private set; }
    public int SiteCount => _siteCount;
    public int OpenSiteCount => _openSites.Count;
    public int SitesCleared => _siteCount - _openSites.Count;
    public IReadOnlyCollection<Component> OpenSites => _openSites;
    public float TimeRemaining => Mathf.Max(timer, 0f);
    public float RoundLength => _roundLength;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        isOver = 1;

        // Deliberately NOT DontDestroyOnLoad: RestartGame reloads this scene, and a manager that
        // survived the reload kept running a second timer and day/night cycle against the old
        // scene's objects while the fresh one took over Instance.

        hud = GetComponent<Module2HUD>();
        if (hud == null) hud = gameObject.AddComponent<Module2HUD>();

        if (GetComponent<Module2Interaction>() == null) gameObject.AddComponent<Module2Interaction>();
    }

    void Start()
    {
        isDay = true;
        isNight = false;

        _roundLength = Mathf.Max(timer, 0.0001f);
        // dayDuration >= the round length would mean night never arrives, so fall back to halfway.
        _dayLength = (dayDuration > 0f && dayDuration < _roundLength) ? dayDuration : _roundLength * 0.5f;

        if (skybox != null && skybox.HasProperty(Exposure))
        {
            _skyboxExposureOnLoad = skybox.GetFloat(Exposure);
            _hasSkyboxExposure = true;
        }

        if (gameOver != null) gameOver.SetActive(false);

        BuildConfirmActions();

        bool skipIntro = s_skipIntroOnLoad;
        s_skipIntroOnLoad = false;

        if (hud != null)
        {
            // The runtime HUD supersedes the labels baked into the player prefab.
            hud.HideLegacyLabels(socreText, timerText);
            if (showIntro && !skipIntro)
            {
                hud.ShowIntro();
                _introShownAt = Time.time;
            }
        }

        RefreshScoreText();

        // With no HUD to dismiss the intro, or on a retry, play starts at once.
        if (hud == null || !showIntro || skipIntro) BeginRound();
    }

    private void OnDestroy()
    {
        // The skybox is a shared project asset: leave its exposure as we found it so the value
        // does not drift between runs and show up as a stray change in version control.
        if (_hasSkyboxExposure && skybox != null) skybox.SetFloat(Exposure, _skyboxExposureOnLoad);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Sites register in their own Start, so the subset can only be chosen once those have all run.
        if (!_subsetChosen) { _subsetChosen = true; ChooseSiteSubset(); }

        StartTimer();
        DayNightSystem();
        TickEnergy();
        TickRepellent();
        AutoStartIfStuck();
        RestartGame();
    }

    // Last resort: if no confirm ever arrives, start anyway rather than leave the player stranded on
    // the briefing with no timer and no mosquitoes.
    private void AutoStartIfStuck()
    {
        if (_started || _introShownAt < 0f || introAutoStartSeconds <= 0f) return;
        if (Time.time - _introShownAt < introAutoStartSeconds) return;

        Debug.LogWarning("[M2] No confirm press seen - starting the round automatically.");
        BeginRound();
    }

    #region Confirm input
    // Two deliberately different sets.
    //
    // The briefing can be dismissed by almost anything - there is nothing to lose before the round
    // starts, and the A button only fires intermittently on this headset.
    //
    // Restart must NOT be that permissive. Accepting Select (grip) and Activate (trigger) meant the
    // next natural grab after the round ended instantly reloaded the scene, and "XRI UI/Submit" is
    // bound to */{Submit} - a wildcard across every device - which let a thumbstick reach it too.
    // Restart now only listens for a primary button (A / X), which nobody presses by accident.
    private void BuildConfirmActions()
    {
        var intro = new List<InputAction>();
        var restart = new List<InputAction>();

        if (aButton != null && aButton.action != null)
        {
            intro.Add(aButton.action);
            restart.Add(aButton.action);
        }

        var asset = aButton != null && aButton.action != null && aButton.action.actionMap != null
            ? aButton.action.actionMap.asset
            : null;

        if (asset != null)
        {
            // Grip and trigger, both hands. Briefing only - never restart.
            string[] paths =
            {
                "XRI Right Interaction/Select",
                "XRI Left Interaction/Select",
                "XRI Right Interaction/Activate",
                "XRI Left Interaction/Activate",
            };
            foreach (var path in paths)
            {
                var action = asset.FindAction(path, false);
                if (action != null && !intro.Contains(action)) intro.Add(action);
            }
        }

        _introActions = intro.ToArray();
        _restartActions = restart.ToArray();
        Debug.Log($"[M2] Confirm sources - briefing: {_introActions.Length} action(s) + any controller button; restart: {_restartActions.Length} action(s) + primary button only.");
    }

    /// <summary>Broad confirm for the briefing. Frame-cached so several callers can ask.</summary>
    public bool IntroConfirmPressed()
    {
        if (Time.frameCount != _introFrame)
        {
            _introFrame = Time.frameCount;
            _introPressed = ActionFired(_introActions)
                            | EdgeOf(LegacyHeld(AnyButton, out string detail), ref _legacyAnyLastFrame)
                            | KeyboardConfirm();
            if (showInputDebug) ConfirmDebug = $"actions:{(ActionFired(_introActions) ? 1 : 0)}  {detail}";
        }
        return _introPressed;
    }

    /// <summary>Narrow confirm for restart: a primary button press, nothing incidental.</summary>
    public bool RestartConfirmPressed()
    {
        if (Time.frameCount != _restartFrame)
        {
            _restartFrame = Time.frameCount;
            _restartPressed = ActionFired(_restartActions)
                              | EdgeOf(LegacyHeld(PrimaryOnly, out _), ref _legacyPrimaryLastFrame)
                              | KeyboardConfirm();
        }
        return _restartPressed;
    }

    private static bool ActionFired(InputAction[] actions)
    {
        if (actions == null) return false;
        foreach (var action in actions)
            if (action != null && action.enabled && action.WasPerformedThisFrame()) return true;
        return false;
    }

    private static bool EdgeOf(bool held, ref bool lastFrame)
    {
        bool edge = held && !lastFrame;
        lastFrame = held;
        return edge;
    }

    private static bool KeyboardConfirm()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
    }

    private const int AnyButton = 0;
    private const int PrimaryOnly = 1;

    // The legacy UnityEngine.XR device API, which Module 1's PlayerMain drives its whole game from on
    // this same headset. It reports current state, so callers edge-detect.
    private static bool LegacyHeld(int mode, out string detail)
    {
        detail = "";
        bool any = false;

        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Controller, s_devices);

        for (int i = 0; i < s_devices.Count; i++)
        {
            var device = s_devices[i];
            if (!device.isValid) continue;

            bool primary = false, trigger = false, grip = false;
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out primary);
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out trigger);
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out grip);

            bool right = (device.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Right) != 0;
            detail += $"[{(right ? "R" : "L")} a:{(primary ? 1 : 0)} t:{(trigger ? 1 : 0)} g:{(grip ? 1 : 0)}]";

            if (mode == PrimaryOnly ? primary : (primary || trigger || grip)) any = true;
        }

        if (s_devices.Count == 0) detail = "no controllers seen";
        return any;
    }
    #endregion

    #region Round lifecycle
    /// <summary>True once the briefing has been on screen long enough to accept a confirm.</summary>
    public bool IntroArmed => _introShownAt < 0f || Time.time - _introShownAt >= introArmDelay;

    public void BeginRound()
    {
        if (_started) return;
        _started = true;
        _roundStartedAt = Time.time;
        if (hud != null)
        {
            hud.HideIntro();
            hud.Toast(Module2Text.StartToast);
        }
    }

    private void EndRound(RoundEndReason reason)
    {
        if (_ended) return;
        _ended = true;
        _started = true;
        isOver = 0;
        EndReason = reason;

        if (reason == RoundEndReason.AllSitesCleared) AddScore(completionBonus);

        _endShownAt = Time.time;

        if (hud != null) hud.ShowEnd(reason, score, SitesCleared, _siteCount, MosquitoesSwatted, TrashBinned, SourceReductionPercent, BiteCount, ProjectionFromSimulation);
        else if (gameOver != null) gameOver.SetActive(true);
    }

    /// <summary>Kept for existing callers; ends the round as a time-out.</summary>
    public void ShowGameOverPanel()
    {
        EndRound(RoundEndReason.TimeOut);
    }

    /// <summary>Called by a mosquito that reaches the player. Ends the stint at biteLimit.</summary>
    public void NoteBite()
    {
        if (!IsPlaying) return;
        if (RepellentActive) return;
        if (Time.time - _lastBiteAt < Mathf.Max(biteGracePeriod, 0f)) return;

        BiteCount++;
        _lastBiteAt = Time.time;
        Energy = Mathf.Clamp01(Energy - Mathf.Max(energyPerBite, 0f));

        if (hud != null) hud.OnBitten(BiteCount, BiteLimit);

        if (BiteCount >= BiteLimit) EndRound(RoundEndReason.BitesReached);
    }

    /// <summary>Called by the cream tube when it is squeezed onto a hand. Re-applying refreshes it.</summary>
    public void ApplyRepellent()
    {
        if (!IsPlaying) return;
        _repellentUntil = Time.time + Mathf.Max(repellentDuration, 0f);
        _repellentWasActive = true;
        RepellentApplications++;
        if (hud != null) hud.Toast(Module2Text.RepellentOn(Mathf.RoundToInt(repellentDuration)));
    }

    private void TickRepellent()
    {
        if (!_repellentWasActive || RepellentActive) return;
        _repellentWasActive = false;
        if (IsPlaying && hud != null) hud.Toast(Module2Text.RepellentWornOff);
    }

    private void TickEnergy()
    {
        if (!IsPlaying) return;
        if (Time.time - _lastBiteAt < energyRegenDelay) return;
        Energy = Mathf.Clamp01(Energy + energyRegenPerSecond * Time.deltaTime);
    }

    public void NoteMosquitoSwatted()
    {
        if (_ended) return;
        MosquitoesSwatted++;
        // Scoring lives here rather than on the swatter so the value is decided in one place.
        if (swatScore != 0) UpdateScore(swatScore);
    }

    /// <summary>
    /// True once GAMA has acknowledged this round's source-reduction figure, so the debrief can stop
    /// hedging. The number itself is SourceReductionPercent either way - the module no longer invents
    /// a mosquito projection, because A5 defines the export as a percentage.
    /// </summary>
    public bool ProjectionFromSimulation => _gamaProjection >= 0;

    /// <summary>Called when GAMA confirms it has taken the figure. Negative clears it.</summary>
    public void SetSimulationProjection(int value) { _gamaProjection = value; }
    public void NoteTrashBinned()
    {
        if (_ended) return;
        TrashBinned++;
        if (hud != null) hud.Toast(Module2Text.TrashBinned);
    }
    #endregion

    #region UI interaction
    // Each poke used to spawn another swatter, each on its own two-second self-destruct timer, so
    // repeated presses littered the room. The guard the live path was missing already existed in the
    // dead UIHand.SpawnSwatter.
    public void FlySwatterUi()
    {
        // A racket that is being held blocks a second one. One that has been put down does not: it may be
        // out of reach, or under the furniture, and the button is the player's only way to get a swatter.
        var existing = FindObjectsByType<SwatterHand>(FindObjectsSortMode.None);
        foreach (var racket in existing)
        {
            if (!racket.Held) continue;
            if (hud != null) hud.Toast(Module2Text.SwatterAlreadyOut);
            return;
        }
        foreach (var racket in existing) Destroy(racket.gameObject);

        if (flySwatterPrefabs != null) Instantiate(flySwatterPrefabs, pointFontPlayer);
        if (particleSpwn != null) Instantiate(particleSpwn, pointFontPlayer);
        if (hud != null) hud.Toast(Module2Text.SwatterSpawned);
    }

    public void CreamUi()
    {
        if (FindAnyObjectByType<Cream>() != null)
        {
            if (hud != null) hud.Toast(Module2Text.CreamAlreadyOut);
            return;
        }

        if (creamPrefabs != null) Instantiate(creamPrefabs, pointFontPlayer);
        if (particleSpwn != null) Instantiate(particleSpwn, pointFontPlayer);
        if (hud != null) hud.Toast(Module2Text.CreamSpawned);
    }

    /// <summary>Award points for a player action. Ignored once the round is over, so what the
    /// player does after the debrief appears cannot change the numbers it shows.</summary>
    public void UpdateScore(int value)
    {
        if (_ended) return;
        AddScore(value);
    }

    private void AddScore(int value)
    {
        score += value;
        RefreshScoreText();
    }

    private void RefreshScoreText()
    {
        if (socreText == null) return;
        for (int i = 0; i < socreText.Length; i++)
            if (socreText[i] != null) socreText[i].text = "Score: " + score;
    }
    #endregion

    #region Breeding sites
    // A round uses a random handful of the sites in the scene rather than all of them. Two reasons:
    // a 2-minute slot cannot support nine, and a class of children each seeing a different subset
    // collectively explores the whole house instead of all learning the same nine positions.
    private void ChooseSiteSubset()
    {
        if (sitesPerRound <= 0 || _openSites.Count <= sitesPerRound) return;

        var pool = new List<Component>(_openSites);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        for (int i = sitesPerRound; i < pool.Count; i++)
        {
            var site = pool[i];
            _openSites.Remove(site);
            _siteCount--;
            if (site != null) site.gameObject.SetActive(false);
        }

        Debug.Log($"[M2] Using {_siteCount} of the scene's breeding sites this round.");
    }

    public void RegisterBreedingSite(Component site)
    {
        if (site == null) return;
        if (_openSites.Add(site)) _siteCount++;
    }

    public void NeutralizeBreedingSite(Component site)
    {
        if (_ended || site == null || !_openSites.Remove(site)) return;

        if (hud != null) hud.OnSiteCleared(site, _siteCount - _openSites.Count, _siteCount);
        if (_openSites.Count == 0 && IsPlaying) EndRound(RoundEndReason.AllSitesCleared);
    }
    #endregion

    #region Timer Control
    private void StartTimer()
    {
        if (!IsPlaying) return;

        timer -= Time.deltaTime;
        if (timerText != null) timerText.text = "Time: " + Mathf.Round(Mathf.Max(timer, 0f));

        if (timer <= 0f)
        {
            timer = 0f;
            EndRound(RoundEndReason.TimeOut);
        }
    }

    private void DayNightSystem()
    {
        // Progress is measured against the round length captured at Start, so the cycle no longer
        // depends on the timer happening to be seeded with one particular value. The timer does not
        // move until the round begins, so the sun holds still behind the intro card.
        float elapsed = _roundLength - timer;
        float dayProgress = Mathf.Clamp01(elapsed / _roundLength);

        if (sun != null)
        {
            sun.color = lightColor.Evaluate(dayProgress);
            sun.intensity = lightIntensity.Evaluate(dayProgress);
            sun.transform.localRotation = Quaternion.Euler(dayProgress * 360f + 120f, 30f, 0f);
        }

        // Was Evaluate(dayProgress * 0.05f), which only ever sampled the first 5% of the curve
        // and left the skybox at a fixed exposure for the whole round.
        if (skybox != null && _hasSkyboxExposure) skybox.SetFloat(Exposure, lightIntensity.Evaluate(dayProgress));

        if (isDay && elapsed >= _dayLength)
        {
            isDay = false;
            isNight = true;
            if (hud != null) hud.Toast(Module2Text.NightFalls);
        }
    }
    #endregion

    #region Restart
    public void RestartGame()
    {
        if (!_ended) return;

        if (_endShownAt >= 0f && Time.time - _endShownAt < endArmDelay) return;

        if (RestartConfirmPressed())
        {
            int index = SceneManager.GetActiveScene().buildIndex;
            Debug.Log($"[M2] Restart pressed - reloading scene index {index}");
            s_skipIntroOnLoad = true;
            // Reloading the scene rebuilds the manager and its references; no need to reset
            // fields on an instance that is about to be destroyed.
            SceneManager.LoadScene(index);
        }
    }
    #endregion
}
