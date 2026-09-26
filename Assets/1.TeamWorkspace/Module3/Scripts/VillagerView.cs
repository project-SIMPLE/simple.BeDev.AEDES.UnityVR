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

    private Vector3 standingPosition;
    private bool capturedStandingPosition;

    public VisibleCondition Condition { get; private set; }
    public PersonObservation Observation { get; private set; }

    private void OnEnable()
    {
        if (M3Session.Instance != null) M3Session.Instance.OnTurnStarted += OnTurnStarted;
        Refresh();
    }

    private void OnDisable()
    {
        if (M3Session.Instance != null) M3Session.Instance.OnTurnStarted -= OnTurnStarted;
    }

    private void OnTurnStarted(int turnIndex) => Refresh();

    public void Refresh()
    {
        var sim = M3Session.Instance != null ? M3Session.Instance.Neighbourhood : null;
        if (sim == null) return;

        Observation = Observe.Person(sim, personId);
        if (Observation == null) return;
        Condition = Observation.Condition;

        bool unwell = Observation.IsUnwell;
        bool resting = unwell || Condition == VisibleCondition.Improving;

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

    private void MoveToRestingPlace(bool resting)
    {
        if (RestingPlace == null) return;

        if (!capturedStandingPosition)
        {
            standingPosition = transform.position;
            capturedStandingPosition = true;
        }

        transform.position = resting ? RestingPlace.position : standingPosition;
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
