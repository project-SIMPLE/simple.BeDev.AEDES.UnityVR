using System.Collections.Generic;
using UnityEngine;
public class TermalManager : MonoBehaviour
{
    public List<GameObject> termalobj;
    public int TermalLayer;

    [System.Obsolete] // Start is called before the first frame update
    private void OnEnable()
    {
        GameObject[] go = FindObjectsOfType<GameObject>();
        foreach (var item in go)
        {
            if (item.layer == TermalLayer)
            {
                termalobj.Add(item);
            }
        }
    }
    private void Update()
    {

    }
}
