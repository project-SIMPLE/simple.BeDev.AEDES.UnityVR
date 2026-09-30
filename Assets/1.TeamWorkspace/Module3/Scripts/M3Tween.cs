using UnityEngine;

/// <summary>
/// Easing curves for Module 3's small bits of motion: a net dropping over a bed, a screen pressed
/// into its window, a door swinging, a panel popping in. Plain functions of 0..1, so every
/// animation is a single float advanced in its own Update - nothing allocates, nothing is queued,
/// and whatever is animating can jump straight to its end state at any moment.
/// </summary>
public static class M3Tween
{
    public static float EaseOutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    public static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    /// <summary>Ease-out that runs a little past 1 and comes back: the settle at the end of a drop.</summary>
    public static float EaseOutBack(float t, float overshoot = 1.2f)
    {
        t = Mathf.Clamp01(t) - 1f;
        return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
    }

    /// <summary>A decaying wobble, -1..1, that is exactly 0 at both ends.</summary>
    public static float Shake(float t, float wobbles = 3f)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Sin(t * wobbles * 2f * Mathf.PI) * (1f - t);
    }

    /// <summary>Frame-rate independent approach of <paramref name="current"/> toward <paramref name="target"/>.</summary>
    public static float Approach(float current, float target, float sharpness, float dt)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }

    /// <summary>
    /// This frame's step for a UI or prop animation. Capped, so a hitch (a scene build, a turn
    /// handover) moves an animation on by a tenth of a second rather than skipping it.
    /// </summary>
    public static float Dt => Mathf.Min(Time.deltaTime, 0.1f);
}
