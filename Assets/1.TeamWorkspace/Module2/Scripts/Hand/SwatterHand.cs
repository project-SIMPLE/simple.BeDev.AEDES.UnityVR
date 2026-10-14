using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SwatterHand : MonoBehaviour
{
    [Tooltip("Unused: swat scoring moved to M2Manager.swatScore so the value is decided in one place.")]
    public int scoreValue = 0;
    public bool isHandActive = false;

    XRGrabInteractable _grab;

    /// <summary>True while a hand is holding the racket.</summary>
    public bool Held => _grab != null && _grab.isSelected;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();

        // The racket prefab has gravity off, and XRI puts that back on release, so a dropped racket kept
        // whatever speed it left the hand with and drifted out of the house for good - and M2Manager
        // refused to spawn another because one "already" existed.
        if (_grab != null) _grab.forceGravityOnDetach = true;

        // Spawned after Module2Interaction ran, so it never got the tuning the scene's grabbables did.
        // Without it a dropped racket can tunnel through the floor.
        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // The tag is "Mosquito"; the old "Mosquto" spelling is not a defined tag, so CompareTag
        // threw and the score was never awarded even though the mosquito died.
        if (!other.CompareTag("Mosquito")) return;

        // NoteMosquitoSwatted awards M2Manager.swatScore (zero by default): swatting adults is
        // exactly the measure that does not control the population, so paying for it taught the
        // opposite of the lesson and could out-score a perfect clear.
        if (M2Manager.Instance != null) M2Manager.Instance.NoteMosquitoSwatted();
        Destroy(other.gameObject);
    }

    /// <summary>Wired to the racket's selectEntered: the swatter has been picked up.</summary>
    public void OnEnter()
    {
        isHandActive = true;
    }

    /// <summary>
    /// Wired to the racket's selectExited in the prefab, where it deleted the swatter the moment you
    /// let go of it. Combined with a two-second InvokeRepeating self-destruct for a swatter that had
    /// never been grabbed, the tool was effectively impossible to keep. The entry point stays because
    /// the prefab calls it by name, but it now only honours a swatter that was never picked up.
    /// </summary>
    public void DestroySelf()
    {
        if (isHandActive) return;
        Destroy(gameObject);
    }
}
