using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class M3_Manager : MonoBehaviour
{
    public int Score;
    public M3_Watch watch;
    public static M3_Manager instance;
    public M3_Player player;
    public Camera cam;

    public int time = 300;
    private void Awake()
    {
        instance = this;
        InvokeRepeating("Set_Time",0,1);
        cam = Camera.main;
    }

    public void setScore(int score)
    {
        Score += score;
        watch.ScoreText.text = "Score: "+Score.ToString();
    }
    public void Set_Time()
    {
        if (time > 0)
        {
            time--;
            int M = time/60, N = time% 60;
            watch.TimeText.text = "Time:"+M.ToString()+":"+N.ToString();
        }
    }
}
