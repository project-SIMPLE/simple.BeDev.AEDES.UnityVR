using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps SIMPLE's in-headset "Debug Overlay" (DebugManager) hidden until someone asks for it.
///
/// The overlay was always on: a grey panel of console text in the middle of the view, which is
/// in the way of the class and of the Coach watching the cast. It is still wanted for on-headset
/// debugging, so it is gated rather than removed.
///
///   Headset:  hold BOTH grips, then press A, B, A, B on the right controller within 3 seconds.
///   Editor:   the backquote key ( ` ).
///
/// Only the canvases are hidden, never the GameObject: DebugManager keeps collecting log lines
/// while hidden, so revealing it shows what just happened.
///
/// Attached automatically to every DebugManager in each loaded scene, so no scene edit is
/// needed. The controllers are read through UnityEngine.XR.InputDevices, the API Modules 1 and 2
/// already rely on for the Quest.
/// </summary>
public class DebugOverlayGate : MonoBehaviour
{
    public enum Press { A, B }

    /// <summary>The sequence pressed on the right controller while both grips are held.</summary>
    public static readonly Press[] Sequence = { Press.A, Press.B, Press.A, Press.B };

    [Tooltip("Seconds allowed from the first press of the sequence to the last.")]
    [SerializeField] private float window = 3f;
    [SerializeField] private bool visibleOnStart = false;

    public bool Visible { get; private set; }

    private Canvas[] canvases;
    private int progress;
    private float startedAt;
    private bool lastA, lastB;
    private static readonly List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();

    private void Awake()
    {
        // DebugManager sits on the text object; the canvas (and its background) is a parent.
        var parent = GetComponentInParent<Canvas>(true);
        canvases = parent != null ? new[] { parent.rootCanvas } : GetComponentsInChildren<Canvas>(true);
        SetVisible(visibleOnStart);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame) SetVisible(!Visible);

        ReadControllers(out bool gripsHeld, out bool a, out bool b);
        bool aDown = a && !lastA, bDown = b && !lastB;
        lastA = a;
        lastB = b;
        if (Feed(gripsHeld, aDown, bDown, Time.unscaledTime)) SetVisible(!Visible);
    }

    /// <summary>
    /// Advances the sequence by one frame of input. Returns true when it completes. Kept separate
    /// from the device reads so the logic can be driven without a headset.
    /// </summary>
    public bool Feed(bool gripsHeld, bool aDown, bool bDown, float now)
    {
        if (progress > 0 && now - startedAt > window) progress = 0;
        if (!gripsHeld) { progress = 0; return false; }
        if (!aDown && !bDown) return false;

        Press? pressed = aDown && !bDown ? Press.A : bDown && !aDown ? Press.B : (Press?)null;
        if (pressed != Sequence[progress])
        {
            // A wrong press starts over - counting it as a new first step if it is one.
            progress = pressed == Sequence[0] ? 1 : 0;
            startedAt = now;
            return false;
        }

        if (progress == 0) startedAt = now;
        progress++;
        if (progress < Sequence.Length) return false;

        progress = 0;
        return true;
    }

    public void SetVisible(bool visible)
    {
        Visible = visible;
        if (canvases == null) return;
        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null) canvases[i].enabled = visible;
    }

    private static void ReadControllers(out bool gripsHeld, out bool a, out bool b)
    {
        bool leftGrip = false, rightGrip = false;
        a = b = false;

        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Controller, devices);
        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            if (!d.isValid) continue;
            d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool grip);
            if ((d.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Left) != 0)
            {
                leftGrip |= grip;
                continue;
            }
            rightGrip |= grip;
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool p)) a |= p;
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool s)) b |= s;
        }
        gripsHeld = leftGrip && rightGrip;
    }

    // ---- automatic attachment ----

    private static bool installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        AttachToAll();
        if (installed) return;
        installed = true;
        SceneManager.sceneLoaded += (scene, mode) => AttachToAll();
    }

    private static void AttachToAll()
    {
        foreach (var manager in FindObjectsByType<DebugManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (manager.GetComponent<DebugOverlayGate>() == null)
                manager.gameObject.AddComponent<DebugOverlayGate>();
        }
    }
}
