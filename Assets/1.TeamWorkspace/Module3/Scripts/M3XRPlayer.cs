using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// The Pilot in the headset. Spawns Module 2's player rig (VR_Player_Module_2) at the start of the
/// road and sets up what Module 3 needs on it, replacing the scene's plain desktop camera.
///
/// Module 3 controls exactly like Module 2, because it is Module 2's rig, untouched:
///   Left stick           walk (forward is where you look), at Module 2's pace
///   Right stick          turn left/right, smoothly
///   Grip (reach out)     talk to a villager within arm's reach - where Module 2 grabs a jar
///   Right index finger   poke a button, on the person panel or on the watch
///   Look at your wrist   the watch menu: Ready at the start of a turn, End turn during one, then
///                        Yes / No - where Module 2 keeps its swatter and cream (M3WristMenu)
/// A, B and the left menu button answer as well (ready / yes, no, end turn), as do A, Enter, Escape
/// and Backspace on a keyboard without a headset.
///
/// Also the turn's gatekeeper: M3Session holds each turn's clock until the incoming Pilot, now in
/// the headset, presses Ready.
/// </summary>
public class M3XRPlayer : MonoBehaviour
{
    [Tooltip("Module 2's player rig. Its sticks, hands, poke and wrist watch are used as they are.")]
    [SerializeField] private GameObject rigPrefab;
    [Tooltip("Where the Pilot stands at the start of the session.")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(-8f, 0f, 8f);
    [SerializeField] private float spawnYaw = 90f;
    [Tooltip("Tallest the body capsule may be. Keeps the body clear of low beams and eaves for a tall "
             + "wearer; the doorways themselves are 1.9 m clear, so nobody needs to duck.")]
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

    private void Awake()
    {
        Instance = this;
        // The buttons come first. Everything below (the desktop-camera early return, the rig set-up)
        // used to sit ahead of them, so a Pilot could be left with a prompt and nothing listening
        // whenever any of that returned early or threw.
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
        TakeOverModule2Rig();

        // Stand on the real floor: doorways, panel heights and the hand positions all assume true
        // standing height, where a device origin falls back to a fixed 1.36 m camera offset.
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
    /// What is Module 2's game rather than its controls stands down: the "game over" card that follows
    /// the camera, and the device poller that only logs. The sticks, hands, grab and poke are left exactly
    /// as Module 2 has them. The watch stays too, with Module 3's buttons on it.
    /// </summary>
    private void TakeOverModule2Rig()
    {
        var lose = Rig.transform.Find("Canvas_Lose");
        if (lose != null) lose.gameObject.SetActive(false);
        foreach (var poller in Rig.GetComponentsInChildren<InputVR>(true)) poller.enabled = false;
        M3WristMenu.Attach(Rig);
    }

    /// <summary>
    /// The three buttons the game itself listens to, besides the watch. A = ready and yes, B = no, the left
    /// menu button asks to end the turn. The keyboard answers to the same letter as well as to the keys
    /// the desktop driver has always used. B is left off the keyboard on purpose: the desktop driver
    /// already uses it to take a net back.
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
    }

    private void OnEnable() { ready?.Enable(); menu?.Enable(); no?.Enable(); }
    private void OnDisable() { ready?.Disable(); menu?.Disable(); no?.Disable(); }

    private void Update()
    {
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

    /// <summary>The watch's first button: Ready at the start of a turn, Yes while confirming, otherwise it asks to end the turn.</summary>
    public void PressPrimary()
    {
        var s = M3Session.Instance;
        if (s == null || s.Session == null || !s.Running) return;
        if (s.WaitingForPilot) s.PilotReady();
        else if (ConfirmingEndTurn) { ConfirmingEndTurn = false; s.EndTurn(); }
        else ConfirmingEndTurn = true;
    }

    /// <summary>The watch's second button: No.</summary>
    public void PressSecondary() => ConfirmingEndTurn = false;

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
