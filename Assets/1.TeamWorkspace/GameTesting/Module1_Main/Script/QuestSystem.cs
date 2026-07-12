using TMPro;
using UnityEngine;

public class QuestSystem : MonoBehaviour
{
    public string[] QuestList;
    public bool[] target;
    public TextMeshProUGUI[] Quest_Text;
    public GameManager gm;
    bool n,m,b,l;
    public GameObject BloodWarning;
    public GameObject NecWarning;
    private void Start()
    {
        gm = GameManager.instance;
        target = new bool[QuestList.Length];
        for (int i = 0; i < QuestList.Length; i++)
        {
            QuestList[i] = Quest_Text[i].text;
        }
            SetQuest();
    }
    private void Update()
    {
        SetQuest();
    }
    public void checkprogess()
    {
        target[0] = !gm.player.ishungry;
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
                if (i == 0 && n)
                {
                    n = false;

                }
                if (i == 1 && m)
                {
                    m = false;
                }
                if (i == 2 && b)
                {
                    b = false;
                }
                if (i == 3 && l)
                {
                    l = false;
                }
            }
            if (target[i])
            {
                Quest_Text[i].text = "<color=\"green\">" + QuestList[i] + "</color>";
                if (i == 0 && !n)
                {
                    GameManager.instance.DrinkNectarScore += 50;
                    GameManager.instance.setscore(50);

                    n = true;

                }
                if (i == 1 && !m)
                {
                    GameManager.instance.MatingScore += 100;
                    GameManager.instance.setscore(100);

                    m = true;
                }
                if (i == 2 && !b)
                {
                    GameManager.instance.DrinkBloodScore += 50;
                    GameManager.instance.setscore(50);

                    b = true;
                }
                if (i == 3 && !l)
                {
                    l = true;
                }
            }
            BloodWarning.SetActive(!b&&!GameManager.instance.player.RestartAble);
            NecWarning.SetActive(!n && !GameManager.instance.player.RestartAble);
        }
    }
}
