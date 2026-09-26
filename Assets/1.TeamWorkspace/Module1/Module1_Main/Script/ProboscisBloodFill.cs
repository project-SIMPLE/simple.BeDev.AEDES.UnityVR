using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shows the blood meal climbing up the player's proboscis. Sits on the snout object (the
/// "Capsule" under the Main Camera that also carries <see cref="Drink"/>): at start it spawns a
/// slightly thicker copy of the snout mesh in a blood-red material, anchored at the tip, and every
/// frame scales it along the snout as Current_Blood / Max_Blood rises. The snout itself is also
/// tinted progressively towards dark red, because the player sees it almost end-on and the far tip
/// where the column starts is only a few pixels wide. The column throbs a little while blood is
/// actually being drawn, and everything empties again when eggs are laid.
///
/// The snout is the built-in capsule with its +Y pointing forward (rotated 90 deg on X), so the
/// tip is at local y = +1 and the head end at local y = -1.
/// </summary>
public class ProboscisBloodFill : MonoBehaviour
{
    public Color bloodColor = new Color(0.55f, 0.02f, 0.03f);
    [Tooltip("Colour the snout itself blends towards as it fills.")]
    public Color snoutTint = new Color(0.4f, 0.03f, 0.04f);
    [Tooltip("Column length = fraction ^ this. Below 1 makes the first part of the fill longer, to counter the perspective foreshortening of the far end of the snout.")]
    [Range(0.3f, 1f)] public float lengthCurve = 0.6f;
    [Tooltip("Glow added on top of the base colour so the blood still reads against the sky or in shadow.")]
    [Range(0f, 1f)] public float glow = 0.25f;
    [Tooltip("How much thicker than the snout the blood column is, so it sits just outside the dark surface.")]
    public float thickness = 1.15f;
    [Tooltip("Relative thickness swing while feeding.")]
    public float pulseAmount = 0.06f;
    public float pulseSpeed = 9f;
    [Tooltip("How quickly the shown level follows the real one.")]
    public float smoothing = 10f;

    Transform fill;
    Renderer fillRenderer;
    Renderer snoutRenderer;
    Material material;
    MaterialPropertyBlock snoutBlock;
    Color snoutBaseColor;
    float shown;

    void Start()
    {
        var snoutMesh = GetComponent<MeshFilter>();
        snoutRenderer = GetComponent<Renderer>();
        if (snoutMesh == null || snoutRenderer == null || snoutMesh.sharedMesh == null)
        {
            Debug.LogWarning("[ProboscisBloodFill] needs a MeshFilter + Renderer on the snout object.");
            enabled = false;
            return;
        }

        var go = new GameObject("BloodFill (runtime)");
        go.layer = gameObject.layer;
        fill = go.transform;
        fill.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = snoutMesh.sharedMesh;

        // Same shader as the snout (URP/Lit in the current scene) so it lights consistently.
        material = new Material(snoutRenderer.sharedMaterial) { name = "BloodFill (runtime)" };
        material.color = bloodColor;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", bloodColor);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.85f);
        if (glow > 0f && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", bloodColor * glow);
        }

        fillRenderer = go.AddComponent<MeshRenderer>();
        fillRenderer.sharedMaterial = material;
        fillRenderer.shadowCastingMode = ShadowCastingMode.Off;
        fillRenderer.receiveShadows = snoutRenderer.receiveShadows;

        // Tint the snout through a property block so the shared Dark material is left alone.
        var snoutMat = snoutRenderer.sharedMaterial;
        snoutBaseColor = snoutMat.HasProperty("_BaseColor") ? snoutMat.GetColor("_BaseColor") : snoutMat.color;
        snoutBlock = new MaterialPropertyBlock();

        Apply(0f, 1f);
    }

    void LateUpdate()
    {
        var player = PlayerMain.instance;
        if (player == null || fill == null) return;

        float target = player.Max_Blood > 0f ? Mathf.Clamp01(player.Current_Blood / player.Max_Blood) : 0f;
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        bool feeding = player.CurrentInteraction == PlayerMain.Interaction.Human && player.R_primaryValue && target < 1f;
        float pulse = feeding ? 1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed) : 1f;
        Apply(shown, pulse);
    }

    // Column of length `len` (in snout lengths) ending at the tip: local y from 1-2*len to 1.
    void Apply(float fraction, float pulse)
    {
        float len = fraction > 0f ? Mathf.Pow(fraction, lengthCurve) : 0f;
        fillRenderer.enabled = len > 0.01f;
        float t = thickness * pulse;
        fill.localScale = new Vector3(t, Mathf.Max(len, 0.01f), t);
        fill.localPosition = new Vector3(0f, 1f - len, 0f);

        Color tint = Color.Lerp(snoutBaseColor, snoutTint, fraction);
        snoutBlock.SetColor("_BaseColor", tint);
        snoutBlock.SetColor("_Color", tint);
        snoutRenderer.SetPropertyBlock(snoutBlock);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (snoutRenderer != null) snoutRenderer.SetPropertyBlock(null);
    }
}
