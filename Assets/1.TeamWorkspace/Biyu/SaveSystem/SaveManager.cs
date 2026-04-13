using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public PlayerSavedData a;
    public static SaveManager instance;
    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        a = LoadPlayerData();
        if(a != null)
        {
            GameManager.instance.score = a.Score;
        }
        if(SaveManager.instance!= null)
        {
            print("AAAAAAAAAAAAAAAAAAAAAAAAAA");
        }
        else
        {
            print("ALSKIHJYGYJTFJY<GHHIOl");

        }
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