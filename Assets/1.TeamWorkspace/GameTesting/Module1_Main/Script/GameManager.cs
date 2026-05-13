using System;
using System.Data;
using System.Drawing;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int score;
    public int time_M,time_S;
    public int time,Maxtime;
    public TextMeshProUGUI scoretext,TimeUI;
    public GameObject DangerUI,DeathUI,TimeOutUI,questUI;
    public GameObject Rain;
    public PlayerMain player;
    public bool IsRain;
    private void Awake()
    {
        Maxtime = (time_M) * 60 + time_S;
        time = Maxtime;
        InvokeRepeating("Settime",0,1);
        instance = this;
    }

    private void Start()
    {
        if (SaveManager.instance != null)
        {
            score = SaveManager.instance.a.Score;
            print(SaveManager.instance.a.Score);
            setscore(score);
            setscore(score);
        }
        
    }
    private void Update()
    {
        questUI.SetActive(player.L_gripValue);
    }
    public void Settime()
    {
        time -= 1;
        if (time % 60 <= 9)
        {
            TimeUI.text = (time / 60).ToString() + ":0" + (time % 60).ToString();
        }
        if (time % 60>9)
        {
            TimeUI.text = (time / 60).ToString() + ":" + (time % 60).ToString();
        }
        if (time <= 0)
        {
            TimeOut();
            CancelInvoke("Settime");
        }
        if(time<Maxtime*0.66f&&time>Maxtime*0.33f)
        {
            IsRain = true;
            Rain.SetActive(true);
        }
        if (time < Maxtime * 0.33f)
        {
            IsRain = false;
            Rain.SetActive(false);
        }
    }
    public void setscore(int sc)
    {
        score += sc;
        scoretext.text = "Score: "+ score.ToString();
        if (SaveManager.instance != null){
            SaveManager.instance.a.Score = score;
            SaveManager.SavePlayerData(SaveManager.instance.a);
        }
    }
    public void GameOver()
    {
        player.Death = true;
        DeathUI.SetActive(true);
        CancelInvoke("Settime");
        Invoke("RestartAble",1);
    }
    public void TimeOut()
    {
        TimeOutUI.SetActive(true);
        Invoke("RestartAble", 1);
    }
    public void RestartAble()
    {
        player.RestartAble = true;
    }
}
