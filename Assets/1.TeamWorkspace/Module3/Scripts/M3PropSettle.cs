using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Brings a room prop into place instead of letting it pop in: the net drops over the bed, the
/// screen is pressed into its window, a torn screen shakes as it goes up.
///
/// HouseholdView still decides WHAT stands in the room and places it exactly where it always has;
/// this only animates the last stretch toward that pose. The end pose is captured before anything
/// moves and is written back when the animation finishes, when the prop is switched off, and when
/// it is destroyed by the next Refresh - so a turn that advances mid-drop leaves the room exactly
/// as the model says it is, and nothing waits on the animation.
/// </summary>
public class M3PropSettle : MonoBehaviour
{
    private enum Motion { Grow, Slide, Shake }

    private Motion motion;
    private float duration, elapsed;
    private Vector3 endPosition, endScale;
    private Quaternion endRotation;

    private Vector3 anchor;           // Grow: parent-space point that stays put while the scale changes
    private Vector3 fromScale;        // Grow: scale at the start
    private Vector3 fromPosition;     // Slide
    private float shakeDegrees;       // Shake

    /// <summary>
    /// The deployed net unrolls downward from its top, over ~0.6 s, and sags a touch past the
    /// mattress before settling.
    /// </summary>
    public static void DropNet(GameObject net)
    {
        if (net == null || !TryTop(net, out Vector3 top)) return;
        Begin(net, Motion.Grow, 0.6f).GrowFrom(top, new Vector3(1f, 0.06f, 1f));
    }

    /// <summary>
    /// The rolled bundle gathers up toward its hook. Call after HangFromBedPost, so the pose it
    /// ends in is exactly the one the hook gave it.
    /// </summary>
    public static void GatherNet(GameObject net)
    {
        if (net == null) return;
        var hook = M3Sockets.Find(net.transform, Module3Props.Socket.Hook);
        Vector3 at = hook != null ? hook.position : net.transform.position;
        Begin(net, Motion.Grow, 0.4f).GrowFrom(at, new Vector3(1f, 2.2f, 1f));
    }

    /// <summary>
    /// The screen is pressed into the window frame from the room side over ~0.4 s.
    /// <paramref name="intoRoom"/> is a direction in the prop's parent space.
    /// </summary>
    public static void SlideIn(GameObject screen, Vector3 intoRoom, float distance = 0.22f)
    {
        if (screen == null) return;
        var s = Begin(screen, Motion.Slide, 0.4f);
        s.fromPosition = s.endPosition + intoRoom.normalized * distance;
        s.Apply(0f);
    }

    /// <summary>A small in-plane shake as a torn screen goes up.</summary>
    public static void ShakeIn(GameObject screen, float degrees = 4f)
    {
        if (screen == null) return;
        var s = Begin(screen, Motion.Shake, 0.35f);
        s.shakeDegrees = degrees;
        s.Apply(0f);
    }

    private static M3PropSettle Begin(GameObject go, Motion motion, float duration)
    {
        var s = go.GetComponent<M3PropSettle>();
        if (s != null) s.Finish();      // restarting: first put it where the last one ended
        s = go.AddComponent<M3PropSettle>();
        var t = go.transform;
        s.motion = motion;
        s.duration = duration;
        s.endPosition = t.localPosition;
        s.endRotation = t.localRotation;
        s.endScale = t.localScale;
        return s;
    }

    private void GrowFrom(Vector3 worldAnchor, Vector3 scaleFactor)
    {
        anchor = transform.parent != null ? transform.parent.InverseTransformPoint(worldAnchor) : worldAnchor;
        fromScale = Vector3.Scale(endScale, scaleFactor);
        Apply(0f);
    }

    /// <summary>The highest point of the prop's meshes, centred over them - where a net hangs from.</summary>
    private static bool TryTop(GameObject go, out Vector3 top)
    {
        top = default;
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        top = new Vector3(b.center.x, b.max.y, b.center.z);
        return true;
    }

    private void Update()
    {
        elapsed += M3Tween.Dt;
        float t = elapsed / duration;
        if (t >= 1f) { Finish(); return; }
        Apply(t);
    }

    private void Apply(float t)
    {
        var tr = transform;
        switch (motion)
        {
            case Motion.Grow:
            {
                float k = M3Tween.EaseOutBack(t, 0.9f);
                Vector3 scale = Vector3.LerpUnclamped(fromScale, endScale, k);
                // Keep the anchor fixed: the offset from anchor to pivot, measured in the prop's own
                // axes, shrinks and grows with the scale on each axis.
                Vector3 offset = Quaternion.Inverse(endRotation) * (endPosition - anchor);
                offset = new Vector3(offset.x * Ratio(scale.x, endScale.x), offset.y * Ratio(scale.y, endScale.y),
                                     offset.z * Ratio(scale.z, endScale.z));
                tr.localScale = scale;
                tr.localPosition = anchor + endRotation * offset;
                break;
            }
            case Motion.Slide:
                tr.localPosition = Vector3.LerpUnclamped(fromPosition, endPosition, M3Tween.EaseOutCubic(t));
                break;
            case Motion.Shake:
                tr.localRotation = endRotation * Quaternion.Euler(0f, 0f, shakeDegrees * M3Tween.Shake(t, 3.5f));
                tr.localScale = endScale * (1f + 0.04f * M3Tween.Shake(t, 1.5f));
                break;
        }
    }

    private static float Ratio(float a, float b) => Mathf.Abs(b) > 1e-6f ? a / b : 1f;

    /// <summary>Straight to the end pose, and done.</summary>
    public void Finish()
    {
        if (elapsed < 0f) return;       // already finished
        elapsed = -1f;
        var tr = transform;
        tr.localPosition = endPosition;
        tr.localRotation = endRotation;
        tr.localScale = endScale;
        enabled = false;
        Destroy(this);
    }

    // Switched off or destroyed mid-way (the room was refreshed, the house culled): never leave a
    // prop half-dropped.
    private void OnDisable()
    {
        if (elapsed >= 0f) Finish();
    }
}
