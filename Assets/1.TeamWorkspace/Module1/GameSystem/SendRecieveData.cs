using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
public class SendRecieveData : SimulationManager
{
        public UnityEngine.UI.Image img;
    GAMAMessages message = null;
    protected override void ManageOtherMessages(string content)
    {
        message = GAMAMessages.CreateFromJSON(content);
        print(message.cycle+"AAAA");
        lastReceivedTime = Time.time;
        if (!isConnected)
        {
            isConnected = true;
            if (img != null) img.color = Color.green;
        }
    }
    public float lastReceivedTime;
    public bool isConnected = false;

    public void SendScore(int State)
    {
        if (IsGameState(GameState.GAME) && UnityEngine.Random.Range(0.0f, 0.003f) < 0.002f)
        {
            string mes = "A message from Unity at time: " + Time.time;
            var gm = GameManager.instance;
            Dictionary<string, string> args = new Dictionary<string, string> {
               {"id", ConnectionManager.Instance.GetConnectionId()},
               {"mes", mes},
               {"score_val", CurrentScore().ToString()},
               {"Nscore_val", gm != null ? gm.DrinkNectarScore.ToString() : "0"},
               {"Bscore_val", gm != null ? gm.DrinkBloodScore.ToString() : "0"},
               {"Mscore_val", gm != null ? gm.MatingScore.ToString() : "0"},
               {"Lscore_val", gm != null ? gm.LayEggScore.ToString() : "0"},
               {"end_game", State.ToString()},
               {"name_val", ConnectionManager.Instance.GetConnectionId()}
            };
            Debug.Log("sent to GAMA: " + mes);
            Debug.Log($"Sending to GAMA - ID: {args["id"]}, Score: {args["score_val"]}");
            ConnectionManager.Instance.SendExecutableAsk("receive_message", args);
        }
    }

    protected override void OtherUpdate()
    {

        if (isConnected && (Time.time - lastReceivedTime > 0.5f))
        {
            isConnected = false;
            if (img != null) img.color = Color.red;
        }
        if (SceneManager.GetActiveScene().buildIndex!=0)
        {
            if (GameManager.instance == null)
            {
                SendScore(69);
            }
            else if (GameManager.instance.time <= 1)
            {
                SendScore(0);
            }
            else if (GameManager.instance.time > 1)
            {
                SendScore(69);
            }
        }
        else if(SceneManager.GetActiveScene().buildIndex==0)
        {
            SendScore(1);
        }
        if (message != null) print("LLLL"+message.status);
        if (GetComponent<MenuController>() != null)
        {
            if (message != null)
            {
                if (message.status == "Start")
                {

                    Debug.Log("received from GAMA: status " + message.status);
                    GetComponent<MenuController>().StartBtn();
                }
                message = null;
            }
        }

    }
    // The bridge sits in both module scenes: Module 1 has GameManager, Module 2 has M2Manager.
    static int CurrentScore()
    {
        if (GameManager.instance != null) return GameManager.instance.score;
        if (M2Manager.Instance != null) return M2Manager.Instance.score;
        return 0;
    }
    public class GAMAMessages
    {
        public int cycle;public string status;
        public static GAMAMessages CreateFromJSON(string jsonString)
        {
            return JsonUtility.FromJson<GAMAMessages>(jsonString);
        }
        
    }
}
