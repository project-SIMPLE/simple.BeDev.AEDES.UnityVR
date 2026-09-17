using TMPro;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Reference")]
    //public GameObject gameOverPanel;
    public TextMeshProUGUI socreText;
    public TextMeshProUGUI timerText;

    [Header("Game Setting")]
    public int score = 0;
    public float timer = 180f;
    public int isOver;
    public bool isGameStart = false;
    private void Awake()
    {
        Instance = this;
        isOver = 1;

    }

    void Start()
    {
        socreText.text = score.ToString();
    }


    void Update()
    {

    }

    public void UpdateScore(int value)
    {
        score += value;
        socreText.text = score.ToString();
    }
}
