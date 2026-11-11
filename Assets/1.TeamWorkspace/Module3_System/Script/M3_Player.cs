using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class M3_Player : MonoBehaviour
{
    public GameObject DiagnoseUI;
    public M3_Pataint Current_Pataint;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<M3_Pataint>())
        {
            DiagnoseUI.SetActive(true);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<M3_Pataint>())
        {
            DiagnoseUI.SetActive(false);
        }
    }
}
