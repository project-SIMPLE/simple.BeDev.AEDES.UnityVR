using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A soft, rounded, translucent dark panel that sits behind one piece of text and sizes itself to it,
/// so pale type stays readable over a bright sky or a pale wall.
///
/// The world-space HUD floats in the village with nothing behind its text, and light text on a white
/// cloud all but disappeared. A slab behind the whole canvas would be a screen stuck to the Pilot's
/// face, so this hugs the letters instead and hides itself while there is no text.
///
/// Made in code, sprite included, so there is no asset or scene YAML to merge, and it leaves the font
/// asset and its shared material alone (they re-serialise, and every other module uses them).
/// </summary>
public class M3TextBacking : MonoBehaviour
{
    // Dark enough that pale text keeps 4.5:1 or better even where the backing sits over the brightest,
    // sunlit cloud (at 72% a white sky came through and measured 3.6:1 there), and still see-through.
    public static readonly Color DefaultColour = new Color(0.05f, 0.06f, 0.08f, 0.88f);

    // A 64 px rounded square drawn at 200 pixels per unit and sliced at 28 px: on a canvas that uses
    // the usual 100 reference pixels per unit the corners come out 14 units across.
    private const int SpriteSize = 64;
    private const int CornerPixels = 28;
    private const float SpritePixelsPerUnit = 200f;

    private static Sprite sprite;

    private TMP_Text text;
    private Image image;
    private RectTransform rect;
    private Vector2 padding;
    private Vector2 lastSize;
    private Vector3 lastCentre;

    /// <summary>
    /// Puts a backing behind <paramref name="text"/>, <paramref name="padding"/> canvas units wider and
    /// taller than its letters on every side.
    /// </summary>
    public static M3TextBacking Add(TMP_Text text, Vector2 padding, Color? colour = null)
    {
        var go = new GameObject(text.name + " Backing", typeof(RectTransform), typeof(CanvasRenderer));
        var rt = (RectTransform)go.transform;
        // A sibling just under the text, so it draws first and the letters land on top of it.
        rt.SetParent(text.transform.parent, false);
        rt.SetSiblingIndex(text.transform.GetSiblingIndex());
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        var image = go.AddComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = colour ?? DefaultColour;
        image.raycastTarget = false;
        image.enabled = false;   // until there is something to sit behind

        var backing = text.gameObject.AddComponent<M3TextBacking>();
        backing.text = text;
        backing.image = image;
        backing.rect = rt;
        backing.padding = padding;
        return backing;
    }

    private void OnDestroy()
    {
        if (rect != null) Destroy(rect.gameObject);
    }

    private void LateUpdate()
    {
        // The letters' own extents, in the text's local space. Empty text has none.
        bool any = text.textInfo != null && text.textInfo.characterCount > 0 && !string.IsNullOrEmpty(text.text);
        if (image.enabled != any) image.enabled = any;
        if (!any) return;

        Bounds bounds = text.textBounds;
        Vector2 size = (Vector2)bounds.size + padding * 2f;
        Vector3 centre = bounds.center;
        // Writing a rect dirties the canvas, so only do it when the text has actually changed shape.
        if ((size - lastSize).sqrMagnitude < 0.25f && (centre - lastCentre).sqrMagnitude < 0.25f) return;
        lastSize = size;
        lastCentre = centre;

        rect.localPosition = rect.parent.InverseTransformPoint(text.rectTransform.TransformPoint(centre));
        rect.sizeDelta = size;
    }

    private static Sprite RoundedSprite()
    {
        if (sprite != null) return sprite;

        var pixels = new Color32[SpriteSize * SpriteSize];
        float half = SpriteSize * 0.5f;
        float inner = half - CornerPixels;
        for (int y = 0; y < SpriteSize; y++)
        {
            for (int x = 0; x < SpriteSize; x++)
            {
                // Signed distance to a rounded square, one pixel of anti-aliasing at the edge.
                float qx = Mathf.Abs(x + 0.5f - half) - inner;
                float qy = Mathf.Abs(y + 0.5f - half) - inner;
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - CornerPixels;
                pixels[y * SpriteSize + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - distance) * 255f + 0.5f));
            }
        }

        var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
        {
            name = "M3 Text Backing",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);   // the pixels live on the GPU only

        sprite = Sprite.Create(texture, new Rect(0f, 0f, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f),
                               SpritePixelsPerUnit, 0, SpriteMeshType.FullRect,
                               new Vector4(CornerPixels, CornerPixels, CornerPixels, CornerPixels));
        sprite.name = "M3 Text Backing";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
