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
            img.color = Color.green;
        }
    }
    public float lastReceivedTime;
    public bool isConnected = false;
    protected override void OtherUpdate()
    {

        if (isConnected && (Time.time - lastReceivedTime > 0.5f))
        {
            isConnected = false;
            img.color = Color.red;
        }
        if (SceneManager.GetActiveScene().buildIndex!=0)
        {
            if (GameManager.instance.time <= 1)
            {
                if (IsGameState(GameState.GAME) && UnityEngine.Random.Range(0.0f, 0.003f) < 0.002f)
                {
                    string mes = "A message from Unity at time: " + Time.time;
                    Dictionary<string, string> args = new Dictionary<string, string> {
               {"id", ConnectionManager.Instance.GetConnectionId()},
               {"mes", mes},
               {"score_val", GameManager.instance.score.ToString()},
               {"Nscore_val", GameManager.instance.DrinkNectarScore.ToString()},
               {"Bscore_val", GameManager.instance.DrinkBloodScore.ToString()},
               {"Mscore_val", GameManager.instance.MatingScore.ToString()},
               {"Lscore_val", GameManager.instance.LayEggScore.ToString()},
               {"end_game", 0.ToString()},
               {"name_val", ConnectionManager.Instance.GetConnectionId()}
            };
                    Debug.Log("sent to GAMA: " + mes);
                    Debug.Log($"Sending to GAMA - ID: {args["id"]}, Score: {args["score_val"]}");
                    ConnectionManager.Instance.SendExecutableAsk("receive_message", args);
                }
            }
            else if (GameManager.instance.time > 1)
            {
                if (IsGameState(GameState.GAME) && UnityEngine.Random.Range(0.0f, 0.003f) < 0.002f)
                {
                    string mes = "A message from Unity at time: " + Time.time;
                    Dictionary<string, string> args = new Dictionary<string, string> {
               {"id", ConnectionManager.Instance.GetConnectionId()},
               {"mes", mes},
               {"score_val", GameManager.instance.score.ToString()},
               {"Nscore_val", GameManager.instance.DrinkNectarScore.ToString()},
               {"Bscore_val", GameManager.instance.DrinkBloodScore.ToString()},
               {"Mscore_val", GameManager.instance.MatingScore.ToString()},
               {"Lscore_val", GameManager.instance.LayEggScore.ToString()},
                              {"end_game",69.ToString()},
               {"name_val", ConnectionManager.Instance.GetConnectionId()}
            };
                    Debug.Log("sent to GAMA: " + mes);
                    Debug.Log($"Sending to GAMA - ID: {args["id"]}, Score: {args["score_val"]}");
                    ConnectionManager.Instance.SendExecutableAsk("receive_message", args);
                }
            }
        }
        else if(SceneManager.GetActiveScene().buildIndex==0)
        {
            if (IsGameState(GameState.GAME) && UnityEngine.Random.Range(0.0f, 0.003f) < 0.002f)
            {
                string mes = "A message from Unity at time: " + Time.time;
                Dictionary<string, string> args = new Dictionary<string, string> {
               {"id", ConnectionManager.Instance.GetConnectionId()},
               {"mes", mes},
               {"score_val", GameManager.instance.score.ToString()},
               {"Nscore_val", GameManager.instance.DrinkNectarScore.ToString()},
               {"Bscore_val", GameManager.instance.DrinkBloodScore.ToString()},
               {"Mscore_val", GameManager.instance.MatingScore.ToString()},
               {"Lscore_val", GameManager.instance.LayEggScore.ToString()},
               {"name_val", ConnectionManager.Instance.GetConnectionId()}
            };
                Debug.Log("sent to GAMA: " + mes);
                Debug.Log($"Sending to GAMA - ID: {args["id"]}, Score: {args["score_val"]}");
                ConnectionManager.Instance.SendExecutableAsk("receive_message", args);
            }
        }
        print("LLLL"+message.status);
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
    public class GAMAMessages
    {
        public int cycle;public string status;
        public static GAMAMessages CreateFromJSON(string jsonString)
        {
            return JsonUtility.FromJson<GAMAMessages>(jsonString);
        }
        
    }
}
