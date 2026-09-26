using UnityEngine;

public class TrashBin : MonoBehaviour
{
    [Header("Game Setting")]
    public int score = 5;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Trash"))
        {
            Destroy(other.gameObject);
            M2Manager.Instance.score += score;
            Debug.Log("Trash collected!");
        }
    }
}
