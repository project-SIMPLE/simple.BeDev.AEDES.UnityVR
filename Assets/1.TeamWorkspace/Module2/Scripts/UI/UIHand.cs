using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class UIHand : MonoBehaviour
{
    public static UIHand Instance { get; private set; }
    [Header("Reference")]
    public GameObject uiHand;
    public GameObject hand;
    public GameObject SwatterPrefab;
    public GameObject handSwatterPoint;

    [Header("Setting")]
    [Tooltip("Unused. The menu used to open only while a ray from the watch hit a collider on this layer; see ShowUIHanD.")]
    public LayerMask layerMaskHand;

    [Tooltip("The menu opens when the watch face is turned to within this many degrees of the player's eyes.")]
    public float openAngle = 35f;

    [Tooltip("Once open, the menu stays until the watch face is turned further from the eyes than this.")]
    public float closeAngle = 55f;

    [Tooltip("Touching the watch with the other hand's fingertip opens the menu too: how close the fingertip has to come, in metres.")]
    public float touchDistance = 0.07f;

    [Tooltip("The menu stays open while the other hand's fingertip is within this many metres of it, so reaching for a button cannot close it.")]
    public float reachDistance = 0.2f;

    [Tooltip("Seconds the menu stays up after the last reason to show it has gone, so tracking jitter does not make it flicker.")]
    public float closeDelay = 0.5f;

    Transform _head;
    Transform _finger;
    float _nextFingerSearch;
    float _lastWanted = float.NegativeInfinity;

    private void Awake()
    {
        // The watch is a child of the left hand, so DontDestroyOnLoad never applied (it only works on
        // root objects) and only logged a warning on every load; the rig and its watch are rebuilt
        // with the scene. Just track the current one.
        Instance = this;
    }

    public void Start()
    {

    }

    public void Update()
    {
        ShowUIHanD();
    }

    // The menu used to show only while a ray along the watch's up axis hit a collider on layerMaskHand:
    // the 8 cm sphere on the camera, or the 1 cm one on the other hand's fingertip. On a headset that
    // meant aiming the wrist to within about ten degrees of the eyes, which nobody holds. What players
    // found instead was that touching the watch opened the menu - and it shut again the moment the
    // finger left that ray to reach for a button, because the buttons sit either side of it.
    public void ShowUIHanD()
    {
        if (WantsMenu(uiHand.activeSelf)) _lastWanted = Time.unscaledTime;

        bool show = Time.unscaledTime - _lastWanted <= closeDelay;
        if (uiHand.activeSelf != show) uiHand.SetActive(show);
    }

    bool WantsMenu(bool open)
    {
        if (_head == null && Camera.main != null) _head = Camera.main.transform;
        if (_head != null)
        {
            float angle = Vector3.Angle(transform.up, _head.position - transform.position);
            if (angle <= (open ? closeAngle : openAngle)) return true;
        }

        Transform finger = OtherFingertip();
        if (finger == null) return false;

        // Closed, the finger has to come to the watch itself. Open, anywhere around the menu will do.
        return open
            ? (finger.position - uiHand.transform.position).sqrMagnitude <= reachDistance * reachDistance
            : (finger.position - transform.position).sqrMagnitude <= touchDistance * touchDistance;
    }

    // The poking fingertip of the hand that is not wearing the watch.
    Transform OtherFingertip()
    {
        if (_finger != null) return _finger;
        if (Time.unscaledTime < _nextFingerSearch) return null;
        _nextFingerSearch = Time.unscaledTime + 1f;

        foreach (var poke in FindObjectsByType<XRPokeInteractor>(FindObjectsSortMode.None))
        {
            if (transform.parent != null && poke.transform.IsChildOf(transform.parent)) continue;
            _finger = poke.GetAttachTransform(null);
            break;
        }
        return _finger;
    }

    public void SpawnSwatter()
    {
        if (FindAnyObjectByType<SwatterHand>() == null)
        {
            GameObject swatter = Instantiate(SwatterPrefab, handSwatterPoint.transform.position, handSwatterPoint.transform.rotation);
            swatter.transform.SetParent(handSwatterPoint.transform);
        }
    }
}
