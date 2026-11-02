using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnEvery;
    public float startFrom;

    public float spawnRadius;

    public TimerObject timer;

    public bool manualStart;

    public WaveHandler parent;
    public int waveNum;
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
            }, startFrom);

            started = true;
        } else
        {
            Debug.LogError("No timer set for spawner!");
        }
    }

    public void End()
    {
        started = false;
    }
    
    public bool isActive()
    {
        return started;
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
