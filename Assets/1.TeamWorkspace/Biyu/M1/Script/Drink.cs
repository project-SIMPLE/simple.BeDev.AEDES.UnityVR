using UnityEngine;

public class Drink : MonoBehaviour
{
    private void OnTriggerStay(Collider collision)
    {
        if (collision.gameObject.GetComponent<Human>())
        {
            print("AAAAAA");
            if (PlayerMain.instance.R_primaryValue && PlayerMain.instance.Current_Blood < PlayerMain.instance.Max_Blood)
            {
                PlayerMain.instance.Drink();
                PlayerMain.instance.canmove = false;
                PlayerMain.instance.gameObject.transform.parent = collision.gameObject.transform;
            }
            else if (!PlayerMain.instance.R_primaryValue)
            {
                PlayerMain.instance.canmove = true;
                PlayerMain.instance.transform.parent = null;
            }
        }
        if (collision.gameObject.tag == "Flower")
        {
            print("AAAAAA");
            if (PlayerMain.instance.R_primaryValue && PlayerMain.instance.Current_Nec < PlayerMain.instance.Max_Nec)
            {
                PlayerMain.instance.DrinkNectar();
                PlayerMain.instance.canmove = false;
                PlayerMain.instance.gameObject.transform.parent = collision.gameObject.transform;
            }
            else if (!PlayerMain.instance.R_primaryValue)
            {
                PlayerMain.instance.canmove = true;
                PlayerMain.instance.transform.parent = null;
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
