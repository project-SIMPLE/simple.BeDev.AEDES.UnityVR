
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class EndofGameController : MonoBehaviour
{
    TextMeshProUGUI textMP;

    void Start()
    {
        TextMeshProUGUI textPN = GameObject.FindGameObjectWithTag("textPN").GetComponent<TextMeshProUGUI>();
        textPN.text = "Player id: " + StaticInformation.getId();

        textMP = GameObject.FindGameObjectWithTag("textIP").GetComponent<TextMeshProUGUI>();
        textMP.text = StaticInformation.endOfGame;
       
    }

    public void ResetBtn()
    {
        if (SaveManager.instance != null)
        {
            if (SaveManager.instance.a.time <= 0)
            {
                SceneManager.LoadScene("Startup Menu");
            }
            else
            {
                SceneManager.LoadScene("Main Scene");
            }
        }
        else
        {
            SceneManager.LoadScene("Startup Menu");
        }
    }


}
