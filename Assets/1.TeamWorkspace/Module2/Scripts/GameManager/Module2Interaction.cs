using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Scene-wide interaction repairs for Module 2, applied at runtime so none of it needs a prefab or
/// scene edit. M2Manager adds this component to itself, the same way it adds Module2HUD.
///
/// Each fix here is for a defect found during on-headset testing:
///   - the EventSystem carried a plain Input System module, so XR pokes highlighted the wrist-menu
///     buttons but never clicked them;
///   - the right hand's interactor sphere was not a trigger, making it a solid moving collider that
///     physically shoved objects away as you reached for them;
///   - no grabbable had an attachTransform, so objects snapped to your palm by their origin;
///   - grabbables used Discrete collision detection and no interpolation, so thin objects sank into
///     the floor and jittered once released;
///   - colliders were much smaller than the objects they belong to (the vase's flowers had none at
///     all), so only a small part of each object responded to touch;
///   - the litter on the lawn (tag Trash) had no Rigidbody or XRGrabInteractable, so it could not be
///     picked up and TrashBin could never fire (found by playtesting through the harness).
/// </summary>
public class Module2Interaction : MonoBehaviour
{
    [Tooltip("Grow each grabbable's box collider to cover its renderers. Turn off to keep authored colliders exactly as they are.")]
    public bool fitCollidersToRenderers = true;

    [Tooltip("Make every direct interactor's collider a trigger so hands stop physically shoving objects.")]
    public bool forceInteractorTriggers = true;

    [Tooltip("Give grabbables an attach point at the centre of their collider instead of their origin.")]
    public bool addAttachTransforms = true;

    [Tooltip("Radius of each hand's grab sphere. The authored 0.1 m demands precision that children with no VR experience do not have; 0 leaves it alone. A proper far/ray interactor is the real fix and needs a prefab change.")]
    public float interactorRadius = 0.14f;

    [Tooltip("Make litter (tag Trash) grabbable so it can be carried to the bin.")]
    public bool makeTrashGrabbable = true;

    readonly List<XRGrabInteractable> _hooked = new List<XRGrabInteractable>();

    void OnDestroy()
    {
        foreach (var grab in _hooked)
            if (grab != null) grab.selectEntered.RemoveListener(OnAnyGrab);
        _hooked.Clear();
    }

    void OnAnyGrab(SelectEnterEventArgs args)
    {
        if (M2Manager.Instance != null) M2Manager.Instance.NoteFirstGrab();
    }

    void Start()
    {
        FixEventSystem();
        if (forceInteractorTriggers) FixDirectInteractors();
        // Before TuneGrabbables, so the litter gets the same collider/attach/first-grab treatment.
        if (makeTrashGrabbable) MakeTrashGrabbable();
        TuneGrabbables();
    }

    // XRI turns a poke or ray into a UI click through XRUIInputModule. With any other input module
    // the TrackedDeviceGraphicRaycaster still drives the button's highlight, which is why the wrist
    // menu lit up green but never fired its onClick.
    void FixEventSystem()
    {
        var eventSystem = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogWarning("[Module2Interaction] No EventSystem in the scene - the wrist menu cannot be clicked.");
            return;
        }

        if (eventSystem.GetComponent<XRUIInputModule>() != null) return;

        foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            module.enabled = false;

        eventSystem.gameObject.AddComponent<XRUIInputModule>();
        Debug.Log("[Module2Interaction] Added XRUIInputModule to the EventSystem so XR pokes can click UI.");
    }

    void FixDirectInteractors()
    {
        int fixedCount = 0;
        foreach (var interactor in FindObjectsByType<XRDirectInteractor>(FindObjectsSortMode.None))
        {
            foreach (var collider in interactor.GetComponents<Collider>())
            {
                if (!collider.isTrigger)
                {
                    // Detection itself uses OverlapSphere, so this does not change what the hand can
                    // reach - it stops the hand being a solid object that bats things out of the way.
                    collider.isTrigger = true;
                    fixedCount++;
                }

                // XRDirectInteractor sizes its overlap query from this sphere's radius, so widening it
                // widens the grab window.
                if (interactorRadius > 0f && collider is SphereCollider sphere)
                    sphere.radius = interactorRadius;
            }
        }
        if (fixedCount > 0) Debug.Log($"[Module2Interaction] Made {fixedCount} interactor collider(s) triggers.");
    }

    void MakeTrashGrabbable()
    {
        int count = 0;
        foreach (var go in GameObject.FindGameObjectsWithTag("Trash"))
        {
            if (go.GetComponent<XRGrabInteractable>() != null) continue;
            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            go.AddComponent<XRGrabInteractable>();
            count++;
        }
        if (count > 0) Debug.Log($"[Module2Interaction] Made {count} piece(s) of litter grabbable.");
    }

    void TuneGrabbables()
    {
        foreach (var grab in FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
        {
            var body = grab.GetComponent<Rigidbody>();
            if (body != null)
            {
                // ContinuousSpeculative is valid for both kinematic and dynamic bodies, unlike
                // ContinuousDynamic, and stops thin props tunnelling into the floor.
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (fitCollidersToRenderers) FitColliderToRenderers(grab);
            if (addAttachTransforms && grab.attachTransform == null) AddAttachTransform(grab);

            // The HUD drops its beginner guidance as soon as the child picks anything up.
            grab.selectEntered.AddListener(OnAnyGrab);
            _hooked.Add(grab);
        }
    }

    // The vase is the clearest case: one 10 x 29 x 10 cm box around the stem, and no collider at all
    // on the flowers, so most of what you can see could not be touched.
    void FitColliderToRenderers(XRGrabInteractable grab)
    {
        var box = grab.GetComponent<BoxCollider>();
        if (box == null) return;

        var renderers = grab.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        Transform t = box.transform;
        Vector3 scale = t.lossyScale;
        if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) || Mathf.Approximately(scale.z, 0f)) return;

        // Renderer bounds are world axis-aligned, so this is an approximation for a rotated prop.
        // Good enough for the upright props in this scene, and it only ever grows the collider.
        Vector3 localSize = new Vector3(
            worldBounds.size.x / Mathf.Abs(scale.x),
            worldBounds.size.y / Mathf.Abs(scale.y),
            worldBounds.size.z / Mathf.Abs(scale.z));

        if (localSize.sqrMagnitude <= box.size.sqrMagnitude) return;

        box.center = t.InverseTransformPoint(worldBounds.center);
        box.size = localSize;
    }

    void AddAttachTransform(XRGrabInteractable grab)
    {
        var collider = grab.GetComponentInChildren<Collider>();
        if (collider == null) return;

        var attach = new GameObject("[runtime] Attach");
        attach.transform.SetParent(grab.transform, false);
        attach.transform.position = collider.bounds.center;
        attach.transform.rotation = grab.transform.rotation;
        grab.attachTransform = attach.transform;
    }
}
