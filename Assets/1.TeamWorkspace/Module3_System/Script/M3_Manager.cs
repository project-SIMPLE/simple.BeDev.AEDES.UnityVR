using UnityEngine;

public class M3_Manager : MonoBehaviour
{
    public int Score;
    public M3_Watch watch;
    public static M3_Manager instance;
    public Transform Hospital;
    public Transform[] House;
    public M3_Player player;
    private void Awake()
    {
        instance = this;
    }
    public void setScore(int score)
    {
        Score += score;
        watch.ScoreText.text = "Score: "+Score.ToString();
    }
}
