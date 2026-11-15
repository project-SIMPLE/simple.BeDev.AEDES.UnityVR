using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class M3_Player : MonoBehaviour
{
    public GameObject DiagnoseUI;
    public M3_Pataint Current_Pataint;
    public void Finallize(bool b)
    {
        if(Current_Pataint.Rash == b)
        {
            M3_Manager.instance.Score += 1;
        }
        else
        {
            M3_Manager.instance.Score -= 1;
        }
        Current_Pataint.Diagnosed();
        Current_Pataint = null;
        DiagnoseUI.SetActive(false);
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.GetComponent<M3_Pataint>())
        {
            DiagnoseUI.SetActive(true);
            Current_Pataint = other.gameObject.GetComponent<M3_Pataint>();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<M3_Pataint>())
        {
            DiagnoseUI.SetActive(false);
            Current_Pataint = null;
        }
    }
}
