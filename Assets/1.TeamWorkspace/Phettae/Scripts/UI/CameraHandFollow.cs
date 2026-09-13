using UnityEngine;

public class CameraHandFollow : MonoBehaviour
{
    [SerializeField] float followSpeed = 8.0f;

    [SerializeField] Transform handWatch;

    void Update()
    {
        Vector3 targetPosition = handWatch.position;
        Quaternion targetRotation = handWatch.rotation;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followSpeed);
    }
}