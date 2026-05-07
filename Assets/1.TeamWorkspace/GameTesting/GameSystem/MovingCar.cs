using System.Collections;
using UnityEngine;

public class MovingCar : MonoBehaviour
{
    public float BaseSpeed,Speed;
    MeshRenderer mesh;
    public bool isleft;
    private void Awake()
    {
        mesh = GetComponent<MeshRenderer>();
        StartCoroutine(Resetting(Random.Range(0, 3)));
    }
    private void Update()
    {
        transform.Translate(Speed * Time.deltaTime, 0, 0);
        if (isleft)
        {
            if(transform.position.z > 22)
            {
                isleft = false;
                StartCoroutine(Resetting(Random.Range(0, 3)));
            }
        }
        else if(!isleft)
        {
            if (transform.position.z < -22)
            {
                isleft = true;
                StartCoroutine(Resetting(Random.Range(0,3)));
            }
        }
    }
    public void Setposition()
    {
        if (isleft)
        {
            transform.position = new Vector3(5f, 0f, -21.5f);
            transform.rotation = Quaternion.Euler(0f, -90, 0f);
        }
        else
        {
            transform.position = new Vector3(.65f, 0f, 21.5f);
            transform.rotation = Quaternion.Euler(0f, 90, 0f);
        }
    }
    public IEnumerator Resetting(float time)
    {
        mesh.enabled = false;
        Speed = 0;
        yield return new WaitForSeconds(time);
        Setposition();
        Speed = BaseSpeed;
        mesh.enabled = true;
    }
}
