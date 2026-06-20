using System.Collections;
using UnityEngine;

public class MovingCar : MonoBehaviour
{
    public float BaseSpeed,Speed;
    MeshRenderer mesh;
    AudioSource sound;
    public bool isleft;
    private void Awake()
    {
        mesh = GetComponent<MeshRenderer>();
        sound = GetComponent<AudioSource>();
        StartCoroutine(Resetting(Random.Range(0, 7)));
    }
    private void Update()
    {
        transform.Translate(Speed * Time.deltaTime, 0, 0);
        if (isleft)
        {
            if(transform.position.z > 5)
            {
                isleft = false;
                StartCoroutine(Resetting(Random.Range(0, 7)));
            }
        }
        else if(!isleft)
        {
            if (transform.position.z < -52)
            {
                isleft = true;
                StartCoroutine(Resetting(Random.Range(0,7)));
            }
        }
    }
    public void Setposition()
    {
        if (isleft)
        {
            transform.position = new Vector3(23.5f, -0.3407006f, -51f);
            transform.rotation = Quaternion.Euler(0f, -90, 0f);
        }
        else
        {
            transform.position = new Vector3(27.7f, -0.3407006f, 4f);
            transform.rotation = Quaternion.Euler(0f, 90, 0f);
        }
    }
    public IEnumerator Resetting(float time)
    {
        mesh.enabled = false;
        sound.enabled = false;
        Speed = 0;
        yield return new WaitForSeconds(time);
        Setposition();
        Speed = BaseSpeed;
        mesh.enabled = true;
        sound.enabled = true;
    }
}
