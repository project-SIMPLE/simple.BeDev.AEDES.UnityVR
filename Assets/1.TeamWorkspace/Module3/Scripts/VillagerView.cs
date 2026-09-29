using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Shows one villager as they appear to a volunteer standing in the room.
///
/// The three visible states of section 11 are pose, a skin material swap and props - not three
/// meshes. That is cheaper, reads better at conversational distance, and it keeps the framing
/// right.
///
/// Section 5 is emphatic about that framing and it is a constraint on this class specifically:
/// "No mechanic should isolate, mark or penalise a sick character, and the player is never
/// rewarded for avoiding them - they are rewarded for going to them." So there is no icon, no
/// outline, no colour coding and nothing overhead. A sick villager looks tired and uncomfortable,
/// the way a person does. If anything here ever reads as "warning: infected", it is a bug.
/// </summary>
public class VillagerView : MonoBehaviour
{
    private static readonly int RestingParam = Animator.StringToHash("Resting");
    private static readonly int UnwellParam = Animator.StringToHash("Unwell");
    private static readonly int TalkingParam = Animator.StringToHash("Talking");

    [Tooltip("Which resident of the neighbourhood this is.")]
    public int personId;

    [Header("Presentation")]
    [SerializeField] private Animator animator;
    [SerializeField] private Renderer skinRenderer;
    [SerializeField] private Material wellSkin;
    [SerializeField] private Material unwellSkin;

    [Header("Props in the room")]
    [Tooltip("A cloth at the mouth, for someone who cannot keep anything down.")]
    [SerializeField] private GameObject cloth;
    [Tooltip("Water within reach, once the volunteer has brought it.")]
    [SerializeField] private GameObject drinkingVessel;
    [Tooltip("A bucket beside the bed.")]
    [SerializeField] private GameObject bucket;

    /// <summary>
    /// Where this person lies down when they are unwell. Section 6: the net works here because
    /// Aedes bites during the day and the patient is in bed during the day, so a villager who is
    /// resting has to actually be on the bed the net is over.
    /// </summary>
    public Transform RestingPlace { get; set; }

    [Tooltip("Height of the mattress surface above the bed anchor (measured on PF_Bed; the body's own thickness is added on top).")]
    [SerializeField] private float mattressHeight = 0.6f;

    private Vector3 standingPosition;
    private Quaternion standingRotation;
    private bool capturedStandingPosition;

    public VisibleCondition Condition { get; private set; }
    public PersonObservation Observation { get; private set; }

    private void OnEnable()
    {
        if (M3Session.Instance != null)
        {
            M3Session.Instance.OnTurnStarted += OnTurnStarted;
            M3Session.Instance.OnActionResolved += OnActionResolved;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (M3Session.Instance != null)
        {
            M3Session.Instance.OnTurnStarted -= OnTurnStarted;
            M3Session.Instance.OnActionResolved -= OnActionResolved;
        }
    }

    private void OnTurnStarted(int turnIndex) => Refresh();

    // Water, rest and referral change what the room shows; do it when they happen, not at the
    // next turn.
    private void OnActionResolved(ActionResult result) => Refresh();

    /// <summary>For a view added at runtime to a prefab that was not built for it.</summary>
    public void WireFrom(GameObject body)
    {
        if (animator == null) animator = body.GetComponentInChildren<Animator>();
        if (skinRenderer == null) skinRenderer = body.GetComponentInChildren<SkinnedMeshRenderer>();
    }

    public void Refresh()
    {
        var sim = M3Session.Instance != null ? M3Session.Instance.Neighbourhood : null;
        if (sim == null) return;

        Observation = Observe.Person(sim, personId);
        if (Observation == null) return;
        Condition = Observation.Condition;

        bool unwell = Observation.IsUnwell;
        bool resting = unwell || Condition == VisibleCondition.Improving;

        // Someone who has gone to the health centre is not in the room. Nothing was hiding them,
        // so a referred villager went on standing in their house looking perfectly well - which
        // quietly undoes the feedback for the one action the module most wants to reward, and
        // leaves the household talking to somebody who is not there.
        SetBodyVisible(Condition != VisibleCondition.AtTheHealthCentre);

        if (animator != null)
        {
            animator.SetBool(UnwellParam, unwell);
            animator.SetBool(RestingParam, resting);
        }

        MoveToRestingPlace(resting);

        if (skinRenderer != null && wellSkin != null && unwellSkin != null)
        {
            skinRenderer.sharedMaterial = unwell ? unwellSkin : wellSkin;
        }

        // Props follow what the observation says is visible, so the room and the dialogue can
        // never disagree with each other.
        Show(cloth, HasSign("m3.see.bloodOnCloth"));
        Show(bucket, HasSign("m3.see.bucketByTheBed"));
        Show(drinkingVessel, Observation.HasFluids);
    }

    /// <summary>
    /// Hides the body without deactivating this GameObject. Deactivating it would stop
    /// OnTurnStarted arriving, and the villager would stay at the health centre for the rest of
    /// the session even after they came home.
    ///
    /// Only skinned renderers are touched, which is exactly the character and none of the props,
    /// so the cloth and the bucket go on following the observation - and they hide themselves
    /// anyway, because an observation of someone at the health centre carries no signs.
    /// </summary>
    private void SetBodyVisible(bool visible)
    {
        var bodies = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].enabled != visible) bodies[i].enabled = visible;
        }
    }

    /// <summary>
    /// NpcAnimationController has only an idle pose and a walk cycle - there is no lying-down
    /// clip for this rig yet, so a resting villager is laid down by tipping the standing pose flat
    /// rather than by switching animation states. The rig is tipped onto its back (a +90 pitch
    /// would leave the chest facing the mattress) along the bed's own orientation, and shifted half
    /// a body length because the rig's origin is at the feet - without that the body hangs off the
    /// end of the bed. Once a real lying animation exists this can go back to a plain position move.
    /// </summary>
    private void MoveToRestingPlace(bool resting)
    {
        if (RestingPlace == null) return;

        if (!capturedStandingPosition)
        {
            standingPosition = transform.position;
            standingRotation = transform.rotation;
            capturedStandingPosition = true;
        }

        if (resting)
        {
            float s = transform.lossyScale.y;   // children are smaller, so the offsets scale with them
            Vector3 feetOffset = RestingPlace.rotation * new Vector3(0f, 0f, 0.85f * s);
            transform.SetPositionAndRotation(
                RestingPlace.position + feetOffset + Vector3.up * (mattressHeight + 0.12f * s),
                RestingPlace.rotation * Quaternion.Euler(-90f, 0f, 0f));
        }
        else
        {
            transform.SetPositionAndRotation(standingPosition, standingRotation);
        }
    }

    private bool HasSign(string key)
    {
        if (Observation == null) return false;
        for (int i = 0; i < Observation.Signs.Count; i++)
        {
            if (Observation.Signs[i].Key == key) return true;
        }
        return false;
    }

    public void SetTalking(bool talking)
    {
        if (animator != null) animator.SetBool(TalkingParam, talking);
    }

    private static void Show(GameObject go, bool visible)
    {
        if (go != null && go.activeSelf != visible) go.SetActive(visible);
    }
}
