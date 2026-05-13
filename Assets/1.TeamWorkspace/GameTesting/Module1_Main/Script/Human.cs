using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
public class Human : MonoBehaviour
{
    public Transform[] TargetPosition;
    public Transform CurrenTargetPos;
    public int CurrentTarget;
    public float speed, BaseWaitTime,WaitTime;
    public Animator animator;
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }
    private void Start()
    {
        CurrenTargetPos = TargetPosition[CurrentTarget];
        transform.LookAt(CurrenTargetPos);
        animator.SetBool("isWalk", true);
        //SceneManager.LoadScene(0);
    }
    void Update()
    {
        if (CurrenTargetPos != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, CurrenTargetPos.position, speed * Time.deltaTime);
        }
        if (CurrenTargetPos != null)
        {
            if (Vector3.Distance(transform.position, TargetPosition[CurrentTarget].position) < 0.1f)
            {
                if(WaitTime == 0)
                {
                    WaitTime = BaseWaitTime;
                }
                StartCoroutine(WaitToSettarget(Random.Range(0, WaitTime)));
            }
        }
    }
    public IEnumerator WaitToSettarget(float waittime)
    {
        CurrenTargetPos = null;
        animator.SetBool("isWalk",false);
        yield return new WaitForSeconds(waittime);
        settarget();
    }
    public void settarget()
    {
        int r = Random.Range(0, 1);
        print(r);
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
        transform.LookAt(CurrenTargetPos);
        animator.SetBool("isWalk", true);
    }
}
