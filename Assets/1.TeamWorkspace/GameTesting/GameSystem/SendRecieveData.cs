using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class SendRecieveData : SimulationManager
{
    GAMAMessages message = null;
    protected override void ManageOtherMessages(string content)
    {
        message = GAMAMessages.CreateFromJSON(content);
    }
    protected override void OtherUpdate()
    {
        if(SceneManager.GetActiveScene().buildIndex!=0)
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
              // {"end_game", "33"},
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
              // {"end_game", "33"},
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
              // {"end_game", "33"},
               {"name_val", ConnectionManager.Instance.GetConnectionId()}
            };
                Debug.Log("sent to GAMA: " + mes);
                Debug.Log($"Sending to GAMA - ID: {args["id"]}, Score: {args["score_val"]}");
                ConnectionManager.Instance.SendExecutableAsk("receive_message", args);
            }
        }
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
    public class GAMAMessages
    {
        public int cycle;public string status;
        public static GAMAMessages CreateFromJSON(string jsonString)
        {
            return JsonUtility.FromJson<GAMAMessages>(jsonString);
        }
    }
}
