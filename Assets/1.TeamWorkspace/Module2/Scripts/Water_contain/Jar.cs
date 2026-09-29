using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Jar : MonoBehaviour
{
    [Header("Reference")]
    public GameObject topHolo;
    public GameObject top;
    public GameObject top_grab;

    [Header("Game Setting")]
    public int getScore = 10;
    [Tooltip("Seconds before a dropped lid returns to its place beside the jar.")]
    public float lidReturnDelay = 1.5f;
    [Tooltip("How close (metres) the lid has to be to the jar mouth for a release to seat it.")]
    public float placeDistance = 0.25f;

    private bool isInColli = false;
    private bool _isClosed = false;

    XRGrabInteractable _lidGrab;
    Rigidbody _lidBody;
    Vector3 _lidHomePosition;
    Quaternion _lidHomeRotation;
    float _returnAt = -1f;

    /// <summary>True while the player is holding this jar's lid, so the HUD can change its prompt.</summary>
    public bool LidHeld => _lidGrab != null && _lidGrab.isSelected;

    public void Start()
    {
        if (top != null) top.SetActive(false);
        if (topHolo != null) topHolo.SetActive(false);

        if (top_grab != null)
        {
            _lidHomePosition = top_grab.transform.localPosition;
            _lidHomeRotation = top_grab.transform.localRotation;

            // The lid was authored beside the jar at jar height, but its Rigidbody had gravity on,
            // so every lid dropped to the floor the instant play started and the player had to bend
            // down to the real floor to pick one up. Holding it kinematic keeps it where it was put.
            _lidBody = top_grab.GetComponent<Rigidbody>();
            if (_lidBody != null) _lidBody.isKinematic = true;

            _lidGrab = top_grab.GetComponent<XRGrabInteractable>();
            if (_lidGrab != null) _lidGrab.selectExited.AddListener(OnLidReleased);
        }

        if (M2Manager.Instance != null) M2Manager.Instance.RegisterBreedingSite(this);
    }

    void OnDestroy()
    {
        if (_lidGrab != null) _lidGrab.selectExited.RemoveListener(OnLidReleased);
    }

    // The prefab wires PutTop to the lid's LastHoverExited, so the jar was closed when the hand
    // stopped pointing at the lid rather than when the lid was let go over the jar. Hooking the
    // release gives the interaction its intended meaning; _isClosed keeps the two paths idempotent.
    void OnLidReleased(SelectExitEventArgs args)
    {
        // Trigger overlap turned out to be unreliable here: the lid objects carry a negative scale
        // (-2.0857), which mirrors their transform basis and makes Unity's collider bounds wrong, so
        // isInColli often never became true and a release over the jar did nothing. Measuring the
        // distance to the jar mouth instead sidesteps the broken collider entirely.
        if (LidNearMouth()) PutTop(force: true);
        else PutTop();

        if (_isClosed) return;

        _returnAt = Time.time + Mathf.Max(lidReturnDelay, 0.1f);
        // XRI restores the kinematic flag on release, so a dropped or thrown lid used to freeze in
        // mid-air until it teleported home. Let it fall; ReturnLidHome makes it kinematic again.
        if (_lidBody != null) _lidBody.isKinematic = false;
    }

    /// <summary>World-space position the lid is meant to end up at (where Jar_top sits).</summary>
    Vector3 MouthPosition => top != null ? top.transform.position : transform.position;

    bool LidNearMouth()
    {
        if (top_grab == null) return false;
        return Vector3.Distance(top_grab.transform.position, MouthPosition) <= Mathf.Max(placeDistance, 0.01f);
    }

    void Update()
    {
        if (_isClosed) return;

        if (LidHeld)
        {
            // Show the ghost lid whenever the carried lid is close enough to seat, so "am I in the
            // right place?" is answered before the player lets go.
            if (topHolo != null) topHolo.SetActive(LidNearMouth());
            _returnAt = -1f;
            return;
        }

        if (_returnAt < 0f) return;

        if (Time.time < _returnAt) return;
        _returnAt = -1f;
        ReturnLidHome();
    }

    // A lid dropped anywhere other than on its jar goes back to where it started, so it can never be
    // lost under the furniture.
    void ReturnLidHome()
    {
        if (top_grab == null) return;

        if (top_grab.transform.parent != transform) top_grab.transform.SetParent(transform, false);
        top_grab.transform.localPosition = _lidHomePosition;
        top_grab.transform.localRotation = _lidHomeRotation;

        if (_lidBody != null)
        {
            _lidBody.isKinematic = true;
            _lidBody.linearVelocity = Vector3.zero;
            _lidBody.angularVelocity = Vector3.zero;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Jar_Hat") && !_isClosed)
        {
            if (topHolo != null) topHolo.SetActive(true);
            isInColli = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Jar_Hat"))
        {
            if (!_isClosed && topHolo != null) topHolo.SetActive(false);
            isInColli = false;
        }
    }

    public void PutTop()
    {
        PutTop(false);
    }

    /// <param name="force">Seat the lid on a distance match, without needing the trigger to have fired.</param>
    public void PutTop(bool force)
    {
        // _isClosed stops a second call re-awarding the score: isInColli stays true while the lid
        // is still inside the trigger.
        if (_isClosed) return;
        if (!force && !isInColli) return;

        _isClosed = true;
        _returnAt = -1f;
        if (top != null) top.SetActive(true);
        if (topHolo != null) topHolo.SetActive(false);
        if (top_grab != null) top_grab.SetActive(false);

        if (M2Manager.Instance == null) return;
        M2Manager.Instance.UpdateScore(getScore);
        M2Manager.Instance.NeutralizeBreedingSite(this);
    }
}
