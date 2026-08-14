using NUnit.Framework;
using UnityEngine;

public class MosquitoSpawn : MonoBehaviour
{
    [Header("Reference")]
    public GameObject mosquitoPrefab;

    [Header("Game Setting")]
    public int maxMosquitoInMap;
    public int currentMosquitoInMap;
    public GameObject[] spawnPoint;

    private void Update()
    {
        SpawnMosqitoInMap();
    }



    public void SpawnMosqitoInMap()
    {
<<<<<<< HEAD
<<<<<<< HEAD
        //print("isWorking");
=======
        print("isWorking");
>>>>>>> 54c0e92 (update module2)
=======
        print("isWorking");
>>>>>>> 54c0e92 (update module2)
        if (M2Manager.Instance.isOver == 1)
        {
            if (GameObject.FindGameObjectsWithTag("Mosquito").Length < maxMosquitoInMap)
            {
                int randomIndex = Random.Range(0, spawnPoint.Length);
                int randomMosquitoCount = Random.Range(5, M2Manager.Instance.notSaveWaterContainer.Length);
                Instantiate(mosquitoPrefab, spawnPoint[randomIndex].transform.position, Quaternion.identity);
                currentMosquitoInMap = GameObject.FindGameObjectsWithTag("Mosquito").Length;
<<<<<<< HEAD
<<<<<<< HEAD
                //print("isWork");
=======
                print("isWork");
>>>>>>> 54c0e92 (update module2)
=======
                print("isWork");
>>>>>>> 54c0e92 (update module2)
            }
        }
    }
}
