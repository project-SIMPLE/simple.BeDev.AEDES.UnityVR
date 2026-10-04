using TMPro;
using UnityEngine;

/// <summary>
/// The four life-cycle quests: DrinkNectar -> Mating -> DrinkBlood -> LayEgg. Each one latches:
/// once done it stays green and pays out once, even when a bar drains again later (nectar keeps
/// draining, and laying eggs spends the blood).
/// </summary>
public class QuestSystem : MonoBehaviour
{
    /// <summary>One clutch; a full blood meal lays all of them (see PlayerMain.BloodPerEgg).</summary>
    public const int EggsToLay = 4;
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
    bool Done(int i) => i == 0 ? n : i == 1 ? m : i == 2 ? b : l;
    public void checkprogess()
    {
        target[0] = !gm.player.ishungry;
        target[1] = gm.player.isMate;
        target[2] = gm.player.Max_Blood <= gm.player.Current_Blood;
        target[3] = gm.player.EggLayed >= EggsToLay;
        
    }
    public void SetQuest()
    {
        checkprogess();
        for (int i = 0; i < QuestList.Length; i++)
        {
            if (target[i])
            {
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
            Quest_Text[i].text = (Done(i) ? "<color=\"green\">" : "<color=\"red\">") + QuestList[i] + "</color>";
            var p = GameManager.instance.player;
            // Warnings follow the live state, not the latched quests: mated, eggs still to lay and
            // not enough blood for the next one; and hungry right now.
            BloodWarning.SetActive(!p.RestartAble && p.isMate && p.EggLayed < EggsToLay && !p.HasBloodForEgg);
            NecWarning.SetActive(!p.RestartAble && p.ishungry);
        }
    }
}
