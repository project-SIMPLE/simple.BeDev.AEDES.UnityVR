using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public int time_M, time_S;
    public PlayerSavedData a;
    public static SaveManager instance;

    /// <summary>
    /// Module 1's start screen. A run begins there, so that is where the save is reset. This used to
    /// test for build index 0, which stopped meaning the start screen once the module select menu
    /// took that slot.
    /// </summary>
    public const string StartScene = "Startup Menu_New";
    public static bool InStartScene => SceneManager.GetActiveScene().name == StartScene;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (!InStartScene)
            {
                Destroy(gameObject);
                return;
            }
            // Back at the start screen with the last run's manager still alive: the player left
            // that run through the module menu instead of playing it out. Start a fresh one.
            Destroy(instance.gameObject);
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        if (InStartScene)
        {
            a.time = (time_M) * 60 + time_S;
            deleteSave();
            SavePlayerData(a);
        }
        a = LoadPlayerData();
    }

    private void Start()
    {
    }
    public static void SavePlayerData(PlayerSavedData data)
    {
        string json = JsonUtility.ToJson(data);
        string path = Path.Combine(Application.persistentDataPath, "playerSaveData.json");

        try
        {
            File.WriteAllText(path, json);
            print("Saved");
        }
        catch (System.Exception)
        {
            print("AAAAAAAAA");
        }
    }
    public void deleteSave()
    {
        string path = Path.Combine(Application.persistentDataPath, "playerSaveData.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    public static PlayerSavedData LoadPlayerData()
    {
        string path = Path.Combine(Application.persistentDataPath, "playerSaveData.json");

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                PlayerSavedData data = JsonUtility.FromJson<PlayerSavedData>(json);
                print(data.Score);
                return data;
            }
            catch (System.Exception e)
            {
                print("Error loading player data: " + e.Message);
            }
        }
        else
        {
            print("No file in" + path);
        }

        return null;
    }
}