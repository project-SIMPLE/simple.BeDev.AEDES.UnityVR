using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Stops drawing villagers, beds and room props beyond a set distance.
///
/// The village is built at runtime, so no occlusion culling can be baked, and every villager and
/// prop inside the view cone was drawn whether or not a wall stood in the way: 41 skinned bodies and
/// their shadow-map passes for a player who can see into two houses at most. Unity's per-layer cull
/// distance does the job for free: renderers on the DistanceCulled layer are not drawn past
/// <see cref="distance"/>, measured spherically so turning the head never pops them. Houses, palms
/// and the ground stay on Default and are never culled this way; only the things that live indoors
/// are marked, by the neighbourhood builder and by HouseholdView as it places props.
///
/// Only GameObjects that carry a Renderer are moved to the layer, never the colliders, so pointing
/// at a villager and teleporting are unaffected. If the layer is missing from TagManager the scene
/// draws everything and logs a warning rather than hiding anything.
/// </summary>
public class M3DistanceCulling : MonoBehaviour
{
    [Tooltip("Beyond this many metres from the camera, renderers on the DistanceCulled layer are not drawn. "
             + "Plots are at least 14.5 m apart along the lane and 18 m across it (VillageLayout spreads them "
             + "further), so 22 m keeps this house and its neighbours and drops the rest.")]
    [SerializeField] private float distance = 22f;

    private Camera appliedTo;

    public static int Layer => LayerMask.NameToLayer(Module3Props.DistanceCulledLayer);

    /// <summary>Puts every renderer under <paramref name="root"/> on the culled layer.</summary>
    public static void Mark(GameObject root)
    {
        if (root == null) return;
        int layer = Layer;
        if (layer < 0) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = layer;
    }

    // The XR rig, and with it the main camera, is spawned by M3XRPlayer after this scene starts, so
    // the distances are applied to whichever camera is main once there is one.
    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || cam == appliedTo) return;
        Apply(cam);
        appliedTo = cam;
    }

    private void Apply(Camera cam)
    {
        int layer = Layer;
        if (layer < 0)
        {
            Debug.LogWarning($"Module 3: no '{Module3Props.DistanceCulledLayer}' layer in TagManager; distance culling is off.");
            return;
        }
        var distances = cam.layerCullDistances;   // a copy of the 32 per-layer values, 0 = camera far plane
        distances[layer] = distance;
        cam.layerCullDistances = distances;
        cam.layerCullSpherical = true;
    }
}
