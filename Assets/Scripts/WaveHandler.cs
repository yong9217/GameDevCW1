using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaveHandler : MonoBehaviour
{
    public UIResponder uir;

    Dictionary<int,Wave> waves = new Dictionary<int, Wave>();
    // Kept apart from the waves so callbacks can be registered before the spawners have registered.
    Dictionary<int,Action> endActions = new Dictionary<int, Action>();

    int curWave = -1;
    // True once every spawner of the current wave has finished spawning.
    bool spawningDone;
    public int maxWaves;

    public Debugger debug;
    public int executeWave;

    public int CurrentWave => curWave;
    public int LastWave => waves.Count > 0 ? waves.Keys.Max() : -1;

    void Awake()
    {
        debug.Add("2", () =>
        {
            Debug.Log(waves.ContainsKey(executeWave));
            DebugDict();
        }, "ManualStart");
    }

    // A wave is beaten when it has finished spawning AND every enemy on the map is dead.
    void Update()
    {
        if (curWave >= 0 && spawningDone && EnemyHealth.Active.Count == 0)
        {
            EndWave();
        }
    }

    public void DebugDict()
    {
        foreach (Wave w in waves.Values)
        {
            Debug.Log(w);
        }
    }

    public void Register(Spawner s)
    {
        if(s.waveNum < 0)
        {
            Debug.LogError("Unable to have negative wave numbers! Rejecting spawner");
        } else
        {
            if(!waves.TryAdd(s.waveNum, new Wave(s)))
            {
                waves[s.waveNum].spawners.Add(s);
            }
        }
    }

    public void StartWave(int n)
    {
        // Finish whatever wave is still running before starting the next one.
        if (curWave >= 0)
        {
            EndWave();
        }

        if(waves.TryGetValue(n, out Wave ss))
        {
            curWave = n;
            spawningDone = ss.spawners.Count == 0;

            foreach (Spawner sp in ss.spawners.ToList())
            {
                sp.Activate();
            }
        } else
        {
            Debug.LogError($"No wave indexed {n} to start");
        }
    }

    //Stops every spawner of the wave
    public bool ForceEndWave(int n)
    {
        if(waves.TryGetValue(n, out Wave ss))
        {
            // Copy first: Spawner.End removes itself from this list.
            foreach (Spawner sp in ss.spawners.ToList())
            {
                sp.End();
            }

            if (curWave == n)
            {
                curWave = -1;
                spawningDone = false;
            }

            return true;
        }

        return false;
    }

    //Ends the current wave and runs its OnWaveEnd action
    //Returns the wave ended OR -1 
    public int EndWaveOnly()
    {
        if (curWave < 0)
        {
            return -1;
        }

        int ended = curWave;

        if (waves.TryGetValue(ended, out Wave ss) && ss.spawners.Count > 0)
        {
            Debug.LogWarning($"Wave {ended} still has active spawners! Force ending...");
            ForceEndWave(ended);
        }

        Debug.Log($"Ending wave {ended}");

        // Reset before invoking: the action may start the next wave.
        curWave = -1;
        spawningDone = false;

        if (endActions.TryGetValue(ended, out Action ac))
        {
            ac.Invoke();
        }

        return ended;
    }

    public void Remove(Spawner s)
    {
        if(waves.TryGetValue(s.waveNum, out Wave ls))
        {
            ls.spawners.Remove(s);

            if(ls.spawners.Count <= 0 && s.waveNum == curWave)
            {
                Debug.Log($"Wave {curWave} finished spawning. Ends when its enemies are dead");
                spawningDone = true;
            }
        }
    }

    public void EndWave()
    {
        int waveEnded = EndWaveOnly();

        if(waveEnded >= 0)
        {
            UpdateHUD(waveEnded);
        }
    }

    public void OnWaveEnd(int waveNum, Action ac)
    {
        endActions[waveNum] = ac ?? (() => {});
    }

    public void ResetWaveEndAction(int waveNum)
    {
        endActions.Remove(waveNum);
    }

    void UpdateHUD(int waveEnded)
    {
        if (maxWaves <= 0 || !uir) return;

        float remaining = Mathf.Clamp(maxWaves - (waveEnded + 1), 0, maxWaves);
        uir.UpdateWaveMeter(100f * remaining / maxWaves, true);
    }
    
}

class Wave
{
    public List<Spawner> spawners;

    public Wave(Spawner s)
    {
        spawners = new List<Spawner>{s};
    }

    public override string ToString()
    {
        string ot = $"spawners.count = {spawners.Count} [";

        foreach (Spawner sp in spawners)
        {
            ot += $"{sp}, ";
        }

        ot += "]\n";

        return ot;
    }
}
