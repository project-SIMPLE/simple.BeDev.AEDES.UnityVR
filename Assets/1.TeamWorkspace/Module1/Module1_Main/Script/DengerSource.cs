using UnityEngine;

public class DengerSource : MonoBehaviour
{
    public float AttactCD;
    private float attackcouttime;
    public bool isHunt,inRange;
    public float flyspeed;
    public string DeathMessage;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMain>())
        {
            attackcouttime = 0;
            if (!other.gameObject.GetComponent<PlayerMain>().RestartAble)
            {
                GameManager.instance.DangerUI.SetActive(true);
            }
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if(other.gameObject.GetComponent<PlayerMain>())
        {
            if (other.gameObject.GetComponent<PlayerMain>())
            {
                attackcouttime += Time.deltaTime;
                if (attackcouttime >= AttactCD)
                {
                    GameManager.instance.GameOver(DeathMessage);
                }
                Vector3 lookpos = other.gameObject.transform.position;
                lookpos.y = transform.position.y;
                transform.LookAt(lookpos);
                if (isHunt && Vector3.Distance(transform.position, other.transform.position) > 0.5f)
                {
                    Vector3 tar = other.transform.position;
                    transform.position = Vector3.MoveTowards(transform.position, tar, flyspeed * Time.deltaTime);
                    inRange = true;
                }
            }
            if (other.gameObject.GetComponent<PlayerMain>().RestartAble)
            {
                GameManager.instance.DangerUI.SetActive(false);
            }
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMain>())
        {
            attackcouttime = 0;
            GameManager.instance.DangerUI.SetActive(false);
            inRange = false;
        }
    }
}
