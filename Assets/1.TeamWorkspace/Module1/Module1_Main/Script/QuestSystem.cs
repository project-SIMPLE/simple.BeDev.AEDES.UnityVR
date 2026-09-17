using UnityEngine;

/// <summary>
/// The four life-cycle quests: DrinkNectar -> Mating -> DrinkBlood -> LayEgg. Each is worth
/// <see cref="Reward"/> points once. Quests latch: a bar draining again later (blood resets to 0
/// after laying eggs) does not undo a completed quest. The HUD reads <see cref="Quests"/> to draw
/// the list and <see cref="CurrentObjective"/> to pick what to point the player at.
/// </summary>
public class QuestSystem : MonoBehaviour
{
    public const int QuestCount = 4;
    public const int Reward = 25;
    public const int EggsToLay = 4;

    public class Quest
    {
        public string title;
        public string hint;
        public bool done;
        /// <summary>Optional "n/m" shown after the title.</summary>
        public string progress;
    }

    public Quest[] Quests { get; private set; }
    public GameManager gm;

    public int CompletedCount
    {
        get
        {
            int n = 0;
            foreach (var q in Quests) if (q.done) n++;
            return n;
        }
    }

    /// <summary>Index of the first unfinished quest in life-cycle order, or -1 when all are done.</summary>
    public int CurrentObjective
    {
        get
        {
            for (int i = 0; i < Quests.Length; i++) if (!Quests[i].done) return i;
            return -1;
        }
    }

    private void Awake()
    {
        Quests = new[]
        {
            new Quest { title = Module1Text.QuestNectar, hint = Module1Text.QuestNectarHint },
            new Quest { title = Module1Text.QuestMate,   hint = Module1Text.QuestMateHint },
            new Quest { title = Module1Text.QuestBlood,  hint = Module1Text.QuestBloodHint },
            new Quest { title = Module1Text.QuestEggs,   hint = Module1Text.QuestEggsHint, progress = "0/" + EggsToLay },
        };
    }

    private void Start()
    {
        gm = GameManager.instance;
    }

    private void Update()
    {
        if (gm == null || gm.player == null) return;
        CheckProgress();
    }

    public void CheckProgress()
    {
        var p = gm.player;
        Quests[3].progress = Mathf.Min(p.EggLayed, EggsToLay) + "/" + EggsToLay;
        // Small tolerance: nectar drains a little every frame, so an exact "== max" is only true
        // for a frame or two after the bar fills.
        Complete(0, p.Current_Nec >= p.Max_Nec - 0.05f);
        Complete(1, p.isMate);
        Complete(2, p.Current_Blood >= p.Max_Blood - 0.05f);
        Complete(3, p.EggLayed >= EggsToLay);
    }

    void Complete(int i, bool condition)
    {
        if (Quests[i].done || !condition) return;
        Quests[i].done = true;
        gm.SetScore(Reward, Module1Text.QuestComplete(Quests[i].title));
        gm.hud.OnQuestCompleted(i);
    }
}
