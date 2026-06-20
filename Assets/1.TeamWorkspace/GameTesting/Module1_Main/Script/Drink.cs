using UnityEngine;

public class Drink : MonoBehaviour
{
    public PlayerMain player;
    private void Start()
    {
        player = PlayerMain.instance;
    }
    private void OnTriggerStay(Collider collision)
    {
        if (collision.gameObject.GetComponent<WaterContainer>())
        {
            if (player.Current_Blood >= player.Max_Blood && player.isMate)
            {
                if (player.R_primaryValue)
                {
                    if (player.returnValue)
                    {
                        player.LayEggparti.Play();
                    }
                    player.Current_Blood = 0;
                    player.BloodBar.value = player.Current_Blood;
                    player.EggLayed++;
                    GameManager.instance.setscore(collision.gameObject.GetComponent<WaterContainer>().Score);
                }
            }
        }
        if (collision.gameObject.GetComponent<Wild_Mosquitos>())
        {
            if (collision.gameObject.GetComponent<Wild_Mosquitos>().Gender == Wild_Mosquitos.genderlist.male && !player.isMate)
            {
                if (player.R_primaryValue)
                {
                    if (player.returnValue)
                    {
                        player.MateParti.Play();
                    }
                    player.isMate = true;
                }
            }
        }
        if (collision.gameObject.GetComponent<Human>())
        {
            print("AAAAAA");
            if (player.R_primaryValue && player.Current_Blood < player.Max_Blood)
            {
                if (player.returnValue)
                {
                    player.DrinkBloodParti.Play();
                }
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
            print("AAAAAA");
            if (player.R_primaryValue && player.Current_Nec < player.Max_Nec)
            {
                if (player.returnValue)
                {
                    player.DrinknectarParti.Play();
                }
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
            player.canmove = true;
            player.transform.parent = null;
        }
    }
}
