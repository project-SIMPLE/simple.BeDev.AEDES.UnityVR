using UnityEngine;

public class M3_Manager : MonoBehaviour
{
    public int Score;
    public M3_Watch watch;
    public static M3_Manager instance;
    public Transform Hospital;
    public Transform[] House;
    public M3_Player player;
    public Transform NPC_PF;
    public int Max_NPC;

    public float SickChance;
    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        CheckNPC();
    }
    public void CheckNPC()
    {
        int i = FindObjectsByType<NPC>(FindObjectsSortMode.None).Length;

        if(i < Max_NPC)
        {
            Instantiate(NPC_PF, transform.position, Quaternion.identity);
            CheckNPC();
            return;
        }
        else if (i == Max_NPC)
        {
            return;
        }

    }
    public void setScore(int score)
    {
        Score += score;
        watch.ScoreText.text = "Score: "+Score.ToString();
    }
}
