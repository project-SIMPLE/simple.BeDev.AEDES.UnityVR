using UnityEngine;

/// <summary>
/// Kill volume (predator, spray, ...). The player gets <see cref="AttactCD"/> seconds of warning -
/// red vignette + "DANGER" prompt on the HUD - to get out before it kills them.
/// Not placed in the current scene; attach it to a dragonfly / fish / spray to use it.
/// </summary>
public class DengerSource : MonoBehaviour
{
    public float AttactCD;
    private float attackcouttime;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMain>())
        {
            attackcouttime = 0;
            GameManager.instance.SetDanger(true);
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMain>())
        {
            attackcouttime += Time.deltaTime;
            if (attackcouttime >= AttactCD)
            {
                GameManager.instance.GameOver(GameManager.GameOverReason.Eaten);
            }
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMain>())
        {
            attackcouttime = 0;
            GameManager.instance.SetDanger(false);
        }
    }
}
