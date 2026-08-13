using UnityEngine;
using UnityEngine.AI;
using System.Collections;
public class RaomingHuman : HumanM3
{

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        Setpart();
    }

    public IEnumerator wait()
    {
        yield return new WaitUntil(() => agent.remainingDistance < 1);
            Setpart();
    }
    public void Setpart()
    {
        agent.SetDestination(new Vector3(Random.Range(-24.5f, 24.5f), 0, Random.Range(-24.5f, 24.5f)));
        StartCoroutine(wait());
    }
}
