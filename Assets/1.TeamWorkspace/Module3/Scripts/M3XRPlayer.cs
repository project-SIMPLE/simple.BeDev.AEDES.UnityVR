using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// The Pilot in the headset. Spawns the XR Interaction Toolkit starter rig at the head of the
/// lane and sets up what it needs, replacing the scene's plain desktop camera.
///
/// Controls (the XRI defaults, so they match every other Quest app a class will have seen):
///   Left stick           walk
///   Right stick          left/right snap turn, push forward and release to teleport
///   Point + trigger      talk to a villager, press a button on a panel
///   Grip (reach out)     talk to a villager within arm's reach
///   A                    "I'm ready" at the start of a turn
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
    [Tooltip("Walking pace with the left stick, metres per second. The starter rig ships at 2.5, a jog, "
             + "which is uncomfortable in a headset - especially for children - and overshoots doorways.")]
    [SerializeField] private float walkSpeed = 1.4f;
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

    private void Awake()
    {
        Instance = this;
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
        foreach (var move in Rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>(true))
            move.moveSpeed = walkSpeed;

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
        MakeGroundTeleportable();

        ready = new InputAction("Ready", InputActionType.Button);
        ready.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
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
        if (s == null || s.Session == null || !s.Running) return;

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

    private static void MakeGroundTeleportable()
    {
        var ground = GameObject.Find("Ground");
        if (ground != null && ground.GetComponent<TeleportationArea>() == null)
            ground.AddComponent<TeleportationArea>();
    }
}
