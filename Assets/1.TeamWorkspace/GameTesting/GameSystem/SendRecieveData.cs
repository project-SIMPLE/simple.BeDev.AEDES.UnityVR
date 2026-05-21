using System.Collections.Generic;
using UnityEngine;
public class SendRecieveData : SimulationManager
{
    GAMAMessages message = null;
    protected override void ManageOtherMessages(string content)
    {
        message = GAMAMessages.CreateFromJSON(content);
    }
    protected override void OtherUpdate()
    {
        if (IsGameState(GameState.GAME) && UnityEngine.Random.Range(0.0f, 1.0f) < 0.002f)
        {
            string mes = "A message from Unity at time: " + Time.time;
            Dictionary<string, string> args = new Dictionary<string, string> {
               {"id", ConnectionManager.Instance.GetConnectionId()},
               {"mes", mes},
               {"score_val", GameManager.instance.score.ToString()},
               {"name_val", "NIGG"}
            };
            Debug.Log("sent to GAMA: " + mes);
            Debug.Log($"Sending to GAMA - ID: {args["id"]}, Score: {args["score_val"]}");
            ConnectionManager.Instance.SendExecutableAsk("receive_message", args);
        }
        if (message != null)
        {
            Debug.Log("received from GAMA: cycle " + message.cycle);
            message = null;
        }
    }
    public class GAMAMessages
    {
        public int cycle;
        public static GAMAMessages CreateFromJSON(string jsonString)
        {
            return JsonUtility.FromJson<GAMAMessages>(jsonString);
        }
    }
}
