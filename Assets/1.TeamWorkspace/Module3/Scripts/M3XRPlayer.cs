using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// The Pilot in the headset. Spawns the XR Interaction Toolkit starter rig at the head of the
/// lane and sets up what it needs, replacing the scene's plain desktop camera.
///
/// Controls:
///   Right stick          walk (forward is where you look)
///   Left stick           turn left/right, smoothly
///   Point + trigger      talk to a villager, press a button on a panel
///   Grip (reach out)     talk to a villager within arm's reach
///   A                    "I'm ready" at the start of a turn (also the A key on a keyboard)
///   Left menu button     end your turn early (asks first)
///
/// Also the turn's gatekeeper: M3Session holds each turn's clock until the incoming Pilot, now in
/// the headset, presses A.
/// </summary>
public class M3XRPlayer : MonoBehaviour
{
    [SerializeField] private GameObject rigPrefab;
    [Tooltip("Where the Pilot stands at the start of the session: the head of the lane.")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(-8f, 0f, 8f);
    [SerializeField] private float spawnYaw = 90f;
    [Tooltip("Walking pace with the right stick, metres per second. The starter rig ships at 2.5, a jog, "
             + "which is uncomfortable in a headset - especially for children - and overshoots doorways.")]
    [SerializeField] private float walkSpeed = 1.4f;
    [Tooltip("How fast the left stick turns the Pilot, degrees per second.")]
    [SerializeField] private float turnSpeed = 60f;
    [Tooltip("Left-stick travel that counts as no input. The starter rig's Turn action has no dead zone "
             + "(only Move has one), so a stick that rests a little off centre - common on a well-used "
             + "loaner controller - slowly spun the Pilot round. Scaled past it, so a full push still turns at turnSpeed.")]
    [Range(0f, 0.6f)]
    [SerializeField] private float turnDeadzone = 0.3f;
    [Tooltip("Tallest the body capsule may be. Keeps the body clear of low beams and eaves for a tall "
             + "wearer; the doorways themselves are 2.1 m clear, so nobody needs to duck.")]
    [SerializeField] private float maxBodyHeight = 1.6f;
    [Tooltip("How high the body steps over a ledge. The character controller sweeps this far UP before "
             + "stepping forward, so at the stock 0.5 m it can hit a doorway's lintel and refuse to enter. "
             + "The doorstep is 0.26 m, so 0.3 clears it.")]
    [SerializeField] private float stepOffset = 0.3f;

    public static M3XRPlayer Instance { get; private set; }

    /// <summary>Editor-only switch (AEDES > Module 3 > Use Desktop Camera) that skips the XR rig.</summary>
    public const string DesktopCameraPref = "AEDES.Module3.UseDesktopCamera";
    public GameObject Rig { get; private set; }
    public Transform Head => Camera.main != null ? Camera.main.transform : null;

    /// <summary>True between the "end your turn?" question and its answer.</summary>
    public bool ConfirmingEndTurn { get; private set; }

    private InputAction ready, menu, no;
    private InputAction turnStick;
    private ContinuousTurnProvider turnProvider;

    private void Awake()
    {
        Instance = this;
        // The buttons come first. Everything below (the desktop-camera early return, the rig set-up)
        // used to sit ahead of them, so a Pilot could be left with a prompt that says "press A" and
        // nothing listening whenever any of that returned early or threw.
        CreateInputs();
#if UNITY_EDITOR
        // Working on the scene without a headset: keep the plain camera and the keyboard driver.
        if (UnityEditor.EditorPrefs.GetBool(DesktopCameraPref, false)) return;
#endif
        if (rigPrefab == null)
        {
            Debug.LogError("Module 3: M3XRPlayer has no rig prefab - staying on the desktop camera.");
            return;
        }

        // The scene's desktop camera stands down; the rig brings its own tracked one.
        foreach (var c in GetComponents<Camera>()) c.enabled = false;
        foreach (var l in GetComponents<AudioListener>()) l.enabled = false;
        gameObject.tag = "Untagged";

        EnsureInteractionManager();
        Rig = Instantiate(rigPrefab, spawnPosition, Quaternion.Euler(0f, spawnYaw, 0f));
        Rig.name = "XR Origin (Module 3 Pilot)";
        SetUpSticks();

        // The starter rig brings locomotion this module does not want. A is "ready" and "yes" here,
        // and the rig binds it to a 1.25 m JUMP (a hop in a headset is nauseating, and it happened
        // the moment a Pilot pressed ready); grip is how you reach for a villager, and the rig binds
        // it to grab-move, which would drag the Pilot along; nothing here is climbable.
        foreach (var provider in Rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true))
        {
            string kind = provider.GetType().Name;
            if (kind == "JumpProvider" || kind == "GrabMoveProvider" || kind == "TwoHandedGrabMoveProvider" || kind == "ClimbProvider")
                provider.enabled = false;
        }

        // Stand on the real floor. The starter rig leaves this unspecified, which falls back to a fixed
        // 1.36 m camera offset instead of the wearer's own height - and doorways, panel heights and the
        // hand positions all assume true standing height.
        var origin = Rig.GetComponent<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
            origin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;

        var controller = Rig.GetComponentInChildren<CharacterController>(true);
        if (controller != null) controller.stepOffset = stepOffset;

        var body = Rig.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.XRBodyTransformer>(true);
        if (body != null)
        {
            var capped = ScriptableObject.CreateInstance<M3CappedBodyManipulator>();
            capped.maxCapsuleHeight = maxBodyHeight;
            body.constrainedBodyManipulator = capped;
        }
        EnsureEventSystem();
    }

