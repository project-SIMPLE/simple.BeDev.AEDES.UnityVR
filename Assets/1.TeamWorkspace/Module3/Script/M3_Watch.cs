using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class M3_Watch : MonoBehaviour
{

    public static M3_Watch Instance { get; private set; }
    public GameObject uiHand;
    public TextMeshProUGUI ScoreText, TimeText;
    public GameObject hand;
    public LayerMask layerMaskHand;
    public float openAngle = 35f;
    public float closeAngle = 55f;
    public float touchDistance = 0.07f;
    public float reachDistance = 0.2f;
    public float closeDelay = 0.5f;

    Transform _head;
    Transform _finger;
    float _nextFingerSearch;
    float _lastWanted = float.NegativeInfinity;

    private void Awake()
    {
        Instance = this;
    }

    public void Start()
    {

    }

    public void Update()
    {
        ShowUIHanD();
    }
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
        return open
            ? (finger.position - uiHand.transform.position).sqrMagnitude <= reachDistance * reachDistance
            : (finger.position - transform.position).sqrMagnitude <= touchDistance * touchDistance;
    }
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

}

