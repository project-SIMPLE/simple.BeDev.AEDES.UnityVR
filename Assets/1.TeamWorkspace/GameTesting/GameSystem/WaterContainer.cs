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
        yield return new WaitUntil(()=>GameManager.instance.IsRain);
        WaterOBJ.SetActive(true);
        isFill = true;
    }
}
