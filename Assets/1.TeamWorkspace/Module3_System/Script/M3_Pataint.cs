using System.Collections;
using TMPro;
using UnityEngine;

public class M3_Pataint : MonoBehaviour
{
    public int Patiant_Index;
    public float Body_Temp;
    public GameObject Rash;
    public GameObject[] Char;
    public bool a = false;
    public TextMeshProUGUI GM;
    private void Start()
    {
        StartCoroutine(wait());
    }
    public IEnumerator wait()
    {
        yield return new WaitUntil(()=>a);
        GameManager.instance.score++;
        M3_Manager.instance.watch.ScoreText.text = "Score: "+ M3_Manager.instance.Score.ToString();
        Diagnosed();
    }
    public void Sick()
    {
        gameObject.SetActive(true);
        foreach (GameObject obj in Char)
        {
            obj.SetActive(false);
        }
        Char[Random.Range(0, Char.Length)].SetActive(true);
        Set_Symptom();
    }
    public void Set_Symptom()
    {
        if(Random.Range(0,2) == 1)
        {
            Body_Temp = Random.Range(37f, 38f);
            Rash.SetActive(false);
        }
        else
        {
            Body_Temp = Random.Range(38f, 39f);
            Rash.SetActive(true);
        }
    }
    public void Diagnosed()
    {

        Spreading_Manager.Instanse.Current_Patiants_Index.Add(Patiant_Index);
        gameObject.SetActive(false);
    }

}
