using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class M3_Player : MonoBehaviour
{
    public ContinuousMoveProvider Movement;
    public NPC currentNPC;
    public GameObject ConversationUI,FinallizeSymptomsUI;
    public void NPCInteracted(NPC npc)
    {
        currentNPC= npc;
        currentNPC.text.gameObject.SetActive(true);
        currentNPC.text.text = "Hello";
    }
    public void Ask_How_feeling()
    {
        currentNPC.StartCoroutine(currentNPC.ShowText(currentNPC.voiceLine));
        print("Talk");
    }
    public void FinalizeSymptoms()
    {
        ConversationUI.SetActive(false);
        Invoke("SetFinallizeSymptomsUIActive",1f);
    }
    public void SetFinallizeSymptomsUIActive()
    {
        FinallizeSymptomsUI.SetActive(true);
    }
    public void EndConversation(int i)
    {
        NPC.Result r = NPC.Result.ToHospital;
        if (i == 0)
        {
            r = NPC.Result.ToHospital;
        }
        if (i == 1)
        {
            r = NPC.Result.ToHome;
        }
        if (i == 2)
        {
            r = NPC.Result.Fine;
        }
        currentNPC.GetInstruction(r);
        currentNPC.text.gameObject.SetActive(false);
        //currentNPC.text.transform.parent.transform.LookAt(Camera.main.transform);
        currentNPC.text.text = "Hi";
        currentNPC.outline.enabled = false;
        Movement.moveSpeed = 2.5f;
        currentNPC = null;
        FinallizeSymptomsUI.SetActive(false);
        print(r);
    }
}
