using System.Collections.Generic;
using UnityEngine;
public class TermalManager : MonoBehaviour
{
    public List<GameObject> termalobj;
    public List<ParticleSystem> pat;
    public int TermalLayer;
    public float Range,Multiply;
    public ParticleSystem ps;
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
    private void Awake()
    {
        foreach (var item in termalobj)
        {
            if (item.GetComponent<ParticleSystem>())
            {
                pat.Add(item.GetComponent<ParticleSystem>());
            }
        }
    }
    private void Update()
    {
        float dis = Vector3.Distance(gameObject.transform.position, ps.transform.position);

        if (dis < Range)
        {
            float Value = Mathf.InverseLerp(Range, 0, dis);
            var emission = ps.GetComponent<ParticleSystem>().emission;
            emission.rateOverTime = Value * Multiply;

        }
    }
}
