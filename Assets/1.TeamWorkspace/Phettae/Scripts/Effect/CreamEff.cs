using UnityEngine;

public class CreamEff : MonoBehaviour
{

    private void Update()
    {
        Invoke("DestroyEffect", 1f);
    }

    void DestroyEffect()
    {
        Destroy(gameObject);
    }
}
