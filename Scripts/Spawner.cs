using UnityEngine;

public class Spawner : MonoBehaviour
{
    //Spawning Parameters
    public GameObject enemyPrefab;
    public float spawnEvery;
    public float startFrom; //Time offset
    public float spawnRadius; //Radius of circle to randomly spawn enemies in

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
            }, TaskName(), startFrom);

            started = true;
        } else
        {
            Debug.LogError("No timer set for spawner!");
        }
    }

    public void End()
    {
        started = false;
        timer.EndTask(TaskName());
    }
    
    public bool isActive()
    {
        return started;
    }

    //MAY NOT be unique in timer
    string TaskName()
    {
        return $"Spawner W{waveNum}";
    }

    void Spawn()
    {
        if (started)
        {
            Vector2 spawnPoint = Random.insideUnitCircle*spawnRadius;
            Vector3 spawnPointWorld = new Vector3(spawnPoint.x, 0, spawnPoint.y);
            Instantiate(enemyPrefab,spawnPointWorld + gameObject.transform.position, Quaternion.identity);
        }
    }
}
