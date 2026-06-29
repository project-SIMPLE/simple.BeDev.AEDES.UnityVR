using System;
using System.Data;
using System.Drawing;
using TMPro;
using UnityEngine;
using WebSocketSharp;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int score;
    public int LayEggScore, MatingScore,DrinkBloodScore,DrickNectarScore;
    public int time,Maxtime;
    public TextMeshProUGUI ScoreUI, LayEggUI, MatingUI, DrinkBloodUI, DrickNectarUI;
    public TextMeshProUGUI scoretext,TimeUI,DeathText;
    public GameObject DangerUI,DeathUI,TimeOutUI,questUI;
    public GameObject Rain;
    public PlayerMain player;
    public GameObject[] Human;
    public WaterContainer[] waterContainers;
    public bool IsRain;
    private void Awake()
    {
        instance = this;
        waterContainers = FindObjectsOfType<WaterContainer>();
    }

    private void Start()
    {
        if (SaveManager.instance != null)
        {
            score = SaveManager.instance.a.Score;
            LayEggScore = SaveManager.instance.a.LayEggScore;
            MatingScore = SaveManager.instance.a.MatingScore;
            DrickNectarScore = SaveManager.instance.a.DrickNectarScore;
            DrinkBloodScore = SaveManager.instance.a.DrinkBloodScore;
            scoretext.text = "Score: " + score.ToString();
            time = SaveManager.instance.a.time;
        }
        else
        {
            time = 300;
        }
        Maxtime = time;
        CancelInvoke("Settime");
        InvokeRepeating("Settime", 0, 1);
    }
    private void Update()
    {
        questUI.SetActive(player.L_gripValue);
    }
    public void Settime()
    {
        time -= 1;
        if(SaveManager.instance!= null)
        {
            SaveManager.instance.a.time = time;
            SaveManager.SavePlayerData(SaveManager.instance.a);
        }
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
            if(SaveManager.instance.a!=null)
            {
                SaveManager.instance.a.Score = score;
                SaveManager.instance.a.LayEggScore = LayEggScore;
                SaveManager.instance.a.MatingScore = MatingScore;
                SaveManager.instance.a.DrickNectarScore = DrickNectarScore;
                SaveManager.instance.a.DrinkBloodScore = DrinkBloodScore;
            }
            else
            {

            }
            SaveManager.SavePlayerData(SaveManager.instance.a);
        }
    }
    public void GameOver(string DeathMessage)
    {
        player.Death = true;
        DeathUI.SetActive(true);
        //CancelInvoke("Settime");
        Invoke("RestartAble",1);
        DeathText.text = DeathMessage;
    }
    public void TimeOut()
    {
        TimeOutUI.SetActive(true);
        ScoreUI.text = score.ToString();
        LayEggUI.text = LayEggScore.ToString();
        MatingUI.text = MatingScore.ToString();
        DrinkBloodUI.text = DrinkBloodScore.ToString();
        DrickNectarUI.text = DrickNectarScore.ToString();
        Invoke("RestartAble", 1);
    }
    public void RestartAble()
    {
        player.RestartAble = true;
    }
}
