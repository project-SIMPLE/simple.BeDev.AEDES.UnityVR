using System.Collections.Generic;
using UnityEngine;

public class Spreading_Manager : MonoBehaviour
{
    public int Spread_Rate;
    public int Spread_Time;
    public M3_Pataint[] Pataints;
    public List<int> Current_Patiants_Index;
    public static Spreading_Manager Instanse;
    private void Awake()
    {
        Instanse = this;
    }
    private void Start()
    {
        //Current_Patiants.Add(Patiants[1].gameObject);
       for(int i = 0; i < Pataints.Length; i++)
        {
            Pataints[i].Patiant_Index = i;
            Current_Patiants_Index.Add(i);
        }
        spreading();
        spreading();
        spreading();
        InvokeRepeating("Spreading_Check",Spread_Time, Spread_Time);
    }
    public void Spreading_Check()
    {
        int Spreading = (Pataints.Length - Current_Patiants_Index.Count);
        print(Spreading);
        if (Spreading > 0 && Spreading+Spread_Rate < Pataints.Length)
        {
            for (int i = 0; i < Spread_Rate; i++)
            {
                spreading();
            }
        }
        else
        {
            print("Over");
        }
    }
    public void spreading()
    {
        int R = Random.Range(0, Current_Patiants_Index.Count);
        Pataints[Current_Patiants_Index[R]].Sick();
        Current_Patiants_Index.Remove(Current_Patiants_Index[R]);
    }
}
