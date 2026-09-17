using UnityEngine;
using UnityEngine.InputSystem;

public class Cream : MonoBehaviour
{
    public GameObject creamEffect;
 
    public void CheckHand()
    {
        if (Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, 0.5f))
        {
            if (hit.collider.CompareTag("Hand"))
            {
                Debug.Log("Hand Detected");
                Instantiate(creamEffect, hit.point, Quaternion.identity);
            }
        }

        Debug.DrawRay(transform.position, -transform.up * 0.5f, Color.red, 1f);
    }

    public void OnExit()
    {
        Destroy(gameObject);
    }
}
