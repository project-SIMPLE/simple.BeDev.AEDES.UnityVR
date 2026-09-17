using UnityEngine;

public class DengerSource : MonoBehaviour
{
    public float AttactCD;
    private float attackcouttime;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMain>())
        {
            attackcouttime = 0;
            GameManager.instance.DangerUI.SetActive(true);
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMain>())
        {
            attackcouttime += Time.deltaTime;
            if (attackcouttime >= AttactCD)
            {
                print("PlayerDeath");
                GameManager.instance.GameOver();
            }
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMain>())
        {
            attackcouttime = 0;
            GameManager.instance.DangerUI.SetActive(false);
        }
    }
}
