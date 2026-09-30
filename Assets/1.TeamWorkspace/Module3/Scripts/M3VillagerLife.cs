using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Makes an indoor villager look alive: they breathe, shift their weight, glance about, turn to face
/// the Pilot when they come near, nod when spoken to or helped, and gesture a little while they talk.
///
/// The villagers used to stand frozen. Their Animator (NpcAnimationController, shared with Module 1's
/// street NPCs) has one idle state and a walk cycle, and the Resting / Unwell / Talking parameters
/// VillagerView sets are connected to nothing, so there was no motion at all. Authoring humanoid clips
/// by hand would mean guessing a full muscle pose (a muscle left out falls back to a T-pose), so this
/// adds motion on top of whatever the Animator plays instead, as small bone rotations after the
/// animation has run each frame. That works for every body (the adult, the two children and the elder
/// all have Humanoid rigs) and needs no clip, no controller edit and nothing changed in a shared prefab.
///
/// Every rotation is about the BODY's own axes (right, up, forward, worked out each frame from the
/// hips, head and shoulders), never a bone's local axes, which differ from rig to rig. Only bones move:
/// the faces stay as they are, the same two plain bars on everyone.
///
/// Section 5: nothing here marks a sick person. Someone lying ill breathes more slowly and more
/// shallowly and turns their head less far and less quickly - tired, the way a person is. There is no
/// shaking, no coughing, and no gesture that reads as a warning.
///
/// Only villagers within a few metres of the Pilot are moved, so the cost is a handful of bones on one
/// or two bodies.
/// </summary>
[DefaultExecutionOrder(100)]   // after the Animator has posed the body
public class M3VillagerLife : MonoBehaviour
{
    [Tooltip("Beyond this many metres from the Pilot's head the villager is left to the Animator.")]
    [SerializeField] private float activeRadius = 8f;
    [Tooltip("Within this many metres, and in front of them, the villager looks at the Pilot.")]
    [SerializeField] private float lookRadius = 5f;
    [Tooltip("The first time the Pilot comes this close the villager gives a small nod of greeting.")]
    [SerializeField] private float greetRadius = 3.2f;

    public int personId = -1;

    private static readonly Dictionary<int, M3VillagerLife> byPerson = new Dictionary<int, M3VillagerLife>();

    /// <summary>The Pilot has started or stopped talking to this person (M3PersonPanel).</summary>
    public static void Talking(int personId, bool talking)
    {
        if (byPerson.TryGetValue(personId, out var life) && life != null) life.SetTalking(talking);
    }

    // ---- the joints we move ----

    private enum J { Spine, Chest, Neck, Head, RightUpperArm, RightLowerArm, Count }

    private static readonly HumanBodyBones[] Bones =
    {
        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
    };

    private struct Joint
    {
        public Transform t;
        /// <summary>The pose the Animator gave it, before our rotation.</summary>
        public Quaternion baseLocal;
        /// <summary>What we left it at last frame. If it is still that, the Animator did not touch it this frame.</summary>
        public Quaternion written;
        public bool primed;
    }

    private readonly Joint[] joints = new Joint[(int)J.Count];
    private readonly Quaternion[] offsets = new Quaternion[(int)J.Count];
    private Transform leftUpperArm, rightUpperArm, hips, head;

    private VillagerView view;
    private Animator animator;
    private Camera eye;
    private float seed;                 // per-person, so a room never breathes in step

    // ---- state ----

    private bool talking;
    private float talkWeight;           // 0..1, eased
    private float lookWeight;           // 0..1, eased
    private float lookYaw, lookPitch;   // degrees, eased; + is to the right / downward
    private float nodStart = -10f;
    private float nodDepth = 9f;
    private float nextTalkNod;
    private bool greeted;
    private int household = -1;
    private bool subscribed;

    private const float NodSeconds = 0.6f;

    /// <summary>Called by M3NeighbourhoodBuilder once the body stands and its VillagerView is wired.</summary>
    public void Init(int person)
    {
        personId = person;
        seed = (person * 0.6180339f) % 1f * 100f;
        nextTalkNod = 0f;
        var sim = M3Session.Instance != null ? M3Session.Instance.Neighbourhood : null;
        var p = sim != null ? sim.PersonById(person) : null;
        household = p != null ? p.HouseholdId : -1;
        Register();
    }

