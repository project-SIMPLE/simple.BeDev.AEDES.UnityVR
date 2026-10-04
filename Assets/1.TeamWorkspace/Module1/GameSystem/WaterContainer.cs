using System.Collections;
using UnityEngine;

public class WaterContainer : MonoBehaviour
{
    public GameObject WaterOBJ;
    public bool isFill;
    public int Score;
    private void Start()
    {
        WaterOBJ.SetActive(isFill);
        StartCoroutine(Fill());
    }
    public IEnumerator Fill()
    {
        // Shared prefabs carry this script into Module 2, which has no GameManager: wait instead of throwing.
        yield return new WaitUntil(() => GameManager.instance != null && GameManager.instance.IsRain);
        WaterOBJ.SetActive(true);
        isFill = true;
    }
}
