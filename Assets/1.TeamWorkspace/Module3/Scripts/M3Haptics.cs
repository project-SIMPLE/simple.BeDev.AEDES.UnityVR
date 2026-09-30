using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Controller buzzes for Module 3's UI, so a press is felt as well as seen:
///   hover over a button     a short, low tick
///   press it                a medium pulse
///   the action went through a soft two-beat, on the hand that pressed
///
/// The pulse goes to the controller of whichever interactor (ray or poke) the UI event came from,
/// through the starter rig's HapticImpulsePlayer. The rig's own SimpleHapticFeedback already buzzes
/// for hovering and grabbing villagers; that is left alone, and nothing here repeats it.
///
/// It is the same for every villager and every action (section 5): a success beat says "done",
/// never anything about who it was done for.
/// </summary>
public static class M3Haptics
{
    // Both settings are remembered between sessions (PlayerPrefs), so a settings screen, or a teacher
    // setting them once on a loaner headset, only has to write them. There is no such screen yet: until
    // there is, they can be changed from code or by editing the prefs.
    private const string EnabledKey = "AEDES.Module3.Haptics", IntensityKey = "AEDES.Module3.HapticsIntensity";
    private static int enabledCache = -1;
    private static float intensityCache = -1f;

    /// <summary>Off switch for every Module 3 pulse.</summary>
    public static bool Enabled
    {
        get
        {
            if (enabledCache < 0) enabledCache = PlayerPrefs.GetInt(EnabledKey, 1) != 0 ? 1 : 0;
            return enabledCache != 0;
        }
        set
        {
            enabledCache = value ? 1 : 0;
            PlayerPrefs.SetInt(EnabledKey, enabledCache);
        }
    }

    /// <summary>Multiplies every amplitude below, 0..1.</summary>
    public static float Intensity
    {
        get
        {
            if (intensityCache < 0f) intensityCache = Mathf.Clamp01(PlayerPrefs.GetFloat(IntensityKey, 1f));
            return intensityCache;
        }
        set
        {
            intensityCache = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(IntensityKey, intensityCache);
        }
    }

    // Amplitude 0..1, seconds.
    public const float HoverAmplitude = 0.1f, HoverSeconds = 0.02f;
    public const float SelectAmplitude = 0.35f, SelectSeconds = 0.06f;
    public const float SuccessAmplitude = 0.22f, SuccessSeconds = 0.05f;
    /// <summary>When the two success beats land after the action resolves, clear of the press pulse.</summary>
    public const float SuccessFirstBeat = 0.1f, SuccessSecondBeat = 0.24f;

    /// <summary>Hover ticks closer together than this, per hand, are dropped (sweeping a ray down a list).</summary>
    private const float HoverSpacing = 0.06f;
    /// <summary>
    /// No hover ticks this long after a press: the panel rebuilds its buttons when the action
    /// resolves, and the new one under the ray would tick between the press and the success beat.
    /// </summary>
    private const float QuietAfterSelect = 0.4f;

    private static readonly Dictionary<Component, HapticImpulsePlayer> players = new Dictionary<Component, HapticImpulsePlayer>();
    private static Component lastHover;
    private static float lastHoverAt = -1f;
    private static float lastSelectAt = -1f;
    private static Component pressedBy;
    private static M3HapticsRunner runner;

    public static void Hover(PointerEventData e)
    {
        var hand = HandOf(e);
        if (hand == lastHover && Time.time - lastHoverAt < HoverSpacing) return;
        if (lastSelectAt >= 0f && Time.time - lastSelectAt < QuietAfterSelect) return;
        lastHover = hand;
        lastHoverAt = Time.time;
        Pulse(hand, HoverAmplitude, HoverSeconds, "hover");
    }

    /// <summary>A button went down: remember the hand, so the success beat finds it.</summary>
    public static void Down(PointerEventData e)
    {
        pressedBy = HandOf(e);
        EnsureRunner();
    }

    public static void Select(PointerEventData e)
    {
        var hand = HandOf(e);
        // Not pressedBy = hand: Down already set it, and by the time a click arrives the button's onClick
        // has resolved the action and the success beat has taken it. Setting it again here left a stale
        // hand behind, which the next action that no button pressed would have buzzed.
        lastSelectAt = Time.time;
        Pulse(hand, SelectAmplitude, SelectSeconds, "select");
    }

    /// <summary>A villager opened by a trigger pull on the ray (the rig's own feedback covers the grip).</summary>
    public static void Select(Component interactor) => Pulse(interactor, SelectAmplitude, SelectSeconds, "select");

    private static Component HandOf(PointerEventData e)
    {
        EnsureRunner();
        return e is TrackedDeviceEventData tracked ? tracked.interactor as Component : null;
    }

    internal static void Pulse(Component hand, float amplitude, float seconds, string what)
    {
        EnsureRunner();
        runner.Record(hand, amplitude * Intensity, seconds, what);
        if (!Enabled || Intensity <= 0f || hand == null) return;

        if (!players.TryGetValue(hand, out var player) || player == null)
        {
            player = hand.GetComponentInParent<HapticImpulsePlayer>(true);
            players[hand] = player;
        }
        float a = Mathf.Clamp01(amplitude * Intensity);
        if (player != null) player.SendHapticImpulse(a, seconds);
        else if (hand is XRBaseInputInteractor input) input.SendHapticImpulse(a, seconds);
    }

    /// <summary>The hand that pressed the last button, once: the success beat takes it.</summary>
    internal static Component TakePressingHand()
    {
        var hand = pressedBy;
        pressedBy = null;
        return hand;
    }

    internal static void Forget(M3HapticsRunner r)
    {
        if (runner == r) runner = null;
        players.Clear();
    }

    private static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("M3 Haptics");
        runner = go.AddComponent<M3HapticsRunner>();
    }
}
