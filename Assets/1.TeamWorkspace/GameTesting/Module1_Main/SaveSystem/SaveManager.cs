using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public int time_M, time_S;
    public PlayerSavedData a;
    public static SaveManager instance;
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        if (SceneManager.GetActiveScene().buildIndex == 0)
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