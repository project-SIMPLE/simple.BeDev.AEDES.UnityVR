using UnityEngine;

public class SwatterHand : MonoBehaviour
{
    [Tooltip("Unused: swat scoring moved to M2Manager.swatScore so the value is decided in one place.")]
    public int scoreValue = 0;
    public bool isHandActive = false;

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
