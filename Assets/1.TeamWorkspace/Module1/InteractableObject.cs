using UnityEngine;

/// <summary>
/// Marks something the mosquito can act on. Its Outline used to be switched on when the proboscis
/// trigger touched it; PlayerMain now outlines whatever is in reach of the head instead, which is
/// what the A button acts on.
/// </summary>
public class InteractableObject : MonoBehaviour
{
    protected Outline Outline;
    private void Start()
    {
        Outline = GetComponent<Outline>();
    }
}
