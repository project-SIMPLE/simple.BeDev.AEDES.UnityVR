using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Test : MonoBehaviour
{
    private void Update()
    {
        if (Mouse.current.leftButton.IsPressed())
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.GetComponent<HumanM3>())
                {
                    print(hit.collider.GetComponent<HumanM3>().BodyTemp);
                    hit.collider.GetComponent<HumanM3>().ToHosPital();
                }
            }
        }
    }
}
