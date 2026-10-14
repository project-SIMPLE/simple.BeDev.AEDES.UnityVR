using System.Collections;
using UnityEngine;

public class DragonFlyRoaminh : DengerSource
{
    public Transform[] TargetPosition;
    public Transform CurrenTargetPos;
    public int CurrentTarget;
    public float speed;
    private void Start()
    {
        CurrenTargetPos = TargetPosition[CurrentTarget];
        transform.LookAt(CurrenTargetPos);
    }
    void Update()
    {
        if(!inRange)
        {
            if (CurrenTargetPos != null)
            {
                transform.position = Vector3.MoveTowards(transform.position, CurrenTargetPos.position, speed * Time.deltaTime);
            }
            if (CurrenTargetPos != null)
            {
                if (Vector3.Distance(transform.position, TargetPosition[CurrentTarget].position) < 0.1f)
                    StartCoroutine(WaitToSettarget(Random.Range(0, 0)));
            }
            transform.LookAt(CurrenTargetPos);
        }
    }
    public IEnumerator WaitToSettarget(float waittime)
    {
        CurrenTargetPos = null;
        yield return new WaitForSeconds(waittime);
        settarget();
    }
    public void settarget()
    {
        int r = Random.Range(0, 1);
        if (r == 0)
        {
            CurrentTarget++;
        }
        else
        {
            CurrentTarget--;
        }
        if (CurrentTarget >= TargetPosition.Length)
        {
            CurrentTarget = 0;
        }
        CurrenTargetPos = TargetPosition[CurrentTarget];
    }
}

