using UnityEngine;

public class Spawner : MonoBehaviour
{
    //Spawning Parameters
    public string taskName; //MAY NOT be unique in timer
    public GameObject enemyPrefab;
    public float spawnEvery;
    public float startFrom; //Time offset
    public float spawnRadius; //Radius of circle to randomly spawn enemies in
    public int maxSpawn; //Maximum number of units to spawn

    //Timer
    public TimerObject timer;

    //Debug

    //Manually start the spawner without a wave
    public bool manualStart;

    //Wave Manager
    public WaveHandler parent;
    public int waveNum;

    //Flag to indicate if the spawner is active
    bool started = false;
    int spawned = 0;

    void Awake()
    {
        if (parent)
        {
            parent.Register(this);
        } else
        {
            if (!manualStart)
            {
                Debug.LogWarning("No wave set to start this spawner!");
            }
        }
    }

    void Start()
    {
        if (manualStart)
        {
            Activate();
        }
    }

    public void Activate()
    {
        if (timer)
        {
            timer.OnTimeInvoke(spawnEvery, () =>
            {
                Spawn();
            }, taskName, startFrom);

            started = true;
        } else
        {
            Debug.LogError("No timer set for spawner!");
        }
    }

    public void End()
    {
        started = false;
        timer.EndTask(taskName);
        if (parent)
        {
            parent.Remove(this);
        }
    }
    
    public bool isActive()
    {
        return started;
    }

    void Spawn()
    {
        if (started)
        {
            if(spawned <= maxSpawn)
            {
                Vector2 spawnPoint = Random.insideUnitCircle*spawnRadius;
                Vector3 spawnPointWorld = new Vector3(spawnPoint.x, 0, spawnPoint.y);
                Instantiate(enemyPrefab,spawnPointWorld + gameObject.transform.position, Quaternion.identity);
                spawned++;
            } else
            {
                End();
            }
        }
    }
}
