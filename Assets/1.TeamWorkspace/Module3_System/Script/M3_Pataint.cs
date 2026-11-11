using UnityEngine;

public class M3_Pataint : MonoBehaviour
{
    public int Patiant_Index;
    public float Body_Temp;
    public GameObject Rash;
    public GameObject[] Char;
    private void Start()
    {

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
        Invoke("Diagnosed", 15);
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
