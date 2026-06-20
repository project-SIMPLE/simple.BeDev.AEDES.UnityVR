using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    protected Outline Outline;
    private void Start()
    {
        Outline = GetComponent<Outline>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<Drink>())
        {
            Outline.enabled = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if(other.gameObject.GetComponent<Drink>()) 
        {
            Outline.enabled = false; 
        }
    }
}

