using UnityEngine;

/// <summary>
/// A house door that opens as the Pilot walks up to it and swings shut a little after they have
/// gone. It stays open while they are anywhere inside the house, so nobody is shut in a room
/// behind their back while they talk to a family. Added at runtime to the door leaves of each house the builder places, so the shared Lao
/// house prefab (Modules 1 and 2 use it too) is never edited.
///
/// The leaf is visual only. Its MeshCollider is switched off: a leaf swinging through the space the
/// CharacterController stands in would shove or trap the Pilot, and the doorway is walkable whether
/// the leaf is open or not. The door opens well before anyone reaches it (2.4 m out, in 0.7 s, at a
/// 1.4 m/s walk), so nobody ever meets a closed one.
/// </summary>
public class M3DoorSwing : MonoBehaviour
{
    [Tooltip("How far the leaf swings open, in degrees. The builder used to stand every leaf open at 90.")]
    [SerializeField] private float openAngle = 95f;
    [Tooltip("Opens when the Pilot's head comes within this many metres of the doorway (measured flat).")]
    [SerializeField] private float openWithin = 2.4f;
    [Tooltip("Starts counting down to close once the Pilot is further than this.")]
    [SerializeField] private float closeBeyond = 3.2f;
    [Tooltip("Seconds the Pilot must stay away before the door swings shut.")]
    [SerializeField] private float closeAfter = 1.2f;
    [SerializeField] private float swingSeconds = 0.7f;

    // The doorway and the footprint are kept in the house's own space, and read back through it: the
    // builder turns each plot to follow the lane after its doors are fitted, which would leave a world-space
    // doorway where the door used to be.
    private Quaternion closed;
    private Transform houseRoot;
    private Vector3 doorway;       // centre of the closed leaf, in the house's space
    private Bounds house;          // footprint of the house the leaf belongs to, in the house's space
    private float openness;        // 0 closed .. 1 open, before easing
    private bool wantOpen;
    private float awayFor;

    /// <summary>The leaf's eased opening, 0..1. For the harness and tests.</summary>
    public float Openness => M3Tween.EaseInOutCubic(openness);

    /// <summary>
    /// Fits <paramref name="leaf"/> with a swing. Its pivot must be the hinge edge, which is how the
    /// Lao house model has them. The leaf is taken as closed in the pose it has now.
    /// </summary>
    public static M3DoorSwing Fit(Transform leaf)
    {
        if (!leaf.TryGetComponent(out M3DoorSwing swing)) swing = leaf.gameObject.AddComponent<M3DoorSwing>();
        foreach (var c in leaf.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        return swing;
    }

    private void Awake()
    {
        closed = transform.localRotation;
        houseRoot = transform.parent;
        var r = GetComponentInChildren<Renderer>();
        Vector3 leaf = r != null ? r.bounds.center : transform.position;
        doorway = houseRoot != null ? houseRoot.InverseTransformPoint(leaf) : leaf;
        house = Footprint(houseRoot);
    }

    /// <summary>The house floor, walls included, in the house's space: the model's Main_Floor, or everything if it has none.</summary>
    private static Bounds Footprint(Transform houseRoot)
    {
        var b = new Bounds(Vector3.zero, Vector3.zero);
        if (houseRoot == null) return b;
        var floor = houseRoot.Find("Main_Floor");
        var renderers = (floor != null ? floor : houseRoot).GetComponentsInChildren<Renderer>();
        bool first = true;
        foreach (var r in renderers)
        {
            // The renderer's own box carried into the house's space corner by corner, so it is exact
            // however either is turned (a world-space box of a turned renderer is bigger than the part).
            var to = houseRoot.worldToLocalMatrix * r.transform.localToWorldMatrix;
            var box = r.localBounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? box.min.x : box.max.x,
                                         (i & 2) == 0 ? box.min.y : box.max.y,
                                         (i & 4) == 0 ? box.min.z : box.max.z);
                var p = to.MultiplyPoint3x4(corner);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    private bool Inside(Vector3 world)
    {
        if (houseRoot == null || house.size.x <= 0f) return false;
        Vector3 p = houseRoot.InverseTransformPoint(world);
        return p.x > house.min.x && p.x < house.max.x && p.z > house.min.z && p.z < house.max.z;
    }

    private void Update()
    {
        var head = Camera.main;
        if (head != null)
        {
            Vector3 d = head.transform.position - (houseRoot != null ? houseRoot.TransformPoint(doorway) : doorway);
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < openWithin || (wantOpen && Inside(head.transform.position))) { wantOpen = true; awayFor = 0f; }
            else if (dist > closeBeyond && wantOpen)
            {
                awayFor += Time.deltaTime;
                if (awayFor >= closeAfter) wantOpen = false;
            }
        }

        float target = wantOpen ? 1f : 0f;
        if (openness == target) return;
        openness = Mathf.MoveTowards(openness, target, M3Tween.Dt / swingSeconds);
        Apply();
    }

    private void Apply()
    {
        // Same hinge and direction the builder used to stand the doors open with.
        transform.localRotation = Quaternion.Euler(0f, -openAngle * Openness, 0f) * closed;
    }
}
