using System;
using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Makes a thing in the room the button for what it does: reach out and grip the rolled-up net and it
/// goes up over the bed; grip the torn screen and it is mended; grip the fan and it starts. Pointing at
/// it and pulling the trigger works too, exactly as it does on a villager (M3VillagerTarget).
///
/// A small disc with the action's icon floats over the thing while the Pilot is in the room with it
/// and there is something to do, and grows while the ray or the hand is on it. It is depth-tested like
/// the room itself, so it is never seen through a wall from the lane. These go on objects only, never
/// on a person - section 5's rule that nothing may mark a sick character.
///
/// HouseholdView attaches one to each prop it places and says what acting on it means.
/// </summary>
public class M3PropAction : MonoBehaviour
{
    /// <summary>What acting on this prop does at this moment; null when there is nothing to do.</summary>
    private Func<PlayerAction?> resolve;
    /// <summary>
    /// The disc only shows while the Pilot is pointing at the prop. For an action the game should not
    /// invite, but must allow: taking a net back off somebody (section 5, decision D6).
    /// </summary>
    private bool discOnlyOnHover;

    [Tooltip("The disc shows while the Pilot's head is this close (metres, across the floor) and can see it.")]
    [SerializeField] private float showWithin = 3.2f;
    [Tooltip("Diameter of the disc, metres.")]
    [SerializeField] private float discSize = 0.2f;
    [Tooltip("Closer than this (metres, across the floor) the disc fades out: at arm's length it would fill the view.")]
    [SerializeField] private float fadeWithin = 0.9f;

    private XRSimpleInteractable interactable;
    private Transform disc;
    private SpriteRenderer discBack, discIcon;
    private Vector3 discLocal;
    private bool discAbove;
    private float discGrow, discShake;
    private bool inSight;
    /// <summary>What hid the disc at the last line-of-sight check, for looking into a disc that does not show.</summary>
    private string hiddenBy;
    private float nextSightCheck;
    private readonly Dictionary<NearFarInteractor, bool> wasHeld = new Dictionary<NearFarInteractor, bool>();

