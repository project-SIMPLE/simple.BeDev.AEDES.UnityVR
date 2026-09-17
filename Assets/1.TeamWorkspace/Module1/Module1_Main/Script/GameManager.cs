using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int score;
    public int time_M,time_S;
    public int time,Maxtime;
    public GameObject Rain;
    public PlayerMain player;
    public GameObject[] Human;
    public bool IsRain;

    public enum GameOverReason { Starved, Eaten, TimeOut }

    /// <summary>True once the player has dismissed the intro. The timer and the nectar drain wait for it.</summary>
    public bool GameStarted { get; private set; }
    public bool GameEnded { get; private set; }
    public bool IsPlaying => GameStarted && !GameEnded;

    public Module1HUD hud { get; private set; }
    public QuestSystem quests { get; private set; }

    // The intro is only shown on the first run of a session; after "press any button to restart"
    // the player already knows the rules and goes straight back in.
    static bool introShown;
    float introOpenedAt;

    private void Awake()
    {
        Maxtime = (time_M) * 60 + time_S;
        time = Maxtime;
        instance = this;
        quests = GetComponent<QuestSystem>();
        if (quests == null) quests = FindFirstObjectByType<QuestSystem>();
        hud = GetComponent<Module1HUD>();
        if (hud == null) hud = gameObject.AddComponent<Module1HUD>();
    }

    private void Start()
    {
        if (SaveManager.instance != null && SaveManager.instance.a != null)
        {
            score = SaveManager.instance.a.Score;
        }
        if (introShown)
        {
            StartGame();
        }
        else
        {
            introOpenedAt = Time.time;
            hud.ShowIntro();
        }
    }

    private void Update()
    {
        // Half a second of grace so a button still held from the menu does not skip the intro.
        if (!GameStarted && player != null && player.PrimaryDown && Time.time - introOpenedAt > 0.5f)
        {
            StartGame();
        }
    }

    public void StartGame()
    {
        if (GameStarted) return;
        GameStarted = true;
        introShown = true;
        hud.HideIntro();
        hud.ShowQuestPanel(hud.questPanelOnStart);
        hud.Toast(Module1Text.StartToast);
        InvokeRepeating(nameof(Settime), 1, 1);
    }

    public void Settime()
    {
        time -= 1;

        // Rain falls between 66% and 33% of the time; that is when the containers fill up.
        bool rainNow = time < Maxtime * 0.66f && time > Maxtime * 0.33f;
        if (rainNow != IsRain)
        {
            IsRain = rainNow;
            if (Rain != null) Rain.SetActive(rainNow);
            if (rainNow) hud.Toast(Module1Text.RainStarted, hud.rainColor);
            else hud.Toast(Module1Text.RainStopped, hud.rainColor);
        }

        if (time == 60) hud.Toast(Module1Text.OneMinuteLeft, hud.warnColor);
        else if (time == 30) hud.Toast(Module1Text.ThirtySecondsLeft, hud.warnColor);

        if (time <= 0)
        {
            CancelInvoke(nameof(Settime));
            TimeOut();
        }
    }

    public void setscore(int sc)
    {
        setscore(sc, null);
    }

    /// <summary>Adds points; with a label the HUD also shows a "+N label" toast.</summary>
    public void setscore(int sc, string label)
    {
        score += sc;
        if (SaveManager.instance != null && SaveManager.instance.a != null)
        {
            SaveManager.instance.a.Score = score;
            SaveManager.SavePlayerData(SaveManager.instance.a);
        }
        if (label != null) hud.Toast("+" + sc + "  " + label, hud.doneColor);
    }

    public void GameOver(GameOverReason reason)
    {
        if (GameEnded) return;
        GameEnded = true;
        player.Death = true;
        CancelInvoke(nameof(Settime));
        hud.SetDanger(false);
        hud.ShowEnd(reason, score,
            quests != null ? quests.CompletedCount : 0,
            quests != null ? QuestSystem.QuestCount : 0,
            player.EggLayed);
        Invoke(nameof(RestartAble), 1);
    }

    public void TimeOut()
    {
        GameOver(GameOverReason.TimeOut);
    }

    public void RestartAble()
    {
        player.RestartAble = true;
    }

    public void SetDanger(bool on)
    {
        hud.SetDanger(on);
    }
}
