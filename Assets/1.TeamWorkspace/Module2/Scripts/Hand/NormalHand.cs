using UnityEngine;

public class NormalHand : MonoBehaviour
{
    public Material[] swatterMaterial;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Right"))
        {
            GetComponent<MeshRenderer>().material = swatterMaterial[1];
            print("OKOKOK");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Right"))
        {
            GetComponent<MeshRenderer>().material = swatterMaterial[0];
            print("OKOKOK");
        }
    }
}