    /// <param name="colliderTop">
    /// The share of the prop's height, from the top, that the hand or ray can take hold of. The deployed
    /// net covers the bed and whoever is lying in it; a collider round all of it would take the grip meant
    /// for the person, so only its canopy is held.
    /// </param>
    /// <param name="discInTheMiddle">The disc floats in the middle of the prop rather than over it: for an empty window.</param>
    public static M3PropAction Attach(GameObject prop, string icon, Func<PlayerAction?> resolve,
                                      bool discOnlyOnHover = false, float colliderTop = 1f, bool discInTheMiddle = false)
    {
        if (prop == null) return null;
        if (!LocalBounds(prop.transform, out Bounds b)) return null;

        // The colliders go on before the interactable, which gathers them in its Awake.
        var box = prop.AddComponent<BoxCollider>();
        float h = b.size.y * Mathf.Clamp01(colliderTop);
        box.center = new Vector3(b.center.x, b.max.y - h * 0.5f, b.center.z);
        // Never thinner than a hand: the screen is a few millimetres deep.
        box.size = Vector3.Max(new Vector3(b.size.x, h, b.size.z), Vector3.one * 0.08f);

        var action = prop.AddComponent<M3PropAction>();
        action.resolve = resolve;
        action.discOnlyOnHover = discOnlyOnHover;
        action.discLocal = discInTheMiddle ? b.center : new Vector3(b.center.x, b.max.y, b.center.z);
        action.discAbove = !discInTheMiddle;
        action.BuildDisc(icon);
        return action;
    }

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>() ?? gameObject.AddComponent<XRSimpleInteractable>();
        interactable.selectMode = InteractableSelectMode.Multiple;
        interactable.selectEntered.AddListener(e => { if (Act()) M3Haptics.Select(e.interactorObject as Component); });
    }

    /// <summary>Does what this prop is for; false if there is nothing to do with it now.</summary>
    public bool Act()
    {
        var s = M3Session.Instance;
        var action = resolve?.Invoke();
        if (s == null || !s.Running || s.ClockHeld || action == null) return false;
        var r = s.Do(action.Value);
        // No nets left: the disc shakes its head (and the HUD's net count flashes).
        if (r.Outcome == ActionOutcome.NoNetsRemaining || r.Outcome == ActionOutcome.Invalid) discShake = 1f;
        return true;
    }

    private void Update()
    {
        // As on a villager: the trigger pulled while the ray is on the prop acts as well as the grip,
        // caught as an edge of "held" (see M3VillagerTarget for why).
        var hovering = interactable.interactorsHovering;
        for (int i = 0; i < hovering.Count; i++)
        {
            if (!(hovering[i] is NearFarInteractor nf)) continue;
            bool held = nf.uiPressInput.ReadIsPerformed();
            wasHeld.TryGetValue(nf, out bool before);
            wasHeld[nf] = held;
            if (held && !before && Act()) M3Haptics.Select(nf);
        }
        UpdateDisc(hovering.Count > 0);
    }

    private void OnDisable()
    {
        if (disc != null) disc.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (disc != null) Destroy(disc.gameObject);
    }

    // -------------------------------------------------------------------------------------------

    private void BuildDisc(string icon)
    {
        // Not a child of the prop: the net is scaled while it drops, and a disc inside it would squash with it.
        disc = new GameObject($"Action Disc ({name})").transform;

        discBack = new GameObject("Back").AddComponent<SpriteRenderer>();
        discBack.transform.SetParent(disc, false);
        discBack.sprite = Circle;
        discBack.color = M3Ui.PanelColour;

        discIcon = new GameObject("Icon").AddComponent<SpriteRenderer>();
        discIcon.transform.SetParent(disc, false);
        discIcon.transform.localPosition = new Vector3(0f, 0f, -0.002f);
        discIcon.sprite = M3Icons.Get(icon);
        discIcon.color = M3Ui.Text;
        discIcon.sortingOrder = 1;
        disc.gameObject.SetActive(false);
    }

    private void UpdateDisc(bool hovered)
    {
        var head = Camera.main != null ? Camera.main.transform : null;
        var s = M3Session.Instance;
        bool something = head != null && s != null && s.Running && resolve?.Invoke() != null;

        Vector3 top = transform.TransformPoint(discLocal) + (discAbove ? Vector3.up * (discSize * 0.5f + 0.06f) : Vector3.zero);
        Vector3 flat = top - (head != null ? head.position : top);
        flat.y = 0f;
        float fade = Mathf.Clamp01((flat.magnitude - fadeWithin * 0.6f) / (fadeWithin * 0.4f));
        bool show = something && fade > 0f && (hovered || (!discOnlyOnHover && Near(head, top)));
        if (disc.gameObject.activeSelf != show) disc.gameObject.SetActive(show);
        if (!show) { discGrow = 0f; return; }

        float dt = M3Tween.Dt;
        discGrow = M3Tween.Approach(discGrow, hovered ? 1f : 0f, 14f, dt);
        discShake = Mathf.Max(0f, discShake - dt * 2.5f);
        var back = Color.Lerp(M3Ui.PanelColour, M3Ui.ButtonColour, discGrow);
        back.a *= fade;
        discBack.color = back;
        var ink = M3Ui.Text;
        ink.a *= fade;
        discIcon.color = ink;

        // Faces the Pilot, upright, at a constant size whatever the prop's own scale.
        disc.position = top;
        Vector3 look = disc.position - head.position;
        look.y = 0f;
        float wobble = Mathf.Sin(discShake * 40f) * 25f * discShake;
        if (look.sqrMagnitude > 1e-4f) disc.rotation = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0f, 0f, wobble);
        disc.localScale = Vector3.one * discSize * (1f + 0.3f * discGrow);
        discIcon.transform.localScale = Vector3.one * 0.62f / (IconUnits);
        discBack.transform.localScale = Vector3.one / CircleUnits;
    }

    /// <summary>Within reach of the room, and not behind a wall: a quarter-second line-of-sight check.</summary>
    private bool Near(Transform head, Vector3 at)
    {
        Vector3 d = at - head.position;
        d.y = 0f;
        if (d.magnitude > showWithin) { inSight = false; return false; }
        if (Time.time >= nextSightCheck)
        {
            nextSightCheck = Time.time + 0.25f;
            inSight = true;
            var hits = Physics.RaycastAll(head.position, at - head.position, Vector3.Distance(head.position, at),
                                          ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider is CharacterController) continue;
                if (M3XRPlayer.Instance != null && M3XRPlayer.Instance.Rig != null
                    && hit.collider.transform.IsChildOf(M3XRPlayer.Instance.Rig.transform)) continue;
                // A villager between the Pilot and the prop does not hide it; a wall does.
                if (hit.collider.GetComponentInParent<VillagerView>() != null) continue;
                inSight = false;
                hiddenBy = hit.collider.name;
                break;
            }
        }
        return inSight;
    }

    /// <summary>The prop's renderers, in its own space (so a prop part-way through M3PropSettle measures the same).</summary>
    private static bool LocalBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }
        return any;
    }

    // -------------------------------------------------------------------------------------------

    /// <summary>Pixels per unit of the icon sprites (128 px at 100 per unit).</summary>
    private static float IconUnits => 1.28f;
    private const int CirclePixels = 64;
    private static float CircleUnits => CirclePixels / 100f;
    private static Sprite circle;

    /// <summary>A soft-edged white disc, made once.</summary>
    private static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;
            const int n = CirclePixels, ss = 4;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = (x + (sx + 0.5f) / ss) / n - 0.5f, v = (y + (sy + 0.5f) / ss) / n - 0.5f;
                            if (u * u + v * v < 0.25f) hits++;
                        }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            circle.name = "M3 Action Disc";
            circle.hideFlags = HideFlags.HideAndDontSave;
            return circle;
        }
    }
}
