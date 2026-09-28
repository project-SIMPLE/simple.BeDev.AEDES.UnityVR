using UnityEngine;

public class MosquitoSpawn : MonoBehaviour
{
    [Header("Reference")]
    public GameObject mosquitoPrefab;

    [Header("Game Setting")]
    public int maxMosquitoInMap;
    public int currentMosquitoInMap;
    [Tooltip("Mosquitoes sustained per breeding site that is still open.")]
    public int mosquitoesPerSite = 5;
    [Tooltip("Seconds between spawn checks. The old code ran a tag search every frame.")]
    public float spawnInterval = 0.5f;
    [Tooltip("Mosquitoes added per check, so the house fills in a few seconds rather than a few frames.")]
    public int spawnsPerCheck = 3;
    [Tooltip("Height above a container that its mosquitoes appear at.")]
    public float emergeHeight = 0.3f;
    public GameObject[] spawnPoint;

    private float _nextSpawnTime;

    private void Update()
    {
        if (Time.time < _nextSpawnTime) return;
        _nextSpawnTime = Time.time + Mathf.Max(spawnInterval, 0.05f);
        SpawnMosqitoInMap();
    }

    // Mosquitoes now emerge from the containers that still hold water, so covering a jar visibly
    // thins the swarm around it. They used to appear at nine fixed points unrelated to the water,
    // which meant the player's actions had no legible effect on where mosquitoes came from.
    private bool TryGetSpawnPosition(M2Manager manager, out Vector3 position)
    {
        position = Vector3.zero;

        if (manager.OpenSiteCount > 0)
        {
            int index = Random.Range(0, manager.OpenSiteCount);
            int i = 0;
            foreach (var site in manager.OpenSites)
            {
                if (i++ != index) continue;
                if (site == null) break;
                position = site.transform.position + Vector3.up * emergeHeight;
                return true;
            }
        }

        // Nothing left holding water: fall back to the authored points so the scene is never empty.
        if (spawnPoint != null && spawnPoint.Length > 0)
        {
            var point = spawnPoint[Random.Range(0, spawnPoint.Length)];
            if (point != null) { position = point.transform.position; return true; }
        }

        return false;
    }

    public void SpawnMosqitoInMap()
    {
        if (mosquitoPrefab == null) return;

        M2Manager manager = M2Manager.Instance;
        if (manager == null || !manager.IsPlaying) return;

        currentMosquitoInMap = GameObject.FindGameObjectsWithTag("Mosquito").Length;

        // The swarm is sustained by the breeding sites that are still open, so dealing with a
        // container visibly thins it out. This is what the old unused
        // "Random.Range(5, notSaveWaterContainer.Length)" line was reaching for; that array was
        // never populated, so it computed Random.Range(5, 0) and threw the result away.
        int target = manager.SiteCount > 0
            ? Mathf.Min(maxMosquitoInMap, manager.OpenSiteCount * Mathf.Max(mosquitoesPerSite, 1))
            : maxMosquitoInMap;

        int room = target - currentMosquitoInMap;
        if (room <= 0) return;

        int toSpawn = Mathf.Min(room, Mathf.Max(spawnsPerCheck, 1));
        for (int i = 0; i < toSpawn; i++)
        {
            if (!TryGetSpawnPosition(manager, out Vector3 position)) continue;

            Instantiate(mosquitoPrefab, position, Quaternion.identity);
            currentMosquitoInMap++;
        }
    }
}
