using UnityEngine;

public class SwatterHand : MonoBehaviour
{
<<<<<<< HEAD
<<<<<<< HEAD
    public int scoreValue = 2;
    public bool isHandActive = false;

    private void Start()
    {
        InvokeRepeating("CheckOnHand", 2f, 1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Mosquto"))
        {
            M2Manager.Instance.UpdateScore(scoreValue);
            Destroy(other.gameObject);
        }
    }

    public void OnEnter()
    {
        isHandActive = true;
    }

    public void CheckOnHand()
    {
        if (!isHandActive)
        {
            DestroySelf();
        }
    }

    public void DestroySelf()
    {
        Destroy(gameObject);
    }
=======
    
>>>>>>> 54c0e92 (update module2)
=======
    
>>>>>>> 54c0e92 (update module2)
}
