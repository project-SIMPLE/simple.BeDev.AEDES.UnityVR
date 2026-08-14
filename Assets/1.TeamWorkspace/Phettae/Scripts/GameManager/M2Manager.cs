using TMPro;
using UnityEngine;
<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD
using UnityEngine.InputSystem;
=======
>>>>>>> 54c0e92 (update module2)
=======
>>>>>>> 54c0e92 (update module2)
=======
using UnityEngine.InputSystem;
>>>>>>> fea8276 (no message)
using UnityEngine.SceneManagement;

public class M2Manager : MonoBehaviour
{
    public static M2Manager Instance { get; private set; }

    [Header("Reference")]
    //public GameObject gameOverPanel;
    public TextMeshProUGUI[] socreText;
    public TextMeshProUGUI timerText;
<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD
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

=======
=======
>>>>>>> 54c0e92 (update module2)
    public GameObject gameOver;
=======
>>>>>>> fea8276 (no message)
    public GameObject flySwatterPrefabs;
    public GameObject creamPrefabs;
    public Transform pointFontPlayer;
    public GameObject particleSpwn;
    private Transform _cameraPos;
    private static readonly int Exposure = Shader.PropertyToID("_Exposure");

<<<<<<< HEAD
<<<<<<< HEAD
>>>>>>> 54c0e92 (update module2)
=======
>>>>>>> 54c0e92 (update module2)
=======
    [Header("Reference Canvas")]
    public GameObject gameOver;

    [Header("Input System")]
    public InputActionReference aButton;

>>>>>>> fea8276 (no message)
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

<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD

=======
        FlySwatterUi(); 
>>>>>>> 54c0e92 (update module2)
=======
        FlySwatterUi(); 
>>>>>>> 54c0e92 (update module2)
=======

>>>>>>> fea8276 (no message)
    }

    void Start()
    {
        gameOver.SetActive(false);
        socreText[0].text = score.ToString();
        isDay = true;
<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD

        _cameraPos = Camera.main.transform;
        UpdateScore(score);
=======
>>>>>>> 54c0e92 (update module2)
=======
>>>>>>> 54c0e92 (update module2)
=======

        _cameraPos = Camera.main.transform;
        UpdateScore(score);
>>>>>>> fea8276 (no message)
    }


    void Update()
    {
        StartTimer();
        DayNightSystem();
<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD
        RestartGame();

        if (isDay && timer <= dayDuration)
=======

        if(isDay && timer <= dayDuration)
>>>>>>> 54c0e92 (update module2)
=======

        if(isDay && timer <= dayDuration)
>>>>>>> 54c0e92 (update module2)
=======
        RestartGame();

        if (isDay && timer <= dayDuration)
>>>>>>> fea8276 (no message)
        {
            isDay = false;
            isNight = true;
        }
    }
    
    #region UI interaction
    public void FlySwatterUi()
    {
        Instantiate(flySwatterPrefabs, pointFontPlayer);
<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD
        Instantiate(particleSpwn, pointFontPlayer);
=======
>>>>>>> 54c0e92 (update module2)
=======
>>>>>>> 54c0e92 (update module2)
=======
        Instantiate(particleSpwn, pointFontPlayer);
>>>>>>> fea8276 (no message)
    }

    public void CreamUi()
    {
        Instantiate(creamPrefabs, pointFontPlayer);
<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD
        Instantiate(particleSpwn, pointFontPlayer);
=======
>>>>>>> 54c0e92 (update module2)
=======
>>>>>>> 54c0e92 (update module2)
=======
        Instantiate(particleSpwn, pointFontPlayer);
>>>>>>> fea8276 (no message)
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
<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD
=======
>>>>>>> fea8276 (no message)
            if (aButton.action.WasPerformedThisFrame())
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                score = 0;
                timer = 180f;
                isOver = 1;
                gameOver.SetActive(false);
                Debug.Log("Game Restarted");
            }
<<<<<<< HEAD
        }
        
=======
=======
>>>>>>> 54c0e92 (update module2)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            score = 0;
            timer = 180f;
            isOver = 1;
            gameOver.SetActive(false);
        }
<<<<<<< HEAD
>>>>>>> 54c0e92 (update module2)
=======
>>>>>>> 54c0e92 (update module2)
=======
        }
        
>>>>>>> fea8276 (no message)
    }
    #endregion
}
