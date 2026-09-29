using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Repellent cream tube. Hold it in one hand and squeeze (trigger) it onto the other hand to be
/// protected from bites for M2Manager.repellentDuration. Protection only: it does not touch the
/// breeding sites, so it cannot move source reduction - that is the lesson the debrief draws.
/// </summary>
public class Cream : MonoBehaviour
{
    public GameObject creamEffect;
    [Tooltip("How close (metres) the other hand must be to the tube for a squeeze to count. The original check was only a 0.5 m ray straight out of the nozzle, which is hard to aim with a tracked hand.")]
    public float applyRadius = 0.2f;

    private XRGrabInteractable _grab;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
    }

    /// <summary>Wired to the tube's Activated event (trigger pressed while held).</summary>
    public void CheckHand()
    {
        if (!TryFindOtherHand(out Vector3 point)) return;

        if (creamEffect != null) Instantiate(creamEffect, point, Quaternion.identity);
        if (M2Manager.Instance != null) M2Manager.Instance.ApplyRepellent();
    }

    private bool TryFindOtherHand(out Vector3 point)
    {
        point = transform.position;

        // Straight out of the nozzle onto a hand, as originally authored...
        if (Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, 0.5f, ~0, QueryTriggerInteraction.Collide)
            && IsOtherHand(hit.collider))
        {
            point = hit.point;
            return true;
        }

        // ...or simply held against the other hand.
        foreach (var col in Physics.OverlapSphere(transform.position, applyRadius, ~0, QueryTriggerInteraction.Collide))
        {
            if (!IsOtherHand(col)) continue;
            point = col.ClosestPoint(transform.position);
            return true;
        }
        return false;
    }

    private bool IsOtherHand(Collider col)
    {
        var interactor = col.GetComponentInParent<XRDirectInteractor>();
        if (!col.CompareTag("Hand") && interactor == null) return false;
        // The hand holding the tube does not count.
        return interactor == null || _grab == null || !_grab.interactorsSelecting.Contains(interactor);
    }

    /// <summary>Wired to the tube's SelectExited event: one tube per spawn.</summary>
    public void OnExit()
    {
        Destroy(gameObject);
    }
}
