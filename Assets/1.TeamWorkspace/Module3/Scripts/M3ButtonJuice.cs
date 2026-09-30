using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes an M3Ui button answer the hand: it lifts a little and brightens as the ray or finger comes
/// onto it, settles back when pressed, and eases between the two instead of snapping, with a controller
/// buzz for each (M3Haptics).
///
/// It replaces the Button's own instant hover and press tints, which would otherwise stack on top of this.
/// Only the transform's scale and the Image's colour move, so the layout - and the text in it -
/// is exactly what M3Ui built. Every button looks and behaves the same, whatever it is for.
/// </summary>
[RequireComponent(typeof(Button))]
public class M3ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                             IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    // A press settles back to the resting size from the hover lift, and never below it: the rect is also
    // the ray's hit target, and at 0.97 a press within a millimetre of the edge shrank out from under the
    // ray, sent a pointer-exit before the release and lost the click.
    private const float HoverScale = 1.035f, PressScale = 1f;
    /// <summary>How far toward white the colour lifts on hover.</summary>
    private const float Lift = 0.18f;
    private const float Sharpness = 18f;   // ~0.15 s to settle

    private Button button;
    private Image image;
    private Color baseColour, liftedColour;
    private Vector3 baseScale;
    private bool hovered, pressed;
    private float scale = 1f, lift;

    private void Awake()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        // Hover and press now come from here; the resting and disabled tints are left as M3Ui set them.
        var colours = button.colors;
        colours.highlightedColor = colours.pressedColor = colours.selectedColor = colours.normalColor;
        button.colors = colours;
        baseScale = transform.localScale;
        if (image != null)
        {
            baseColour = image.color;
            liftedColour = Color.Lerp(baseColour, Color.white, Lift);
            liftedColour.a = baseColour.a;
        }
    }

    private bool Live => button != null && button.IsInteractable();

    public void OnPointerEnter(PointerEventData e)
    {
        if (!Live) return;
        hovered = true;
        M3Haptics.Hover(e);
    }

    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }

    public void OnPointerDown(PointerEventData e)
    {
        if (!Live) return;
        pressed = true;
        M3Haptics.Down(e);
    }

    public void OnPointerUp(PointerEventData e) => pressed = false;

    public void OnPointerClick(PointerEventData e)
    {
        if (Live) M3Haptics.Select(e);
    }

    private void Update()
    {
        bool live = Live;
        float targetScale = !live ? 1f : pressed ? PressScale : hovered ? HoverScale : 1f;
        float targetLift = live && (hovered || pressed) ? 1f : 0f;
        if (scale == targetScale && lift == targetLift) return;

        float dt = M3Tween.Dt;
        scale = M3Tween.Approach(scale, targetScale, Sharpness, dt);
        lift = M3Tween.Approach(lift, targetLift, Sharpness, dt);
        if (Mathf.Abs(scale - targetScale) < 0.001f) scale = targetScale;
        if (Mathf.Abs(lift - targetLift) < 0.005f) lift = targetLift;
        Apply();
    }

    private void Apply()
    {
        transform.localScale = baseScale * scale;
        if (image != null) image.color = Color.LerpUnclamped(baseColour, liftedColour, lift);
    }

    private void OnDisable()
    {
        hovered = pressed = false;
        scale = 1f;
        lift = 0f;
        if (button != null) Apply();
    }
}
