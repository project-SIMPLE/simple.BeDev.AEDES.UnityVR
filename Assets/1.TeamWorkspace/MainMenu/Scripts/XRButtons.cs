using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Reads the controller buttons both hands share, with edge detection and hold timers.
///
/// It uses the legacy <see cref="UnityEngine.XR.InputDevices"/> API rather than Input System
/// actions because that is what the rest of the AEDES gameplay code uses (PlayerMain, InputVR),
/// and mixing the two on one rig is how you end up with a menu that works in the editor and does
/// nothing on the headset.
///
/// Not a MonoBehaviour: the owner calls <see cref="Update"/> once per frame.
/// In the editor, when no XR controllers are present, the keyboard stands in so the menu can be
/// driven from Play Mode without a headset.
/// </summary>
public class XRButtons
{
    InputDevice left, right;
    float nextScan;

    // Held this frame (either hand).
    public bool Primary { get; private set; }      // A / X
    public bool Secondary { get; private set; }    // B / Y
    public bool Menu { get; private set; }         // the left controller's menu button
    public bool Trigger { get; private set; }
    public bool Grip { get; private set; }
    public Vector2 Stick { get; private set; }

    // Went down this frame.
    public bool PrimaryDown { get; private set; }
    public bool TriggerDown { get; private set; }
    public bool SecondaryDown { get; private set; }
    public bool MenuDown { get; private set; }

    /// <summary>Seconds B/Y has been held, or 0 when it is up.</summary>
    public float SecondaryHeld { get; private set; }
    /// <summary>Any button or stick input at all - for "press anything to continue".</summary>
    public bool AnyInput { get; private set; }
    public bool HasControllers => left.isValid || right.isValid;

    bool primaryLast, secondaryLast, triggerLast, menuLast;

    /// <summary>Stick flicks, debounced into discrete steps so a held stick does not race through a list.</summary>
    public int StepX { get; private set; }
    float stickCooldown;
    const float StickDeadzone = 0.6f;
    const float MaxHoldStep = 0.1f;

    public void Update()
    {
        if (Time.unscaledTime >= nextScan && (!left.isValid || !right.isValid))
        {
            nextScan = Time.unscaledTime + 0.5f;
            if (!left.isValid) left = FirstDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left);
            if (!right.isValid) right = FirstDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right);
        }

        Primary = Button(CommonUsages.primaryButton);
        Secondary = Button(CommonUsages.secondaryButton);
        Menu = Button(CommonUsages.menuButton);
        Trigger = Button(CommonUsages.triggerButton);
        Grip = Button(CommonUsages.gripButton);
        Stick = Axis(CommonUsages.primary2DAxis);

#if UNITY_EDITOR
        if (!HasControllers) KeyboardFallback();
#endif

        PrimaryDown = Primary && !primaryLast;
        SecondaryDown = Secondary && !secondaryLast;
        MenuDown = Menu && !menuLast;
        TriggerDown = Trigger && !triggerLast;
        primaryLast = Primary; secondaryLast = Secondary; triggerLast = Trigger; menuLast = Menu;

        // Capped per frame: a hitch (a scene loading, a stutter) must not turn a tap into a long hold.
        SecondaryHeld = Secondary ? SecondaryHeld + Mathf.Min(Time.unscaledDeltaTime, MaxHoldStep) : 0f;
        AnyInput = Primary || Secondary || Trigger || Grip || Stick.sqrMagnitude > 0.25f;

        StepX = 0;
        stickCooldown -= Time.unscaledDeltaTime;
        if (Mathf.Abs(Stick.x) < StickDeadzone * 0.5f) stickCooldown = 0f;
        else if (Mathf.Abs(Stick.x) > StickDeadzone && stickCooldown <= 0f)
        {
            StepX = (int)Mathf.Sign(Stick.x);
            stickCooldown = 0.35f;
        }
    }

    /// <summary>A short buzz. Silently does nothing on devices without haptics.</summary>
    public void Rumble(float amplitude = 0.3f, float duration = 0.06f)
    {
        Rumble(right, amplitude, duration);
        Rumble(left, amplitude, duration);
    }

    static void Rumble(InputDevice d, float amplitude, float duration)
    {
        if (d.isValid && d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            d.SendHapticImpulse(0, amplitude, duration);
    }

    bool Button(InputFeatureUsage<bool> usage)
    {
        bool a = right.isValid && right.TryGetFeatureValue(usage, out bool rv) && rv;
        bool b = left.isValid && left.TryGetFeatureValue(usage, out bool lv) && lv;
        return a || b;
    }

    Vector2 Axis(InputFeatureUsage<Vector2> usage)
    {
        Vector2 v = Vector2.zero;
        if (left.isValid && left.TryGetFeatureValue(usage, out Vector2 lv)) v = lv;
        if (right.isValid && right.TryGetFeatureValue(usage, out Vector2 rv) && rv.sqrMagnitude > v.sqrMagnitude) v = rv;
        return v;
    }

    static InputDevice FirstDevice(InputDeviceCharacteristics characteristics)
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);
        return devices.Count > 0 ? devices[0] : default;
    }

#if UNITY_EDITOR
    // Left/right arrow or A/D to move, Space/Enter to confirm, Escape as B/Y, M as the menu button.
    void KeyboardFallback()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        Primary = kb.spaceKey.isPressed || kb.enterKey.isPressed;
        Secondary = kb.escapeKey.isPressed;
        Menu = kb.mKey.isPressed;
        Trigger = kb.leftCtrlKey.isPressed;
        float x = (kb.rightArrowKey.isPressed || kb.dKey.isPressed ? 1f : 0f)
                - (kb.leftArrowKey.isPressed || kb.aKey.isPressed ? 1f : 0f);
        Stick = new Vector2(x, 0f);
    }
#endif
}