    private void Awake()
    {
        view = GetComponent<VillagerView>();
        animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            // No humanoid rig to move: leave the body to its Animator rather than guess at bones.
            enabled = false;
            return;
        }
        for (int i = 0; i < joints.Length; i++)
        {
            joints[i].t = animator.GetBoneTransform(Bones[i]);
            offsets[i] = Quaternion.identity;
        }
        // Not every rig has a neck or a chest bone; their share goes to the joint next to them.
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        head = joints[(int)J.Head].t;
        leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm = joints[(int)J.RightUpperArm].t;
        if (head == null || hips == null || leftUpperArm == null || rightUpperArm == null) enabled = false;
    }

    private void OnEnable()
    {
        Register();
        Subscribe();
    }

    private void Start()
    {
        // The session may not have woken when this was enabled (see VillagerView / M3Hud).
        Subscribe();
    }

    private void OnDisable()
    {
        if (byPerson.TryGetValue(personId, out var life) && life == this) byPerson.Remove(personId);
        if (subscribed && M3Session.Instance != null) M3Session.Instance.OnActionResolved -= OnAction;
        subscribed = false;
    }

    private void Register()
    {
        if (personId >= 0) byPerson[personId] = this;
    }

    private void Subscribe()
    {
        if (subscribed || M3Session.Instance == null) return;
        M3Session.Instance.OnActionResolved += OnAction;
        subscribed = true;
    }

    private void OnAction(ActionResult result)
    {
        // Something was done for this person, or for their household: an acknowledging nod.
        var a = result.Action;
        bool mine = a.PersonId == personId || (a.PersonId < 0 && household >= 0 && a.HouseholdId == household);
        if (!mine) return;
        if (result.Outcome == ActionOutcome.Invalid || result.Outcome == ActionOutcome.AlreadyDone
            || result.Outcome == ActionOutcome.NoNetsRemaining) return;
        Nod(8f);
    }

    private void SetTalking(bool value)
    {
        if (talking == value) return;
        talking = value;
        if (talking) { Nod(7f); nextTalkNod = Time.time + 2.5f; }
        if (view != null) view.SetTalking(value);   // the animator parameter, for a talking clip if one is ever added
    }

    private void Nod(float degrees)
    {
        nodStart = Time.time;
        nodDepth = degrees;
    }

    // ---- per frame ----

    private void LateUpdate()
    {
        if (eye == null || (Time.frameCount & 31) == 0) eye = Camera.main;
        if (eye == null) return;

        Vector3 eyeAt = eye.transform.position;
        Vector3 toEye = eyeAt - head.position;
        float distance = toEye.magnitude;
        if (distance > activeRadius)
        {
            greeted = false;   // they have gone; the next visit gets a greeting again
            return;
        }

        float now = Time.time, dt = Time.deltaTime;
        bool unwell = view != null && view.Observation != null && view.Observation.IsUnwell;
        bool resting = unwell || (view != null && view.Condition == VisibleCondition.Improving);
        float tired = unwell ? 1f : resting ? 0.5f : 0f;   // how much of the way to "worn out" this person is

        // The body's own axes, from the pose the Animator just gave it.
        Vector3 up = (head.position - hips.position).normalized;
        Vector3 right = rightUpperArm.position - leftUpperArm.position;
        right = (right - up * Vector3.Dot(right, up)).normalized;
        Vector3 forward = Vector3.Cross(right, up);

        // ---- looking at the Pilot ----
        Vector3 local = new Vector3(Vector3.Dot(toEye, right), Vector3.Dot(toEye, up), Vector3.Dot(toEye, forward));
        bool inFront = local.z > 0.15f * distance;
        bool looking = distance < lookRadius && inFront;
        lookWeight = Ease(lookWeight, looking ? 1f : 0f, resting ? 2f : 3.5f, dt);
        float maxYaw = Mathf.Lerp(60f, 35f, tired), maxPitch = Mathf.Lerp(25f, 14f, tired);
        float wantYaw = looking ? Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw) : 0f;
        float horizontal = Mathf.Sqrt(local.x * local.x + local.z * local.z);
        float wantPitch = looking ? Mathf.Clamp(Mathf.Atan2(-local.y, horizontal) * Mathf.Rad2Deg, -maxPitch, maxPitch) : 0f;
        float follow = resting ? 2.2f : 4f;
        lookYaw = Ease(lookYaw, wantYaw, follow, dt);
        lookPitch = Ease(lookPitch, wantPitch, follow, dt);

        // Nobody near: an occasional slow glance about the room instead of a fixed stare.
        float glance = (Mathf.PerlinNoise(now * 0.11f, seed) - 0.5f) * 2f * Mathf.Lerp(11f, 6f, tired) * (1f - lookWeight);

        // ---- greeting ----
        if (!greeted && distance < greetRadius && inFront && !resting)
        {
            greeted = true;
            Nod(6f);
        }

        // ---- talking ----
        talkWeight = Ease(talkWeight, talking && !resting ? 1f : 0f, 3f, dt);
        if (talking && now >= nextTalkNod)
        {
            Nod(4.5f);
            nextTalkNod = now + 2.4f + 2.2f * Mathf.PerlinNoise(now, seed);
        }
        float nodT = (now - nodStart) / NodSeconds;
        float nod = nodT >= 0f && nodT < 1f ? Mathf.Sin(nodT * Mathf.PI) * nodDepth : 0f;   // down, then back

        // ---- breathing and weight shift ----
        float rate = Mathf.Lerp(0.26f, 0.19f, tired);              // breaths a second: about 16 a minute, 11 when worn out
        float depth = Mathf.Lerp(1f, 0.6f, tired);
        float breath = Mathf.Sin((now * rate + seed) * Mathf.PI * 2f);          // -1..1, in with +
        float sway = Mathf.Sin((now * 0.085f + seed * 1.7f) * Mathf.PI * 2f);   // a slow shift of weight
        float drift = Mathf.Sin((now * 0.043f + seed * 2.3f) * Mathf.PI * 2f);

        // Pitch about the body's right axis is + downward (forward), so leaning back to fill the chest is -.
        float chestPitch = -1.6f * depth * breath;
        float spinePitch = -0.7f * depth * breath;
        float spineRoll = 1.1f * (1f - 0.5f * tired) * sway;          // weight onto one hip
        float spineYaw = 1.8f * (1f - 0.5f * tired) * drift;

        // ---- work out each joint's rotation ----
        // Turning to look is shared out down the spine: a little from the chest, a third from the neck and
        // the rest from the head, so it reads as a person turning and not a head on a pole. A rig without a
        // neck (or a chest) hands that share to the joint next to it.
        bool hasNeck = joints[(int)J.Neck].t != null, hasChest = joints[(int)J.Chest].t != null;
        float chestLook = hasChest ? 0.15f : 0f, neckLook = hasNeck ? 0.3f : 0f;
        float headLook = 1f - chestLook - neckLook;
        float lookHere = lookYaw + glance;   // the glance is the same turn, made with nobody there to look at

        float roll = -spineRoll * 0.6f + (resting ? 2.5f * drift : 0f);
        Set(J.Spine, spineYaw * 0.5f + (hasChest ? 0f : spineYaw * 0.5f), spinePitch + (hasChest ? 0f : chestPitch), spineRoll);
        Set(J.Chest, spineYaw * 0.5f + lookHere * chestLook, chestPitch, -spineRoll * 0.4f);
        Set(J.Neck, lookHere * neckLook, lookPitch * neckLook, 0f);
        // The head also nods, and cancels part of the chest's rise so the gaze stays steady while they breathe.
        Set(J.Head, lookHere * headLook, lookPitch * (1f - neckLook) + nod - chestPitch * 0.5f, roll);

        // The talking gesture: the forearm comes up a little in front of the body and moves with the words.
        float phase = now * 1.15f + seed * 3f;
        float gesture = talkWeight * (0.6f + 0.4f * Mathf.Sin(phase * Mathf.PI * 2f));
        float wave = talkWeight * Mathf.Sin(phase * Mathf.PI * 2f * 1.7f);
        // Small: the arm's idle pose already has the elbow a little bent, and 34 degrees more looked like a
        // raised hand rather than someone talking.
        Set(J.RightUpperArm, wave * 2.5f, -8f * gesture, 0f);
        Set(J.RightLowerArm, wave * 3f, -20f * gesture - 4f * wave, 0f);

        // ---- write them, parents before children ----
        for (int i = 0; i < joints.Length; i++) Apply(i, up, right, forward);
    }

    // The offsets are stored as angles (yaw about up, pitch about right, roll about forward) until Apply
    // knows the body's axes; a small struct-free way to hand them across.
    private readonly float[] yaws = new float[(int)J.Count];
    private readonly float[] pitches = new float[(int)J.Count];
    private readonly float[] rolls = new float[(int)J.Count];

    private void Set(J joint, float yaw, float pitch, float roll)
    {
        int i = (int)joint;
        yaws[i] = yaw;
        pitches[i] = pitch;
        rolls[i] = roll;
    }

    private void Apply(int i, Vector3 up, Vector3 right, Vector3 forward)
    {
        ref var j = ref joints[i];
        if (j.t == null) return;

        // If the bone is exactly where we left it, the Animator did not write it this frame (it is
        // culled, or the clip has no curve for this bone): rebuild from the pose it had before we
        // touched it, so the offsets never pile up frame after frame.
        var current = j.t.localRotation;
        if (!j.primed || current != j.written)
        {
            j.baseLocal = current;
            j.primed = true;
        }
        j.t.localRotation = j.baseLocal;

        var offset = Quaternion.AngleAxis(yaws[i], up) * Quaternion.AngleAxis(pitches[i], right) * Quaternion.AngleAxis(rolls[i], forward);
        j.t.rotation = offset * j.t.rotation;
        j.written = j.t.localRotation;
    }

    /// <summary>Exponential approach: rate is how quickly, per second (about a third of a second to settle at 3).</summary>
    private static float Ease(float from, float to, float rate, float dt) => Mathf.Lerp(from, to, 1f - Mathf.Exp(-rate * dt));
}