    /// <summary>
    /// Right stick walks, left stick turns, and nothing teleports.
    ///
    /// The starter rig splits the sticks the other way round: the left walks, and the right snap-turns
    /// 45 degrees on a flick, spins 180 degrees when pulled back and aims a teleport when pushed
    /// forward - which is what playtesting called the right stick "sending me to a random view or
    /// location". Each controller's ControllerInputActionManager switches those actions on and off
    /// again as the hand points at UI, and it can only give a hand either walking or teleport-and-turn,
    /// never turning alone, so both are switched off here (before their Start) and the providers read
    /// the one stick each is meant to. The rig's InputActionManager keeps every action enabled.
    /// </summary>
    private void SetUpSticks()
    {
        foreach (var manager in Rig.GetComponentsInChildren<MonoBehaviour>(true))
            if (manager.GetType().Name == "ControllerInputActionManager") manager.enabled = false;

        foreach (var move in Rig.GetComponentsInChildren<ContinuousMoveProvider>(true))
        {
            move.moveSpeed = walkSpeed;
            move.leftHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
        }
        foreach (var turn in Rig.GetComponentsInChildren<ContinuousTurnProvider>(true))
        {
            turn.enabled = true;
            turn.turnSpeed = turnSpeed;
            turn.enableTurnAround = false;
            turn.rightHandTurnInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
            // The left stick is read here, through a dead zone, and handed to the provider each frame.
            turn.leftHandTurnInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            turnProvider = turn;
        }
        foreach (var snap in Rig.GetComponentsInChildren<SnapTurnProvider>(true))
            snap.enabled = false;

        foreach (var teleport in Rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>(true))
            teleport.enabled = false;
        foreach (var ray in Rig.GetComponentsInChildren<XRRayInteractor>(true))
            if (ray.name.Contains("Teleport")) ray.gameObject.SetActive(false);
    }

    /// <summary>
    /// The three buttons the game itself listens to. The on-screen prompts name the Quest's buttons
    /// (A = ready / yes, B = no), so the keyboard answers to the same letter as well as to the keys
    /// the desktop driver has always used: someone testing without a headset reads "press A" and
    /// presses A. B is left off the keyboard on purpose: the desktop driver already uses it to take a net back.
    /// </summary>
    private void CreateInputs()
    {
        ready = new InputAction("Ready", InputActionType.Button);
        ready.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
        ready.AddBinding("<Keyboard>/a");
        ready.AddBinding("<Keyboard>/enter");
        menu = new InputAction("End turn", InputActionType.Button);
        menu.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        menu.AddBinding("<Keyboard>/backspace");
        no = new InputAction("No", InputActionType.Button);
        no.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
        no.AddBinding("<Keyboard>/escape");
        // Invariant culture: the processor string is parsed, and a decimal comma would break it.
        string min = turnDeadzone.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        turnStick = new InputAction("Turn", InputActionType.Value, "<XRController>{LeftHand}/{Primary2DAxis}",
                                    processors: $"stickDeadzone(min={min},max=0.925)");
    }

    private void OnEnable() { ready?.Enable(); menu?.Enable(); no?.Enable(); turnStick?.Enable(); }
    private void OnDisable() { ready?.Disable(); menu?.Disable(); no?.Disable(); turnStick?.Disable(); }

    private void Update()
    {
        if (turnProvider != null && turnStick != null)
            turnProvider.leftHandTurnInput.manualValue = turnStick.ReadValue<Vector2>();

        var s = M3Session.Instance;
        if (ready == null || s == null || s.Session == null || !s.Running) return;

        if (s.WaitingForPilot)
        {
            if (ready.WasPressedThisFrame()) s.PilotReady();
            return;
        }

        if (ConfirmingEndTurn)
        {
            if (ready.WasPressedThisFrame()) { ConfirmingEndTurn = false; s.EndTurn(); }
            else if (no.WasPressedThisFrame() || menu.WasPressedThisFrame()) ConfirmingEndTurn = false;
            return;
        }
        if (menu.WasPressedThisFrame()) ConfirmingEndTurn = true;
    }

    private static void EnsureInteractionManager()
    {
        if (FindFirstObjectByType<XRInteractionManager>() == null)
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
    }

    private static void EnsureEventSystem()
    {
        var es = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        foreach (var module in es.GetComponents<BaseInputModule>())
            if (!(module is XRUIInputModule)) module.enabled = false;
        if (es.GetComponent<XRUIInputModule>() == null) es.gameObject.AddComponent<XRUIInputModule>();
    }
}
