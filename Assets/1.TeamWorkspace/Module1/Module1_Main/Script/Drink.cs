using UnityEngine;

/// <summary>
/// Trigger volume on the proboscis. Holding A over a person or a flower drinks blood / nectar and
/// parents the player to it so they stay attached; the HUD prompt comes from ReportInteraction.
/// </summary>
public class Drink : MonoBehaviour
{
    private void OnTriggerStay(Collider collision)
    {
        var player = PlayerMain.instance;
        if (player == null) return;

        if (collision.gameObject.GetComponent<Human>())
        {
            player.ReportInteraction(PlayerMain.Interaction.Human);
            if (player.R_primaryValue && player.Current_Blood < player.Max_Blood)
            {
                player.Drink();
                player.canmove = false;
                player.gameObject.transform.parent = collision.gameObject.transform;
            }
            else if (!player.R_primaryValue)
            {
                player.canmove = true;
                player.transform.parent = null;
            }
        }
        if (collision.gameObject.tag == "Flower")
        {
            player.ReportInteraction(PlayerMain.Interaction.Flower);
            if (player.R_primaryValue && player.Current_Nec < player.Max_Nec)
            {
                player.DrinkNectar();
                player.canmove = false;
                player.gameObject.transform.parent = collision.gameObject.transform;
            }
            else if (!player.R_primaryValue)
            {
                player.canmove = true;
                player.transform.parent = null;
            }
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<Human>())
        {
            PlayerMain.instance.canmove = true;
            PlayerMain.instance.transform.parent = null;
        }
    }
}
