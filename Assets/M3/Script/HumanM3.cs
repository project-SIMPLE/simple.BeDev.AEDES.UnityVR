using System.Collections;
using TMPro;

using UnityEngine;
using UnityEngine.AI;

public class HumanM3 : MonoBehaviour
{
    public NavMeshAgent agent;
    public float BodyTemp;
    public bool isSick;
    public GameObject UI;
    public void ToHosPital()
    {
        agent.SetDestination(M3Manager.instance.HospitalPos.position);
        UI.SetActive(false);
    }
    public void interacted()
    {
        UI.SetActive(true);
        Vector3 cam = (Camera.main.transform.position);
        cam.y = 0f;
        transform.LookAt(cam);
    }

}
