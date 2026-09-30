using UnityEngine;

/// <summary>
/// Pops a world-space panel in instead of switching it on: it grows from 92% to full size and
/// fades up over ~0.15 s. Only the transform's scale and a CanvasGroup's alpha move, never the
/// layout, so the text sits exactly where it would without this.
///
/// The panel is pressable from the first frame (the CanvasGroup keeps blocking raycasts), and it
/// snaps to full size and opacity if it is closed mid-pop.
/// </summary>
public class M3PanelPop : MonoBehaviour
{
    private const float Seconds = 0.15f;
    private const float FromScale = 0.92f;

    private Vector3 fullScale;
    private CanvasGroup group;
    private float elapsed = -1f;   // < 0: not popping

    /// <summary>Plays the pop on <paramref name="panel"/>. Call right after activating it.</summary>
    public static void Play(Transform panel)
    {
        if (panel == null) return;
        if (!panel.TryGetComponent(out M3PanelPop pop)) pop = panel.gameObject.AddComponent<M3PanelPop>();
        pop.Begin();
    }

    private void Begin()
    {
        if (group == null)
        {
            fullScale = transform.localScale;
            // Not `GetComponent() ?? AddComponent()`: in the editor a missing built-in component comes
            // back as a fake null that `??` lets through, and the panel never got its CanvasGroup.
            if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        }
        else Snap();               // a pop still running: start again from its real size
        elapsed = 0f;
        Apply(0f);
    }

    private void Update()
    {
        if (elapsed < 0f) return;
        elapsed += M3Tween.Dt;
        float t = elapsed / Seconds;
        if (t >= 1f) { Snap(); return; }
        Apply(t);
    }

    private void Apply(float t)
    {
        float k = M3Tween.EaseOutCubic(t);
        transform.localScale = fullScale * Mathf.LerpUnclamped(FromScale, 1f, k);
        group.alpha = k;
    }

    private void Snap()
    {
        elapsed = -1f;
        transform.localScale = fullScale;
        if (group != null) group.alpha = 1f;
    }

    private void OnDisable()
    {
        if (group != null) Snap();
    }
}
