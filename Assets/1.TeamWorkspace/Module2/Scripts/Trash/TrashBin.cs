using UnityEngine;

public class TrashBin : MonoBehaviour
{
    [Header("Game Setting")]
    public int score = 5;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Trash")) return;

        Destroy(other.gameObject);
        // Was "M2Manager.Instance.score += score", which raised the total but never refreshed the
        // HUD, so binning trash looked like it did nothing.
        if (M2Manager.Instance != null)
        {
            M2Manager.Instance.UpdateScore(score);
            M2Manager.Instance.NoteTrashBinned();
        }
        Debug.Log("Trash collected!");
    }
}
