using UnityEngine;

public class M3_MiniMap : MonoBehaviour
{
    private void Update()
    {
        transform.rotation = Quaternion.Euler(90, Camera.main.transform.rotation.eulerAngles.y, 0f);
    }
}
