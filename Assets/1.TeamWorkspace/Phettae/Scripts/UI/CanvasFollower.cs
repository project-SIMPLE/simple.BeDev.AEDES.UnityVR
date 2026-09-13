using UnityEngine;

public class CanvasFollower : MonoBehaviour
{
    [SerializeField] Vector3 distanceFromCamera = new Vector3(0, 0, 6);
    [SerializeField] float yRotationOffset = 0;
    [SerializeField] float followSpeed = 8.0f;
    [SerializeField] bool rotateWithCamera = false;

    Transform handWatch;

    void OnEnable()
    {
        handWatch = Camera.main.transform;

        Vector3 targetPosition = handWatch.position + (handWatch.forward * distanceFromCamera.z) + (handWatch.right * distanceFromCamera.x);
        Quaternion targetRotation;
        if (rotateWithCamera)
        {
            targetPosition += handWatch.up * distanceFromCamera.y;
            targetRotation = Quaternion.LookRotation(targetPosition - handWatch.position);
        }
        else
        {
            targetPosition.y = handWatch.position.y + distanceFromCamera.y;
            targetRotation = Quaternion.Euler(0, handWatch.eulerAngles.y + yRotationOffset, 0);
        }
        transform.position = targetPosition;
        transform.rotation = targetRotation;
    }

    void LateUpdate()
    {
        if (handWatch == null) handWatch = Camera.main.transform;

        Vector3 targetPosition = handWatch.position + (handWatch.forward * distanceFromCamera.z) + (handWatch.right * distanceFromCamera.x);
        Quaternion targetRotation;
        if (rotateWithCamera)
        {
            targetPosition += handWatch.up * distanceFromCamera.y;
            targetRotation = Quaternion.LookRotation(targetPosition - handWatch.position);
        }
        else
        {
            targetPosition.y = handWatch.position.y + distanceFromCamera.y;
            targetRotation = Quaternion.Euler(0, handWatch.eulerAngles.y + yRotationOffset, 0);
        }
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followSpeed);
    }
}