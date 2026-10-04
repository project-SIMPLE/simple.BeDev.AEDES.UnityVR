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
    private void Start()
    {
        foreach (var item in termalobj)
        {
            if (item.GetComponent<ParticleSystem>())
            {
                pat.Add(item.GetComponent<ParticleSystem>());
            }
        }
        //InvokeRepeating("CheckDis",0.1f,0.05f);
    }
    private void Update()
    {

        CheckDis();
    }
    public void CheckDis()
    {
        foreach (ParticleSystem p in pat)
        {
            float dis = Vector3.Distance(gameObject.transform.position, p.transform.position);
            if (dis <= Range)
            {
                print(dis);
                p.gameObject.SetActive(true);
                float Value = Mathf.InverseLerp(Range, 0, dis);
                print(Value);
                var emission = p.GetComponent<ParticleSystem>().emission;
                emission.rateOverTime = Value * Multiply;
            }
            else if (dis > Range)
            {
                p.gameObject.SetActive(false);
            }
        }
    }
}
