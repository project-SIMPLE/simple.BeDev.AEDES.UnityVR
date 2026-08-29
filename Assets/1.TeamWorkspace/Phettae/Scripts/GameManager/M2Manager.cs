using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class M2Manager : MonoBehaviour
{
    public static M2Manager Instance { get; private set; }

    [Header("Reference")]
    //public GameObject gameOverPanel;
    public TextMeshProUGUI[] socreText;
    public TextMeshProUGUI timerText;
    public GameObject flySwatterPrefabs;
    public GameObject creamPrefabs;
    public Transform pointFontPlayer;
    public GameObject particleSpwn;
    private Transform _cameraPos;
    private static readonly int Exposure = Shader.PropertyToID("_Exposure");

    [Header("Reference Canvas")]
    public GameObject gameOver;

    [Header("Input System")]
    public InputActionReference aButton;

    [Header("Game Setting")]
    public int score = 0; 
    public float timer = 180f;
    public int isOver;
    public GameObject[] saveWaterContainer;
    public GameObject[] notSaveWaterContainer;


    [Header("Day Night System")]
    public Light sun;
    public Material skybox;
    public Gradient lightColor;
    public AnimationCurve lightIntensity;
    public float dayDuration = 120.0f;
    public bool isDay, isNight;

    private void Awake()
    {
        Instance = this;
        isOver = 1;

        DontDestroyOnLoad(gameObject);


    }

    void Start()
    {
        gameOver.SetActive(false);
        socreText[0].text = score.ToString();
        isDay = true;

        _cameraPos = Camera.main.transform;
        UpdateScore(score);
    }


    void Update()
    {
        StartTimer();
        DayNightSystem();
        RestartGame();

        if (isDay && timer <= dayDuration)
        {
            isDay = false;
            isNight = true;
        }
    }
    
    #region UI interaction
    public void FlySwatterUi()
    {
        Instantiate(flySwatterPrefabs, pointFontPlayer);
        Instantiate(particleSpwn, pointFontPlayer);
    }

    public void CreamUi()
    {
        Instantiate(creamPrefabs, pointFontPlayer);
        Instantiate(particleSpwn, pointFontPlayer);
    }

    public void UpdateScore(int value)
    {
        score += value;
        socreText[0].text = "Score: " + score.ToString();
        socreText[1].text = "Score: " + score.ToString();
    }

    public void ShowGameOverPanel()
    {
        gameOver.SetActive(true);
    }
    #endregion
    #region Timer Control
    private void StartTimer()
    {
        if (isOver == 1)
        {
            timer -= Time.deltaTime;
            timerText.text = "Time: " + Mathf.Round(timer).ToString();
            //print(timer);

            if (timer <= 0)
            {
                ShowGameOverPanel();
                isOver = 0;
            }
        }
    }

    private void DayNightSystem()
    {
        float elapsed = 120f - timer;
        float dayProgress = Mathf.Clamp01(elapsed / dayDuration);
        sun.color = lightColor.Evaluate(dayProgress);
        skybox.SetFloat(Exposure, lightIntensity.Evaluate(dayProgress * 0.05f));
        sun.intensity = lightIntensity.Evaluate(dayProgress);
        sun.transform.localRotation = Quaternion.Euler(dayProgress * 360f - -120, 30, 0);
    }
    #endregion
    #region Singleton
    public void RestartGame()
    {
        if (isOver == 0)
        {
            if (aButton.action.WasPerformedThisFrame())
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                score = 0;
                timer = 180f;
                isOver = 1;
                gameOver.SetActive(false);
                Debug.Log("Game Restarted");
            }
        }
        
    }
    #endregion
}
