using TMPro;
using UnityEngine;

public class QuestSystem : MonoBehaviour
{
    private string[] QuestList = new string[] { "DrinkNectar", "Mating", "DrinkBlood", "LayEgg" };
    private bool[] target;
    public TextMeshProUGUI[] Quest_Text;
    public GameManager gm;
    bool n,m,b,l;
    private void Start()
    {
        gm = GameManager.instance;
        target = new bool[QuestList.Length];
        SetQuest();
    }
    private void Update()
    {
        SetQuest();
    }
    public void checkprogess()
    {
        target[0] = gm.player.Max_Nec <= gm.player.Current_Nec;
        target[1] = gm.player.isMate;
        target[2] = gm.player.Max_Blood <= gm.player.Current_Blood;
        target[3] = gm.player.EggLayed == 4;
        
    }
    public void SetQuest()
    {
        checkprogess();
        for (int i = 0; i < QuestList.Length; i++)
        {
            if (!target[i])
            {
                Quest_Text[i].text = "<color=\"red\">" + QuestList[i] + "</color>";
                if (i == 2 && b)
                {
                    b = false;
                }
                if (i == 0 && n)
                {
                    n = false;
                }
                if (i == 1 && m)
                {
                    m = false;
                }
                if (i == 3 && l)
                {
                    l = false;
                }
            }
            if (target[i])
            {
                Quest_Text[i].text = "<color=\"green\">" + QuestList[i] + "</color>";
                if(i == 2 && !b)
                {
                    GameManager.instance.setscore(25);
                    b = true;
                }
                if (i == 0 && !n)
                {
                    GameManager.instance.setscore(25);
                    n = true;
                }
                if (i == 1 && !m)
                {
                    GameManager.instance.setscore(25);
                    m = true;
                }
                if (i == 3 && !l)
                {
                    GameManager.instance.setscore(25);
                    l = true;
                }
            }
        }
    }
}
